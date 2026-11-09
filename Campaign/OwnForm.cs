namespace Ironfield;

/// Your own form and the aspects of forms (docs/MAGIC_AURA.md 4.4-4.5, MA6). A captain who never swore to an order makes a
/// form from how he fights once he is an Aura Expert: the battles keep a tally of his style, and the top two are offered.
/// A form at rank 3 takes one of two aspects, for good.
public sealed partial class Campaign
{
    /// Style points that let an unsworn Aura Expert make his own form; how much stronger it is than the form it grows from.
    public const float OwnFormAt = 40, OwnFormEdge = 1.1f;
    /// The five styles: the tally's key, the form's kind and word, the form whose numbers it starts from, and its line.
    public static readonly (string Style, string Kind, string Word, string From, string Line)[] Styles =
    {
        ("answer", "counter", "Answer", "measured_blade", "For a while your parries come easier and each one strikes back hard."),
        ("charge", "leap", "Charge", "falcon_dive", "Drive at a foe ahead and strike him down; the only leap you can make from the saddle."),
        ("mark", "shot", "Mark", "sky_arrow", "An arrow of aura down your line, through the men in its way."),
        ("ruin", "burst", "Ruin", "ashen_smite", "Your weapon's weight thrown round you: the men nearest are struck down."),
        ("wall", "guard", "Wall", "ashen_guard", "For a while every blow from the front stops on your guard."),
    };

    /// After a battle you fought: how you fought adds to your tally (parries and ripostes, lance and saddle, the bow, a heavy
    /// weapon's blows, sword and shield), and the offer comes once you may make your form.
    internal void StyleAfterBattle(BattleResult r)
    {
        if (r.Duration <= 0) return;
        var weapon = Captain.Kit.Weapon != "" ? Gear.Get(Captain.Kit.Weapon) : null;
        float melee = Math.Max(0, r.PlayerKills - r.ArrowKills - r.SaddleKills);
        void Add(string style, float n) { if (n > 0) Captain.StyleTally[style] = Captain.StyleTally.GetValueOrDefault(style) + n; }
        Add("answer", r.Parries + 2 * r.Ripostes);
        Add("charge", 2 * r.LanceHits + r.SaddleKills);
        Add("mark", r.ArrowHits);
        Add(weapon is { TwoHanded: true } ? "ruin" : Captain.Kit.Shield != "" ? "wall" : "ruin", 2 * melee);
        OfferOwnForm();
    }

    /// Can you make your own form: an Aura Expert, never sworn to an order, without one yet.
    public bool MayMakeForm => Captain.AuraTier >= 3 && Captain.Order == "" && Captain.Forsworn.Count == 0 && Captain.OwnForm == null;

    /// Your top two styles, offered once the tally is full.
    void OfferOwnForm()
    {
        if (!MayMakeForm || Captain.OwnFormOffer.Count > 0 || Captain.StyleTally.Values.Sum() < OwnFormAt) return;
        Captain.OwnFormOffer = Captain.StyleTally.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).Take(2).Select(kv => kv.Key).ToList();
        if (Captain.OwnFormOffer.Count == 0) return;
        Beat = ("A FORM OF YOUR OWN", new[] { "YOU FIGHT LIKE NO ONE ELSE. THE AURA HAS TAKEN YOUR SHAPE.", "CHOOSE YOUR OWN FORM ON THE CAPTAIN SHEET (K)." });
        Log("Your aura has taken the shape of how you fight. You can make a form of your own (K).");
    }

    /// The name a style's form takes from your company: the Red Lions' Answer.
    public string OwnFormName(string style)
    {
        string co = (LegendCompany ?? BestCompany)?.Name ?? "Captain's";
        if (co.StartsWith("The ", StringComparison.OrdinalIgnoreCase)) co = co[4..];
        return $"The {co} {Styles.First(s => s.Style == style).Word}";
    }

    /// The form a style makes: the numbers of the form it grows from, a tenth stronger, open at your tier.
    public FormDef MakeOwnForm(string style)
    {
        var s = Styles.First(x => x.Style == style);
        var f = Forms.Get(s.From).Clone();
        f.Id = Forms.OwnId; f.Order = ""; f.Great = false; f.Tier = 3; f.Corruption = 0;
        f.Name = OwnFormName(style); f.Line = s.Line;
        f.Damage *= OwnFormEdge; f.Duration *= OwnFormEdge;
        return f;
    }

    /// Make your own form from one of the two styles offered. Returns null or why not.
    public string? ChooseOwnForm(string style)
    {
        if (!MayMakeForm) return "YOU CANNOT MAKE A FORM NOW";
        if (!Captain.OwnFormOffer.Contains(style)) return "THAT STYLE IS NOT YOURS";
        var f = MakeOwnForm(style);
        Captain.OwnForm = f;
        Captain.OwnFormOffer.Clear();
        Forms.Register(f);
        LearnForm(f.Id);
        SlotForm(f.Id);
        Deed($"You made a form of your own: {f.Name}.", "own_form");
        return null;
    }

    /// A saved own form goes back into the registry when the campaign loads.
    void RegisterOwnForm() { if (Captain.OwnForm is FormDef f) Forms.Register(f); }

    // ---- aspects (4.5) ----

    /// Why this form cannot take this aspect now, or null.
    public string? CannotTakeAspect(string form, string aspect)
    {
        if (!Captain.Forms.Contains(form) || !Forms.Has(form)) return "YOU DO NOT KNOW THAT FORM";
        if (Captain.FormAspects.ContainsKey(form)) return "IT HAS ITS ASPECT ALREADY";
        if (FormRank(Captain, form) < 3) return "AT RANK III";
        if (Forms.Aspect(aspect) is not AspectDef a || a.Kind != Forms.Get(form).Kind) return "NOT AN ASPECT OF THAT FORM";
        return null;
    }

    /// The form takes an aspect, for good. Returns null or why not.
    public string? TakeAspect(string form, string aspect)
    {
        if (CannotTakeAspect(form, aspect) is string why) return why;
        Captain.FormAspects[form] = aspect;
        Log($"Your {Forms.Get(form).Name} grows {Forms.Aspect(aspect)!.Name.ToLowerInvariant()}.");
        return null;
    }
}
