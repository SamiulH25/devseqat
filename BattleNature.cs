namespace Ironfield;

/// How a commander's nature changes his battle (docs/PLAYTEST.md PT6b): numbers the general reads in place of his
/// fixed ones. 1 and 0 are the plain general.
public readonly record struct Nature(float Closes, float ReserveWait, float CaptainJoins, float Retreat, float Patience, float HuntWait, bool Honourable)
{
    public static readonly Nature Plain = new(0, 1, 12, 0.18f, 1, 1, false);

    /// A lord's traits as his way of fighting. Reckless goes in early, throws in his reserve and himself, and does not
    /// sound the retreat; Cautious waits on his ground, keeps his reserve and leaves while he still has men; Ambitious
    /// sends for your head sooner; Honourable will not have his bows mark you.
    public static Nature Of(IEnumerable<string> traits)
    {
        var n = Plain;
        foreach (var t in traits)
            n = t switch
            {
                "Reckless" => n with { Closes = n.Closes + 8, ReserveWait = n.ReserveWait * 0.5f, CaptainJoins = 3, Retreat = 0.06f, Patience = n.Patience * 0.5f },
                "Cautious" => n with { Closes = n.Closes - 5, ReserveWait = n.ReserveWait * 1.7f, CaptainJoins = 40, Retreat = 0.4f, Patience = n.Patience * 1.8f },
                "Ambitious" => n with { HuntWait = n.HuntWait * 0.6f, CaptainJoins = MathF.Min(n.CaptainJoins, 8) },
                "Cruel" => n with { Retreat = MathF.Min(n.Retreat, 0.12f) },
                "Honourable" => n with { Honourable = true },
                _ => n,
            };
        return n;
    }

    /// One line for the encounter panel: how he will fight, or "" for a plain commander.
    public static string Line(IEnumerable<string> traits)
    {
        var t = traits.ToList();
        return t.Contains("Reckless") ? "RECKLESS: HE CHARGES AT ONCE AND LEADS IT HIMSELF."
            : t.Contains("Cautious") ? "CAUTIOUS: HE WAITS ON HIS GROUND AND KEEPS A RESERVE."
            : t.Contains("Ambitious") ? "AMBITIOUS: HE WILL COME FOR YOUR HEAD."
            : t.Contains("Honourable") ? "HONOURABLE: HIS BOWS WILL NOT MARK YOU."
            : t.Contains("Cruel") ? "CRUEL: HE FIGHTS ON LONG AFTER IT IS LOST."
            : "";
    }
}

public sealed partial class Battle
{
    Nature? nature;
    /// The enemy commander's nature, from `BattleOptions.LeaderTraits`.
    public Nature Nature => nature ??= Nature.Of(Options.LeaderTraits);
    /// Your own side plays as the plain general when the AI leads it.
    Nature NatureOf(int team) => team == 1 ? Nature : Nature.Plain;
}
