using Microsoft.Xna.Framework;

namespace Ironfield;

/// Battle: the beasts of docs/archive/MONSTERS.md. Poison that keeps working through armour; spiders' webs that pin men
/// where they stand; the brood mother, who lays spiderlings as she fights; the wounded bear's rage; and the wyvern's
/// flight: aloft it is out of reach of every blade, so it dives to strike and climbs away, and only bows and a man
/// standing where it lands can hurt it.
public partial class Battle
{
    /// A poisoned bite leaves this share of its blow behind as poison, worked off at PoisonRate hp a second.
    public const float PoisonShare = 0.7f, PoisonRate = 9;
    /// A web covers WebRadius (more from a brood mother) and holds the men in it for WebTime, slowed to WebSlow of their pace.
    public const float WebRadius = 2.2f, WebTime = 3.5f, WebRange = 13, WebSlow = 0.2f;
    /// A flyer cruises FlyHigh above the ground; above Aloft no blade reaches it. After a strike it climbs away ClimbTime.
    public const float FlyHigh = 7, Aloft = 1.6f, ClimbTime = 1.6f;
    /// A brood mother lays two spiderlings every BroodEvery seconds while foes are near, up to BroodMax a side.
    public const float BroodEvery = 4.5f;
    public const int BroodMax = 30;
    /// A wounded beast in a rage (below half its hp) strikes this much harder, and quicker.
    public const float RageBlow = 1.4f, RageWindup = 0.7f;

    readonly List<Unit> nearBeast = new(64);
    readonly float[] broodTimer = new float[2];
    readonly Squad?[] brood = new Squad?[2];
    /// Any flyer on the field: arrows then look up as well as round (UpdateArrows).
    bool flyers;

    public bool Webbed(Unit u) => Time < u.WebbedUntil;
    /// Aloft: out of reach of blades, lances and horses.
    public static bool Airborne(Unit u) => u.Altitude > Aloft;
    static bool Raging(Unit u) => u.Type.Wild && u.Hp < u.Type.MaxHp * 0.5f && u.Type.Has("rage");

    /// Each tick, for a poisoned man or a creature: the poison works, a spider spits, a flyer rises or dives.
    void UpdateBeast(Unit u, float dt)
    {
        if (u.Poison > 0) Poisoned(u, dt);
        if (!u.Active || !u.Type.Wild) return;
        if (u.Type.Has("web")) SpitWeb(u, dt);
        if (u.Type.Flying) Fly(u, dt);
    }

    /// Poison eats a man's hp (armour is no help) until it is worked off; it can kill him.
    void Poisoned(Unit u, float dt)
    {
        float d = MathF.Min(u.Poison, PoisonRate * dt);
        u.Poison -= d;
        u.Hp -= d;
        if (u.Hp <= 0) Kill(u, u.PoisonedBy ?? u);
    }

    /// A poisoned blow landed: the undead and other beasts of its kind feel nothing; anyone else takes it in.
    void Envenom(Unit a, Unit t, float dmg)
    {
        if (!t.Active || t.Type.Has("raised") || t.Type.Breed == a.Type.Breed) return;
        t.Poison = MathF.Min(t.Poison + dmg * PoisonShare, t.Type.MaxHp);
        t.PoisonedBy = a;
    }

    /// A spider spits a web at the nearest foe it can reach: the men round where it lands are caught for a few seconds.
    void SpitWeb(Unit u, float dt)
    {
        if ((u.Spit -= dt) > 0 || u.Routing || u.Action != UnitAction.None || u.EnemyDist > WebRange) return;
        var (t, d) = Nearest(u);
        if (t == null || d < 2.5f || d > WebRange) { u.Spit = 0.5f; return; }
        u.Spit = 6 + rng.NextSingle() * 4;
        float r = u.Type.Huge ? WebRadius * 1.5f : WebRadius;
        foreach (var m in Near(t.Pos, r, nearBeast))
            if (m.Active && m.Team != u.Team && !m.Type.Huge && !Airborne(m) && Vector2.DistanceSquared(m.Pos, t.Pos) < r * r) m.WebbedUntil = Time + WebTime;
        u.Face(t.Pos - u.Pos);
        Flash("web", t.Pos, r, u.Team, "", t.Pos - u.Pos);
        Sound(Cue.Throw, u.Pos);
    }

    /// A flyer's height: high while it climbs away or closes from afar, low as it closes on its prey, down on it in a strike.
    void Fly(Unit u, float dt)
    {
        if (u.Climbing && Time >= u.ClimbUntil) u.Climbing = false;
        float goal = u.Routing || u.Climbing ? FlyHigh
            : u.Action is UnitAction.Windup or UnitAction.Recover or UnitAction.Stagger ? 0
            : Math.Clamp((u.EnemyDist - 2.5f) * 0.6f, 0, FlyHigh);
        u.Altitude += (goal - u.Altitude) * MathF.Min(1, dt * (goal < u.Altitude ? 3.5f : 2f));
    }

    /// A flyer's wits: wheel high over the field, pick the nearest prey, dive on it, strike, and climb away to come again.
    void ThinkFlyer(Unit u, float dt)
    {
        var s = u.Squad!;
        if (u.Action is UnitAction.Windup or UnitAction.Recover) { u.Wish = u.Dir * u.Type.Speed * 0.3f; return; } // it glides through its strike
        if (u.Action != UnitAction.None && u.Action != UnitAction.Block) { u.Wish = Vector2.Zero; return; }
        var (enemy, d) = s.Target != null && s.Order == Order.Charge ? Nearest(u, s.Target) : Nearest(u);
        if (enemy == null) (enemy, d) = Nearest(u);
        u.EnemyDist = d;
        if (enemy == null || s.Order != Order.Charge && d > 30 && !u.IsLeader || u.IsLeader && s.Order != Order.Charge && d > 18)
        {
            var home = u.Slot - u.Pos;
            if (home.LengthSquared() > 0.16f) { u.Wish = Vector2.Normalize(home) * MathF.Min(u.Type.Speed, s.MarchSpeed) * MathF.Min(1, home.Length()); u.Face(home); }
            else { u.Wish = Vector2.Zero; u.Face(s.FaceAt(u.Slot)); }
            return;
        }
        if (u.Climbing)
        {
            // Away and up past its prey, banking a little so it comes round again from a new side.
            var away = u.Pos - enemy.Pos;
            if (away.LengthSquared() < 0.01f) away = u.Dir;
            away = Vector2.Normalize(away) + new Vector2(-away.Y, away.X) / MathF.Max(1, away.Length()) * 0.6f;
            u.Wish = Vector2.Normalize(away) * u.Type.Speed;
            u.Face(away);
            return;
        }
        var to = enemy.Pos - u.Pos;
        u.Face(to);
        if (d <= ReachOf(u) + 0.3f) { u.Wish = Vector2.Zero; if (u.Cooldown <= 0) StartSwing(u, enemy); return; }
        u.Wish = to / MathF.Max(0.01f, to.Length()) * u.Type.Speed;
    }

    /// A flyer has struck: it climbs away to wheel and come again.
    void Climb(Unit a)
    {
        a.Climbing = true;
        a.ClimbUntil = Time + ClimbTime + rng.NextSingle();
    }

    /// Every few seconds each living brood mother with foes near lays two spiderlings beside her, which run at once.
    void UpdateBrood(float dt)
    {
        for (int team = 0; team < 2; team++)
        {
            if ((broodTimer[team] -= dt) > 0) continue;
            broodTimer[team] = BroodEvery;
            foreach (var m in Units.Where(u => u.Active && u.Team == team && u.Type.Wild && u.Type.Has("brood") && u.EnemyDist < 30).ToList())
                for (int k = 0; k < 2 && (brood[team]?.StartCount ?? 0) < BroodMax; k++) Hatch(m);
        }
    }

    void Hatch(Unit mother)
    {
        var s = brood[mother.Team];
        if (s == null)
        {
            s = brood[mother.Team] = new Squad("The Brood", mother.Team) { Anchor = mother.Pos, Front = mother.Dir, Order = Order.Charge, Formation = Formation.Loose };
            Squads.Add(s);
            if (!scripted) Role(mother.Team, "infantry").Add(s);
            Log(mother.Team == 0 ? "The brood mother lays her young!" : "The brood mother lays her young! Spiderlings swarm from under her.");
        }
        var at = mother.Pos - mother.Dir * 1.2f + new Vector2(rng.NextSingle() - 0.5f, rng.NextSingle() - 0.5f) * 2;
        var u = Spawn(Troops.Get("spiderling"), mother.Team, s, at);
        u.Face(mother.Dir);
        u.Heading = u.Facing;
        s.Units.Add(u);
        s.StartCount++;
        s.MarchSpeed = MathF.Min(s.MarchSpeed, u.Type.Speed);
    }

    /// How hard a creature's blow lands, past what any man's does: a raging bear's harder.
    static float BeastBlow(Unit a) => Raging(a) ? RageBlow : 1;

    /// A blow at something aloft: only a flyer reaches another flyer; arrows are not blows and go through Hit regardless.
    static bool OutOfReach(Unit a, Unit t, bool ranged) => !ranged && Airborne(t) && !a.Type.Flying;

    /// An arrow high in the air may still take a flyer: near it across the ground and at its height.
    Unit? FlyerHit(Vector2 flat, float above, int team)
    {
        foreach (var t in Near(flat, 2, nearBeast))
        {
            if (t.Team == team || !t.Active || !t.Type.Flying) continue;
            float r = BodyRadius(t) * 1.4f;
            if (Vector2.DistanceSquared(t.Pos, flat) <= r * r && MathF.Abs(above - t.Altitude - 0.8f) < 1.4f) return t;
        }
        return null;
    }
}
