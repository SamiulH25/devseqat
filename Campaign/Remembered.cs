using Microsoft.Xna.Framework;

namespace Ironfield;

/// A cairn over your named men where they fell (docs/archive/COMPANY.md C7): save state, never part of the map's generation.
public sealed class Cairn
{
    public int Id, Day, Company = -1;
    public Vector2 Pos;
    /// The battle's name, and the named men under the stones (soldier ids, in S.Fallen).
    public string Field = "";
    public List<int> Men = new();
    /// The season the company last honoured it (`Campaign.SeasonCount`), -1 never.
    public int Honoured = -1;
}

/// A captain who died in the field (C7, the opt-in rule): first in the book of the fallen.
public sealed class DeadCaptain
{
    public string Name = "", Field = "";
    public int Day, Age;
}

/// Campaign: what is remembered (C7). Cairns where named men fell, the day each year that a battle cost three or more of them,
/// and the captain who can die. The book of the fallen reads `BookOfTheFallen`.
/// Your last battle as the party screen tells it (docs/archive/COMPANY.md C8): where, against whom, and the named men in it.
public sealed class LastFight
{
    public int Day, Kills, Lost;
    public string Field = "", Foe = "";
    public bool Won;
    /// Each named man the battle touched, and what it was: his deed, a name won, a wound, his fall.
    public List<LastFightMan> Men = new();
}

public sealed class LastFightMan
{
    public int Id;
    public string What = "";
}

public sealed partial class Campaign
{
    /// The most cairns the map keeps (the oldest goes back to grass; the book keeps the men), how near you must ride, the
    /// cohesion a visit gives once a season, the named men a battle must cost for its day to be kept, and the captain's odds.
    public const int MaxCairns = 12, DayFellMen = 3;
    public const float CairnReach = 3.5f, CairnCohesion = 3, CaptainDeathChance = 1 / 6f;

    /// The battle the men being lost now fell in (set by `Apply` for its roll pass).
    string rollField = "";

    /// The season counted from the campaign's start (not the season of the year, `SeasonIndex`), so a cairn is honoured once each.
    public int SeasonCount => (Day - 1) / (DaysPerYear / 4);

    /// After a battle of yours: a cairn over the named men it cost, where it was fought; the oldest goes past the cap.
    void RaiseCairn(string field)
    {
        var men = rollFell.Select(f => f.Soldier).ToList();
        if (men.Count == 0) return;
        var company = rollFell.GroupBy(f => f.Company).OrderByDescending(g => g.Count()).First().Key;
        PlaceCairn(new Cairn { Id = S.NextId++, Day = Day, Company = company, Pos = CairnSpot(), Field = field == "" ? "the field" : field, Men = men, Honoured = SeasonCount });
    }

    /// Open ground beside you for a cairn: the first of a ring of spots clear of every settlement and the sea.
    Vector2 CairnSpot()
    {
        for (int i = 0; i < 40; i++)
        {
            float a = i * 0.9f, d = (3f + i * 0.6f) * Map.Spread;
            var p = Player.Pos + new Vector2(MathF.Cos(a), MathF.Sin(a)) * d;
            if (!Map.InSea(p) && S.Settlements.All(s => Vector2.Distance(s.Pos, p) > 12 * Map.Spread)) return p;
        }
        return Player.Pos + new Vector2(2.5f, 1.5f) * Map.Spread;
    }

    /// Put a cairn on the map; past `MaxCairns` the oldest goes back to grass.
    internal void PlaceCairn(Cairn c)
    {
        S.Cairns.Add(c);
        while (S.Cairns.Count > MaxCairns) S.Cairns.RemoveAt(0);
    }

    /// The names under a cairn.
    public IEnumerable<string> CairnNames(Cairn c) => c.Men.Select(id => S.Fallen.FirstOrDefault(m => m.Id == id)?.Name).Where(n => n != null)!;

    /// Hourly: riding by a cairn the company has not honoured this season raises its event.
    internal void CairnsHourly()
    {
        if (S.Event != null || Sailing) return;
        foreach (var c in S.Cairns)
        {
            if (c.Honoured == SeasonCount || Vector2.Distance(c.Pos, Player.Pos) > CairnReach * Map.Spread) continue;
            c.Honoured = SeasonCount;
            Raise(new PendingEvent { Id = "cairn_visit", Company = Player.Companies.Any(k => k.Id == c.Company) ? c.Company : -1, Cairn = c.Id });
            return;
        }
    }

    /// Daily: on the year's day of a battle that cost `DayFellMen` named men or more, a line, and a camp event for a living man
    /// of the company that fought it.
    internal void DayFellDaily()
    {
        if (Day <= DaysPerYear) return;
        foreach (var g in S.Fallen.Where(m => m.Fate == "fell" && m.DiedDay > 0 && (Day - m.DiedDay) % DaysPerYear == 0 && Day > m.DiedDay)
                     .GroupBy(m => (m.DiedDay, m.Field)).Where(g => g.Count() >= DayFellMen))
        {
            int years = (Day - g.Key.DiedDay) / DaysPerYear;
            string field = g.Key.Field != "" ? g.Key.Field : g.First().DiedAt is { Length: > 0 } foe ? $"the fight with {foe}" : "the field";
            LogYours($"{(years == 1 ? "A year" : $"{years} years")} ago today, {g.Count()} of your named men fell at {field}.");
            if (S.Event != null) continue;
            var k = Player.Companies.FirstOrDefault(c => c.Id == g.First().Company && c.Roll.Any(m => !m.Hurt)) ?? Player.Companies.FirstOrDefault(c => c.Roll.Any(m => !m.Hurt));
            if (k == null) continue;
            var man = k.Roll.Where(m => !m.Hurt).OrderByDescending(m => m.Battles).First();
            Raise(new PendingEvent { Id = "men_day_fell", Company = k.Id, Soldier = man.Id, Field = field, Dead = g.Select(m => m.Id).ToList() });
        }
    }

    /// The book of the fallen: every named man lost, grouped under the battle (its name, or the foe and the day), newest first;
    /// `byCompany` groups under the company instead.
    public List<(string Title, List<Soldier> Men)> BookOfTheFallen(bool byCompany = false)
    {
        var dead = S.Fallen.Where(m => m.Fate is "fell" or "missing").ToList();
        if (byCompany)
            return dead.GroupBy(m => m.CompanyName == "" ? "a company long gone" : m.CompanyName)
                .Select(g => (g.Key, g.OrderByDescending(m => m.DiedDay).ToList())).OrderByDescending(g => g.Item2.Count).ToList();
        return dead.GroupBy(m => (m.DiedDay, Name: m.Field != "" ? m.Field : m.DiedAt != "" ? $"against {m.DiedAt}" : "the field"))
            .OrderByDescending(g => g.Key.DiedDay)
            .Select(g => ($"{g.Key.Name}, day {g.Key.DiedDay}", g.OrderByDescending(m => m.Battles).ToList())).ToList();
    }

    /// The opt-in rule (C7): a captain who falls on a lost field may die. The company goes to the senior companion from the
    /// ranks at once; with none, the campaign ends. Returns the log line, or null when he lives.
    string? MaybeCaptainDies(bool won, LeaderFate fate, string field)
    {
        if (!S.CaptainDeath || won || fate is not (LeaderFate.Wounded or LeaderFate.Captured) || !rng.Chance(CaptainDeathChance)) return null;
        S.DeadCaptains.Add(new DeadCaptain { Name = Me.Name, Field = field == "" ? "the field" : field, Day = Day, Age = Me.Age });
        Deed($"{Me.Name} fell at {(field == "" ? "the field" : field)}, and did not rise.");
        if (Successor is not Character heir)
        {
            S.GameOver = true;
            return $"{Me.Name} is dead. There is no one to take command: your war is over.";
        }
        string old = Me.Name;
        TakeCommand(heir);
        Captain.WoundedUntil = 0;
        return $"{old} is dead. {heir.Name} takes command of the company.";
    }
    /// Shots only (C7): named men lost at two fields, a cairn over the first beside you, and a captain dead before you.
    internal void StageFallen()
    {
        S.Hours += 24 * 60; // two months in, so the days read true
        var cos = Player.Companies.Where(k => k.Roll.Count > 2).Take(2).ToList();
        string[] fields = { "the Battle of Harrow Ford", "the Fight at Greywater" };
        var first = new List<int>();
        for (int i = 0; i < cos.Count; i++)
        {
            rollField = fields[i];
            foreach (var m in cos[i].Roll.Take(i == 0 ? 3 : 2).ToList()) { Lose(cos[i], m, "fell", "the Red Lances"); m.DiedDay = Day - 20 * (2 - i); if (i == 0) first.Add(m.Id); }
        }
        rollField = "";
        PlaceCairn(new Cairn { Id = S.NextId++, Day = Day - 40, Company = cos.FirstOrDefault()?.Id ?? -1, Pos = CairnSpot(), Field = fields[0], Men = first, Honoured = SeasonCount - 1 });
        // The last fight as the party screen tells it: the men who fell at Greywater, and the living with what they did.
        var lf = new LastFight { Day = Day - 20, Field = fields[1], Foe = "the Red Lances", Won = true, Kills = 61, Lost = 23 };
        lf.Men.AddRange(Player.Companies.SelectMany(k => k.Roll).Take(3).Select((m, i) => new LastFightMan { Id = m.Id, What = i == 0 ? "Cut down a captain against the Red Lances." : i == 1 ? "Won his name. Killed 5 against the Red Lances." : "Wounded." }));
        lf.Men.AddRange(S.Fallen.Where(m => m.Field == fields[1]).Select(m => new LastFightMan { Id = m.Id, What = "Fell." }));
        S.LastFight = lf;
        S.DeadCaptains.Add(new DeadCaptain { Name = "Aldric Vey", Field = "the Siege of Corwen", Day = Math.Max(1, Day - 90), Age = 41 });
    }
    /// Your named men as a battle found them (deeds told, hurt), so `NoteLastFight` can say what it did to each.
    readonly Dictionary<int, (int Deeds, bool Hurt)> rollBefore = new();

    void SnapshotRoll()
    {
        rollBefore.Clear();
        foreach (var m in Player.Companies.SelectMany(k => k.Roll)) rollBefore[m.Id] = (m.Deeds.Count, m.Hurt);
    }

    /// After your battle: keep it, and the named men in it, for the party screen. Men who won a name or a deed first, then the
    /// fallen, then the hurt.
    void NoteLastFight(string field, string foe, bool won)
    {
        var f = new LastFight { Day = Day, Field = field, Foe = foe, Won = won };
        foreach (var k in Player.Companies)
            if (k.Ledger.Count > 0 && k.Ledger[^1].Day == Day) { f.Kills += k.Ledger[^1].Kills; f.Lost += k.Ledger[^1].Lost; }
        foreach (var m in Player.Companies.SelectMany(k => k.Roll))
        {
            if (!rollBefore.TryGetValue(m.Id, out var was)) f.Men.Add(new LastFightMan { Id = m.Id, What = "Won his name. " + (m.Deeds.Count > 0 ? m.Deeds[^1] : "") });
            else if (m.Deeds.Count != was.Deeds && m.Deeds.Count > 0) f.Men.Add(new LastFightMan { Id = m.Id, What = m.Deeds[^1] });
            else if (m.Hurt && !was.Hurt) f.Men.Add(new LastFightMan { Id = m.Id, What = "Wounded." });
        }
        foreach (var m in S.Fallen.Where(m => m.DiedDay == Day && rollBefore.ContainsKey(m.Id)))
            f.Men.Add(new LastFightMan { Id = m.Id, What = m.Fate == "missing" ? "Missing." : "Fell." });
        S.LastFight = f;
        rollBefore.Clear();
    }

    /// A man of the last fight, living or fallen.
    public Soldier? ManOfLastFight(int id) => LivingMan(id) ?? S.Fallen.FirstOrDefault(m => m.Id == id);
}
