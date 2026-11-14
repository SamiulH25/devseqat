using Microsoft.Xna.Framework;

namespace Ironfield;

/// The captain's path in battle (docs/ARCANA.md AR6), from `BattleOptions.Path`. The **Sword** (forms and the blade): every
/// form costs less aura. The **Banner** (aura spent on your men): your aura inspires the companies round you from the first
/// tier and further off, and the banner forms hold and steady them. The **Hearth** (aura that feeds the circle): your circle
/// near you draws half from the Tide, and the hearth forms lend it your aura, anchor its overreach and kindle the Tide.
/// "" = no path chosen yet: as before.
public sealed partial class Battle
{
    public const float SwordThrift = 0.85f, BannerInspire = 16, HearthReach = 25, HearthDraw = 0.5f;

    /// A form's aura cost on your path.
    public float FormCost(FormDef f) => f.Aura * (Options.Path == "sword" ? SwordThrift : 1);
    /// How far and from which tier your aura inspires the companies round you.
    float InspireReach => Options.Path == "banner" ? BannerInspire : InspireRadius;
    float InspireFrom => Options.Path == "banner" ? 1 : InspireTier;

    /// A company of your circle close enough to a Hearth captain to feel his aura.
    bool Hearthside(Squad? s) => s != null && s.Team == 0 && Options.Path == "hearth" && Player.Active
        && Vector2.Distance(s.Center, Player.Pos) < HearthReach + s.Radius;
    float anchorUntil = -1;
    /// Your anchor holds this circle's working: an overreach does not turn on it.
    public bool Anchored(Squad s) => Hearthside(s) && Time < anchorUntil;

    /// A hearth form: lend your aura to the circle, anchor its working, or kindle the Tide.
    void HearthForm(Unit p, FormDef f, float k)
    {
        var circles = Squads.Where(s => s.Team == p.Team && s.Alive > 0 && CasterCompany(s) && Vector2.Distance(s.Center, p.Pos) < f.Radius + s.Radius).ToList();
        switch (f.Act)
        {
            case "lend":
                if (circles.OrderBy(s => Vector2.DistanceSquared(s.Center, p.Pos)).FirstOrDefault() is Squad c)
                {
                    c.Will = MathF.Min(WillCap(c), c.Will + FormCost(f) * f.Damage * k);
                    Flash("lend", c.Center, 3, p.Team, "arms");
                }
                else PlayerHit = ("NO CIRCLE NEAR", Time);
                break;
            case "anchor":
                anchorUntil = Time + f.Duration * k;
                break;
            case "kindle":
                Tide = TideCap;
                foreach (var o in circles) o.Will = WillCap(o);
                Flash("kindle", p.Pos, f.Radius, p.Team, "arms");
                Log("The ground wakes under your aura: the Tide runs full.");
                break;
        }
    }
}
