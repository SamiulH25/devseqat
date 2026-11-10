namespace Ironfield;

/// The Black Flame (docs/archive/KNIGHT_ORDERS.md 7, K7): the Hollow Crown's pact, found and never offered on the main road. It burns
/// through a wall with no trial and opens the Black Flame forms; corruption (`CaptainState.Corruption`) only rises from it
/// (the forms, a wall burnt, the pact, grief in anger) and draws a price at each 25: the men notice, the orders hunt you,
/// madness, and at 100 an ending. One hard way back: purging it, once, drops you an aura tier.
public sealed partial class Campaign
{
    /// What the pact and a burnt wall cost in corruption; the thresholds; what a purge takes off.
    public const float PactCost = 5, BurnCost = 10, NoticedAt = 25, HuntedAt = 50, MadAt = 75, PurgeOff = 50;
    /// A day's chance at 75+ that the flame makes a choice for you on the map.
    public const float MapMadness = 0.05f;

    /// The flame's stage: 0-4, one for each 25 of corruption.
    public static int FlameStage(CaptainState c) => (int)(Math.Clamp(c.Corruption, 0, 100) / 25);

    /// The flame's hold on you in battle: a chance a second that it takes you (0 below 75; once in 50 s at 75, twice as often at 100).
    public float MadnessInBattle => Captain.Corruption < MadAt ? 0 : 0.02f + (Captain.Corruption - MadAt) / 1250f;

    /// Corruption rises (from the flame only); never past 100.
    internal void Corrupt(float by) => Captain.Corruption = Math.Clamp(Captain.Corruption + by, 0, 100);

    /// The Hollow Crown comes to you: after a cracked core, or when your rival who went over seeks you out. Once purged,
    /// it never asks again.
    internal void TemptFlame()
    {
        if (Captain.Pact || Captain.Purged || Captain.FlameEnd != "") return;
        Force("flame_bargain", new PendingEvent { Lord = Captain.Rival });
    }

    /// The pact: corruption, and the Black Flame forms your aura can hold.
    internal string TakePact()
    {
        if (Captain.Pact) return "";
        if (Captain.Purged) return "He finds nothing left in you to burn.";
        Captain.Pact = true;
        Corrupt(PactCost);
        CompanionsReact("flame");
        LearnFlameForms();
        Deed("You take the Hollow Crown's bargain. A black fire wakes in you.", "flame");
        return "The black flame is yours.";
    }

    /// The Black Flame forms up to your aura tier (with the pact only).
    void LearnFlameForms()
    {
        if (!Captain.Pact) return;
        foreach (var f in Forms.OfOrder("hollow"))
            if (f.Tier <= Captain.AuraTier) LearnForm(f.Id);
    }

    /// Why the flame cannot burn through your wall now (null = it can).
    public string? CannotBurnWall() =>
        !Captain.Pact ? "YOU HAVE NO PACT WITH THE FLAME" : !Captain.AuraWall ? "YOUR AURA IS AT NO WALL" : S.GameOver ? "IT IS OVER" : null;

    /// Burn through the wall: no trial, a tier, and the flame's price.
    public string? BurnWall()
    {
        if (CannotBurnWall() is string why) return why;
        Corrupt(BurnCost);
        BreakThrough("You burn through the wall with the black flame.");
        return null;
    }

    /// Why you cannot purge the flame now (null = you can): it needs a priest among your companions or the relic, in a town.
    public string? CannotPurge()
    {
        if (!Captain.Pact && Captain.Corruption <= 0) return "THERE IS NO FLAME IN YOU";
        if (Captain.Purged) return "YOU HAVE PURGED IT ONCE: THERE IS NO SECOND TIME";
        if (Captain.Corruption >= 100 || Captain.FlameEnd != "") return "IT IS TOO LATE";
        if (SettlementAt(Player.Pos) is not { Kind: not SettlementKind.Village }) return "IN A TOWN OR CASTLE";
        if (!Companions.Any(k => k.Calling == "Priest" && S.Hours >= k.WoundedUntil) && !RelicReady) return "NEEDS A PRIEST COMPANION OR THE RELIC";
        return null;
    }

    /// Purge the flame: the pact and its forms go, corruption falls by half its scale, and your aura drops a tier.
    public string? Purge()
    {
        if (CannotPurge() is string why) return why;
        if (!Companions.Any(k => k.Calling == "Priest" && S.Hours >= k.WoundedUntil)) RelicSpent();
        Captain.Pact = false;
        Captain.Purged = true;
        Captain.Forms.RemoveAll(id => Forms.Has(id) && Forms.Get(id).Order == "hollow");
        Captain.Corruption = MathF.Max(0, Captain.Corruption - PurgeOff);
        Captain.FlameSeen = FlameStage(Captain);
        if (Captain.AuraTier > 0) { Captain.AuraTier--; Captain.AuraXp = 0; Captain.AuraWall = false; }
        Deed($"The black flame is burnt out of you. It takes your strength with it: you are {AuraName(Captain.AuraTier)} again.", "flame");
        return null;
    }

    /// Each day: a new threshold draws its price, madness makes its choices, and at 100 the ending comes (once).
    void FlameDaily()
    {
        if (S.GameOver) return;
        int stage = FlameStage(Captain);
        while (Captain.FlameSeen < Math.Min(stage, 3)) FlameThreshold(++Captain.FlameSeen);
        if (stage >= 3 && rng.Chance(MapMadness)) MadDay();
        if (stage >= 4 && Captain.FlameEnd == "") Force("flame_end", new PendingEvent());
    }

    /// A threshold crossed: 25 the men notice (the pious leave first), 50 the orders hunt you and villages bar their gates,
    /// 75 madness.
    void FlameThreshold(int stage)
    {
        switch (stage)
        {
            case 1:
                var pious = Companions.Where(k => k.Calling == "Priest" || k.Traits.Contains("Pious")).OrderBy(k => k.Id).FirstOrDefault();
                if (pious != null) Leave(pious);
                Beat = ("THE MEN NOTICE", new[] { "THE MEN DO NOT MEET YOUR EYES. THE FIRE BURNS DARKER THAN IT SHOULD.", pious != null ? $"{pious.Name.ToUpperInvariant()} WILL NOT SERVE A MAN WHO BURNS LIKE THAT." : "NO ONE SAYS IT ALOUD. YET." });
                break;
            case 2:
                if (Captain.Order != "" && Captain.Order != "hollow") LeaveOrder();
                foreach (var o in S.Orders) o.Standing = MathF.Min(o.Standing, -50);
                Beat = ("HUNTED", new[] { "EVERY ORDER KNOWS WHAT YOU ARE NOW. THEIR KNIGHTS WILL HUNT YOU.", "VILLAGES BAR THEIR GATES TO YOU." });
                break;
            case 3:
                Beat = ("THE FLAME HAS A WILL", new[] { "SOMETIMES YOU WAKE AND DO NOT KNOW WHAT YOU DID.", "IN BATTLE IT CAN TAKE YOU, AND IT DOES NOT CARE WHOSE MEN ARE NEAREST." });
                break;
        }
        if (Beat is var (title, _)) Log($"The black flame: {title.ToLowerInvariant()}.");
    }

    /// A day the flame chose for you: gold gone, a company shaken, or a name blackened.
    void MadDay()
    {
        switch (rng.Next(3))
        {
            case 0: float lost = MathF.Round(S.Gold * 0.1f); S.Gold -= lost; Log($"You wake with ash on your hands and {lost:0} crowns gone."); break;
            case 1: ApplyEffect("cohesion:-3", new PendingEvent()); Log("You wake to your men watching you. Someone was beaten in the night, and it was you."); break;
            default: Captain.Renown = MathF.Max(0, Captain.Renown - 5); Log("A village burns behind you. You do not remember the order."); break;
        }
    }

    /// The ending at 100: you take the Hollow Crown, or you burn out. Both close the campaign; it happens once.
    string EndFlame(bool crown)
    {
        if (Captain.FlameEnd != "") return "";
        Captain.FlameEnd = crown ? "crown" : "burnt";
        S.GameOver = true;
        if (crown)
        {
            Deed($"{Me.Name} takes the Hollow Crown. Every realm rises against the black flame.", "flame");
            S.EndReason = "YOU WEAR THE HOLLOW CROWN. THE WORLD UNITES AGAINST YOU.";
        }
        else
        {
            Deed($"{Me.Name} burns out. They found only ash in the armour.", "flame");
            S.EndReason = "THE BLACK FLAME BURNS YOU OUT. YOUR COMPANY BURIES AN EMPTY SUIT OF ARMOUR.";
        }
        return "It is over.";
    }
}
