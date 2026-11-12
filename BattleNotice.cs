using Microsoft.Xna.Framework;

namespace Ironfield;

/// The enemy sees you (docs/COMPANY.md C12). A foe weighs the captain as nearer than he is by how much he is worth
/// noticing (renown, aura, kills this battle, a horse, the banner beside you), but only a few may hold you as their mark at
/// once (`MarkCap`); the rest fight your men. By grade: rabble rush you when you stand alone and give ground for a moment
/// after you kill; veterans and elites send their best man out to meet you while the men beside him keep to theirs. Each
/// kill you make shakes the company you cut into; a company that brings you down takes heart. All on state the sim keeps
/// and its own order of thinking, so replays stay exact.
public sealed partial class Battle
{
    /// Enemies holding the captain as their mark (counted each tick from `Unit.OnCaptain`), for the cap and the HUD.
    public int MarkedBy { get; private set; }
    /// When and where the captain last felled a man (rabble near it give ground for a moment).
    float captainKillAt = -99;
    Vector2 captainKillPos;

    /// Farthest a foe notices you from, in metres.
    const float NoticeRange = 30;
    /// How long rabble give ground after you kill one of them, and how near the kill they must be.
    const float GiveGroundFor = 1.5f, GiveGroundNear = 6;

    /// Most foes that may mark you at once: 2 / 3 / 4 by difficulty, one more against an aura master or better.
    public int MarkCap => (Options.EnemyDamage < 0.9f ? 2 : Options.EnemyDamage > 1.1f ? 4 : 3) + (AuraTier >= 4 ? 1 : 0);

    /// How worth noticing you are, 0-1: renown (full at a marshal's 500), aura tier, kills this battle, a horse, the banner near.
    public float Fame()
    {
        var p = Player;
        float f = 0.4f * Math.Clamp(Options.Renown / 500, 0, 1) + 0.25f * AuraTier / 5f + 0.2f * MathF.Min(PlayerKills, 10) / 10f
            + (p.Mounted ? 0.1f : 0) + (Bearer is { Active: true } b && Vector2.DistanceSquared(b.Pos, p.Pos) < 64 ? 0.05f : 0);
        return Math.Clamp(f, 0, 1);
    }

    /// Recount the marks at the top of each tick (men who have dropped you, died or broken no longer count).
    void CountMarks()
    {
        int n = 0;
        foreach (var u in Units)
            if (u.OnCaptain)
            {
                if (!u.Active || u.Routing || !Player.Active || Player.Routing) u.OnCaptain = false;
                else n++;
            }
        MarkedBy = n;
    }

    /// Whether this foe turns from his nearest man to you: the captain's distance shrunk by fame, a free place round you,
    /// and his grade's habit. Returns the target to go for (unchanged when he keeps his man).
    (Unit? unit, float dist) Notice(Unit u, Unit? enemy, float d)
    {
        var p = Player;
        if (u.Team == p.Team || !p.Active || p.Routing || u.Type.Wild || u.IsLeader || u.Squad is not Squad s) { u.OnCaptain = false; return (enemy, d); } // a leader has his own mind (ChampionWants)
        float pd = Vector2.Distance(u.Pos, p.Pos);
        if (enemy == p) return Mark(u, enemy, d);
        if (pd > NoticeRange) { u.OnCaptain = false; return (enemy, d); }
        if (enemy != null && d <= ReachOf(u) + 0.5f && !u.OnCaptain) return (enemy, d); // at blows with someone: he keeps his man
        var grade = s.Grade;
        float factor = 1 - 0.5f * Fame();
        if (grade == Grade.Rabble && Alone(p)) factor *= 0.7f; // the mob rushes a lone captain
        if (grade >= Grade.Veteran && pd > 2.5f)
        {
            // Veterans send their best: one man steps out to meet you; the rest keep to their own fight and cover him.
            var best = MeetsCaptain(s);
            if (best != u) { u.OnCaptain = false; return (enemy, d); }
            factor *= 0.6f;
        }
        if (enemy != null && pd * factor >= d) { u.OnCaptain = false; return (enemy, d); }
        return Mark(u, enemy, d);

        (Unit?, float) Mark(Unit m, Unit? e, float ed)
        {
            if (m.OnCaptain) return (p, pd);
            bool champion = m.Squad?.Meets == m; // the man sent to meet you does not wait for a place
            if (MarkedBy >= MarkCap && !champion) return e == p ? (NotThePlayer(m, ed)) : (e, ed); // no place round you: fight your men
            m.OnCaptain = true;
            MarkedBy++;
            return (p, pd);
        }
    }

    /// No place round the captain: the nearest of your men instead (or the captain still, when nobody else is near).
    (Unit?, float) NotThePlayer(Unit u, float pd)
    {
        Unit? best = null;
        float bd = 10 * 10;
        foreach (var o in Near(u.Pos, 10, nearNotice))
        {
            if (o.Team == u.Team || o.Routing || o.IsPlayer) continue;
            float dd = Vector2.DistanceSquared(o.Pos, u.Pos);
            if (dd < bd) { bd = dd; best = o; }
        }
        return best == null ? (Player, pd) : (best, MathF.Sqrt(bd));
    }

    readonly List<Unit> nearNotice = new(32);

    /// No man of yours within 5 m of you.
    bool Alone(Unit p)
    {
        foreach (var o in Near(p.Pos, 5, nearNotice))
            if (o.Team == p.Team && !o.IsPlayer && !o.Routing && Vector2.DistanceSquared(o.Pos, p.Pos) < 25) return false;
        return true;
    }

    /// The man a veteran company sends to meet you: its sergeant or highest tier man, the nearer to you on a tie; kept while he
    /// stands and is within the notice range.
    public Unit? MeetsCaptain(Squad s)
    {
        if (s.Meets is { Active: true, Routing: false } m && Vector2.Distance(m.Pos, Player.Pos) < NoticeRange) return m;
        Unit? best = null;
        float bestScore = float.MinValue;
        foreach (var u in s.Units)
        {
            if (!u.Active || u.Routing || u.IsLeader) continue;
            float dd = Vector2.Distance(u.Pos, Player.Pos);
            if (dd > NoticeRange) continue;
            float score = u.Type.Tier * 100 + (u.Sergeant ? 50 : 0) - dd;
            if (score > bestScore) { bestScore = score; best = u; }
        }
        return s.Meets = best;
    }

    /// Rabble who just saw you kill one of theirs give ground: a step back, guard toward you. True while it lasts.
    bool GivesGround(Unit u)
    {
        if (u.Team == Player.Team || u.Squad is not Squad s || s.Grade != Grade.Rabble || Time - captainKillAt > GiveGroundFor || !Player.Active) return false;
        if (Vector2.DistanceSquared(u.Pos, captainKillPos) > GiveGroundNear * GiveGroundNear) return false;
        var away = u.Pos - Player.Pos;
        float l = away.Length();
        if (l < 1e-3f || l > GiveGroundNear + 2) return false;
        u.Face(-away);
        u.Wish = away / l * u.Type.Speed * 0.5f;
        u.OnCaptain = false;
        return true;
    }

    /// The captain felled a man: rabble near give ground; his company is shaken in sight of it, more by a famed captain,
    /// and a company you have cut five and ten men out of wavers.
    void CaptainKilled(Unit t)
    {
        captainKillAt = Time;
        captainKillPos = t.Pos;
        if (t.Squad is not Squad s || t.Team == Player.Team) return;
        s.CutByCaptain++;
        float shake = 2 + 4 * Fame() + (s.CutByCaptain is 5 or 10 ? 10 : 0);
        foreach (var u in s.Units)
            if (u.Active && !u.Routing && Vector2.DistanceSquared(u.Pos, Player.Pos) < 25 * 25 && u.Mods.Fury <= 0) u.Morale -= shake;
    }

    /// The captain fell: the company that brought him down takes heart for the rest of the battle.
    void CaptainBroughtDown(Unit killer)
    {
        if (killer.Squad is not Squad s || killer.Team == Player.Team) return;
        s.Heartened = true;
        foreach (var u in s.Units) if (u.Active) u.Morale = MathF.Min(100, u.Morale + 20);
    }
}
