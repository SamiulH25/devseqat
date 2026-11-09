using Microsoft.Xna.Framework;

namespace Ironfield;

/// How well a company is trained (Grade, from its Drill).
public enum Grade { Rabble, Trained, Veteran, Elite }

/// Battle: drill and order (docs/MELEE.md D1-D2). Drill is a company's training, fixed for the battle; Formed is how
/// well its ranks hold right now. Men read Drill x Formed (Squad.Discipline); below Formed 0.4 the ranks are broken.
public partial class Battle
{
    /// Drill by troop tier 1-7.
    static readonly float[] TierDrill = { 0.10f, 0.10f, 0.22f, 0.38f, 0.50f, 0.64f, 0.78f, 0.92f };

    /// A troop's own drill: its data's, else its tier's. Creatures have none (the raised dead a little: they keep their
    /// places, mindlessly; their order never breaks, see UpdateFormed).
    public static float DrillOf(UnitType t) => t.Drill >= 0 ? t.Drill : t.Has("raised") ? 0.25f : t.Wild ? 0 : TierDrill[Math.Clamp(t.Tier, 0, 7)];

    /// A company's drill: its men's (averaged over a mixed company), plus cohesion (up to +0.1), its officer
    /// (Veteran +0.05, Hothead -0.05) and a companion at its head (+0.05). A sergeant adds his skill (AddDivision).
    public static float DrillOf(Squad s, Company? c = null)
    {
        float sum = 0;
        int n = 0;
        foreach (var u in s.Units) if (!u.Hero) { sum += DrillOf(u.Type); n++; }
        if (s.Swarm || n == 0) return 0;
        float d = sum / n;
        if (c != null)
        {
            d += Math.Clamp(c.Cohesion, 0, 30) / 30f * 0.1f;
            if (c.Has("Veteran")) d += 0.05f;
            if (c.Has("Hothead")) d -= 0.05f;
            if (c.Hero.Length > 0) d += 0.05f;
        }
        return Math.Clamp(d, 0, 1);
    }

    public static Grade GradeOf(float drill) => drill < 0.30f ? Grade.Rabble : drill < 0.56f ? Grade.Trained : drill < 0.85f ? Grade.Veteran : Grade.Elite;
    public static string GradeName(Grade g) => g switch { Grade.Rabble => "Rabble", Grade.Trained => "Trained", Grade.Veteran => "Veteran", _ => "Elite" };

    /// A man of the company falls: the gap shakes the ranks, less in a drilled company.
    static void FellFromRanks(Unit u)
    {
        if (u.Squad is not { } s || s.StartCount == 0) return;
        s.Formed = MathF.Max(0, s.Formed - 0.6f / s.StartCount * (1.2f - s.Drill));
    }

    /// Struck in the flank (or the rear, twice as much): the ranks turn and lose their order.
    static void Flanked(Squad s, bool rear) => s.Formed = MathF.Max(0, s.Formed - (rear ? 0.02f : 0.01f) * (1.2f - s.Drill));

    /// Each tick: order is lost by running, by the press of a melee (rabble only), on bad ground and when its own men run;
    /// won back out of contact (and slowly in it, by veterans), quicker with a sergeant or your banner near.
    void UpdateFormed(Squad s, float dt)
    {
        if (s.Swarm || s.Cavalry || s.Alive == 0) return;
        if (s.Units[0].Type.Has("raised")) { s.Formed = 1; return; } // the dead feel nothing
        bool contact = false;
        int routing = 0;
        foreach (var u in s.Units)
        {
            if (!u.Active) continue;
            if (u.Routing) routing++;
            else if (u.EnemyDist < 3) contact = true;
        }
        s.InContact = contact;
        float f = s.Formed;
        if (s.Order == Order.Charge && s.Foe != null && !contact) f -= 0.0075f * (1 - s.Drill) * MathF.Min(s.MarchSpeed, 6) * dt; // a long run opens the ranks
        if (contact && s.Drill < 0.3f && !Overwhelms(s)) f -= (0.3f - s.Drill) * 0.6f * dt; // rabble cannot keep a line at blows (swarming a beast is no line)
        f -= MathF.Min(0.1f, routing * 0.02f) * dt;
        float rate = contact ? GradeOf(s.Drill) >= Grade.Veteran ? s.Drill * 0.05f : 0 : s.Drill * 0.25f;
        if (rate > 0 && Steadied(s)) rate *= 1.5f;
        f += rate * dt;
        float ground = Terrain.GroundDefAt(s.Anchor).Order;
        if (ground < 0.6f && f > ground) f = MathF.Max(ground, f - 0.2f * dt); // woods and water break up any ranks
        s.Formed = Math.Clamp(f, 0, 1);
        if (!s.Broken && s.Formed < 0.4f) s.Broken = true;
        else if (s.Broken && s.Formed >= 0.55f) s.Broken = false;
    }

    /// A sergeant of its division, or your banner (your side), within 10 m of the company's edge.
    bool Steadied(Squad s)
    {
        float r = (10 + s.Radius) * (10 + s.Radius);
        if (s.Division?.Leader is { Active: true } sgt && Vector2.DistanceSquared(sgt.Pos, s.Center) < r) return true;
        return s.Team == 0 && BannerUp && Player.Active && Vector2.DistanceSquared(Player.Pos, s.Center) < r;
    }

    readonly List<Unit> nearGuard = new(32);

    /// How a man's guard holds against a blow: worse with two or more foes on him (15 % less for each past the
    /// first, at least 40 % left); better in his rank with a friend at each shoulder and his company in good order;
    /// worse when his company's ranks are broken.
    float RankGuard(Unit t)
    {
        int foes = 0;
        bool left = false, right = false;
        var side = new Vector2(-t.Dir.Y, t.Dir.X);
        foreach (var o in Near(t.Pos, 2.5f, nearGuard))
        {
            if (o == t || !o.Active || o.Routing) continue;
            var d = o.Pos - t.Pos;
            float d2 = d.LengthSquared();
            if (o.Team != t.Team) { if (d2 < 6.25f) foes++; }
            else if (d2 < 3.4f && o.Squad == t.Squad) { float x = Vector2.Dot(d, side); if (x > 0.6f) left = true; else if (x < -0.6f) right = true; }
        }
        float g = MathF.Max(0.4f, 1 - 0.15f * Math.Max(0, foes - 1));
        if (t.Squad is { Swarm: false } s)
        {
            if (s.Broken) g *= 0.85f;
            else if (left && right && s.Formed > 0.6f && InOrder(t)) g *= 1 + 0.15f * s.Drill;
        }
        return g;
    }

    /// A blow from a man in his rank, a friend at each shoulder and his company in good order, is harder to stop:
    /// up to 20 % off the defender's block for an elite company.
    float RankEdge(Unit a)
    {
        if (a.Squad is not { Swarm: false, Broken: false } s || s.Formed <= 0.6f || !InOrder(a)) return 1;
        bool left = false, right = false;
        var side = new Vector2(-a.Dir.Y, a.Dir.X);
        foreach (var o in Near(a.Pos, 2f, nearGuard))
        {
            if (o == a || !o.Active || o.Routing || o.Squad != s) continue;
            var d = o.Pos - a.Pos;
            if (d.LengthSquared() >= 3.4f) continue;
            float x = Vector2.Dot(d, side);
            if (x > 0.6f) left = true; else if (x < -0.6f) right = true;
        }
        return left && right ? 1 - 0.2f * s.Drill : 1;
    }

    /// Rabble (and a company under a hothead) holding with an enemy company close by may charge without orders: each
    /// second a roll against (0.3 - drill) x 2 (a hothead at least 1 in 4, from 22 m), unless a sergeant or your banner
    /// is near. Your companies too: the price of levies (docs/MELEE.md D4).
    void BreakRanks(Squad s)
    {
        if (s.BrokeRanks && !s.Broken && !s.InContact) s.BrokeRanks = false; // re-formed: back under orders
        if (s.Order != Order.Hold || s.BrokeRanks || s.Swarm || s.Cavalry || Wall != null || s.Alive == 0 || s.Units.Count == 0) return;
        var t = s.Units[0].Type;
        if (t.Ammo > 0 || t.Wild || t.Has("raised") || t.Leader) return;
        bool hot = s.Company?.Has("Hothead") == true;
        if (s.Grade != Grade.Rabble && !hot) return;
        var foe = NearestFoe(s);
        if (foe == null || Vector2.Distance(foe.Center, s.Center) - foe.Radius - s.Radius > (hot ? 22 : 12) || Steadied(s)) return;
        if (rng.NextSingle() >= MathF.Max(hot ? 0.25f : 0, (0.3f - s.Drill) * 2)) return;
        s.Order = Order.Charge;
        s.Target = foe;
        s.Formed = MathF.Min(s.Formed, 0.3f);
        s.BrokeRanks = true;
        string name = s.Company?.Name is { Length: > 0 } n ? n : s.Name;
        Log(s.Team == 0 ? $"{name} break ranks and charge!" : $"The enemy {name} break ranks!");
        Sound(Cue.Shout, s.Center);
    }
}
