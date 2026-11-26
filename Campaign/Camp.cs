namespace Ironfield;

/// The camp (docs/archive/COMPANY.md C5): what the company's rest looks like. Read only; the screen is MapScreen.Camp.cs.
public sealed partial class Campaign
{
    /// How the camp feels: after a won field the men cheer, after a lost one the fires are few and they sit apart.
    public enum CampMood { Quiet, Won, Lost }

    /// The last battle any company fought, if it was within `Days` days: won or lost. Older than that the camp is quiet.
    public CampMood MoodOfCamp(int days = 2)
    {
        var last = Player.Companies.Select(k => k.Ledger.Count > 0 ? k.Ledger[^1] : null).Where(e => e != null).OrderByDescending(e => e!.Day).FirstOrDefault();
        if (last == null || Day - last.Day > days) return CampMood.Quiet;
        return last.Won ? CampMood.Won : CampMood.Lost;
    }

    /// The companies that keep a fire in the camp: those with named men, most first; then the rest, so every company shows.
    public List<CompanyRecord> CampFires() =>
        Player.Companies.Where(k => k.Stacks.Sum(s => s.Men) > 0 || k.Roll.Count > 0)
            .OrderByDescending(k => k.Roll.Count).ThenByDescending(k => k.Battles).ToList();

    /// The named men at a company's fire (fit ones standing round it) and its hurt ones (at the surgeon's).
    public static (List<Soldier> Fit, List<Soldier> Hurt) AtFire(CompanyRecord k) =>
        (k.Roll.Where(m => !m.Hurt).OrderByDescending(OfBanner).ThenByDescending(m => m.Battles).ToList(),
         k.Roll.Where(m => m.Hurt).ToList());

    /// For the camp's shots only: named men in the four biggest companies (six, four, five and three), ties, two hurt, one of the banner.
    internal void StageCamp()
    {
        for (int g = 0; g < 4 && Player.Companies.Count < 4; g++) Split(Player.Companies.OrderByDescending(k => k.Men).First());
        // Other troops at the other fires, so the shot shows bows and better men too.
        var others = Troops.All.Where(t => !t.Wild && t.Mount == null && t.Tier is >= 2 and <= 4).OrderBy(t => t.Class == UnitClass.Archer ? 0 : 1).ThenByDescending(t => t.Tier).ToList();
        var cos = Player.Companies.OrderByDescending(k => k.Men).Take(4).ToList();
        for (int i = 1; i < cos.Count && i - 1 < others.Count; i++) foreach (var st in cos[i].Stacks) st.Troop = others[(i - 1) * 2 % others.Count].Id;
        int[] count = { 6, 4, 5, 3 };
        string[] deeds = { "Killed 6 against the Red Lances.", "Stood when half the company fell.", "Held the ford alone.", "Pulled his sergeant out of the press." };
        for (int i = 0; i < cos.Count; i++)
        {
            for (int j = cos[i].Roll.Count; j < count[i]; j++)
                if (NameMan(cos[i], deeds[(i + j) % deeds.Length]) is Soldier m) { m.Battles = 2 + j * 3 + i; m.Kills = j * 2 + i; }
            StageTies(cos[i]);
        }
        if (cos.Count > 0 && cos[0].Roll.Count > 0) cos[0].Roll[0].Mentions = BannerMentions;
        foreach (var k in cos.Skip(1).Take(2)) if (k.Roll.Count > 2) k.Roll[^1].Hurt = true;
        if (!CanWork("faith")) // a priest, so the camp rites show
            S.Characters.Add(new Character { Id = S.NextId++, Kind = "companion", Name = "Brother Aldo", Culture = "common", Calling = "Priest", PartyId = Player.Id, CompanyId = Player.Companies[0].Id });
    }
}
