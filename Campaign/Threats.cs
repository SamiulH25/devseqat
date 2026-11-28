using Microsoft.Xna.Framework;

namespace Ironfield;

/// Lord quests and the named threats the world raises (step F8): a lord's favour as a quest, a price on a band's head,
/// brigand kings and pretenders. Their events come through the events' queue (Campaign/EventDirector.cs).
public sealed partial class Campaign
{
    public const int BrigandKingMen = 40;

    // ---------- lord quests (8) ----------

    /// A lord's quest, on the contract machinery: find his father's sword (S3), hunt a lord at war with his realm, or clear the
    /// worst band near his home.
    /// The reward is mostly his favour.
    string? GiveQuest(Character giver)
    {
        var home = PartyById(giver.PartyId) is Party gp && gp.HomeId >= 0 ? SettlementById(gp.HomeId) : null;
        var at = home?.Pos ?? Player.Pos;
        float deadline = (float)S.Hours + 12 * 24;
        Contract? q = null;
        if (rng.Chance(0.35f) && RelicQuest(giver, at) is Contract relic)
            q = relic;
        else if (S.Parties.Where(p => p.Kind == PartyKind.Lord && !p.Down && FactionsAtWar(giver.Faction, p.Faction) && Hostile(Player, p))
                .OrderBy(p => Vector2.Distance(p.Pos, at)).FirstOrDefault() is Party rival)
            q = new Contract { Kind = "hunt", Target = rival.Id, Reward = 150, Text = $"Defeat {rival.Name} for {giver.Name}." };
        else if (S.Parties.Where(p => p.Kind == PartyKind.Bandit && !p.Down).OrderBy(p => Vector2.Distance(p.Pos, at)).FirstOrDefault() is Party band)
            q = new Contract { Kind = "clear", Target = band.Id, Reward = 100, Text = $"Destroy the {band.Name} for {giver.Name}." };
        if (q == null) return "He finds there is nothing you can do after all.";
        q.Id = S.NextId++;
        q.Faction = Captain.Contract;
        q.Deadline = deadline;
        q.Giver = giver.Id;
        S.Contracts.Add(q);
        return $"Quest: {q.Text}";
    }

    /// A price on a band's head, as a contract of your own.
    string? Bounty(Party? band)
    {
        if (band == null || band.Down || S.Contracts.Any(k => k.Target == band.Id)) return null;
        S.Contracts.Add(new Contract
        {
            Id = S.NextId++, Kind = "clear", Target = band.Id, Faction = Captain.Contract, Deadline = (float)S.Hours + 15 * 24,
            Reward = 200 + Rules.PerMan(Rules.Strength(band) / 2) + (band.CharId >= 0 ? 400 : 0), Text = $"Destroy the {band.Name}.",
        });
        return $"A bounty is posted on the {band.Name}.";
    }

    // ---------- named threats (8) ----------

    /// Daily: the largest band, grown past a size, crowns its chief a brigand king; a king grows, and burns villages near him.
    void BrigandKingsDaily()
    {
        var king = S.Parties.FirstOrDefault(p => p.Kind == PartyKind.Bandit && !p.Down && p.CharId >= 0 && CharacterById(p.CharId) is { Kind: "brigand" });
        if (king == null && Day >= 30)
        {
            var big = S.Parties.Where(p => p.Kind == PartyKind.Bandit && !p.Lair && !p.Down && p.HomeId < 0 && p.CharId < 0 && p.Men >= Scaled(BrigandKingMen)).OrderByDescending(p => p.Men).FirstOrDefault();
            if (big != null) Crown(big);
        }
        else if (king != null)
        {
            if (king.Men < Scaled(150) && king.Companies.Count > 0) { var st = king.Companies[0].Stacks[0]; if (Rules.Room(king.Companies[0], st.Troop) > 0) st.Men += Math.Min(Rules.Room(king.Companies[0], st.Troop), Scaled(1)); else if (king.Companies.Count < 5) king.Companies.Add(NewCompany(st.Troop, Rules.TroopRole(st.Troop), Scaled(6), 0)); }
            foreach (var v in S.Settlements.Where(v => v.Kind == SettlementKind.Village && v.ThreatUntil < S.Hours && Vector2.Distance(v.Pos, king.Pos) < 15 * Map.Spread))
            {
                v.ThreatUntil = (float)S.Hours + 7 * 24;
                v.Recruits = 0;
                Raided(v);
            }
        }
    }

    /// A band's chief becomes a brigand king: a named character with a growing host.
    internal void Crown(Party band)
    {
        var n = Names.Get();
        var ch = NewCharacter("brigand", PersonName(Factions.Common), -1, Factions.Common, 2);
        ch.PartyId = band.Id;
        band.CharId = ch.Id;
        band.Name = $"{ch.Name.Split(' ')[0]}'s Host";
        string troop = band.Companies.FirstOrDefault()?.Troop ?? "brevon_fyrd";
        band.Companies.Add(NewCompany(troop, Rules.TroopRole(troop), Scaled(16), 0));
        Log($"{ch.Name} gathers the brigands of the land and calls himself their king.");
        Force("brigand_king", new PendingEvent { Party = band.Id, Lord = ch.Id });
    }

    /// A brigand king or a pretender beaten in the field is gone for good.
    void ThreatFalls(Party p, bool byYou)
    {
        if (CharacterOf(p) is not Character ch) return;
        ch.Dead = true;
        ch.PartyId = -1;
        if (ch.Kind == "brigand") Log($"{ch.Name}, the brigand king, is dead{(byYou ? " by your hand" : "")}.");
        if (ch.Kind == "pretender") Log($"{ch.Name} is beaten and the rising is over.");
        if (byYou) Captain.Renown += ch.Kind == "brigand" ? 25 : 15;
    }

    /// A rising led by a pretender: a named leader, and a seat that opens its gates sooner while he is at large.
    void RaisePretender(Settlement s, Party rebels)
    {
        var n = Names.Get();
        var ch = NewCharacter("pretender", rng.Pick(n.LordsOf(s.Culture)) + " of " + s.Name, s.Faction, s.Culture, 2);
        ch.PartyId = rebels.Id;
        rebels.CharId = ch.Id;
        rebels.Name = $"{ch.Name.Split(' ')[0]}'s Rising";
        rebels.Companies.Add(NewCompany(Factions.Root(s.Culture), "infantry", 12, 0, s));
        Log($"{ch.Name} claims {s.Name} and leads its rebels.");
        if (s.Owner == Player.Id) Force("pretender", new PendingEvent { Settlement = s.Id, Party = rebels.Id, Lord = ch.Id });
    }

    /// While a pretender is abroad his seat goes over at 85 unrest rather than 100.
    float DefectThreshold(Settlement s) => Rebels(s) is Party r && CharacterOf(r) is { Kind: "pretender" } ? 85 : DefectAt;
}
