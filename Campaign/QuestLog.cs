using Microsoft.Xna.Framework;

namespace Ironfield;

/// One line of the quests screen (docs/PLAYTEST.md PT3), built from whatever gave the work: a steward's contract, an elders'
/// job, a lord's quest, an order's charge, a step of a chain. State is ACTIVE, AVAILABLE, DONE or FAILED.
public sealed record QuestView(int Id, string Title, string Giver, Vector2? Place, string Where, float Reward, float Deadline, string State, bool MoreSteps);

/// A finished or failed quest, kept for the quests screen's history.
public sealed class QuestRecord
{
    public string Text = "", Giver = "";
    public float At, Reward;
    public bool Done;
}

public sealed partial class Campaign
{
    /// How many finished quests the history keeps.
    public const int QuestHistoryKept = 30;

    /// Every quest the captain should know of: the ones he holds, offers on boards he has seen (nearest first), and the
    /// history, newest first. Reads the existing sources; changes nothing.
    public List<QuestView> QuestList()
    {
        var list = S.Contracts.Select(c => View(c, "ACTIVE")).ToList();
        list.AddRange(S.Settlements.Where(s => s.Offers.Count > 0)
            .SelectMany(s => s.Offers.Where(o => o.Deadline > S.Hours && OfferValid(o)).Select(o => (s, o)))
            .OrderBy(x => Vector2.Distance(x.s.Pos, Player.Pos)).Take(8)
            .Select(x => View(x.o, "AVAILABLE", x.s)));
        list.AddRange(S.QuestHistory.AsEnumerable().Reverse().Select(h =>
            new QuestView(-1, h.Text, h.Giver, null, "", h.Reward, h.At, h.Done ? "DONE" : "FAILED", false)));
        return list;
    }

    QuestView View(Contract c, string state, Settlement? board = null)
    {
        var (place, where) = PlaceOf(c);
        if (state == "AVAILABLE" && board != null && place == null) (place, where) = (board.Pos, board.Name);
        return new QuestView(c.Id, c.Text, GiverOf(c, board), place, where, c.Reward, c.Deadline, state, c.Then != null);
    }

    /// Who asked for the work, in a few words.
    string GiverOf(Contract c, Settlement? board = null)
    {
        if (c.Order != "" && KnightOrders.Get(c.Order) is OrderDef o) return o.Short;
        if (CharacterById(c.Giver) is Character ch) return ch.Name;
        if (c.From >= 0 && c.From < S.Settlements.Count) return $"The elders of {S.Settlements[c.From].Name}";
        if (board != null) return $"The steward of {board.Name}";
        if (c.Faction >= 0 && c.Faction < FactionNames.Length) return $"A steward of the {FactionNames[c.Faction]}";
        return "Your own";
    }

    /// Where the work is: the quarry's band, the town or village, the site; null when it has no one place (an escort not yet
    /// set up, a quarry gone).
    public (Vector2? Place, string Where) PlaceOf(Contract c)
    {
        switch (c.Kind)
        {
            case "hunt" or "clear" or "rescue" or "escort":
                return PartyById(c.Target) is Party p && !p.Down ? (p.Pos, p.Name) : (null, "");
            case "visitsite":
                return Sites.FirstOrDefault(x => x.Id == c.Target) is Site site ? (site.Pos, site.Name) : (null, "");
            default:
                return c.Target >= 0 && c.Target < S.Settlements.Count ? (S.Settlements[c.Target].Pos, S.Settlements[c.Target].Name) : (null, "");
        }
    }

    /// A quest finished or failed goes into the history (the last steps of chains only; a step done is not the end).
    void RecordQuest(Contract c, bool done)
    {
        S.QuestHistory.Add(new QuestRecord { Text = c.Text, Giver = GiverOf(c), At = (float)S.Hours, Reward = c.Reward, Done = done });
        if (S.QuestHistory.Count > QuestHistoryKept) S.QuestHistory.RemoveAt(0);
        if (S.PinnedQuest == c.Id) S.PinnedQuest = c.Then is Contract next && done ? next.Id : -1; // the pin follows the chain
    }

    /// For the quests screen shot: take two jobs from the nearest town's board and one from a village's elders (the rest stay
    /// on offer), pin the first, and remember one done and one failed.
    public void StageQuests()
    {
        var town = S.Settlements.Where(s => s.Kind == SettlementKind.Town && !Refuses(s)).OrderBy(s => Vector2.Distance(s.Pos, Player.Pos)).First();
        var village = S.Settlements.Where(s => s.Kind == SettlementKind.Village && !Refuses(s)).OrderBy(s => Vector2.Distance(s.Pos, Player.Pos)).First();
        foreach (var o in Offers(town).Take(2).Concat(ElderOffers(village).Take(1)).ToList())
        {
            town.Offers.Remove(o); village.Offers.Remove(o);
            S.Contracts.Add(o);
        }
        if (S.Contracts.Count > 0) S.PinnedQuest = S.Contracts[0].Id;
        S.QuestHistory.Add(new QuestRecord { Text = $"Drive off the wolves that take {village.Name}'s sheep.", Giver = $"The elders of {village.Name}", At = (float)S.Hours - 30, Reward = 90, Done = true });
        S.QuestHistory.Add(new QuestRecord { Text = $"Scout {town.Name}.", Giver = $"The steward of {town.Name}", At = (float)S.Hours - 10, Reward = 160, Done = false });
    }
}
