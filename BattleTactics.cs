using Microsoft.Xna.Framework;

namespace Ironfield;

/// What a company's own wits have it doing (docs/MELEE.md D5): set by Battle.Tactics, read by Close and the HUD.
public enum Plan { None, Hold, Advance, Pin, Flank, Refuse, Reform, Regroup, RollUp, Pursue, Shelter }

/// Battle: company wits (docs/MELEE.md D5). The commander says what (Order, Target, Anchor); the company decides how,
/// as well as its grade allows. Rabble go straight in and chase the broken. Trained companies keep their ranks, wheel to
/// face a threat on the flank and re-form after a win. Veterans also take a pinned enemy in the flank, wrap the ends of a
/// narrower line, step back to re-form when broken and rotate tired men out of the front rank. Elites also plan pincers
/// with a friendly company, pick the decisive target and roll up the enemy line.
public partial class Battle
{
    /// How often a company looks round (in ticks) and how far it sees, by grade.
    static int TacticsEvery(Grade g) => g switch { Grade.Rabble => 120, Grade.Trained => 60, _ => 30 };
    static float SightOf(Grade g) => g switch { Grade.Rabble => 15, Grade.Trained => 25, Grade.Veteran => 40, _ => 60 };

    /// The company's half-second (to two-second) think; a threat on its flank turns a holding company every tick.
    void Tactics(Squad s)
    {
        if ((tick + s.Id * 7) % TacticsEvery(s.Grade) == 0) Wits(s);
        if (s.Threat != null && s.Order != Order.Charge)
        {
            if (s.Threat.Alive == 0) s.Threat = null;
            else if ((s.Threat.Center - s.Anchor).LengthSquared() > 1) s.Front = Vector2.Normalize(s.Threat.Center - s.Anchor);
        }
    }

    /// What the company does next, as well as its grade allows.
    void Wits(Squad s)
    {
        var g = s.Grade;
        if (s.Cavalry) { HorseWits(s); return; }
        if (s.Alive > 0 && s.Units[0].Type.Ammo > 0) BowsWits(s);
        if (!Ranked(s) || s.Alive == 0) { s.Plan = Plan.None; s.Bend = 0; s.Threat = null; return; }
        if (g == Grade.Rabble)
        {
            if (s.InContact) s.Fought = s.Foe;
            if (s.Order == Order.Charge && s.Fought is { Alive: 0 }) { s.Fought = null; SetPlan(s, Plan.Pursue); } // the broken are fair game, as far as they run
            if (s.Plan == Plan.Pursue) s.Pursue = true;
            return;
        }
        float sight = SightOf(g);
        // Broken and not at blows: step back, re-form, go in again (veterans and better).
        if (g >= Grade.Veteran && s.Broken && !s.InContact && s.Order == Order.Charge && s.Plan != Plan.Reform && !s.BrokeRanks)
        {
            s.Resume = s.Target ?? s.Foe;
            s.HoldInPlace();
            s.Anchor -= s.Front * 6;
            s.Bend = 0;
            SetPlan(s, Plan.Reform);
            return;
        }
        if (s.Plan == Plan.Reform)
        {
            if (s.Formed < 0.7f) return;
            s.Order = Order.Charge;
            s.Target = s.Resume is { Alive: > 0 } r ? r : null;
            SetPlan(s, Plan.Advance);
        }
        if (s.Order != Order.Charge)
        {
            s.Threat = FlankThreat(s, sight);
            s.Bend = 0;
            SetPlan(s, s.Threat != null ? Plan.Refuse : Plan.Hold);
            return;
        }
        s.Threat = null;
        if (s.InContact) s.Fought = s.Foe;
        // Its foe has broken: trained men re-form rather than run after it; elites turn on the next company's flank.
        if (s.Fought is { Alive: 0 } && !s.InContact)
        {
            s.Fought = null;
            if (g == Grade.Elite && NextVictim(s, sight) is { } next) { s.Target = next; SetPlan(s, Plan.RollUp); }
            else if (EnemySquadNear(s.Center, 15, 1 - s.Team) == null) { s.HoldInPlace(); s.Bend = 0; SetPlan(s, Plan.Regroup); return; }
        }
        if (g == Grade.Elite && s.Target == null && Choose(s, sight) is { } pick) s.Target = pick; // the decisive target
        var foe = s.Target is { Alive: > 0 } t ? t : s.Foe;
        if (foe == null) return;
        if (!s.InContact)
        {
            // Hit a pinned enemy in the flank; an elite also goes round when a friend will reach the foe first (a pincer).
            var pinner = Pinner(s, foe, g == Grade.Elite);
            if (pinner != null)
            {
                if (s.Plan != Plan.Flank) s.FlankSide = FlankSide(s, foe, pinner);
                SetPlan(s, Plan.Flank);
            }
            else if (s.Plan != Plan.Flank && s.Plan != Plan.RollUp) SetPlan(s, Plan.Advance);
            s.Bend = 0;
            return;
        }
        if (s.Plan is Plan.Advance or Plan.RollUp) SetPlan(s, Plan.Pin);
        if (g >= Grade.Veteran) { Wrap(s, foe, g == Grade.Elite ? 1 : 0.55f); Rotate(s); }
    }

    /// An enemy company coming at its flank or rear while nothing is at its front: within sight, closing, more than 50
    /// degrees off its front.
    Squad? FlankThreat(Squad s, float sight)
    {
        if (s.InContact || s.Formation == Formation.Square) return null;
        Squad? best = null;
        float bd = sight;
        foreach (var e in Squads)
        {
            if (e.Team == s.Team || e.Alive == 0) continue;
            var to = e.Center - s.Center;
            float gap = to.Length() - e.Radius - s.Radius;
            if (gap >= bd || to.LengthSquared() < 1) continue;
            if (Vector2.Dot(Vector2.Normalize(to), s.Front) > 0.64f) { if (gap < 25) return null; continue; } // a foe in front: hold the front
            if (e.Order != Order.Charge && gap > 6) continue; // one standing off is no threat yet
            bd = gap;
            best = e;
        }
        return best;
    }

    /// A friendly company already fighting this foe (or, for an elite, about to reach it first), else null.
    Squad? Pinner(Squad s, Squad foe, bool elite)
    {
        float mine = Vector2.Distance(s.Center, foe.Center) - foe.Radius - s.Radius;
        foreach (var o in Squads)
        {
            if (o == s || o.Team != s.Team || o.Alive == 0 || o.Cavalry || (o.Foe != foe && o.Target != foe)) continue;
            if (o.InContact) return o;
            if (elite && Vector2.Distance(o.Center, foe.Center) - foe.Radius - o.Radius < mine - 3) return o;
        }
        return null;
    }

    /// Which of the foe's flanks to take (+1 his left, -1 his right, from his front): the one away from the friend who
    /// pins him, or, with him straight ahead of the foe, the one this company is nearer.
    static int FlankSide(Squad s, Squad foe, Squad pinner)
    {
        var perp = new Vector2(-foe.Front.Y, foe.Front.X);
        float mine = Vector2.Dot(s.Center - foe.Center, perp), his = Vector2.Dot(pinner.Center - foe.Center, perp);
        if (MathF.Abs(his) > foe.Radius * 0.5f) return his > 0 ? -1 : 1;
        return mine >= 0 ? 1 : -1;
    }

    /// The point a flanking company makes for (where its front rank should stand, and which way it then faces): beside the
    /// foe's end, coming round it wide while still in front of his line.
    static (Vector2 at, Vector2 face) FlankPoint(Squad s, Squad foe, float standoff)
    {
        var perp = new Vector2(-foe.Front.Y, foe.Front.X) * s.FlankSide;
        var at = foe.Center + perp * (foe.Radius * 0.8f + standoff + 0.5f);
        var rel = s.FrontPos - foe.Center;
        if (Vector2.Dot(rel, foe.Front) > 1 && Vector2.Dot(rel, perp) < foe.Radius + 2) at += perp * 5 + foe.Front * 2; // round the end, not across his front
        return (at, -perp);
    }

    /// Bend the outer files forward round the foe's ends where this company is the wider (Squad.Bend, read by Squad.Slot).
    void Wrap(Squad s, Squad foe, float bend)
    {
        if (s.Formation != Formation.Line) { s.Bend = 0; return; }
        var rt = new Vector2(-s.Front.Y, s.Front.X);
        float lo = float.MaxValue, hi = float.MinValue;
        foreach (var o in foe.Units)
        {
            if (!o.Active || o.Routing) continue;
            float x = Vector2.Dot(o.Pos - s.Anchor, rt);
            lo = MathF.Min(lo, x); hi = MathF.Max(hi, x);
        }
        if (lo > hi) { s.Bend = 0; return; }
        float half = (Math.Clamp(s.Columns, 1, Math.Max(1, s.Alive)) - 1) / 2f * s.Spacing, from = MathF.Max(-lo, hi) + 0.8f;
        if (half < from + 1.5f) { s.Bend = 0; return; } // no overlap to use
        if (s.Bend == 0) SetPlan(s, Plan.Flank, wrap: true);
        s.Bend = bend;
        s.BendFrom = from;
    }

    /// A tired front-rank man (stamina under 30) swaps places with the fresh man behind him.
    static void Rotate(Squad s)
    {
        int cols = s.LayoutCols;
        if (cols < 1 || s.Formation is Formation.Square or Formation.Wedge) return;
        foreach (var u in s.Units)
        {
            if (!u.Active || u.Routing || u.Rank != 0 || u.Stamina >= 30 || u.Action != UnitAction.None) continue;
            foreach (var b in s.Units)
                if (b.Active && !b.Routing && b.Place == u.Place + cols && b.Stamina > 60 && b.Action == UnitAction.None)
                {
                    (u.Place, b.Place) = (b.Place, u.Place);
                    (u.Rank, b.Rank) = (b.Rank, u.Rank);
                    break;
                }
        }
    }

    /// An elite's pick when none was ordered: the nearest company within sight, but one wavering, one pinned by a friend
    /// or one with a leader in it counts as nearer.
    Squad? Choose(Squad s, float sight)
    {
        Squad? best = null;
        float bs = float.MaxValue;
        foreach (var e in Squads)
        {
            if (e.Team == s.Team || e.Alive == 0) continue;
            float gap = Vector2.Distance(e.Center, s.Center) - e.Radius - s.Radius;
            if (gap > sight) continue;
            float score = gap;
            if (AvgMorale(e) < 45) score -= 15;
            if (e.InContact) score -= 10;
            if (e.Units.Any(u => u.Active && (u.IsLeader || u.Hero || u.Sergeant))) score -= 8;
            if (score < bs) { bs = score; best = e; }
        }
        return best;
    }

    /// After its foe breaks, an elite's next: the nearest enemy company within sight that a friend is fighting.
    Squad? NextVictim(Squad s, float sight)
    {
        Squad? best = null;
        float bd = sight;
        foreach (var e in Squads)
        {
            if (e.Team == s.Team || e.Alive == 0 || !e.InContact) continue;
            float gap = Vector2.Distance(e.Center, s.Center) - e.Radius - s.Radius;
            if (gap < bd) { bd = gap; best = e; }
        }
        return best;
    }

    static float AvgMorale(Squad s)
    {
        float sum = 0;
        int n = 0;
        foreach (var u in s.Units) if (u.Active && !u.Routing) { sum += u.Morale; n++; }
        return n == 0 ? 0 : sum / n;
    }

    /// Change plan, telling the feed when it is worth hearing about (once per plan per company each 15 s).
    void SetPlan(Squad s, Plan p, bool wrap = false)
    {
        if (s.Plan == p && !wrap) return;
        s.Plan = p;
        if (Time - s.PlanSaid < 15 && s.PlanSaidOf == p) return;
        string name = s.Company?.Name is { Length: > 0 } n ? n : s.Name;
        string? line = p switch
        {
            Plan.Flank => wrap ? $"{name} wrap round their flank!" : $"{name} swing onto their flank!",
            Plan.Refuse => $"{name} wheel to face the flank.",
            Plan.Reform => $"{name} fall back to re-form.",
            Plan.RollUp => $"{name} roll up the line!",
            Plan.Shelter => $"{name} fall back behind the foot.",
            _ => null,
        };
        if (line == null) return;
        s.PlanSaid = Time;
        s.PlanSaidOf = p;
        Log(s.Team == 0 ? line : "Enemy " + char.ToLowerInvariant(line[0]) + line[1..]);
    }
}
