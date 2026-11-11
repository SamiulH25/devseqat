using Microsoft.Xna.Framework;

namespace Ironfield;

/// Battle: the captain's aura (docs/archive/KNIGHT_ORDERS.md 5.1-5.2, K1). A pool beside stamina that the aura blade and aura
/// armour spend; it refills slowly in a fight and quickly out of one. Only the player has it (champions: K6).
public partial class Battle
{
    public const float AuraMax = 100, AuraLow = 20;
    /// Aura per second while the blade is lit, per blade blow landed, and the share of a blow's damage aura armour soaks
    /// (arrows: less) and what each point soaked costs.
    public const float BladeDrain = 5, BladeHitCost = 2, ArmourSoak = 0.4f, ArrowSoak = 0.2f, SoakCost = 0.35f;
    /// The aura blade's blows: harder, through more armour, and an AI guard holds against them half as often.
    public const float BladeDamage = 1.3f, BladePierce = 0.4f, BladeGuard = 0.5f;

    /// The pool now (0 while unawakened).
    public float Aura;
    public int AuraTier => Math.Min(5, Options.AuraTier + brokeTier); // a breakthrough in the field lifts it at once (MA7)
    /// The blade burns now (hold the aura key, tier 2+, at least AuraLow to light it).
    public bool AuraBlade { get; private set; }
    /// Under AuraLow: the blade will not light and aura armour fails.
    public bool Spent => AuraTier > 0 && Aura < AuraLow;
    /// When you last struck or were struck (aura comes back slowly while you fight).
    float lastFought = -9;

    void SetupAura() => Aura = AuraTier > 0 ? AuraMax : 0;

    /// Breath comes back faster to a kindled captain (tier 1+); a spent one breathes slower than a plain man (MA3); Fever slows it (MA5).
    float AuraBreath => (AuraTier < 1 ? 1 : Spent ? SpentBreath : 1.25f) * (Fevered ? FeverBreath : 1);

    /// The pool that matters (docs/archive/MAGIC_AURA.md 4.1): aura comes back slowly out of a fight and twice as fast standing still
    /// with no foe near; at full, blows you land bank a surge that your next form spends first; spent, your blows and breath fail.
    public const float RegenOut = 1, RegenStill = 2, StillFoe = 30, SurgeMax = 20, SurgePerBlow = 4, SpentBlow = 0.9f, SpentBreath = 0.75f;
    /// Aura banked over the top (0-SurgeMax), spent first by a form.
    public float Surge;
    /// Your blows while spent.
    float SpentEdge => Spent ? SpentBlow : 1;

    /// Your men see your aura (4.2): from tier 2, fighting within this of one of your companies, it takes heart.
    public const float InspireRadius = 10, InspireTier = 2;
    static readonly AbilityDef Inspired = new() { Id = "inspired", Name = "Inspired", Icon = "star", Kind = "buff", Effect = "inspired", Duration = 1.5f, Radius = InspireRadius, Tradition = "arms" };
    float inspireTick;

    void UpdateAura(Unit p, bool hold, float dt)
    {
        if (AuraTier <= 0) return;
        bool fighting = Time - lastFought < 4;
        // A burning blade takes, it does not gather; a Master's aura comes back as he fights (K5).
        bool still = !fighting && p.Vel.LengthSquared() < 0.25f && p.EnemyDist > StillFoe;
        if (!AuraBlade) Aura = MathF.Min(AuraMax, Aura + (fighting ? (AuraTier >= 4 ? 1.5f : 0.5f) : still ? RegenStill : RegenOut) * dt);
        if (hold && AuraTier >= 2 && !AuraBlade && Aura >= AuraLow && p.Active && Time >= QuenchedUntil)
        {
            AuraBlade = true;
            PlayerHit = ("AURA BLADE", Time);
            Sound(Cue.AuraIgnite, p.Pos, priority: true);
        }
        if (AuraBlade && (!hold || Aura <= 0 || !p.Active)) AuraBlade = false;
        if (AuraBlade) Aura = MathF.Max(0, Aura - BladeDrain * dt);
        Presence(p, dt);
        Inspire(p, fighting, dt);
    }

    /// Your companies near you while you fight take heart from your aura (tier 2+, not spent).
    void Inspire(Unit p, bool fighting, float dt)
    {
        inspireTick += dt;
        if (inspireTick < 0.5f) return;
        inspireTick = 0;
        if (AuraTier < InspireTier || Spent || !fighting || !p.Active) return;
        foreach (var s in Squads)
            if (s.Team == p.Team && s.Alive > 0 && Vector2.Distance(s.Center, p.Pos) < InspireRadius + s.Radius) AddTimed(s, Inspired, 1);
    }

    /// A Master's presence (tier 4+, not spent, K5): foes within reach lose heart, your own men near you steady. It can
    /// break the company in front of you; it never reaches the whole field.
    public const float PresenceRadius = 14, PresenceShake = 3f, PresenceSteady = 1.5f, PresenceFloor = 10;
    float presenceTick;
    readonly List<Unit> presenceNear = new();

    void Presence(Unit p, float dt)
    {
        if (AuraTier < 4 || Spent || !p.Active) return;
        presenceTick += dt;
        if (presenceTick < 0.5f) return;
        float step = presenceTick;
        presenceTick = 0;
        float reach = AuraTier >= 5 ? LightRadius : PresenceRadius; // a Transcendent's presence reaches further (MA7)
        foreach (var u in Near(p.Pos, reach, presenceNear))
        {
            if (u == p || Vector2.DistanceSquared(u.Pos, p.Pos) > reach * reach) continue;
            if (u.Team != p.Team) { if (u.Morale > PresenceFloor) u.Morale = MathF.Max(PresenceFloor, u.Morale - PresenceShake * step); }
            else u.Morale = MathF.Min(90, u.Morale + PresenceSteady * step);
        }
    }

    /// Your blow lands with the blade lit: its cost.
    void BladeLanded()
    {
        if (AuraTier > 0 && !AuraBlade && Aura >= AuraMax) Surge = MathF.Min(SurgeMax, Surge + SurgePerBlow); // fighting well at full banks the overflow
        if (AuraBlade) { Aura = MathF.Max(0, Aura - BladeHitCost); Flash("blade", Player.Pos + Player.Dir * Player.Type.Reach, 1, 0); }
        if (edgeLeft > 0) edgeLeft--; // Thorn Edge (K2): one blow spent
    }

    /// Aura armour (tier 3+): the share of a blow on you that aura takes instead of flesh, paid from the pool.
    float AuraSoak(Unit a, Unit t, float real, bool ranged)
    {
        if (a.IsPlayer || t.IsPlayer) lastFought = Time;
        if (!t.IsPlayer || AuraTier < 3 || Spent) return real;
        float soaked = real * (ranged ? ArrowSoak : ArmourSoak);
        Flash("soak", t.Pos, 1, t.Team);
        Aura = MathF.Max(0, Aura - soaked * SoakCost);
        return real - soaked;
    }
}
