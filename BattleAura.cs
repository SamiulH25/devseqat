using Microsoft.Xna.Framework;

namespace Ironfield;

/// Battle: the captain's aura (docs/KNIGHT_ORDERS.md 5.1-5.2, K1). A pool beside stamina that the aura blade and aura
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
    public int AuraTier => Options.AuraTier;
    /// The blade burns now (hold the aura key, tier 2+, at least AuraLow to light it).
    public bool AuraBlade { get; private set; }
    /// Under AuraLow: the blade will not light and aura armour fails.
    public bool Spent => AuraTier > 0 && Aura < AuraLow;
    /// When you last struck or were struck (aura comes back slowly while you fight).
    float lastFought = -9;

    void SetupAura() => Aura = AuraTier > 0 ? AuraMax : 0;

    /// Breath comes back faster to a kindled captain (tier 1+, not spent).
    float AuraBreath => AuraTier >= 1 && !Spent ? 1.25f : 1;

    void UpdateAura(Unit p, bool hold, float dt)
    {
        if (AuraTier <= 0) return;
        bool fighting = Time - lastFought < 4;
        // A burning blade takes, it does not gather; a Master's aura comes back as he fights (K5).
        if (!AuraBlade) Aura = MathF.Min(AuraMax, Aura + (fighting ? (AuraTier >= 4 ? 1.5f : 0.5f) : 2f) * dt);
        if (hold && AuraTier >= 2 && !AuraBlade && Aura >= AuraLow && p.Active)
        {
            AuraBlade = true;
            PlayerHit = ("AURA BLADE", Time);
        }
        if (AuraBlade && (!hold || Aura <= 0 || !p.Active)) AuraBlade = false;
        if (AuraBlade) Aura = MathF.Max(0, Aura - BladeDrain * dt);
        Presence(p, dt);
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
        foreach (var u in Near(p.Pos, PresenceRadius, presenceNear))
        {
            if (u == p || Vector2.DistanceSquared(u.Pos, p.Pos) > PresenceRadius * PresenceRadius) continue;
            if (u.Team != p.Team) { if (u.Morale > PresenceFloor) u.Morale = MathF.Max(PresenceFloor, u.Morale - PresenceShake * step); }
            else u.Morale = MathF.Min(90, u.Morale + PresenceSteady * step);
        }
    }

    /// Your blow lands with the blade lit: its cost.
    void BladeLanded()
    {
        if (AuraBlade) Aura = MathF.Max(0, Aura - BladeHitCost);
        if (edgeLeft > 0) edgeLeft--; // Thorn Edge (K2): one blow spent
    }

    /// Aura armour (tier 3+): the share of a blow on you that aura takes instead of flesh, paid from the pool.
    float AuraSoak(Unit a, Unit t, float real, bool ranged)
    {
        if (a.IsPlayer || t.IsPlayer) lastFought = Time;
        if (!t.IsPlayer || AuraTier < 3 || Spent) return real;
        float soaked = real * (ranged ? ArrowSoak : ArmourSoak);
        Aura = MathF.Max(0, Aura - soaked * SoakCost);
        return real - soaked;
    }
}
