namespace Ironfield;

/// Your own order (docs/KNIGHT_ORDERS.md 8, K8): where the knight thread meets the legacy. Founded from your company once your
/// aura, rank or fame and a seat allow; its creed is three vows you keep; your named men are knighted into it; its two
/// signature troops (Sworn Brothers, Masters of the Order) rise from any tier IV foot line; and when you hand over, your
/// successor leads it a tier below you, with the form it teaches.
public sealed partial class Campaign
{
    /// Founding: an Aura Expert, a lord or fame enough, and a seat. A named man is knighted after three battles.
    public const int FoundAura = 3, FoundFame = 150, KnightBattles = 3;
    /// How many living knights your order holds: two, and one more for each aura tier.
    public int KnightsMax => 2 + Captain.AuraTier;

    /// Why you cannot found an order now (null = you can).
    public string? CannotFound()
    {
        if (S.OwnOrder != null) return "YOU HAVE FOUNDED YOUR ORDER";
        if (Captain.AuraTier < FoundAura) return $"NEEDS AN AURA OF {AuraName(FoundAura).ToUpperInvariant()}";
        if (Rank < Rank.Lord && Fame < FoundFame) return $"NEEDS THE RANK OF LORD OR FAME {FoundFame} ({Fame} NOW)";
        if (FiefSeats == 0) return "NEEDS A SEAT OF YOUR OWN: A TOWN OR CASTLE";
        if (Captain.Order != "") return "LEAVE YOUR ORDER FIRST";
        if (Captain.Pact || Captain.Corruption >= HuntedAt) return "NO KNIGHT WILL SWEAR TO A MAN WHO BURNS BLACK";
        if (BestCompany == null) return "YOU HAVE NO COMPANY";
        return null;
    }

    /// The name of an order founded from this company: the Red Lions found the Order of the Red Lions.
    public static string OrderNameOf(string company)
    {
        string rest = company.StartsWith("The ", StringComparison.OrdinalIgnoreCase) ? company[4..] : company;
        return rest.StartsWith("Order", StringComparison.OrdinalIgnoreCase) ? rest : $"Order of the {rest}";
    }

    /// The form your order would teach: your great form, else your highest form in a slot (never the Black Flame's).
    public string OrderFormNow => SlottedForms(Captain).Where(Forms.Has).Select(Forms.Get).Where(f => f.Order != "hollow")
        .OrderByDescending(f => f.Great).ThenByDescending(f => f.Tier).Select(f => f.Id).FirstOrDefault() ?? "";

    /// Found your order from your company (the legend's, else your best), with a creed of three vows.
    public string? FoundOrder(IEnumerable<string> vows)
    {
        if (CannotFound() is string why) return why;
        var v = vows.Distinct().ToList();
        if (v.Count != 3 || !v.All(KnightOrders.VowKinds.Contains)) return "CHOOSE THREE VOWS FOR ITS CREED";
        var co = LegendCompany ?? BestCompany!;
        S.OwnOrder = new FoundedOrder { Name = OrderNameOf(co.Name), Company = co.Name, Vows = v, Form = OrderFormNow, Day = Day };
        Captain.Renown += 25;
        Deed($"You found the {S.OwnOrder.Name} from {co.Name}. You are its first grand master.", "orders_founded");
        return null;
    }

    /// A named man on your roll, by id (null if he is dead or gone).
    public Soldier? NamedMan(int id) => Player.Companies.SelectMany(c => c.Roll).FirstOrDefault(m => m.Id == id);

    /// Your order's knights still living.
    public int KnightsAlive => S.OwnOrder?.Knights.Count(id => NamedMan(id) != null) ?? 0;

    /// The named man most worth knighting next: the most battles among those not yet knighted.
    public Soldier? NextKnight => Player.Companies.SelectMany(c => c.Roll).Where(m => !m.Knight).OrderByDescending(m => m.Battles).ThenBy(m => m.Id).FirstOrDefault();

    /// Why this man cannot be knighted now (null = he can).
    public string? CannotKnight(Soldier? m) =>
        S.OwnOrder == null ? "FOUND YOUR ORDER FIRST" : m == null ? "NO NAMED MAN TO KNIGHT" : m.Knight ? "HE IS A KNIGHT ALREADY"
        : m.Battles < KnightBattles ? $"{m.Name.ToUpperInvariant()} NEEDS {KnightBattles} BATTLES" : KnightsAlive >= KnightsMax ? $"YOUR ORDER HOLDS {KnightsMax} KNIGHTS AT YOUR AURA" : null;

    /// Knight a named man into your order.
    public string? Knight(Soldier? m)
    {
        if (CannotKnight(m) is string why) return why;
        m!.Knight = true;
        S.OwnOrder!.Knights.Add(m.Id);
        m.Deed($"knighted into the {S.OwnOrder.Name}");
        Log($"You knight {m.Name}. He is a brother of the {S.OwnOrder.Name} now.");
        return null;
    }

    /// You broke a vow of your own creed: your knights saw it.
    void OwnVowBroken(string what)
    {
        Captain.Renown = MathF.Max(0, Captain.Renown - 10);
        Log($"You {what}, and your own creed forbids it. Your knights saw.");
    }

    /// Handing over (L8): the new captain is another man. His aura is a tier below yours if he leads your order (with the
    /// form it teaches), two below if there is none, never below his own. Your flame, oaths, rival and cracked core stay with you.
    void HandOnAura(Character heir)
    {
        var own = S.OwnOrder;
        Captain.AuraTier = Math.Max(heir.Aura, Math.Max(0, Captain.AuraTier - (own != null ? 1 : 2)));
        Captain.AuraXp = 0;
        Captain.AuraWall = false;
        Captain.Corruption = 0;
        Captain.Pact = Captain.Purged = false;
        Captain.FlameSeen = 0;
        Captain.Order = "";
        Captain.OrderRank = Captain.OrderDeeds = 0;
        Captain.Penance = false;
        Captain.Forsworn.Clear();
        Captain.Rival = -1;
        Captain.Wounds.Remove("core");
        Captain.CoreUntil = 0;
        Captain.Forms.Clear();
        Captain.FormUses.Clear();
        LearnForms();
        if (own == null) return;
        if (own.Form != "") LearnForm(own.Form);
        own.Masters++;
        Deed($"{heir.Name} is the {own.Name}'s grand master now.", "");
    }
}
