namespace Ironfield;

/// The enemy hunts you (docs/COMPANY.md C13): a lord with a grudge comes for you himself in battle.
public sealed partial class Campaign
{
    /// Below this opinion of you a lord swears to have your head.
    public const float GrudgeOpinion = -40;

    /// The line a lord with a grudge swore ("" for none): his want is revenge on your house, or he hates you.
    public string GrudgeOf(Party e)
    {
        if (e.Kind != PartyKind.Lord || CharacterOf(e) is not Character ch || ch.Kind != "lord") return "";
        bool revenge = ch.Want == "revenge" && ch.WantOn >= 0 && ch.WantOn == Me.House;
        if (!revenge && OpinionOfYou(ch) > GrudgeOpinion) return "";
        return $"{ch.Name} has sworn to have your head!";
    }
}
