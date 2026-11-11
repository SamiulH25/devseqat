namespace Ironfield;

/// The character creator's choices on a new campaign (Origins.cs shapes the captain; this shapes the world around them).
public sealed partial class Campaign
{
    /// Name, age, the company's name and the birth's and past's world effects: gold, renown, food, horses, the homeland's
    /// lords, and the men who followed you. Called once, at the end of `New`.
    internal void ApplyMaking(CaptainMaking m, Settlement home)
    {
        var me = Me;
        if (m.Name.Trim() != "") me.Name = m.Name.Trim();
        me.Age = Origins.Age(m);
        if (m.Company.Trim() != "") Player.Name = m.Company.Trim();
        int homeland = Player.Faction;
        if (m.Arms >= 0) foreach (var c in Player.Companies) c.Banner = m.Arms; // your arms over your first companies
        foreach (var o in new[] { Origins.Find(m.Birth), Origins.Find(m.Past) }.OfType<OriginDef>())
        {
            foreach (var (verb, a) in o.Effects.Select(Origins.Parse))
            {
                float n = a.Length > 0 && float.TryParse(a[^1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : 0;
                switch (verb)
                {
                    case "gold": S.Gold = MathF.Max(0, S.Gold + n); break;
                    case "renown": Captain.Renown = MathF.Max(0, Captain.Renown + n); break;
                    case "food": Player.Food = MathF.Max(0, Player.Food + n); break;
                    case "horses": Player.Horses = Math.Max(0, Player.Horses + (int)n); break;
                    case "opinion_realm": // the homeland's lords, whether or not you serve them
                        foreach (var l in S.Characters.Where(x => x.Kind == "lord" && !x.Dead && x.Faction == homeland))
                            l.Opinion[S.CaptainChar] = Math.Clamp(l.Opinion.GetValueOrDefault(S.CaptainChar) + n, -100, 100);
                        break;
                    case "men": FollowedYou(a[0], Scaled((int)n), home); break;
                }
            }
            if (o.Line != "") Log(o.Line);
            if (o.Perk != "" && Skills.Perk(o.Perk) is PerkDef p) Log($"From your old life you know {p.Name}.");
        }
    }

    /// Men from the captain's past: into a company of theirs already in the party, else a company of their own.
    void FollowedYou(string troop, int men, Settlement home)
    {
        if (men <= 0) return;
        var same = Player.Companies.FirstOrDefault(c => c.Stacks.Any(s => s.Troop == troop));
        if (same != null) { Rules.AddMen(same, troop, men); return; }
        Player.Companies.Add(NewCompany(troop, Rules.TroopRole(troop), men, 0, home));
    }

    /// The captain's birth and past by name, for the K sheet ("" when the campaign began without the creator).
    public string OriginNote => (Origins.Find(Captain.Birth), Origins.Find(Captain.Origin)) switch
    {
        (OriginDef b, OriginDef p) => $"{b.Name}, once a {p.Name}",
        (null, OriginDef p) => $"Once a {p.Name}",
        _ => "",
    };
}
