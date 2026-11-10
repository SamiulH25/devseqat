using Microsoft.Xna.Framework;

namespace Ironfield;

/// Folding a company into another in battle (docs/archive/COMBAT_QOL.md Q1): a worn company's standing men join a fuller one and
/// fill its ranks from behind. The fallen and the fled stay with their own colours, so each company's result still goes back to it.
public sealed partial class Battle
{
    /// How far apart two companies may stand and still fold together (metres between their centres).
    public const float MergeReach = 120;

    /// Folds made this tick, for the replay (from, into).
    List<(int From, int Into)>? mergeRecords;

    /// The men of `from` who would walk over: standing, not fleeing, not its companion.
    static IEnumerable<Unit> Movers(Squad from) => from.Units.Where(u => u.Active && !u.Routing && !u.Hero);

    /// Why `from` cannot fold into `into` now; null if it can.
    public string? CannotMerge(Squad from, Squad into)
    {
        if (from == into) return "PICK TWO COMPANIES";
        if (from.Team != into.Team) return "NOT YOUR COMPANY";
        if (from.FoldedInto != null || into.FoldedInto != null || from.Alive == 0 || into.Alive == 0) return "THAT COMPANY IS GONE";
        if (from.AiControlled || into.AiControlled) return "AN ALLY'S COMPANY KEEPS ITS OWN COLOURS";
        if (from.Division != into.Division) return "THEY SERVE IN DIFFERENT DIVISIONS";
        if (from.Swarm || into.Swarm) return "CREATURES KEEP TO THEIR OWN PACK";
        if (CasterCompany(from) || CasterCompany(into)) return "MAGES KEEP THEIR OWN CIRCLE";
        if (from.Cavalry != into.Cavalry) return "HORSE AND FOOT CANNOT FORM ONE COMPANY";
        if (from.Units[0].Type.Ammo > 0 != into.Units[0].Type.Ammo > 0) return "ARCHERS AND MEN AT ARMS FIGHT APART";
        if (from.Units.Any(u => u.Hero && u.Active)) return $"A COMPANION LEADS {from.Name.ToUpperInvariant()}: FOLD THE OTHER INTO IT";
        if (from.InContact) return $"{from.Name.ToUpperInvariant()} IS AT BLOWS: PULL IT OUT FIRST";
        if (Movers(from).Count() == from.Units.Count && Root(from) != Root(into)) return $"{from.Name.ToUpperInvariant()} HAS LOST NO MEN"; // halves of a split company may rejoin whole
        float d = Vector2.Distance(from.Center, into.Center);
        if (d > MergeReach) return $"TOO FAR APART ({d:0} M): BRING THEM WITHIN {MergeReach:0} M";
        return null;
    }

    /// Fold `from` into `into`: its standing men join `into` (they walk to the back of its ranks), the drill and order of
    /// the joined company are the weighted mix, a little shaken. Returns the men moved (0 and nothing done if it cannot).
    public int Merge(Squad from, Squad into, bool record = true)
    {
        if (CannotMerge(from, into) != null) return 0;
        if (record && Recording != null) (mergeRecords ??= new()).Add((Squads.IndexOf(from), Squads.IndexOf(into)));
        return Fold(from, into);
    }

    /// The fold itself, without the checks (a replay repeats what the recording did).
    int Fold(Squad from, Squad into)
    {
        var movers = Movers(from).ToList();
        int n = movers.Count, had = into.Alive;
        if (n == 0) return 0;
        float w = n / (float)(had + n);
        into.Drill = MathHelper.Lerp(into.Drill, from.Drill, w);
        into.Formed = MathHelper.Lerp(into.Formed, from.Formed, w) * (1 - 0.3f * w); // newcomers shuffle the ranks for a while
        foreach (var u in movers)
        {
            from.Units.Remove(u);
            into.Units.Add(u);
            u.Squad = into;
            u.Mods = u.Type.Mods + into.Mods;
            into.MarchSpeed = MathF.Min(into.MarchSpeed, u.Type.Speed);
        }
        // Two unhurt halves rejoining leave one empty: it keeps a stand-in (not on the field, never active) so the
        // company is never without a first man.
        if (from.Units.Count == 0) from.Units.Add(new Unit { Type = movers[0].Type, Team = from.Team, Squad = from, Fled = true, Index = -1, Palette = movers[0].Palette });
        into.StartCount += n;
        from.StartCount -= n;
        into.Alive += n;
        from.Alive = 0;
        into.LayoutN = -1; // deal the places again (BattleRanks.PlaceMen)
        from.FoldedInto = into;
        from.Order = Order.Hold;
        from.Target = null;
        Bounds(into);
        Log($"{from.Name}: {n} men fall in with {into.Name}.");
        return n;
    }

    /// Replay: repeat a recorded fold.
    public void ReplayMerge(int from, int into) => Fold(Squads[from], Squads[into]);
}
