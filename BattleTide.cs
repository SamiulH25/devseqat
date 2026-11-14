namespace Ironfield;

/// The Tide (docs/ARCANA.md AR3): the power in a battle's ground, one pool both sides' circles draw on. Its size comes from
/// the well that reaches the field (`BattleOptions.Tide`, 0-3); it fills back over two minutes. A cast spends its Will and
/// draws the same from the Tide (half for a spell of the well's own tradition). With the Tide too low a circle can still
/// cast by **overreaching**: twice the Will, the Tide's last drop, and a risk that the working turns on a caster. The
/// enemy's casters overreach only for a great working; your ordered casts overreach when they must (you chose to).
/// Hero abilities draw nothing. All of it on the battle's own dice, so replays hold.
public sealed partial class Battle
{
    /// Any company on the field can cast: the Tide matters (the HUD shows it).
    public bool Arcane => anyCaster;
    /// The Tide now and its full measure.
    public float Tide, TideCap;
    /// The Tide of a field with no well (a quick battle), and what each step of a well's strength adds.
    public const float TideBase = 60, TideRefill = 120;
    /// The chance an overreach lashes back, and how hard (a share of the caster's health).
    public const float OverreachRisk = 0.35f, Backlash = 0.6f;
    /// Overreaches and backlashes this battle, by team (the HUD, the report).
    public readonly int[] Overreaches = new int[2], Backlashes = new int[2];

    void SetupTide()
    {
        TideCap = TideBase * (1 + Math.Clamp(Options.Tide, 0, 3));
        Tide = TideCap;
    }

    void TideTick(float dt) => Tide = MathF.Min(TideCap, Tide + TideCap / TideRefill * dt);

    /// What a cast of `will` Will draws from the Tide: half for a spell of the well's own tradition.
    /// A Hearth captain's circle near him draws half again less (BattlePaths.cs).
    public float TideDraw(AbilityDef a, float will, Squad? s = null) =>
        will * (Options.TideTradition != "" && a.Tradition == Options.TideTradition ? 0.5f : 1) * (Hearthside(s) ? HearthDraw : 1);

    /// The Tide holds enough for this company's cast.
    public bool TideHolds(AbilityDef a, float will, Squad? s = null) => Tide >= TideDraw(a, will, s);

    /// Spend a cast's Will: from the Tide, or (when it is short) by overreaching. False if the company cannot pay either way.
    bool PayForCast(Squad s, AbilityDef a, float will, bool mayOverreach)
    {
        if (TideHolds(a, will, s)) { s.Will -= will; Tide -= TideDraw(a, will, s); return true; }
        if (!mayOverreach || s.Will < will * 2) return false;
        s.Will -= will * 2;
        Tide = 0;
        Overreaches[s.Team]++;
        if (rng.NextDouble() < OverreachRisk && !Anchored(s)) LashBack(s, a); // a Hearth captain's anchor holds it (BattlePaths.cs)
        return true;
    }

    /// The working turns on the circle: one caster is struck to his knees, the rest are shaken; the craft's draws the Omen.
    void LashBack(Squad s, AbilityDef a)
    {
        var casters = s.Units.Where(u => u.Active && !u.Routing && IsCaster(u)).ToList();
        if (casters.Count == 0) return;
        var u = casters[rng.Next(casters.Count)];
        Backlashes[s.Team]++;
        u.Hp = MathF.Max(1, u.Hp - u.Type.MaxHp * Backlash); // struck down to his knees, not killed: no one's kill to count
        u.HitFlash = 0.15f;
        u.HurtAt = Time;
        foreach (var o in casters) o.Morale -= 12; // and the circle is shaken
        Flash("backlash", u.Pos, 3, s.Team, a.Tradition);
        if (s.Team == 0 && a.Tradition == "craft") OmenRaised += 1;
        Log(s.Team == 0 ? $"{s.Name} overreach: the working turns on them!" : "The enemy's casters overreach, and the working turns on them!");
        Sound(Cue.Spell, u.Pos);
    }
}
