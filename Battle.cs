using Microsoft.Xna.Framework;

namespace Ironfield;

public struct PlayerInput
{
    public Vector2 Move;      // on foot: world-space wish direction, length 0..1
    public float Throttle;    // mounted: +1 forward, -1 rein back
    public float Steer;       // mounted: +1 turn right, -1 turn left
    public float Facing;      // where the player looks (and strikes)
    public AttackDir Dir;     // attack / block direction, picked by mouse movement
    public bool Sprint, Attack, Block, Rally, Couch, Mount;
}

public sealed class Projectile
{
    public Vector3 Pos, Vel;
    public Unit Shooter = null!;
    public float Damage, Stuck = -1; // Stuck >= 0: seconds spent lying in the ground
}

/// Headless battle simulation. No graphics, so the self-test can run it.
public sealed partial class Battle
{
    public const float MapRadius = 95, Gravity = 9.8f, ArrowSpeed = 28, RallyRadius = 15;
    public readonly BattleOptions Options;
    /// Sound cues from the last Update (the screen plays them; headless runs ignore them).
    public readonly List<(Cue Cue, Vector2 Pos)> Sounds = new();
    void Sound(Cue c, Vector2 at) { if (Sounds.Count < 64) Sounds.Add((c, at)); }
    const float LanceFrom = 1.0f, LanceTip = 2.7f, BraceExtra = 0.8f;

    public readonly List<Unit> Units = new();
    public readonly List<Squad> Squads = new();
    public readonly List<Projectile> Arrows = new();
    public readonly List<LooseHorse> LooseHorses = new();
    public readonly List<DeadHorse> DeadHorses = new();
    public readonly ArmySpec?[] Armies = new ArmySpec?[2];
    public readonly List<(string Text, float At)> Events = new();
    public readonly Terrain Terrain;
    public Weather Weather => Options.Weather;
    public Unit Player = null!, EnemyCaptain = null!;
    public PlayerInput Input;
    public bool Commanding; // player is in command view: no leadership aura, body stands idle
    public int Winner = -1, PlayerKills;
    public float Time, RallyCooldown;
    public (string Text, float At) PlayerHit = ("", -10); // last thing the player's weapon did, shown under the crosshair

    readonly Random rng;
    readonly Vector2[] centroid = new Vector2[2];
    readonly bool scripted; // self-test scenario: no enemy general
    Squad eGuard = null!;
    readonly Dictionary<string, List<Squad>>[] roles = { new(), new() };
    readonly Dictionary<Squad, float> lateral = new(); // sideways place within its role group, in metres
    bool enemyCharged, enemyHorseCharged;
    float generalTimer, reinforceTimer;
    readonly bool[] charged = new bool[2], horseCharged = new bool[2];
    readonly Queue<(Company C, string Role)>[] reserves = { new(), new() };
    readonly List<(Company C, int Team)> heldBack = new(); // companies that never reached the field

    /// The prototype's standard battle (level 2 armies on both sides).
    public Battle(int seed) : this(seed, Ironfield.Armies.Standard(0), Ironfield.Armies.Standard(1)) { }

    /// A battle between two armies as the campaign map would hand them over.
    public Battle(int seed, ArmySpec player, ArmySpec enemy, BattleOptions? options = null) : this(seed, false, options)
    {
        var mount = Options.PlayerMount ?? Mount.Courser; // you lead from the saddle (on foot in an assault: horses do not climb ladders)
        Player.Horse = Options.Siege ? null : mount;
        Player.HorseHp = mount.Hp;
        // The field holds at most Options.Cap men; each side gets its share and the rest wait as reinforcements.
        int total = player.Companies.Sum(c => c.Count) + enemy.Companies.Sum(c => c.Count);
        float share = total > Options.Cap ? Options.Cap / (float)total : 1;
        Deploy(0, player, (int)(player.Companies.Sum(c => c.Count) * share));
        Deploy(1, enemy, (int)(enemy.Companies.Sum(c => c.Count) * share));
        if (Options.PlayerWounded) Player.Fled = true; // too hurt to ride: you command from the rear
        UpdateCentroids();
    }

    List<Squad> Role(int team, string role) => roles[team].TryGetValue(role, out var l) ? l : roles[team][role] = new List<Squad>();

    /// Deployment spots per role on your side (the enemy's are mirrored through the centre): infantry in front,
    /// archers behind, spears on the left flank, horse on the right. Several companies of one role stand side by side.
    void Deploy(int team, ArmySpec army, int budget)
    {
        Armies[team] = army;
        float f = team == 0 ? 1 : -1;
        // Biggest companies take the field first; the rest wait in reserve.
        var onField = new List<Company>();
        int used = 0;
        foreach (var c in army.Companies.Where(c => c.Count > 0).OrderByDescending(c => c.Count))
        {
            if (onField.Count == 0 || used + c.Count <= budget) { onField.Add(c); used += c.Count; }
            else reserves[team].Enqueue((c, Ironfield.Armies.RoleOf(c)));
        }
        if (Options.Siege && team == 1) { DeployDefenders(onField); return; }
        foreach (var group in onField.GroupBy(Ironfield.Armies.RoleOf))
        {
            var spot = group.Key switch
            {
                "archers" => new Vector2(0, -38), "spears" => new Vector2(-22, -31), "horse" => new Vector2(24, -33), _ => new Vector2(0, -30),
            };
            var list = group.ToList();
            var widths = list.Select(c =>
            {
                var t = Troops.Get(c.Troop);
                int cols = (c.Count + 1) / 2;
                return (c, t, cols, w: cols * (t.Mount != null ? 3f : 1.3f));
            }).ToList();
            float total = widths.Sum(x => x.w) + 4 * (widths.Count - 1), at = -total / 2;
            foreach (var (c, t, cols, w) in widths)
            {
                float off = at + w / 2;
                at += w + 4;
                var pos = (spot + new Vector2(off, 0)) * f;
                var s = AddSquad(team, t, c.Count, pos, cols, new Vector2(0, f), c);
                Role(team, group.Key).Add(s);
                lateral[s] = off;
            }
        }
    }

    /// An empty field with only the two leaders, far apart; scenarios add squads with <see cref="AddSquad"/>.
    public static Battle Empty(int seed) => new(seed, true);

    Battle(int seed, bool scripted, BattleOptions? options = null)
    {
        rng = new Random(seed);
        this.scripted = scripted;
        Options = options ?? new BattleOptions();
        Terrain = scripted ? Terrain.Flat() : new Terrain(seed, Options.Ground ?? TerrainParams.Meadow, Options.Siege ? new SiegeWall(Options.Ladders, Options.GateHp) : null);
        Player = Spawn(Options.PlayerType ?? UnitType.Captain, 0, null, new Vector2(0, scripted ? -80 : -34));
        Player.IsPlayer = Player.IsLeader = true;
        Player.Facing = Player.Heading = MathHelper.PiOver2;
        eGuard = AddSquad(1, UnitType.Captain, 1, new(0, scripted ? 80 : 34), 1, -Vector2.UnitY);
        EnemyCaptain = eGuard.Units[0];
        EnemyCaptain.IsLeader = true;
        UpdateCentroids();
    }

    public IEnumerable<Squad> SquadsOf(int team) => Squads.Where(s => s.Team == team);
    public Vector2 Centroid(int team) => centroid[team];
    public void Log(string text) => Events.Add((text, Time));

    public Squad AddSquad(int team, UnitType type, int n, Vector2 anchor, int cols, Vector2 front, Company? company = null)
    {
        var s = new Squad(type.Name, team) { Anchor = anchor, Columns = cols, Front = front, StartCount = n, Cavalry = type.Mount != null && !Options.Siege, Company = company,
            AiControlled = company?.Allied == true && team == 0 };
        for (int i = 0; i < n; i++)
        {
            var u = Spawn(type, team, s, s.Slot(i, n));
            if (company != null) u.Morale = company.Morale;
            u.Face(front);
            u.Heading = u.Facing;
            s.Units.Add(u);
        }
        Squads.Add(s);
        return s;
    }

    public Unit Spawn(UnitType t, int team, Squad? s, Vector2 pos)
    {
        var u = new Unit { Type = t, Team = team, Squad = s, Pos = pos, Hp = t.MaxHp, Ammo = t.Ammo, Anim = rng.NextSingle() * 2, Coat = rng.Next(Style.Coats.Length),
            Palette = Options.Palette[Math.Clamp(team, 0, 1)] };
        if (t.Mount != null && !Options.Siege) { u.Horse = t.Mount; u.HorseHp = t.Mount.Hp; } // assaults are fought on foot
        Units.Add(u);
        return u;
    }

    public void Update(float dt)
    {
        Sounds.Clear();
        Time += dt;
        RallyCooldown = MathF.Max(0, RallyCooldown - dt);
        UpdateCentroids();
        if (!scripted && (generalTimer -= dt) <= 0)
        {
            generalTimer = 0.5f;
            General(1);
            if (Options.AutoPlayer || Squads.Any(q => q.AiControlled && q.Alive > 0)) General(0);
        }
        Batter(dt);
        if ((reinforceTimer -= dt) <= 0) { reinforceTimer = 1; Reinforce(); }
        foreach (var s in Squads) UpdateSquad(s, dt);
        foreach (var u in Units)
        {
            if (!u.Active) continue;
            u.HitFlash = MathF.Max(0, u.HitFlash - dt);
            u.Cooldown -= dt;
            u.ImpactCd -= dt;
            u.BraceCd -= dt;
            if (u.IsPlayer) UpdatePlayer(u, dt); else Think(u, dt);
            TickAction(u, dt);
        }
        Move(dt);
        Braces();
        Lances();
        UpdateArrows(dt);
        UpdateMorale(dt);
        CheckEnd();
        Events.RemoveAll(e => Time - e.At > 6);
    }

    void UpdateCentroids()
    {
        for (int t = 0; t < 2; t++)
        {
            var sum = Vector2.Zero;
            int n = 0;
            foreach (var u in Units) if (u.Team == t && u.Active && !u.Routing) { sum += u.Pos; n++; }
            if (n > 0) centroid[t] = sum / n;
        }
    }

    void UpdateSquad(Squad s, float dt)
    {
        s.Pressure *= MathF.Exp(-0.6f * dt);
        if (s.Target is { Alive: 0 }) s.Target = null; // target broken: fall back to the nearest enemy
        if (s.Order == Order.Follow && Player.Active)
        {
            var toEnemy = centroid[1 - s.Team] - Player.Pos;
            if (toEnemy.LengthSquared() > 1) s.Front = Vector2.Normalize(toEnemy);
            int rank = SquadsOf(s.Team).TakeWhile(o => o != s).Count();
            s.Anchor = Player.Pos - s.Front * (3 + rank * 5); // squads queue up behind the player
        }
        int n = 0;
        foreach (var u in s.Units) if (u.Active && !u.Routing) n++;
        s.Alive = n;
        if (!s.RoutSounded && n > 0 && s.Units.Count(u => u.Active && u.Routing) > n) { s.RoutSounded = true; Sound(Cue.Rout, s.Centroid()); }
        int i = 0;
        foreach (var u in s.Units) if (u.Active && !u.Routing) { u.Slot = s.Slot(i++, n); }
    }

    /// Nearest active, non-routing enemy; restricted to squad `only` when given.
    (Unit? unit, float dist) Nearest(Unit u, Squad? only = null)
    {
        Unit? best = null;
        float bd = float.MaxValue;
        foreach (var o in only?.Units ?? Units)
        {
            if (o.Team == u.Team || !o.Active || o.Routing) continue;
            float d = Vector2.DistanceSquared(o.Pos, u.Pos);
            if (d < bd) { bd = d; best = o; }
        }
        return (best, MathF.Sqrt(bd));
    }

    /// Spears (and shield walls standing tight) set themselves against horses when holding still.
    static bool CanBrace(Unit b) =>
        !b.Mounted && b.Squad is { Order: Order.Hold } s && !b.Routing
        && (b.Type.Class == UnitClass.Skirmisher || (b.Type.Class == UnitClass.Infantry && !s.Loose));

    static bool Braced(Unit b) => CanBrace(b) && b.Action is UnitAction.None or UnitAction.Block && b.Vel.LengthSquared() < 1;

    AttackDir RandomSwing(Unit u) => u.Type.Class is UnitClass.Skirmisher or UnitClass.Cavalry
        ? (rng.Next(4) == 0 ? AttackDir.Overhead : AttackDir.Thrust)
        : (AttackDir)rng.Next(4);

    void StartSwing(Unit u, Unit target)
    {
        u.Action = UnitAction.Windup;
        u.ActionTimer = u.Type.Windup;
        u.Target = target;
        u.Swing = RandomSwing(u);
        u.Face(target.Pos - u.Pos);
    }

    void Think(Unit u, float dt)
    {
        if (u.Routing)
        {
            var away = u.Pos - centroid[1 - u.Team];
            if (away.LengthSquared() < 1e-3f) away = Vector2.UnitX;
            away.Normalize();
            u.Couched = false;
            u.Wish = away * (u.Mounted ? u.Horse!.Gallop : u.Type.Speed * 1.15f);
            if (!u.Mounted) u.Face(away);
            u.EnemyDist = float.MaxValue;
            if (u.Pos.Length() > MapRadius) u.Fled = true;
            return;
        }
        if (u.Action != UnitAction.None && u.Action != UnitAction.Block)
        {
            if (!u.Mounted) u.Wish = Vector2.Zero; // committed to a swing; a rider keeps riding
            return;
        }

        var s = u.Squad!;
        var (enemy, d) = Nearest(u);
        u.EnemyDist = d;
        if (u.Mounted) { ThinkCav(u, s, enemy, d, dt); return; }
        u.Wish = Vector2.Zero;

        float toSlot = Vector2.Distance(u.Pos, u.Slot);
        bool charge = s.Order == Order.Charge;
        if (charge && s.Target != null && d > 3) (enemy, d) = Nearest(u, s.Target); // go for the ordered target

        // Archers shoot once settled in formation (or anywhere when charging).
        float range = MathF.Min(Weather.Vision, u.Type.Range * (enemy != null && Terrain.Height(u.Pos) > Terrain.Height(enemy.Pos) + 2 ? 1.2f : 1)); // height carries arrows further; fog and night hide targets
        if (enemy != null && u.Ammo > 0 && s.FireAtWill && d > 4 && d < range && (charge || toSlot < 2.5f))
        {
            u.Face(enemy.Pos - u.Pos);
            if (u.Cooldown <= 0) Shoot(u, enemy);
            return;
        }

        float reach = enemy == null ? 0 : u.Type.Reach + (enemy.Mounted ? 0.6f : 0);
        // Brace for the charge: hold the slot and present the point instead of stepping out to meet it.
        if (enemy is { Mounted: true } && CanBrace(u) && d < 14 && toSlot < 3)
        {
            u.Face(enemy.Pos - u.Pos);
            if (d <= reach && u.Cooldown <= 0 && enemy.Speed < 4) StartSwing(u, enemy);
            return;
        }

        bool leashed = !charge && toSlot > 14;
        if (enemy != null && !leashed && (charge || d < 6))
        {
            var to = Via(u, enemy.Pos) - u.Pos;
            u.Face(to);
            if (d <= reach) { if (u.Cooldown <= 0) StartSwing(u, enemy); }
            else u.Wish = to / MathF.Max(0.01f, to.Length()) * u.Type.Speed;
            return;
        }

        if (toSlot > 0.4f)
        {
            var to = Via(u, u.Slot) - u.Pos;
            u.Wish = to / toSlot * u.Type.Speed * MathF.Min(1, toSlot);
            u.Face(to);
        }
        else u.Face(s.Front);
    }

    /// Cavalry AI. Wish is the desired velocity; Steer() turns it into heading and speed with momentum.
    void ThinkCav(Unit u, Squad s, Unit? enemy, float d, float dt)
    {
        var h = u.Horse!;
        var hd = u.HeadingDir;

        // Riders strike at whatever comes into reach as they pass (not with a couched lance).
        if (enemy != null && !u.Couched && u.Cooldown <= 0
            && d <= u.Type.Reach + (enemy.Mounted ? 0.6f : 0.2f) + MathF.Abs(u.Speed) * 0.12f
            && Vector2.Dot((enemy.Pos - u.Pos) / MathF.Max(d, 0.01f), hd) > -0.3f)
            StartSwing(u, enemy);

        // Horse archers loose from the saddle at anything in range, whatever their order.
        if (u.Ammo > 0 && enemy != null && s.FireAtWill && d > 5 && d < u.Type.Range && u.Cooldown <= 0 && u.Action == UnitAction.None)
        {
            Shoot(u, enemy);
            u.Cooldown *= 1.3f; // slower from horseback
        }
        if (u.Ammo > 0 && s.Order == Order.Charge && enemy != null)
        {
            // Skirmish: ride in to bow range, wheel away when they come close, circle the enemy's flank.
            var away = u.Pos - enemy.Pos;
            var side = new Vector2(-away.Y, away.X) / MathF.Max(0.01f, d);
            var dir = d < u.Type.Range * 0.45f ? away / MathF.Max(0.01f, d) + side * 0.4f : d > u.Type.Range * 0.8f ? -away / MathF.Max(0.01f, d) : side;
            if (u.Pos.Length() > MapRadius - 20) dir -= Vector2.Normalize(u.Pos);
            u.Wish = Vector2.Normalize(Via(u, u.Pos + dir * 10) - u.Pos + new Vector2(1e-4f, 0)) * (h.Trot + h.Gallop) / 2;
            u.Couched = false;
            return;
        }
        if (s.Order != Order.Charge)
        {
            u.Cav = CavState.Charge;
            u.Contact = u.Couched = false;
            float toSlot = Vector2.Distance(u.Pos, u.Slot);
            if (enemy != null && d < 15 && toSlot < 25)
            {
                // Counter-charge whatever comes close: horses standing still are easy meat for a charge.
                u.Wish = (enemy.Pos - u.Pos) / MathF.Max(d, 0.01f) * (d > 5 ? h.Gallop : h.Trot);
                if (u.Type.Class == UnitClass.Cavalry && u.Speed > 6.5f && d > 4 && u.Cooldown <= 0) u.Couched = true;
                return;
            }
            Ride(u, Via(u, u.Slot), toSlot > 12 ? h.Gallop * 0.8f : h.Trot);
            return;
        }

        var (t, td) = s.Target != null ? Nearest(u, s.Target) : (enemy, d);
        if (t == null) { (t, td) = (enemy, d); }
        if (t == null) { Ride(u, u.Slot, h.Trot); return; }

        switch (u.Cav)
        {
            case CavState.Withdraw:
            {
                // Ride out, away from the enemy mass, then turn for another charge.
                u.CavTimer -= dt;
                u.Couched = false;
                var away = u.Pos - centroid[1 - u.Team];
                away = away.LengthSquared() > 1e-3f ? Vector2.Normalize(away) : hd;
                var dir = hd + away * 1.5f;
                if (u.Pos.Length() > MapRadius - 25) dir -= Vector2.Normalize(u.Pos) * 3; // stay on the field
                u.Wish = Vector2.Normalize(dir) * h.Gallop;
                if (u.CavTimer <= 0) u.Cav = CavState.Charge;
                return;
            }
            case CavState.Melee:
            {
                // Bogged down: stand and hack, then break off.
                u.CavTimer -= dt;
                u.Wish = td > u.Type.Reach ? (t.Pos - u.Pos) / td * h.Trot * 0.6f : Vector2.Zero;
                if (u.CavTimer <= 0) { u.Cav = CavState.Withdraw; u.CavTimer = 3.5f; }
                return;
            }
            default:
            {
                // Charge: aim a few metres past the target to ride through it.
                var to = (t.Pos - u.Pos) / MathF.Max(td, 0.01f);
                var aimAt = Via(u, t.Pos + to * 6);
                u.Wish = (aimAt - u.Pos).LengthSquared() > 1e-4f ? Vector2.Normalize(aimAt - u.Pos) * h.Gallop : Vector2.Zero;
                if (u.Type.Class == UnitClass.Cavalry && u.Speed > 6.5f && td < 30 && u.Cooldown <= 0) u.Couched = true;
                if (td < 4) u.Contact = true;
                if (u.Contact && td > 4 && Vector2.Dot(t.Pos - u.Pos, hd) < 0)
                {
                    u.Cav = CavState.Withdraw; // rode through: wheel away
                    u.CavTimer = 3f;
                    u.Contact = false;
                }
                u.Bogged = u.Speed < 2.5f && td < 4 ? u.Bogged + dt : 0;
                if (u.Bogged > 1.2f) { u.Cav = CavState.Melee; u.CavTimer = 4f; u.Bogged = 0; u.Couched = false; }
                return;
            }
        }
    }

    static void Ride(Unit u, Vector2 to, float speed)
    {
        var d = to - u.Pos;
        float l = d.Length();
        if (l < 1.2f) { u.Wish = Vector2.Zero; u.Face(u.Squad!.Front); return; }
        u.Wish = d / l * MathF.Min(speed, l * 0.9f + 0.5f); // slow down into the slot
    }

    static float Approach(float v, float target, float step) => v < target ? MathF.Min(target, v + step) : MathF.Max(target, v - step);
    float Mud => Weather.MudFactor;
    static float TurnRate(Unit u) => u.Horse!.TurnRate * (1.25f - 0.6f * MathF.Min(1, MathF.Abs(u.Speed) / u.Horse.Gallop));

    /// AI horses: turn toward Wish at a limited rate, slow down for sharp turns, accelerate and brake with inertia.
    void Steer(Unit u, float dt)
    {
        var h = u.Horse!;
        float ws = u.Action == UnitAction.Stagger ? 0 : u.Wish.Length(), rate = TurnRate(u);
        if (ws > 0.05f)
        {
            float diff = MathHelper.WrapAngle(MathF.Atan2(u.Wish.Y, u.Wish.X) - u.Heading);
            u.Heading += Math.Clamp(diff, -rate * dt, rate * dt);
            ws *= MathF.Max(0.2f, MathF.Cos(MathF.Min(MathF.Abs(diff), MathHelper.PiOver2)));
        }
        else if (MathF.Abs(u.Speed) < 1.5f)
            u.Heading += Math.Clamp(MathHelper.WrapAngle(u.Facing - u.Heading), -rate * 0.6f * dt, rate * 0.6f * dt); // turn on the spot
        u.Speed = Approach(u.Speed, ws * Mud, (ws > u.Speed ? h.Accel : h.Accel * 1.8f) * dt);
        if (u.Action == UnitAction.None && (ws > 0.05f || u.Speed > 1.5f)) u.Facing = u.Heading;
    }

    void UpdatePlayer(Unit p, float dt)
    {
        var inp = Input;
        if (Commanding) { inp = default; inp.Dir = Input.Dir; p.Charging = false; p.Charge = 0; }
        else p.Facing = inp.Facing;

        if (inp.Mount) ToggleMount(p);

        bool busy = p.Action is UnitAction.Windup or UnitAction.Recover or UnitAction.Stagger;
        if (!busy)
        {
            if (inp.Block && p.Charging) { p.Charging = false; p.Charge = 0; } // feint: cancel the swing into a block
            if (inp.Block) p.Action = UnitAction.Block;
            else if (p.Action == UnitAction.Block) p.Action = UnitAction.None;
            if (p.Action == UnitAction.None && !p.Couched)
            {
                if (inp.Attack)
                {
                    if (!p.Charging) p.Swing = inp.Dir; // direction locks in when the swing starts
                    p.Charging = true;
                    p.Charge = MathF.Min(1, p.Charge + dt / 0.7f);
                }
                else if (p.Charging)
                {
                    // Release: power scales with how long the swing was held.
                    p.Charging = false;
                    p.Power = (0.6f + p.Charge) * p.Swing switch { AttackDir.Overhead => 1.2f, AttackDir.Thrust => 1f, _ => 0.95f };
                    p.Charge = 0;
                    p.Action = UnitAction.Windup;
                    p.ActionTimer = p.Swing == AttackDir.Overhead ? 0.2f : 0.15f;
                    p.Stamina -= 8;
                }
            }
        }

        if (p.Mounted)
        {
            var h = p.Horse!;
            float rate = TurnRate(p) * 1.2f;
            p.Heading += inp.Steer * rate * dt;
            float target = inp.Throttle > 0 ? (inp.Sprint ? h.Gallop : h.Trot) : inp.Throttle < 0 ? -1.8f : 0;
            if (p.Action == UnitAction.Stagger) target = 0;
            bool speedingUp = MathF.Abs(target) > MathF.Abs(p.Speed) && target * p.Speed >= 0;
            p.Speed = Approach(p.Speed, target, (speedingUp ? h.Accel : h.Accel * 1.8f) * dt);
            p.Wish = p.HeadingDir * p.Speed;
            if (inp.Couch && !p.Couched && p.Speed > 6 && p.Cooldown <= 0 && p.Action == UnitAction.None && !p.Charging)
            {
                p.Couched = true;
                p.Action = UnitAction.None;
            }
            else if (inp.Couch && p.Couched) p.Couched = false;
            if (p.Couched && p.Speed < 4.5f) p.Couched = false;
            p.Stamina = Math.Clamp(p.Stamina + (p.Action == UnitAction.Block ? 4 : 15) * dt, 0, 100);
        }
        else
        {
            bool sprint = inp.Sprint && inp.Move != Vector2.Zero && p.Stamina > 5 && !p.Charging && p.Action == UnitAction.None;
            float speed = p.Type.Speed * (sprint ? 1.6f : 1)
                * (p.Charging || p.Action == UnitAction.Block ? 0.5f : 1)
                * (p.Action == UnitAction.Stagger ? 0 : busy ? 0.3f : 1);
            p.Wish = inp.Move * speed;
            p.Stamina = Math.Clamp(p.Stamina + (sprint ? -20 : p.Action == UnitAction.Block ? 4 : 15) * dt, 0, 100);
        }
        if (inp.Rally && RallyCooldown <= 0 && p.Stamina >= 30) Rally(p);
    }

    /// Dismount (when the horse is nearly still), or mount the nearest loose horse within reach.
    void ToggleMount(Unit p)
    {
        if (p.Action is UnitAction.Windup or UnitAction.Stagger) return;
        if (p.Mounted)
        {
            if (MathF.Abs(p.Speed) > 1.5f) { Log("Slow down to dismount."); return; }
            var side = new Vector2(-p.HeadingDir.Y, p.HeadingDir.X);
            LooseHorses.Add(new LooseHorse { Mount = p.Horse!, Hp = p.HorseHp, Pos = p.Pos, Heading = p.Heading, Team = p.Team, Coat = p.Coat });
            p.Pos -= side * 1.1f;
            p.Horse = null;
            p.Speed = 0;
            p.Couched = false;
            Log("You dismount.");
            return;
        }
        LooseHorse? best = null;
        float bd = 2.8f * 2.8f;
        foreach (var lh in LooseHorses)
        {
            float d = Vector2.DistanceSquared(lh.Pos, p.Pos);
            if (d < bd) { bd = d; best = lh; }
        }
        if (best == null) { Log("No horse within reach."); return; }
        LooseHorses.Remove(best);
        p.Horse = best.Mount;
        p.HorseHp = best.Hp;
        p.Coat = best.Coat;
        p.Heading = best.Heading;
        p.Pos = best.Pos;
        p.Speed = 0;
        p.Charging = false;
        p.Action = UnitAction.None;
        Log(best.Team == p.Team ? "You mount up." : "You seize an enemy horse!");
    }

    void Rally(Unit p)
    {
        p.Stamina -= 30;
        RallyCooldown = Options.RallyCooldown;
        foreach (var u in Units)
            if (u.Active && u.Team == p.Team && !u.IsPlayer && Vector2.DistanceSquared(u.Pos, p.Pos) < RallyRadius * RallyRadius)
                u.Morale = MathF.Min(100, u.Morale + (u.Routing ? Options.RallyBonus + 20 : Options.RallyBonus));
        Log("Rally! Hold the line!");
        Sound(Cue.Horn, p.Pos);
    }

    void TickAction(Unit u, float dt)
    {
        if (u.Action == UnitAction.None || (u.IsPlayer && u.Action == UnitAction.Block)) return;
        if ((u.ActionTimer -= dt) > 0) return;
        switch (u.Action)
        {
            case UnitAction.Windup:
                Strike(u);
                u.Action = UnitAction.Recover;
                u.ActionTimer = u.IsPlayer ? 0.3f : u.Type.Recover;
                break;
            case UnitAction.Recover:
                u.Action = UnitAction.None;
                if (!u.IsPlayer) u.Cooldown = MathF.Max(u.Cooldown, rng.NextSingle() * 0.5f); // desync the line
                break;
            default:
                u.Action = UnitAction.None;
                break;
        }
    }

    void Strike(Unit a)
    {
        float dmg = a.Type.Damage * a.Power;
        if (!a.IsPlayer)
        {
            if (a.Target is not { Active: true } t) return;
            float extra = t.Mounted ? 0.6f : 0;
            // A rider's blow lands on whatever is beside the horse; a footman needs the target in front.
            bool lands = a.Mounted
                ? Vector2.Distance(a.Pos, t.Pos) <= a.Type.Reach + 1.2f + extra
                : InArc(a, t, a.Type.Reach + 0.4f + extra, 0.5f);
            if (lands) Hit(a, t, dmg, false);
            return;
        }
        // Player: thrusts reach far but hit one man; side cuts sweep a wide arc and cleave up to 3.
        var (reach, minDot, cleave) = a.Swing switch
        {
            AttackDir.Thrust => (a.Type.Reach + 0.7f, 0.8f, 1),
            AttackDir.Overhead => (a.Type.Reach + 0.3f, 0.6f, 1),
            _ => (a.Type.Reach + 0.3f, 0.2f, 3),
        };
        if (a.Mounted) reach += 0.4f;
        int hits = 0;
        foreach (var t in Units.Where(t => t.Team != a.Team && t.Active).OrderBy(t => Vector2.DistanceSquared(t.Pos, a.Pos)))
            if (InArc(a, t, reach + (t.Mounted ? 0.6f : 0), minDot) && hits++ < cleave) Hit(a, t, dmg, false);
    }

    static bool InArc(Unit a, Unit t, float reach, float minDot)
    {
        var d = t.Pos - a.Pos;
        float l = d.Length();
        return l <= reach && (l < 0.01f || Vector2.Dot(d / l, a.Dir) >= minDot);
    }

    /// Mount & Blade speed bonus: blows land harder the faster the two bodies close on each other.
    static float SpeedBonus(Unit a, Unit t)
    {
        var n = t.Pos - a.Pos;
        float l = n.Length();
        if (l < 0.01f) return 0;
        var rel = a.Vel - t.Vel;
        float eff = 0.4f * rel.Length() + 0.6f * Vector2.Dot(rel, n / l);
        return MathF.Max(0, eff - 2) * 0.06f;
    }

    enum Side { Front, Flank, Rear }

    static Side SideOf(Unit a, Unit t)
    {
        var from = a.Pos - t.Pos;
        if (from.LengthSquared() < 1e-4f) return Side.Front;
        float dot = Vector2.Dot(Vector2.Normalize(from), t.Dir);
        return dot > 0.4f ? Side.Front : dot < -0.4f ? Side.Rear : Side.Flank;
    }

    /// One blow (melee, arrow or lance) from a at t: horse hits, blocks and parries, then damage multipliers.
    internal void Hit(Unit a, Unit t, float dmg, bool ranged, bool lance = false)
    {
        if (a.Team == 1) dmg *= Options.EnemyDamage;
        if (ranged && Wall != null && SiegeWall.Inside(t.Pos) && !SiegeWall.Inside(a.Pos) && Terrain.Height(t.Pos) > Terrain.Height(a.Pos) + 2) dmg *= 0.6f; // merlons
        // Arrows and spears often take the horse instead of the rider.
        if (t.Mounted && rng.NextSingle() < (ranged ? 0.45f : a.Type.Class == UnitClass.Skirmisher ? 0.5f : 0.15f))
        {
            HitHorse(t, dmg * a.Type.VsMounted, a);
            return;
        }

        var side = SideOf(a, t);
        bool facing = side == Side.Front;
        if (t.IsPlayer)
        {
            if (facing && t.Action == UnitAction.Block)
            {
                // Matching the incoming direction is a parry: free, and it throws the attacker off balance.
                if (!ranged && !lance && Input.Dir == Dirs.Mirror(a.Swing))
                {
                    Stagger(a, 0.9f);
                    t.Stamina = MathF.Max(0, t.Stamina - 2);
                    PlayerHit = ("PARRY!", Time);
                    Sound(Cue.Clash, t.Pos);
                    return;
                }
                t.Stamina -= ranged ? 4 : dmg * (lance ? 1.2f : 0.8f); // a shield stops any direction, at a stamina cost
                Sound(Cue.Block, t.Pos);
                if (t.Stamina <= 0) { t.Stamina = 0; Stagger(t, 0.8f); Log("Guard broken!"); }
                else if (!ranged && !lance) Stagger(a, 0.35f);
                return;
            }
        }
        else if (facing && t.Action is UnitAction.None or UnitAction.Block)
        {
            // Shields cover every direction; without one, thrusts and overheads are harder to read.
            float chance = t.Type.BlockChance * (lance ? 0.5f : 1) * (t.Type.HasShield
                ? (a.Swing == AttackDir.Thrust ? 0.9f : 1)
                : a.Swing switch { AttackDir.Thrust => 0.7f, AttackDir.Overhead => 0.85f, _ => 1 });
            if (ranged && !t.Type.HasShield) chance *= 0.3f;
            if (rng.NextSingle() < chance)
            {
                t.Action = UnitAction.Block;
                t.ActionTimer = 0.35f;
                if (a.IsPlayer) PlayerHit = ("BLOCKED", Time);
                Sound(t.Type.HasShield ? Cue.Block : Cue.Clash, t.Pos);
                return;
            }
        }

        float speed = ranged || lance ? 0 : SpeedBonus(a, t);
        // Total War high ground: up to +-15% for fighting downhill / uphill.
        float high = ranged ? 0 : Math.Clamp((Terrain.Height(a.Pos) - Terrain.Height(t.Pos)) * 0.05f, -0.15f, 0.15f);
        float officer = a.Squad?.Company is Company oc ? (oc.Has("Hothead") ? 1.1f : 1) * (oc.Has("Veteran") ? 1.05f : 1) : 1;
        float mult = officer * (1 + speed) * (1 + high) * (side == Side.Rear ? 1.5f : side == Side.Flank ? 1.2f : 1) * (t.Mounted ? a.Type.VsMounted : 1);
        if (side != Side.Front && t.Squad != null) t.Squad.Pressure += side == Side.Rear ? 1 : 0.5f;
        float real = Damage(a, t, dmg * mult, side == Side.Rear ? 4 : side == Side.Flank ? 1.5f : 0, ranged ? a.Type.Pierce : 0);
        Sound(ranged ? Cue.ArrowHit : t.Type.Armor >= 8 ? Cue.Clash : Cue.Hit, t.Pos);
        if (a.IsPlayer)
        {
            var text = $"{real:0} DMG";
            if (lance) text = "COUCHED LANCE " + text;
            if (speed >= 0.1f) text += $"  +{speed * 100:0}% SPEED";
            if (side != Side.Front) text += side == Side.Rear ? "  REAR" : "  FLANK";
            if (high >= 0.05f) text += "  HIGH GROUND";
            PlayerHit = (text, Time);
        }
    }

    /// Armour, then hp, morale shock and interrupts. Returns the damage dealt.
    float Damage(Unit a, Unit t, float dmg, float extraShock, float pierce = 0)
    {
        float real = MathF.Max(dmg * 0.25f, dmg - t.Type.Armor * (1 - pierce)); // bolts punch through part of the armour
        t.Hp -= real;
        t.HitFlash = 0.15f;
        t.Morale -= real * 0.25f + extraShock;
        if (t.Action == UnitAction.Windup || t.Charging) Stagger(t, 0.3f); // hits interrupt swings
        if (t.Hp <= 0) Kill(t, a);
        return real;
    }

    internal void HitHorse(Unit r, float dmg, Unit by)
    {
        r.HorseHp -= dmg;
        r.HitFlash = 0.15f;
        if (by.IsPlayer) PlayerHit = ($"HORSE HIT {dmg:0}", Time);
        if (r.HorseHp > 0) return;
        // The horse goes down and throws its rider.
        DeadHorses.Add(new DeadHorse(r.Pos, r.Heading, r.Team, r.Coat, r.Horse!.Barding));
        r.Horse = null;
        r.Speed = 0;
        r.Vel = Vector2.Zero;
        r.Couched = false;
        r.Morale -= 10;
        if (r.IsPlayer) Log("Your horse is slain! Find a loose one to mount.");
        Stagger(r, 1.2f);
        Damage(by, r, 12, 0);
    }

    static void Stagger(Unit u, float time)
    {
        u.Action = UnitAction.Stagger;
        u.ActionTimer = time;
        u.Charging = false;
        u.Charge = 0;
        u.Couched = false;
    }

    void Kill(Unit t, Unit killer)
    {
        t.Dead = true;
        t.Hp = 0;
        t.Action = UnitAction.None;
        t.Vel = t.Wish = Vector2.Zero;
        if (t.Mounted)
        {
            LooseHorses.Add(new LooseHorse { Mount = t.Horse!, Hp = t.HorseHp, Pos = t.Pos, Heading = t.Heading, Team = t.Team, Coat = t.Coat });
            t.Horse = null;
            t.Pos -= new Vector2(-t.HeadingDir.Y, t.HeadingDir.X) * 1.0f; // the rider falls off to the side
        }
        if (killer.IsPlayer) PlayerKills++;
        if (killer.Squad != null && killer.Team != t.Team) killer.Squad.KillsByTier[Math.Clamp(t.Type.Tier, 0, 4)]++;
        if (killer.Squad != null && killer.Team != t.Team) killer.Squad.Kills++;
        foreach (var u in Units)
        {
            if (!u.Active) continue;
            float d = Vector2.Distance(u.Pos, t.Pos);
            if (u.Team == t.Team) { if (t.IsLeader) u.Morale -= 25; else if (d < 10) u.Morale -= 5; }
            else if (d < 10) u.Morale += killer.IsPlayer ? 6 : 2; // heroics inspire more
        }
        if (t.IsPlayer) Log("You have fallen! Your troops waver.");
        else if (t.IsLeader) Log("The enemy captain is slain!");
    }

    void Shoot(Unit u, Unit t)
    {
        u.Ammo--;
        Sound(Cue.Bow, u.Pos);
        u.Cooldown = (u.Type.Windup + u.Type.Recover + 0.8f + rng.NextSingle() * 0.8f) * u.Type.Reload; // a crossbow takes long to span
        u.Action = UnitAction.Recover; // shows the release pose
        u.ActionTimer = 0.25f;
        float d = Vector2.Distance(u.Pos, t.Pos);
        var aim = t.Pos + t.Vel * (d / ArrowSpeed); // lead the target
        aim += new Vector2(Gauss(), Gauss()) * d * 0.035f * (u.Squad?.Company?.Has("Sharpshooter") == true ? 0.65f : 1) * (u.Mounted ? 1.5f : 1) * Weather.Spread;
        float time = MathF.Max(0.1f, Vector2.Distance(u.Pos, aim) / ArrowSpeed);
        var h = (aim - u.Pos) / time;
        float from = Terrain.Height(u.Pos) + 1.5f, land = Terrain.Height(aim) + (t.Mounted ? 1.5f : 1.0f);
        // Launch 1.5 m above the archer's ground, land at chest height on the target's ground after `time` seconds.
        Arrows.Add(new Projectile
        {
            Pos = new(u.Pos.X, from, u.Pos.Y),
            Vel = new(h.X, 0.5f * Gravity * time + (land - from) / time, h.Y),
            Shooter = u,
            Damage = u.Type.Damage,
        });
    }

    float Gauss() => MathF.Sqrt(-2 * MathF.Log(1 - rng.NextSingle())) * MathF.Cos(MathF.Tau * rng.NextSingle());

    void UpdateArrows(float dt)
    {
        for (int i = Arrows.Count - 1; i >= 0; i--)
        {
            var p = Arrows[i];
            if (p.Stuck >= 0)
            {
                if ((p.Stuck += dt) > 10) Arrows.RemoveAt(i);
                continue;
            }
            p.Vel.Y -= Gravity * dt;
            p.Pos += p.Vel * dt;
            var flat = new Vector2(p.Pos.X, p.Pos.Z);
            float ground = Terrain.Height(flat);
            if (p.Pos.Y <= ground) { p.Pos.Y = ground; p.Stuck = 0; continue; }
            if (p.Pos.Y > ground + 2.6f) continue;
            foreach (var t in Units)
            {
                if (t.Team == p.Shooter.Team || !t.Active) continue;
                float r = t.Mounted ? 0.8f : 0.4f;
                if ((!t.Mounted && p.Pos.Y > ground + 2) || Vector2.DistanceSquared(t.Pos, flat) > r * r) continue;
                Hit(p.Shooter, t, p.Damage, true);
                Arrows.RemoveAt(i);
                break;
            }
        }
    }

    void Move(float dt)
    {
        var before = Wall != null ? Units.Select(u => u.Pos).ToArray() : Array.Empty<Vector2>();
        foreach (var u in Units)
        {
            if (!u.Active) continue;
            if (u.Mounted)
            {
                if (!u.IsPlayer) Steer(u, dt);
                u.Vel = u.HeadingDir * u.Speed;
            }
            else u.Vel = u.Wish;
            u.Pos += u.Vel * SlopeFactor(Terrain, u.Pos, u.Vel) * dt;
            u.Anim += dt * u.Vel.Length() * (u.Mounted ? 0.45f : 0.9f); // stride-locked walk / gallop cycle
        }
        // shortcut: O(n^2) separation, fine for ~150 units; switch to a spatial grid past ~400.
        // Horses are big and heavy: they shove footmen aside, and ride into them (Impact) at speed.
        for (int i = 0; i < Units.Count; i++)
        {
            var a = Units[i];
            if (!a.Active) continue;
            float ra = a.Mounted ? 1.0f : 0.375f, ma = a.Mounted ? 6 : 1;
            for (int j = i + 1; j < Units.Count; j++)
            {
                var b = Units[j];
                if (!b.Active) continue;
                float rb = b.Mounted ? 1.0f : 0.375f, r = ra + rb;
                var d = b.Pos - a.Pos;
                float l2 = d.LengthSquared();
                if (l2 >= r * r) continue;
                if (l2 < 1e-6f) { d = new Vector2(0.01f, 0); l2 = 1e-4f; }
                float l = MathF.Sqrt(l2), mb = b.Mounted ? 6 : 1;
                var n = d / l;
                var push = n * (r - l);
                a.Pos -= push * (mb / (ma + mb));
                b.Pos += push * (ma / (ma + mb));
                if (a.Team == b.Team) continue;
                if (a.Mounted && !b.Mounted) Impact(a, b, n, dt);
                else if (b.Mounted && !a.Mounted) Impact(b, a, -n, dt);
                else if (a.Mounted) { a.Speed *= 1 - 1.5f * dt; b.Speed *= 1 - 1.5f * dt; }
            }
        }
        // Trees and boulders are solid; a horse that rides into one pulls up hard.
        foreach (var u in Units)
            if (u.Active && Terrain.PushOut(ref u.Pos, u.Mounted ? 0.9f : 0.35f) && u.Mounted && u.Speed > 3) u.Speed *= 0.6f;
        EnforceWall(before);
    }

    /// Uphill is slow, downhill a little quicker: a multiplier on ground speed.
    internal static float SlopeFactor(Terrain t, Vector2 pos, Vector2 vel)
    {
        float slope = vel.LengthSquared() > 0.01f ? Vector2.Dot(t.Gradient(pos), Vector2.Normalize(vel)) : 0;
        return Math.Clamp(1 - slope * 1.5f, 0.55f, 1.15f);
    }

    /// A horse at speed running into an enemy footman: knockdown and damage, unless the man is braced.
    void Impact(Unit r, Unit f, Vector2 n, float dt)
    {
        r.Speed *= 1 - 1.2f * dt; // pushing through bodies bogs a charge down
        float closing = Vector2.Dot(r.Vel - f.Vel, n);
        if (f.ImpactCd > 0 || closing < 4) return;
        f.ImpactCd = 0.8f;
        Sound(Cue.Impact, f.Pos);
        r.Contact = true;
        if (Braced(f) && Vector2.Dot(-n, f.Dir) > 0.4f)
        {
            r.Speed *= 0.35f; // the horse balks at the wall
            HitHorse(r, closing * 2, f);
            return;
        }
        r.Speed *= f.Type.Class == UnitClass.Infantry && f.Squad is { Loose: false } ? 0.7f : 0.85f;
        float real = Damage(r, f, closing * 3.2f, 6);
        if (f.Active)
        {
            Stagger(f, 1.1f); // knocked flat
            f.Pos += n * 0.6f;
        }
        if (r.IsPlayer) PlayerHit = ($"TRAMPLE {real:0}", Time);
        foreach (var u in Units)
            if (u.Active && u.Team == f.Team && Vector2.DistanceSquared(u.Pos, f.Pos) < 25) u.Morale -= 2; // the shock spreads
    }

    /// Braced spears catch a horse that rides onto their points (their reach beats a couched lance).
    void Braces()
    {
        foreach (var b in Units)
        {
            if (!b.Active || b.BraceCd > 0 || !Braced(b)) continue;
            float reach = b.Type.Reach + BraceExtra;
            foreach (var u in Units)
            {
                if (!u.Active || !u.Mounted || u.Team == b.Team) continue;
                var to = u.Pos - b.Pos;
                float d = to.Length();
                if (d > reach || d < 0.01f || Vector2.Dot(to / d, b.Dir) < 0.5f) continue;
                float closing = Vector2.Dot(u.Vel - b.Vel, -to / d);
                if (closing < 5) continue;
                b.BraceCd = 1.5f;
                b.Action = UnitAction.Recover; // shows the thrust
                b.Swing = AttackDir.Thrust;
                b.ActionTimer = b.Type.Recover;
                HitHorse(u, closing * (b.Type.Class == UnitClass.Skirmisher ? 7 : 3.5f), b);
                if (u.Mounted) { u.Speed *= 0.3f; Stagger(u, 0.5f); }
                break;
            }
        }
    }

    /// Couched lances: the point is a segment ahead of the horse; damage comes from the closing speed alone.
    void Lances()
    {
        foreach (var u in Units)
        {
            if (!u.Active || !u.Couched || !u.Mounted) continue;
            var hd = u.HeadingDir;
            Vector2 a = u.Pos + hd * LanceFrom, b = u.Pos + hd * LanceTip;
            foreach (var t in Units)
            {
                if (!t.Active || t.Team == u.Team) continue;
                float r = t.Mounted ? 0.9f : 0.35f;
                if (DistToSegment(t.Pos, a, b) > r) continue;
                float closing = Vector2.Dot(u.Vel - t.Vel, hd);
                if (closing < 4) continue;
                u.Swing = AttackDir.Thrust;
                Hit(u, t, 14 + closing * 6, false, lance: true);
                u.Couched = false;
                u.Cooldown = 1.2f;
                u.Contact = true;
                break;
            }
        }
    }

    static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float k = Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0, 1);
        return Vector2.Distance(p, a + ab * k);
    }

    void UpdateMorale(float dt)
    {
        var lost = new float[2];
        for (int t = 0; t < 2; t++)
        {
            int all = 0, gone = 0;
            foreach (var u in Units) if (u.Team == t) { all++; if (!u.Active) gone++; }
            lost[t] = all == 0 ? 0 : gone / (float)all;
        }
        foreach (var u in Units)
        {
            if (!u.Active || u.IsLeader) continue; // leaders never break
            var lead = u.Team == 0 ? Player : EnemyCaptain;
            bool inspired = lead.Active && !(lead.IsPlayer && Commanding)
                && Vector2.DistanceSquared(lead.Pos, u.Pos) < Options.AuraRadius * Options.AuraRadius;
            float m = 0;
            if (inspired) m += u.Routing ? 8 : 3;
            if (u.Squad!.Alive < u.Squad.StartCount * 0.4f) m -= 1.5f;
            if (u.EnemyDist > 12 && u.Morale < 60) m += 1;
            m -= MathF.Min(4, u.Squad.Pressure * 0.8f); // attacked in the flank or rear
            if (lost[u.Team] > 0.5f) m -= 0.5f;          // the army is losing
            // Neighbours running away, and horsemen thundering at you, are frightening.
            int routing = 0;
            bool charged = false;
            foreach (var o in Units)
            {
                if (!o.Active || o == u) continue;
                float d2 = Vector2.DistanceSquared(o.Pos, u.Pos);
                if (o.Team == u.Team) { if (o.Routing && d2 < 64) routing++; }
                else if (!u.Mounted && o.Mounted && o.Speed > 7 && d2 < 100 && !Braced(u)
                    && Vector2.Dot(o.HeadingDir, Vector2.Normalize(u.Pos - o.Pos)) > 0.8f) charged = true;
            }
            m -= MathF.Min(3, routing * 0.5f);
            if (charged) m -= 4;
            u.Morale = Math.Clamp(u.Morale + m * dt, 0, 100);
            // Iron Will: near you, a company that still has a third of its men will not break.
            if (Options.IronWill && u.Team == 0 && inspired && u.Squad.Alive >= u.Squad.StartCount * 0.3f) u.Morale = MathF.Max(u.Morale, 16);
            float routAt = u.Squad.Company?.Has("Stubborn") == true || (Wall != null && u.Team == 1 && !Wall.GateOpen) ? 10 : 15; // officer trait; walls
            if (!u.Routing && u.Morale < routAt) { u.Routing = true; u.Action = UnitAction.None; u.Couched = false; }
            else if (u.Routing && u.Morale > 45) u.Routing = false;
        }
    }

    /// An AI commander for one side (always the enemy; your side too when Options.AutoPlayer): advance as a block,
    /// charge when close, archers stop at bow range, horse waits wide on the far flank and then hunts the softest companies.
    /// The companies a side's AI general leads: all of the enemy's; of yours, only allies' (or all, in headless runs).
    List<Squad> Led(int team, string role) => Role(team, role).Where(s => team == 1 || Options.AutoPlayer || s.AiControlled).ToList();
    public IEnumerable<Squad> Commanded => SquadsOf(0).Where(s => !s.AiControlled);

    void General(int team)
    {
        if (Wall != null) { if (team == 1) SiegeDefend(); else SiegeAttack(); return; }
        int foe = 1 - team;
        var inf = Led(team, "infantry").Where(s => s.Alive > 0).ToList();
        var front = inf.Count > 0 ? inf.Aggregate(Vector2.Zero, (a, s) => a + s.Centroid()) / inf.Count : team == 1 ? eGuard.Centroid() : centroid[team];
        var dir = centroid[foe] - front;
        if (dir.LengthSquared() < 1) return;
        dir.Normalize();
        var side = new Vector2(-dir.Y, dir.X);
        var spears = Led(team, "spears");
        if (!charged[team])
        {
            foreach (var s in inf) { s.Front = dir; s.Anchor = front + dir * 6 + side * lateral.GetValueOrDefault(s); }
            foreach (var s in spears) { s.Front = dir; s.Anchor = front + dir * 6 + side * (16 + lateral.GetValueOrDefault(s)); } // swing wide on a flank
            if (inf.Any(s => GapTo(s, foe) < 16) || spears.Any(s => s.Alive > 0 && GapTo(s, foe) < 10))
            {
                charged[team] = true;
                foreach (var s in inf.Concat(spears)) s.Order = Order.Charge;
                Log(team == 1 ? "The enemy line charges!" : "Your line charges!");
                Sound(Cue.Shout, front);
            }
        }
        else foreach (var s in inf.Concat(spears)) s.Order = Order.Charge; // late arrivals join the charge
        foreach (var s in Led(team, "archers"))
        {
            if (s.Order == Order.Charge || s.Alive == 0) continue;
            s.Front = dir;
            float gap = GapTo(s, foe), range = s.Units[0].Type.Range;
            if (gap > range - 4) s.Anchor = s.Centroid() + dir * 4;
            if (gap < 7 || s.Units.All(u => !u.Active || u.Ammo == 0)) s.Order = Order.Charge;
        }
        var horse = Led(team, "horse");
        if (!horseCharged[team])
        {
            foreach (var s in horse) { s.Front = dir; s.Anchor = front + dir * 4 - side * (26 - lateral.GetValueOrDefault(s)); } // the other flank
            if (charged[team] || Time > 35 || horse.Any(s => s.Alive > 0 && GapTo(s, foe) < 20))
            {
                horseCharged[team] = true;
                foreach (var s in horse) s.Order = Order.Charge;
                if (horse.Count > 0) Log(team == 1 ? "Enemy horse on the flank!" : "Your horse sweeps the flank!");
            }
        }
        else foreach (var s in horse) s.Order = Order.Charge;
        if (horseCharged[team])
            foreach (var s in horse.Where(s => s.Target == null))
                s.Target = new[] { "archers", "spears", "infantry", "horse" }.SelectMany(r => Role(foe, r)).FirstOrDefault(t => t.Alive > 0); // soft targets first
        if (team == 1)
        {
            eGuard.Front = dir;
            eGuard.Anchor = front - dir * 4; // captain leads from just behind the line
        }
    }

    /// Reserves march on when the side's strength on the field drops: from behind its own lines.
    void Reinforce()
    {
        for (int team = 0; team < 2; team++)
        {
            if (reserves[team].Count == 0) continue;
            int onField = Units.Count(u => u.Team == team && u.Active && !u.Routing && !u.IsLeader);
            int started = Squads.Where(s => s.Team == team && s.Company != null).Sum(s => s.StartCount);
            if (onField > started * 0.6f && Time < 600) continue;
            var (c, role) = reserves[team].Dequeue();
            float f = team == 0 ? 1 : -1;
            var t = Troops.Get(c.Troop);
            var s = AddSquad(team, t, c.Count, new Vector2(lateral.Count % 3 * 12 - 12, -48) * f, (c.Count + 1) / 2, new Vector2(0, f), c);
            s.Anchor = new Vector2(0, -30) * f;
            Role(team, role).Add(s);
            lateral[s] = 0;
            if (team == 1 && charged[1] || team == 0 && Options.AutoPlayer) s.Order = Order.Charge;
            Log(team == 0 ? $"Reinforcements: {t.Name} join the fight!" : $"Enemy reinforcements: {t.Name}!");
        }
    }

    /// What happened, company by company, for the campaign: who stands, who fled, who fell, and whom they killed.
    public BattleResult Result()
    {
        var list = new List<CompanyResult>();
        foreach (var s in Squads.Where(s => s.Company != null))
            list.Add(new CompanyResult(s.Team, s.Company!.Id, s.StartCount,
                Standing: s.Units.Count(u => u.Active && !u.Routing),
                Fled: s.Units.Count(u => u.Fled || (u.Active && u.Routing)),
                Fallen: s.Units.Count(u => u.Dead),
                Kills: s.KillsByTier.Sum(), KillsByTier: (int[])s.KillsByTier.Clone(), Deployed: true));
        for (int team = 0; team < 2; team++)
            foreach (var (c, _) in reserves[team])
                list.Add(new CompanyResult(team, c.Id, c.Count, c.Count, 0, 0, 0, new int[5], Deployed: false));
        var captain = Player.Dead || Options.PlayerWounded ? (Winner == 1 ? LeaderFate.Captured : LeaderFate.Wounded) : LeaderFate.Unhurt;
        var foeLeader = EnemyCaptain.Dead ? LeaderFate.Slain : LeaderFate.Unhurt;
        int horses = LooseHorses.Count(h => h.Team == 1);
        return new BattleResult(Winner, Time, list, captain, foeLeader, Winner == 0 ? horses : 0);
    }

    float GapTo(Squad s, int team)
    {
        float best = float.MaxValue;
        foreach (var a in s.Units)
        {
            if (!a.Active) continue;
            foreach (var b in Units)
                if (b.Team == team && b.Active) best = MathF.Min(best, Vector2.DistanceSquared(a.Pos, b.Pos));
        }
        return MathF.Sqrt(best);
    }

    /// Give up the field: the battle ends as a defeat, and your men on the field count as fled (they live).
    public void Withdraw()
    {
        if (Winner >= 0) return;
        Winner = 1;
        foreach (var u in Units) if (u.Team == 0 && u.Active && !u.IsPlayer) u.Routing = true;
        Log("You sound the retreat.");
    }

    void CheckEnd()
    {
        if (Winner >= 0) return;
        for (int t = 0; t < 2; t++)
        {
            if (Units.Any(u => u.Team == t && u.Active && !u.Routing) || reserves[t].Count > 0 && Units.Any(u => u.Team == t && u.Active && u.Routing)) continue;
            Winner = 1 - t;
            Log(Winner == 0 ? "Victory! The field is yours." : "Defeat. Your army is broken.");
            return;
        }
    }
}
