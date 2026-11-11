namespace Ironfield;

/// Effects on the map (docs/archive/COMPANIONS_MAGIC.md A5): buffs your company carries for a few days and into every battle
/// (`BattleOptions.Standing`), and the Omen that craft draws (C2).
public sealed partial class Campaign
{
    /// Buffs that are on now, each once.
    public IEnumerable<string> ActiveBuffs => S.Buffs.Where(b => b.Until > S.Hours).Select(b => b.Effect).Distinct();
    public bool HasBuff(string id) => S.Buffs.Any(b => b.Effect == id && b.Until > S.Hours);
    /// Days left on a buff (0 when it is off).
    public float BuffDays(string id) => (float)Math.Max(0, (S.Buffs.Where(b => b.Effect == id).Select(b => b.Until).DefaultIfEmpty(0).Max() - S.Hours) / 24);

    /// Lay a buff for `days` (a buff already on is renewed, not stacked).
    public void AddBuff(string id, float days)
    {
        if (!Effects.Has(id)) return;
        S.Buffs.RemoveAll(b => b.Effect == id);
        S.Buffs.Add(new Buff { Effect = id, Until = S.Hours + days * 24 });
    }

    /// Daily: ended buffs go; Omen fades a point.
    void BuffsDaily()
    {
        S.Buffs.RemoveAll(b => b.Until <= S.Hours);
        S.Omen = MathF.Max(0, S.Omen - 1);
    }

    /// What your buffs are worth in an auto-resolve: harder blows, less tiring, steadier hearts, as a fought battle feels them.
    public float BuffEdge
    {
        get
        {
            var m = new Mods();
            foreach (var id in ActiveBuffs) m.Add(Effects.Get(id));
            return Math.Clamp(1 + m.Damage - 0.15f * m.Tire - 0.1f * m.Shock, 0.85f, 1.2f);
        }
    }

    public int FeastPrice => (int)MathF.Ceiling(Rules.PerMan(B.FeastPerMan) * Player.Companies.Sum(c => c.Men + c.Wounded));

    /// A feast in town: three days strong and in good heart, and the companies closer.
    public string? Feast(Settlement s)
    {
        if (!InTown(Player)) return "ONLY IN A TOWN";
        if (HasBuff("feasted")) return "THE MEN STILL FEEL THE LAST FEAST";
        if (S.Gold < FeastPrice) return $"NEEDS {FeastPrice} CROWNS";
        S.Gold -= FeastPrice;
        AddBuff("feasted", 3);
        foreach (var c in Player.Companies) c.Cohesion = MathF.Min(100, c.Cohesion + 5);
        Log($"You feast the company in {s.Name}. The men are in good heart.");
        CompanionsReact("feast");
        AudioEngine.Current?.Play(Cue.FeastToast, 0.8f);
        return null;
    }
}
