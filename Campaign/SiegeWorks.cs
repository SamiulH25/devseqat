using Microsoft.Xna.Framework;

namespace Ironfield;

/// Engines of war on the campaign map (docs/archive/SIEGECRAFT.md A3, A4): siege works a camp builds, the battering they give a wall,
/// a company's train of light engines, and what all of them bring to a battle or an auto-resolve.
public sealed partial class Campaign
{
    public const int MaxWorks = 4, MaxTrain = 4;
    /// Each engine in the train slows the march this much.
    public const float TrainDrag = 0.04f;

    public static IEnumerable<EngineDef> SiegeWorkDefs => Engines.All.Where(e => e.Build > 0).OrderBy(e => e.Build);
    public static IEnumerable<EngineDef> TrainDefs => Engines.All.Where(e => e.Train).OrderBy(e => e.Cost);

    /// The works p's camp has at the place it besieges now (none once it lifts the siege or the assault is over).
    public IReadOnlyList<SiegeWork> WorksOf(Party p) => p.SiegeTarget >= 0 && p.WorksAt == p.SiegeTarget ? p.SiegeWorks : Array.Empty<SiegeWork>();
    public IEnumerable<string> ReadyWorks(Party p) => WorksOf(p).Where(w => w.ReadyAt <= S.Hours).Select(w => w.Id);

    public string? CannotBuildWork(Settlement s, string id)
    {
        if (!Engines.Has(id) || Engines.Get(id).Build <= 0) return "NOT A SIEGE WORK";
        if (Besieging != s) return "BESIEGE IT FIRST";
        if (WorksOf(Player).Count >= MaxWorks) return $"YOUR CAMP HAS ENOUGH IN HAND ({MaxWorks} WORKS)";
        if (S.Gold < Engines.Get(id).Cost) return "NOT ENOUGH CROWNS";
        return null;
    }

    /// Queue a work at your siege: paid now, built after the works before it, in half the time with Siegecraft.
    public string? BuildWork(Settlement s, string id)
    {
        if (CannotBuildWork(s, id) is string why) return why;
        var def = Engines.Get(id);
        S.Gold -= def.Cost;
        Queue(Player, def, Has("Siegecraft") ? 0.5f : 1);
        LogYours($"Your engineers start on a {def.Name.ToLowerInvariant()}.");
        AudioEngine.Current?.Play(Cue.AnvilRing);
        return null;
    }

    void Queue(Party p, EngineDef def, float pace)
    {
        if (p.WorksAt != p.SiegeTarget) { p.SiegeWorks.Clear(); p.WorksAt = p.SiegeTarget; }
        double start = Math.Max(S.Hours, p.SiegeWorks.Select(w => w.ReadyAt).DefaultIfEmpty(0).Max());
        p.SiegeWorks.Add(new SiegeWork { Id = def.Id, ReadyAt = start + def.Build * pace });
    }

    /// Daily, for each place under siege: lords' camps put up a mangonel (a great lord a trebuchet too), the stone engines
    /// that are ready batter the walls, and a place no longer besieged mends its walls.
    void BatterDaily()
    {
        foreach (var s in S.Settlements)
        {
            if (s.Kind == SettlementKind.Village) continue;
            if (!s.Besieged) { s.Battered = MathF.Max(0, s.Battered - 0.1f); continue; }
            var camp = S.Parties.Where(p => p.SiegeTarget == s.Id && !p.Down).ToList();
            foreach (var l in camp.Where(p => p.Kind == PartyKind.Lord && WorksOf(p).Count == 0))
            {
                Queue(l, Engines.Get("mangonel"), 1);
                if (l.Men >= Scaled(70)) Queue(l, Engines.Get("trebuchet"), 1);
            }
            float blows = camp.SelectMany(ReadyWorks).Sum(id => id switch { "trebuchet" => 0.2f, "mangonel" => 0.08f, _ => 0 });
            if (blows <= 0) continue;
            bool was = s.Battered >= 1;
            s.Battered = MathF.Min(1, s.Battered + blows / (1 + 0.5f * Level(s, "walls")));
            if (!was && s.Battered >= 1 && (camp.Contains(Player) || s.Owner == Player.Id)) LogYours($"The walls of {s.Name} are breached.");
        }
    }

    /// The defenders' edge in an assault on paper (SE8): ladders or the gate alone, walls level, supplies,
    /// less what battering, saps, ditch-filling and camp engines have done.
    public float WallFactor(Settlement s, bool ladders, IEnumerable<string> works)
    {
        var w = works.ToList();
        float f = (ladders ? 1.4f : 1.7f) + 0.1f * Level(s, "walls") - 0.3f * s.Battered;
        if (s.Supplies < 10) f -= 0.12f * (1f - s.Supplies / 10f); // starving garrison loses nerve
        if (s.DitchFilled) f -= 0.05f;
        if (SapReady(s)) f -= 0.15f;
        if (w.Contains("ram")) f -= 0.08f;
        f -= 0.08f * Math.Min(2, w.Count(x => x == "tower"));
        f -= 0.04f * Math.Min(3, w.Count(x => x is "mangonel" or "trebuchet"));
        return MathF.Max(1.05f, f);
    }

    /// True if a sap mined under this place's wall is ready to spring a breach (SE6).
    public bool SapReady(Settlement s) => s.SapReadyAt > 0 && s.SapReadyAt <= S.Hours;

    /// Start mining a sap under the wall (3 days, 1.5 with Siegecraft; 180 crowns) to open a breach (SE6).
    public string? MineSap(Settlement s)
    {
        if (Besieging != s) return "BESIEGE IT FIRST";
        if (s.SapReadyAt > 0) return SapReady(s) ? "THE SAP IS READY" : "ALREADY MINING A SAP";
        if (S.Gold < 180) return "NEED 180 CROWNS";
        S.Gold -= 180;
        s.SapReadyAt = (float)S.Hours + (Has("Siegecraft") ? 36f : 72f);
        LogYours($"Your sappers begin a mine under the walls of {s.Name}.");
        return null;
    }

    /// Fill the ditch outside the wall (1 day's labour, 80 crowns) so attackers cross smoothly (SE6).
    public string? FillDitch(Settlement s)
    {
        if (Besieging != s) return "BESIEGE IT FIRST";
        if (Level(s, "walls") < 2) return "NO DITCH TO FILL";
        if (s.DitchFilled) return "THE DITCH IS ALREADY FILLED";
        if (S.Gold < 80) return "NEED 80 CROWNS";
        S.Gold -= 80;
        s.DitchFilled = true;
        LogYours($"Your men fill the ditch before {s.Name} with fascines.");
        return null;
    }

    /// Sections already down when an assault begins: a battered wall and a finished sap each open a breach.
    public int BreachesOf(Settlement s) => (s.Battered >= 1 ? 1 : 0) + (SapReady(s) ? 1 : 0);

    // ---------- the train (A4) ----------

    /// The light engines a party brings to the field: yours, bought; a lord's, by the size of his host.
    public IEnumerable<string> TrainOf(Party p) => p.Kind == PartyKind.Player ? p.Train
        : p.Kind == PartyKind.Lord ? (p.Men >= Scaled(70) ? new[] { "scorpion", "springald" } : p.Men >= Scaled(40) ? new[] { "scorpion" } : Array.Empty<string>())
        : Array.Empty<string>();

    /// Engines each side brings to a battle: the trains of every party on it, and in an assault your camp's ready works.
    public List<EngineSpec> EnginesFor(BattleSetup setup, bool assault)
    {
        var list = new List<EngineSpec>();
        foreach (var p in setup.Side0) foreach (var id in TrainOf(p)) list.Add(new EngineSpec { Team = 0, Id = id, Ally = p != Player });
        if (assault) foreach (var id in ReadyWorks(Player)) list.Add(new EngineSpec { Team = 0, Id = id });
        else foreach (var p in setup.Side1) foreach (var id in TrainOf(p)) list.Add(new EngineSpec { Team = 1, Id = id });
        return list;
    }

    /// An auto-resolve edge from the engines on each side: a few percent each, at most four.
    public float TrainEdge(IEnumerable<Party> side0, IEnumerable<Party> side1)
    {
        static float E(int n) => 1 + 0.03f * Math.Min(4, n);
        return E(side0.Sum(p => TrainOf(p).Count())) / E(side1.Sum(p => TrainOf(p).Count()));
    }

    public string? CannotBuyEngine(Settlement s, string id)
    {
        if (!Engines.Has(id) || !Engines.Get(id).Train) return "NOT SOLD HERE";
        if (s.Kind != SettlementKind.Town) return "ONLY A TOWN'S WORKSHOPS BUILD ENGINES";
        if (Player.Train.Count >= MaxTrain) return $"YOUR TRAIN IS FULL ({MaxTrain} ENGINES)";
        if (S.Gold < Engines.Get(id).Cost) return "NOT ENOUGH CROWNS";
        return null;
    }

    public string? BuyEngine(Settlement s, string id)
    {
        if (CannotBuyEngine(s, id) is string why) return why;
        S.Gold -= Engines.Get(id).Cost;
        Player.Train.Add(id);
        LogYours($"A {Engines.Get(id).Name.ToLowerInvariant()} joins your train.");
        return null;
    }

    /// Sell one back to the workshop for half its price.
    public string? SellEngine(string id)
    {
        if (!Player.Train.Remove(id)) return "YOU HAVE NONE";
        S.Gold += Engines.Get(id).Cost / 2;
        return null;
    }

    /// A lost battle loses the train: the enemy takes the engines it overran.
    void TrainLost()
    {
        if (Player.Train.Count == 0) return;
        LogYours(Player.Train.Count == 1 ? "Your engine is lost to the enemy." : $"Your {Player.Train.Count} engines are lost to the enemy.");
        Player.Train.Clear();
    }
}
