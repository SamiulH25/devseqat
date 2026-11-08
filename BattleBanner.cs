using Microsoft.Xna.Framework;

namespace Ironfield;

/// Battle: your banner and your last stand (docs/PROGRESSION.md P6).
/// With `BattleOptions.Banner` a man near you carries your banner; while it is down your aura is gone, and the fall costs
/// every man of yours some heart. You pick it up by reaching it; after a moment the nearest man of yours does.
/// With `BattleOptions.LastStand`, the first time you fall you lie down for a few seconds: rally (R) to fight on at a
/// quarter of your health, or be carried off.
partial class Battle
{
    public Unit? Bearer;
    public bool BannerDown, BannerEverFell;
    public Vector2 BannerPos;
    float bannerFellAt;

    public const float BannerPickup = 3, BannerMoraleHit = 10, BannerReach = 2.5f;
    public const float LastStandWindow = 4, LastStandHealth = 0.25f;

    /// Your aura needs your banner up.
    bool BannerUp => !Options.Banner || !BannerDown;

    /// The bearer: the first man of your household guard, else of the company nearest you.
    void SetupBanner()
    {
        if (!Options.Banner) return;
        var squad = Squads.Where(s => s.Team == 0 && s.Units.Any(u => !u.Hero))
            .OrderByDescending(s => s.Company?.Has("Household") == true).ThenBy(s => Vector2.DistanceSquared(s.Centroid(), Player.Pos)).FirstOrDefault();
        Bearer = squad?.Units.First(u => !u.Hero);
    }

    void UpdateBanner()
    {
        if (!Options.Banner) return;
        if (!BannerDown)
        {
            if (Bearer is { Active: false } fallen) BannerFalls(fallen);
            return;
        }
        if (Player.Active && Vector2.DistanceSquared(Player.Pos, BannerPos) < BannerReach * BannerReach) { RaiseBanner(Player); return; }
        if (Time - bannerFellAt < BannerPickup) return;
        var near = Units.Where(u => u.Team == 0 && u.Active && !u.Routing && !u.IsLeader && Vector2.DistanceSquared(u.Pos, BannerPos) < 16)
            .OrderBy(u => Vector2.DistanceSquared(u.Pos, BannerPos)).FirstOrDefault();
        if (near != null) RaiseBanner(near);
    }

    void BannerFalls(Unit bearer)
    {
        BannerDown = BannerEverFell = true;
        BannerPos = bearer.Pos;
        bannerFellAt = Time;
        Bearer = null;
        foreach (var u in Units) if (u.Team == 0 && u.Active && !u.IsPlayer) u.Morale -= BannerMoraleHit;
        Log("Your banner falls!");
        Sound(Cue.Rout, BannerPos);
    }

    void RaiseBanner(Unit u)
    {
        Bearer = u;
        BannerDown = false;
        foreach (var m in Units) if (m.Team == 0 && m.Active && !m.IsPlayer) m.Morale = MathF.Min(100, m.Morale + BannerMoraleHit / 2);
        Log(u.IsPlayer ? "You raise your banner!" : "Your banner flies again.");
    }

    // ---------- the last stand ----------

    /// When you went down (-1: you are not down), who put you there, and whether you chose to fight on.
    public float DownSince = -1;
    Unit? downBy;
    bool lastStandUsed;
    public bool FoughtOn;

    /// Your first fall of a battle with a last stand: down, not out. Returns true if it caught the fall.
    bool LastStand(Unit t, Unit killer)
    {
        if (!t.IsPlayer || !Options.LastStand || lastStandUsed || Options.Duel || Options.Tournament) return false;
        lastStandUsed = true;
        DownSince = Time;
        downBy = killer;
        t.Hp = 1;
        Stagger(t, LastStandWindow);
        Log("You are down! Rally to fight on.");
        PlayerHit = ("DOWN: RALLY TO FIGHT ON", Time);
        return true;
    }

    /// While you are down: rally to get up at a quarter of your health, else you are carried off when the moment passes.
    void UpdateLastStand()
    {
        if (DownSince < 0) return;
        if (!Player.Active) { DownSince = -1; return; }
        if (Input.Rally)
        {
            Input.Rally = false; // this rally is spent getting up
            DownSince = -1;
            FoughtOn = true;
            Player.Hp = Player.Type.MaxHp * LastStandHealth;
            Player.ActionTimer = MathF.Min(Player.ActionTimer, 0.3f);
            Log("You get up and fight on!");
            Sound(Cue.Horn, Player.Pos);
        }
        else if (Time - DownSince > LastStandWindow)
        {
            DownSince = -1;
            Kill(Player, downBy ?? Player);
        }
    }
}
