namespace Ironfield;

/// Ties between the men (docs/COMPANY.md C3). A named man has at most two: Brothers (named in the same battle, or five battles
/// side by side), Kin (from the same village, one in four), Rivals (two proud men, or near-equal kills), Master and Lad (a man
/// of ten battles and a newly named one). When a brother or kin falls the other remembers it and fights his next battle
/// avenging; the company misses the fallen half again. When a master falls his lad takes his nature. Ties are by soldier id,
/// so they follow men through splits, merges and promotion; a tie to a man gone becomes a memory line on the living one.
/// All on the roll's own dice.
public sealed partial class Campaign
{
    public const int MostTies = 2;
    /// Battles a man needs to take a lad, and the kills two men may differ by and still be rivals.
    public const int LadMasterBattles = 10, RivalKills = 1;

    /// The named men of yours who are alive, by id.
    Soldier? LivingMan(int id) => Player.Companies.SelectMany(k => k.Roll).FirstOrDefault(m => m.Id == id);

    static bool Free(Soldier m) => m.Ties.Count < MostTies;
    static bool TiedTo(Soldier a, Soldier b) => a.Ties.Any(t => t.Other == b.Id);

    /// Tie two men (both ways); refused when either is full or they are tied already.
    static bool Tie(Soldier a, Soldier b, string kind, string back)
    {
        if (a == b || !Free(a) || !Free(b) || TiedTo(a, b)) return false;
        a.Ties.Add(new SoldierTie { Other = b.Id, Kind = kind });
        b.Ties.Add(new SoldierTie { Other = a.Id, Kind = back });
        return true;
    }

    /// A man just named: brothers with another named this battle, else a lad to a veteran, else kin from his village or a
    /// rival of a proud man.
    void TieOnNaming(CompanyRecord c, Soldier m)
    {
        var others = c.Roll.Where(o => o != m && Free(o)).ToList();
        if (others.FirstOrDefault(o => o.Joined == m.Joined && o.Battles == 0 && o.Deeds.Count == 1 && m.Joined > 1) is Soldier same && Tie(m, same, "Brothers", "Brothers")) return;
        if (others.Where(o => o.Battles >= LadMasterBattles && !o.Ties.Any(t => t.Kind == "Master")).OrderByDescending(o => o.Battles).FirstOrDefault() is Soldier master
            && RollRng.Chance(0.5f) && Tie(m, master, "Lad", "Master")) return; // he is the lad; the other his master
        if (m.Home != "" && others.FirstOrDefault(o => o.Home == m.Home) is Soldier kin && RollRng.Chance(0.25f) && Tie(m, kin, "Kin", "Kin")) return;
        if (m.Nature == "Proud" && others.FirstOrDefault(o => o.Nature == "Proud") is Soldier proud) Tie(m, proud, "Rivals", "Rivals");
    }

    /// After a battle: men five battles side by side become brothers; two with near-equal kills become rivals.
    void TiesAfterBattle(CompanyRecord c)
    {
        var free = c.Roll.Where(m => Free(m) && !m.Hurt).ToList();
        foreach (var a in free.Where(m => m.Battles + 1 == 5))
            if (free.FirstOrDefault(b => b != a && b.Battles + 1 >= 5 && Free(b) && Free(a)) is Soldier b) Tie(a, b, "Brothers", "Brothers");
        foreach (var a in free.Where(m => m.Kills >= 5 && Free(m)))
            if (free.FirstOrDefault(b => b != a && b.Kills >= 5 && Math.Abs(b.Kills - a.Kills) <= RivalKills && Free(b)) is Soldier b && RollRng.Chance(0.2f)) Tie(a, b, "Rivals", "Rivals");
    }

    /// A man has gone: those tied to him remember him. A brother or kin who saw him fall fights his next battle avenging;
    /// a lad whose master fell takes the master's nature. True when someone mourns him as brother or kin.
    bool TiesOnFall(Soldier gone, string fate, string where)
    {
        bool mourned = false;
        foreach (var t in gone.Ties.ToList())
        {
            if (LivingMan(t.Other) is not Soldier o) continue;
            o.Ties.RemoveAll(x => x.Other == gone.Id);
            string at = where != "" ? $" against {where}" : "";
            if (fate != "fell") { o.Deed(fate == "left" ? $"Saw {gone.Name} leave the company." : fate == "pensioned" ? $"Saw {gone.Name} go home." : $"Lost {gone.Name}{at}."); continue; }
            switch (t.Kind)
            {
                case "Brothers" or "Kin":
                    o.Deed($"Saw {gone.Name}, his {(t.Kind == "Kin" ? "kinsman" : "brother in arms")}, fall{at}.");
                    o.Avenging = true;
                    mourned = true;
                    break;
                case "Lad": // the fallen was the lad; o his master
                    o.Deed($"Lost his lad {gone.Name}{at}.");
                    break;
                case "Master": // the fallen was the master; o his lad
                    o.Deed($"Saw his master {gone.Name} fall{at}, and took up his ways.");
                    o.Nature = gone.Nature;
                    break;
                default:
                    o.Deed($"Outlived his rival {gone.Name}.");
                    break;
            }
        }
        return mourned;
    }

    /// For the roll's shot only: brothers, a master and his lad, and one man out for vengeance, when the roll has none.
    internal void StageTies(CompanyRecord c)
    {
        var r = c.Roll.OrderByDescending(m => m.Battles).ToList();
        if (r.Count < 4 || r.Any(m => m.Ties.Count > 0)) return;
        Tie(r[0], r[3], "Master", "Lad");
        Tie(r[1], r[2], "Brothers", "Brothers");
        r[2].Avenging = true;
    }

    /// A tie as the roll shows it: "BROTHER TO LORIN BRIGHTBLADE".
    public string TieLine(Soldier m)
    {
        var parts = new List<string>();
        foreach (var t in m.Ties)
        {
            string name = LivingMan(t.Other)?.Name ?? "?";
            parts.Add((t.Kind switch { "Brothers" => "BROTHER TO ", "Kin" => "KIN TO ", "Rivals" => "RIVAL TO ", "Master" => "MASTER TO ", _ => "LAD TO " }) + name.ToUpperInvariant());
        }
        return string.Join(", ", parts) + (m.Avenging ? (parts.Count > 0 ? ", " : "") + "OUT FOR VENGEANCE" : "");
    }
}
