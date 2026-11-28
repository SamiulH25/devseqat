namespace Ironfield;

/// What a blow does to a man (gear.json weapon `hits`): a blade cuts, a point pierces, a mace or hammer bruises through.
public enum BlowKind { Cut, Pierce, Blunt }

/// What a man's body armour is made of (gear.json body `kind`); creatures and men without kit wear None (flat armour only).
public enum CoatKind { None, Cloth, Padded, Leather, Mail, Scale, Plate }

/// Armour by kind against blows by kind: the share of a blow a coat stops before its flat armour counts (Battle.Damage).
/// Plate turns edges and most points but rings under a mace; mail stops a cut, less a thrust, little of a blow; a padded
/// coat softens a blunt blow best of all.
public static class Armour
{
    /// Share stopped, by coat (rows, CoatKind order) and blow (columns: cut, pierce, blunt).
    static readonly float[,] Stops =
    {
        { 0, 0, 0 },          // None
        { 0.05f, 0, 0.05f },  // Cloth
        { 0.25f, 0.15f, 0.25f }, // Padded: gambeson, kaftan, quilted coats
        { 0.2f, 0.1f, 0.1f }, // Leather, furs
        { 0.45f, 0.2f, 0.1f }, // Mail
        { 0.6f, 0.4f, 0.15f }, // Scale: lamellar, brigandine, coat of plates (near plate against an edge or a point)
        { 0.65f, 0.45f, 0.15f }, // Plate
    };

    public static float Stop(CoatKind coat, BlowKind blow) => Stops[(int)coat, (int)blow];

    /// Mean share stopped over the three kinds of blow (troop comparisons: `Troops.Toughness`).
    public static float MeanStop(CoatKind coat) => (Stops[(int)coat, 0] + Stops[(int)coat, 1] + Stops[(int)coat, 2]) / 3;

    public static bool TryCoat(string s, out CoatKind k) => Enum.TryParse(s, true, out k) && k != CoatKind.None;
    public static bool TryBlow(string s, out BlowKind k) => Enum.TryParse(s, true, out k);
}
