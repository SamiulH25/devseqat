namespace Ironfield;

/// Faith and craft on the map (docs/COMPANIONS_MAGIC.md C2, C4, M4): camp rites a priest or a cunning one (a companion of
/// that calling, or a mage company of that tradition) can work, the relic you carry into battle, and the Omen craft draws.
public sealed partial class Campaign
{
    /// The rites: id, name, who can work it ("faith" or "craft"), crowns, Omen, and what it does in a line.
    public static readonly (string Id, string Name, string Tradition, int Gold, float Omen, string Line)[] RiteList =
    {
        ("bless", "BLESS THE COMPANY", "faith", 50, 0, "THREE DAYS STEADIER, IN BATTLE TOO"),
        ("poultices", "POULTICES", "faith", 40, 0, "THE WOUNDED HEAL HALF AGAIN AS FAST FOR A WEEK"),
        ("mend_core", "MEND THE CORE", "faith", 80, 0, "A CRACKED CORE MENDS: YOUR AURA IS WHOLE AGAIN"),
        ("bones", "READ THE BONES", "craft", 0, 3, "SEE TWICE AS FAR FOR A DAY"),
        ("rain", "CALL RAIN", "craft", 0, 4, "SOAKS THIS PROVINCE: HEAVY GOING FOR HORSE"),
        ("marks", "CRAFT MARKS", "craft", 0, 3, "NEXT BATTLE YOUR AURA BLADE LEAVES FEVER"),
        ("cleanse", "CLEANSE", "faith", 100, 0, "IN A TOWN: THE OMEN FALLS BY 10"),
    };
    public const float OmenEvents = 12, RelicCharges = 3;
    /// The Omen's teeth (docs/MAGIC_AURA.md 3.6, MA8): from OmenStirs the dead rise sooner and in greater numbers, from OmenShuns
    /// one village in three bars its gates; a Cleanse takes CleanseOmen off it.
    public const float OmenStirs = 20, OmenShuns = 30, CleanseOmen = 10;
    /// Does this village bar its gates to you for what you work (a third of them, while the Omen is high)?
    public bool OmenShunned(Settlement s) => s.Kind == SettlementKind.Village && S.Omen >= OmenShuns && s.Id % 3 == 0;

    /// Can someone in your company work this tradition: a companion of its calling, or a mage company that casts it?
    public bool CanWork(string tradition) =>
        Companions.Any(k => S.Hours >= k.WoundedUntil && k.Calling == (tradition == "faith" ? "Priest" : "Cunning"))
        || Player.Companies.Any(c => c.Men > 0 && c.Stacks.Any(st => st.Men > 0 && st.Type.Spells.Any(id => Abilities.Get(id).Tradition == tradition)));

    /// Work a rite (one a day): null on success, else why not.
    public string? Rite(string id)
    {
        var r = RiteList.FirstOrDefault(x => x.Id == id);
        if (r.Id == null) return "NO SUCH RITE";
        if (!CanWork(r.Tradition)) return r.Tradition == "faith" ? "NEEDS A PRIEST OR MAGES OF THE FAITH" : "NEEDS A CUNNING ONE OR MAGES OF THE CRAFT";
        if (S.RiteDay == Day + 1) return "ONE RITE A DAY";
        if (S.Gold < r.Gold) return $"NEEDS {r.Gold} CROWNS";
        if (id == "mend_core" && !Captain.Wounds.Contains("core")) return "YOUR CORE IS WHOLE";
        if (id == "cleanse" && !InTown(Player)) return "IN A TOWN, AT ITS CHAPEL";
        if (id == "cleanse" && S.Omen <= 0) return "THERE IS NO OMEN TO CLEANSE";
        S.Gold -= r.Gold;
        if (r.Tradition == "craft") BreakVow("no_craft", "work the craft");
        S.Omen += r.Omen;
        S.RiteDay = Day + 1;
        switch (id)
        {
            case "bless": AddBuff("bless_rite", 3); break;
            case "poultices": AddBuff("poultices", 7); break;
            case "mend_core": MendCore("Your priest's prayers close the crack in you."); break;
            case "bones": S.FarSightUntil = (float)S.Hours + 24; break;
            case "marks": AddBuff("craft_marks", 7); break;
            case "cleanse": S.Omen = MathF.Max(0, S.Omen - CleanseOmen); break;
            case "rain":
                var pr = S.Provinces.FirstOrDefault(p => p.Id == Map.ProvinceAt(Player.Pos).Id);
                if (pr != null) pr.Wet = MathF.Min(1, pr.Wet + 0.5f);
                break;
        }
        if (r.Tradition == "craft") CompanionsReact("craft");
        Log(id switch
        {
            "bless" => "Your priest blesses the company.",
            "poultices" => "Herbs and dressings for the wounded.",
            "mend_core" => "Candles and long prayers through the night.",
            "bones" => "The bones are read. Far parties show themselves.",
            "marks" => "Old marks are cut into your blade and painted on your brow.",
            "cleanse" => "Candles, salt and water at the chapel. The air feels clean again.",
            _ => "Rain comes on as the old words are spoken.",
        });
        return null;
    }

    /// The relic you carry (a saint's, found in a ruin): its blessing goes before your companies while charges last, a charge a
    /// battle; a shrine fills it again.
    public bool RelicReady => S.Relic && S.RelicCharges > 0;

    /// A battle was fought: the relic spends a charge.
    void RelicSpent() { if (RelicReady) S.RelicCharges--; }

    /// A shrine fills the relic.
    void RelicFilled() { if (S.Relic && S.RelicCharges < RelicCharges) { S.RelicCharges = (int)RelicCharges; Log("The relic is blessed again at the shrine."); } }
}
