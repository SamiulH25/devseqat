using Microsoft.Xna.Framework;

namespace Ironfield;

/// Relieving the line and splitting a company (docs/archive/COMBAT_QOL.md Q2). Relieve: a fresh company marches onto a tired one's
/// line; when it is close behind, the tired men break off and walk back to where the fresh ones stood. Split: one company
/// becomes two halves side by side; both report under the one campaign company (Squad.SplitFrom).
public sealed partial class Battle
{
    /// How near a fresh company must stand to relieve a tired one, and how long the tired men may take breaking off.
    public const float ReliefReach = 60, BreakOffTime = 10;

    /// Reliefs and splits made this tick, for the replay.
    List<(int Tired, int Fresh)>? reliefRecords;
    List<int>? splitRecords;

    /// The company a squad reports under: itself, or the company it was split from.
    public static Squad Root(Squad s) { while (s.SplitFrom != null) s = s.SplitFrom; return s; }

    // ---------- relieve ----------

    /// Why `fresh` cannot relieve `tired` now; null if it can.
    public string? CannotRelieve(Squad tired, Squad fresh)
    {
        if (tired == fresh) return "PICK A TIRED COMPANY AND A FRESH ONE";
        if (tired.Team != fresh.Team || tired.AiControlled || fresh.AiControlled) return "NOT YOUR COMPANY";
        if (tired.Alive == 0 || fresh.Alive == 0 || tired.FoldedInto != null || fresh.FoldedInto != null) return "THAT COMPANY IS GONE";
        if (tired.Division != fresh.Division) return "THEY SERVE IN DIFFERENT DIVISIONS";
        if (tired.Cavalry || fresh.Cavalry) return "HORSE RALLY ON THEIR OWN: RELIEVE FOOT ONLY";
        if (tired.Swarm || fresh.Swarm) return "CREATURES KEEP NO LINE";
        if (tired.RelievedBy != null || fresh.RelievedBy != null || Squads.Any(o => o.RelievedBy == fresh || o.RelievedBy == tired)) return "A RELIEF IS ALREADY UNDER WAY";
        if (fresh.InContact) return $"{fresh.Name.ToUpperInvariant()} IS AT BLOWS ITSELF";
        if (fresh.Alive * 2 < tired.Alive) return $"{fresh.Name.ToUpperInvariant()} IS TOO FEW TO HOLD THAT LINE";
        float d = Vector2.Distance(tired.Center, fresh.Center);
        if (d > ReliefReach) return $"TOO FAR APART ({d:0} M): BRING THEM WITHIN {ReliefReach:0} M";
        return null;
    }

    /// Start a relief: the fresh company takes the tired one's line at once (anchor, front, width); the tired one holds
    /// until the fresh ranks are close behind, then breaks off to the fresh company's old place. Null on success.
    public string? Relieve(Squad tired, Squad fresh, bool record = true)
    {
        if (CannotRelieve(tired, fresh) is string no) return no;
        if (record && Recording != null) (reliefRecords ??= new()).Add((Squads.IndexOf(tired), Squads.IndexOf(fresh)));
        StartRelief(tired, fresh);
        return null;
    }

    void StartRelief(Squad tired, Squad fresh)
    {
        tired.RelievedBy = fresh;
        tired.ReliefTo = fresh.Anchor;
        tired.ReliefAt = Time;
        float width = Math.Min(tired.Columns, Math.Max(1, tired.Alive)) * tired.Spacing;
        fresh.Order = Order.Hold;
        fresh.Target = null;
        fresh.Formation = fresh.Formation is Formation.Square ? Formation.Line : fresh.Formation;
        fresh.Anchor = tired.Anchor;
        fresh.Front = tired.Front;
        fresh.Columns = Math.Max(1, (int)(width / fresh.Spacing) + 1);
        fresh.LayoutN = -1;
        Log($"{fresh.Name} march up to relieve {tired.Name}.");
    }

    /// Each tick: a tired company breaks off once its relief is close behind (or has taken long enough to come), and the
    /// relief ends when the tired men are back.
    void UpdateReliefs()
    {
        foreach (var t in Squads)
        {
            if (t.RelievedBy is not { } f) continue;
            if (f.Alive == 0 || t.Alive == 0) { EndRelief(t); continue; }
            if (t.BreakOffUntil <= 0)
            {
                // The fresh front is within a few paces of the tired company's rear rank, or 25 s have passed.
                int rows = (t.Alive + Math.Max(1, t.Columns) - 1) / Math.Max(1, t.Columns);
                float behind = Vector2.Dot(t.Anchor - f.Centroid(), t.Front);
                if (behind < rows * t.Spacing + 6 || Time - t.ReliefAt > 25)
                {
                    t.BreakOffUntil = Time + BreakOffTime;
                    t.Anchor = t.ReliefTo;
                    t.Order = Order.Hold;
                    t.Target = null;
                    t.Foe = null;
                    t.LayoutN = -1;
                    Log($"{t.Name} fall back through {f.Name}'s ranks.");
                }
            }
            else if (Time >= t.BreakOffUntil || t.Units.All(u => !u.Active || u.Routing || Vector2.DistanceSquared(u.Pos, u.Slot) < 4)) EndRelief(t);
        }
    }

    static void EndRelief(Squad t) { t.RelievedBy = null; t.BreakOffUntil = 0; }

    // ---------- split ----------

    /// Why `s` cannot split now; null if it can.
    public string? CannotSplit(Squad s)
    {
        if (s.Alive == 0 || s.FoldedInto != null) return "THAT COMPANY IS GONE";
        if (s.AiControlled) return "AN ALLY'S COMPANY KEEPS ITS OWN COLOURS";
        if (s.Swarm) return "CREATURES KEEP TO THEIR OWN PACK";
        if (CasterCompany(s)) return "MAGES KEEP THEIR OWN CIRCLE";
        if (s.Alive < 10) return "TOO FEW TO SPLIT (TEN MEN AT LEAST)";
        if (s.InContact) return $"{s.Name.ToUpperInvariant()} IS AT BLOWS: PULL IT OUT FIRST";
        return null;
    }

    /// Split `s` in two side by side: the right half of its line becomes a new company of the same campaign company.
    /// Returns the new half, or null if it cannot.
    public Squad? Split(Squad s, bool record = true)
    {
        if (CannotSplit(s) != null) return null;
        if (record && Recording != null) (splitRecords ??= new()).Add(Squads.IndexOf(s));
        return SplitNow(s);
    }

    Squad SplitNow(Squad s)
    {
        var rt = new Vector2(-s.Front.Y, s.Front.X);
        var movers = s.Units.Where(u => u.Active && !u.Routing && !u.Hero).OrderByDescending(u => Vector2.Dot(u.Pos - s.Anchor, rt)).ThenBy(u => u.Index).ToList();
        movers = movers.Take(movers.Count / 2).ToList();
        float width = Math.Min(s.Columns, Math.Max(1, s.Alive)) * s.Spacing;
        var h = new Squad(s.Name, s.Team)
        {
            Company = s.Company, Cavalry = s.Cavalry, Formation = s.Formation, Drill = s.Drill, Formed = s.Formed, Division = s.Division,
            Front = s.Front, FireAtWill = s.FireAtWill, SplitFrom = s, Mods = s.Mods, Standing = s.Standing, LastSeen = s.LastSeen,
            Anchor = s.Anchor + rt * width / 4, Columns = Math.Max(1, s.Columns / 2), StartCount = movers.Count, PathTimer = Squads.Count % 8 * 0.06f,
        };
        h.StandingIds.AddRange(s.StandingIds);
        h.SeenAt[0] = s.SeenAt[0];
        h.SeenAt[1] = s.SeenAt[1];
        foreach (var u in movers)
        {
            s.Units.Remove(u);
            h.Units.Add(u);
            u.Squad = h;
            h.MarchSpeed = MathF.Min(h.MarchSpeed, u.Type.Speed);
        }
        s.MarchSpeed = s.Units.Where(u => !u.Hero).Select(u => u.Type.Speed).DefaultIfEmpty(float.MaxValue).Min();
        s.StartCount -= movers.Count;
        s.Anchor -= rt * width / 4;
        s.Columns = Math.Max(1, s.Columns / 2);
        s.Alive -= movers.Count;
        h.Alive = movers.Count;
        s.Order = h.Order = Order.Hold;
        s.Target = h.Target = null;
        s.LayoutN = -1;
        s.Division?.Squads.Add(h);
        h.Id = Squads.Count;
        Squads.Add(h);
        Bounds(s);
        Bounds(h);
        Log($"{s.Name} split in two: {movers.Count} men form a second company.");
        return h;
    }

    /// Replay: repeat a recorded relief or split.
    public void ReplayRelief(int tired, int fresh) => StartRelief(Squads[tired], Squads[fresh]);
    public void ReplaySplit(int s) => SplitNow(Squads[s]);
}
