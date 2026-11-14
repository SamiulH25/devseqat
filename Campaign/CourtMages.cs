namespace Ironfield;

/// The enemy's arcana (docs/ARCANA.md AR5): a lord's circle is led by a named court mage, a Stormcaller or a Father of the
/// Circle, who rides with it and falls with it. The bigger brigand bands of the later campaign bring cunning folk. Names come
/// from the lord's id, not the campaign's dice, so the world's draws stay as they were.
public sealed partial class Campaign
{
    /// A tradition's title for the head of a circle.
    public static string MageTitle(string tradition) => tradition == "faith" ? "Father of the Circle" : "Stormcaller";

    /// Name the mage who leads a lord's new circle (and call the circle after him).
    internal void NameCourtMage(Party lord, CompanyRecord circle)
    {
        if (CourtMageOf(lord) != null) return;
        string culture = Factions.CultureOf(Math.Max(0, lord.Faction));
        var names = Names.Get().LordsOf(culture);
        string first = names[(lord.Id * 7 + circle.Id) % names.Count];
        string title = MageTitle(CircleTradition(circle));
        var mage = new Character
        {
            Id = S.NextId++, Kind = "mage", Name = $"{first} the {title}", Faction = lord.Faction, Culture = culture,
            Age = 40 + lord.Id % 30, PartyId = lord.Id, CompanyId = circle.Id,
        };
        S.Characters.Add(mage);
        circle.Name = $"Circle of {first}";
    }

    /// The court mage riding with a party now (his circle still stands), if any.
    public Character? CourtMageOf(Party p) =>
        S.Characters.FirstOrDefault(ch => ch.Kind == "mage" && !ch.Dead && ch.PartyId == p.Id && p.Companies.Any(c => c.Id == ch.CompanyId && c.Men > 0));

    /// After a battle: a court mage whose circle was cut down falls with it (a deed if you were there).
    internal void CourtMagesAfterBattle(IEnumerable<Party> parties, bool playerIn)
    {
        foreach (var p in parties)
            foreach (var mage in S.Characters.Where(ch => ch.Kind == "mage" && !ch.Dead && ch.PartyId == p.Id).ToList())
            {
                if (p.Companies.Any(c => c.Id == mage.CompanyId && c.Men > 0)) continue;
                mage.Dead = true;
                if (playerIn && p.Faction != Captain.Contract) Deed($"{mage.Name}, court mage of {p.Name}, falls with his circle.", "mages");
                else Log($"{mage.Name}, court mage of {p.Name}, has fallen.");
            }
    }

    /// Cunning folk among the brigands (AR5): from day 90 one big band in five of tier III or more brings a few hedge witches.
    internal void CunningFolk(Party band)
    {
        if (Day < 90 || band.Tier < 3 || band.Id % 5 != 0) return;
        string witch = Armies.MageTroop("crimson", 4);
        var c = NewCompany(witch, "casters", Math.Max(2, (int)(Rules.CapOf(witch, "casters") * 0.5f)), 0);
        c.Name = "Cunning Folk";
        band.Companies.Add(c);
    }
}
