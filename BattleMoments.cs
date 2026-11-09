using Microsoft.Xna.Framework;

namespace Ironfield;

/// Battle: moments of the aura (docs/MAGIC_AURA.md 4.6-4.8, MA7). A captain at his wall who fells an enemy champion of his
/// tier or above breaks through on the spot; two lit blades meeting start a clash of champions that the men give room to;
/// a Transcendent's light turns curses and his blade cuts arrows from the air.
public partial class Battle
{
    /// You broke through in this battle (the campaign raises your tier); the tier it gives for the rest of the fight.
    public bool BrokeThrough { get; private set; }
    int brokeTier;
    /// Men near you rally when you break through.
    public const float BreakRally = 30;

    /// A clash of champions: the ring's centre, its radius, how long it lasts at most, how much harder blows between you land.
    public const float ClashRadius = 3, ClashTime = 8, ClashBlow = 1.2f, ClashOut = 5;
    public Vector2 ClashAt { get; private set; }
    public float ClashUntil { get; private set; } = -1;
    float clashAgain;
    public bool ClashOn => Time < ClashUntil;

    /// A Transcendent's light: curses do not land on your companies within it; the blade cuts arrows in a cone ahead.
    public const float LightRadius = 20, ArrowCut = 0.5f, CutCone = 0.866f;
    public int ArrowsCut { get; private set; }

    /// You fell the enemy champion: at your wall, against one of your tier or above, the wall breaks now.
    void ChampionFelled()
    {
        if (!Options.AtWall || BrokeThrough || FoeTier < Options.AuraTier || Options.AuraTier <= 0 || Options.AuraTier >= 5) return;
        BrokeThrough = true;
        brokeTier = 1;
        Aura = AuraMax;
        var p = Player;
        foreach (var u in Units)
            if (u.Active && u.Team == p.Team && !u.IsPlayer && Vector2.DistanceSquared(u.Pos, p.Pos) < BreakRally * BreakRally)
            {
                if (u.Routing && u.Routs <= 1) u.Routing = false;
                u.Morale = MathF.Max(u.Morale, RallyAt + 12);
            }
        Flash("breakthrough", p.Pos, BreakRally, p.Team);
        Sound(Cue.Aura, p.Pos);
        PlayerHit = ("BREAKTHROUGH", Time);
        Log($"The wall breaks! You stand {Campaign.AuraName(AuraTier)} now.");
    }

    /// Each tick: two lit blades within the ring's reach start a clash; it ends when one falls, steps away, or the time runs out.
    void UpdateClash(Unit p)
    {
        var e = EnemyCaptain;
        if (ClashOn)
        {
            if (!p.Active || !e.Active || Vector2.Distance(p.Pos, e.Pos) > ClashOut) { ClashUntil = -1; clashAgain = Time + 10; }
            return;
        }
        if (Time < clashAgain || !AuraBlade || !FoeBladeLit || !p.Active || !e.Active || Vector2.Distance(p.Pos, e.Pos) > ClashRadius) return;
        ClashUntil = Time + ClashTime;
        clashAgain = ClashUntil + 10;
        ClashAt = (p.Pos + e.Pos) / 2;
        Flash("clash", ClashAt, ClashRadius, p.Team);
        Sound(Cue.Aura, ClashAt);
        Log("A clash of champions! The men fall back to give you room.");
    }

    /// Blows between the two champions in a clash.
    float ClashMult(Unit a, Unit t) => ClashOn && (a.IsPlayer && t == EnemyCaptain || a == EnemyCaptain && t.IsPlayer) ? ClashBlow : 1;

    /// The men keep out of the ring while the clash lasts.
    void KeepRing()
    {
        if (!ClashOn) return;
        foreach (var u in Near(ClashAt, ClashRadius + 0.5f, presenceNear).ToList())
        {
            if (!u.Active || u.IsPlayer || u == EnemyCaptain) continue;
            var d = u.Pos - ClashAt;
            float l = d.Length();
            if (l >= ClashRadius) continue;
            u.Pos = ClashAt + (l < 0.01f ? new Vector2(1, 0) : d / l) * ClashRadius;
        }
    }

    /// A Transcendent's light turns a curse from this company.
    bool InLight(Squad s) => AuraTier >= 5 && !Spent && Player.Active && s.Team == Player.Team && Vector2.Distance(s.Center, Player.Pos) < LightRadius + s.Radius * 0.5f;

    /// An arrow about to strike you: a Transcendent's lit blade cuts half of those coming from ahead.
    bool CutsArrow(Unit t, Vector3 vel)
    {
        if (!t.IsPlayer || AuraTier < 5 || !AuraBlade) return false;
        var v = new Vector2(vel.X, vel.Z);
        if (v.LengthSquared() < 0.01f || Vector2.Dot(-Vector2.Normalize(v), t.Dir) < CutCone || rng.NextSingle() >= ArrowCut) return false;
        ArrowsCut++;
        Flash("blade", t.Pos + t.Dir * 0.8f, 1, t.Team);
        return true;
    }
}
