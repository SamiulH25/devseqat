using Microsoft.Xna.Framework;

namespace Ironfield;

/// Campaign: bonds between lords (docs/archive/LORDS.md LD2). Lords remember things about each other with the same `Remember` as
/// about you (fought side by side, slew my kin, took my seat, married into my house); on top of opinion sit named bonds:
/// kin (same house, or by marriage), feud (a house's blood debt, which heirs inherit), friend and rival. Bonds change what
/// lords do: friends ride in each other's armies and relieve each other first, rivals stay home, feuding lords quarrel.
public sealed partial class Campaign
{
    /// What `a` is to `b`: "kin", "feud", "friend", "rival" or "" (nothing much).
    public string BondOf(Character a, Character b)
    {
        if (a == b) return "";
        if (Kin(a, b)) return "kin";
        if (Feud(a, b)) return "feud";
        float o = OpinionOf(a, b);
        return o >= 40 ? "friend" : o <= -35 ? "rival" : "";
    }

    /// Same house, husband and wife, parent and child.
    public bool Kin(Character a, Character b) => a.House >= 0 && a.House == b.House || a.Spouse == b.Id || b.Spouse == a.Id
        || a.Father == b.Id || a.Mother == b.Id || b.Father == a.Id || b.Mother == a.Id
        || CharacterById(a.Spouse) is { House: >= 0 } sa && sa.House == b.House || CharacterById(b.Spouse) is { House: >= 0 } sb && sb.House == a.House;

    public bool Feud(Character a, Character b) => HouseOf(a) is House ha && HouseOf(b) is House hb && (ha.Feuds.Contains(hb.Id) || hb.Feuds.Contains(ha.Id));

    /// The standing part of a bond in an opinion (read by BaseOpinion): kin think well of kin, a feud is bitter.
    float BondRegard(Character a, Character b) => Kin(a, b) ? 20 : Feud(a, b) ? -40 : 0;

    /// A lord slain by another lord's men: the dead man's house owes the killer's house blood.
    void StartFeud(Character slain, Character killer)
    {
        if (HouseOf(slain) is not House a || HouseOf(killer) is not House b || a == b) return;
        if (!a.Feuds.Contains(b.Id)) a.Feuds.Add(b.Id);
        Log($"{a.Name} swears a blood feud against {b.Name}.");
    }

    /// The lords who won a battle together remember it.
    void FoughtTogether(IEnumerable<Party> side)
    {
        var lords = side.Select(CharacterOf).OfType<Character>().Where(c => c.Kind == "lord").ToList();
        foreach (var a in lords)
            foreach (var b in lords)
                if (a != b) Remember(a, b, "we fought side by side", 4);
    }

    /// A seat changed hands: its old holder will not forget who took it.
    void SeatTaken(Settlement s, int oldOwner, int newOwner)
    {
        if (oldOwner < 0 || oldOwner == newOwner || PartyById(oldOwner) is not Party op || CharacterOf(op) is not Character lost) return;
        var taker = newOwner >= 0 && PartyById(newOwner) is Party np ? CharacterOf(np) : null;
        if (taker != null && taker != lost) Remember(lost, taker, $"took {s.Name} from me", -25);
    }

    /// Weekly: feuding lords of one realm who meet come to blows; a long friendship may end in a quarrel over nothing.
    internal void BondsWeekly()
    {
        var lords = S.Characters.Where(c => c.Kind == "lord" && !c.Dead && PartyById(c.PartyId) is { Down: false, Captive: false }).ToList();
        foreach (var a in lords)
            foreach (var b in lords)
            {
                if (a.Id >= b.Id || a.Faction != b.Faction || !Feud(a, b)) continue;
                if (Vector2.Distance(PartyById(a.PartyId)!.Pos, PartyById(b.PartyId)!.Pos) > 15 * Map.Spread || !KRng.Chance(0.3f)) continue;
                Remember(a, b, "we came to blows", -6);
                Remember(b, a, "we came to blows", -6);
                Log($"{a.Name} and {b.Name} come to blows: their houses are at feud.");
            }
    }

    /// His bonds in a line (for the L screen): friends, rivals, feuds, at most two of each.
    public string BondsLine(Character ch)
    {
        var lords = S.Characters.Where(c => c.Kind == "lord" && !c.Dead && c != ch).ToList();
        string Names(string bond) => string.Join(", ", lords.Where(c => BondOf(ch, c) == bond).OrderByDescending(c => MathF.Abs(OpinionOf(ch, c))).Take(2).Select(c => c.Name));
        var parts = new List<string>();
        foreach (var (bond, word) in new[] { ("friend", "friends"), ("rival", "rivals"), ("feud", "feuds with") })
            if (Names(bond) is { Length: > 0 } n) parts.Add($"{word} {n}");
        return string.Join("; ", parts);
    }
}
