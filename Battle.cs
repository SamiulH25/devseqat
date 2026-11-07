using Microsoft.Xna.Framework;

namespace Ironfield;

public struct PlayerInput
{
    public Vector2 Move;      // on foot: world-space wish direction, length 0..1
    public float Throttle;    // mounted: +1 forward, -1 rein back
    public float Steer;       // mounted: +1 turn right, -1 turn left
    public float Facing;      // where the player looks (and strikes)
    public AttackDir Dir;     // attack / block direction, picked by mouse movement
    public bool Sprint, Attack, Block, Rally, Couch, Mount, Bash, Swap;
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
    public const float Gravity = 9.8f, ArrowSpeed = 28, RallyRadius = 15;
    /// How far men can run before they have left the field: 95 m for a few hundred men, wider for a big battle.
    public float MapRadius { get; private set; } = 95;
    /// Morale at which a company breaks (Stubborn officers and men behind an unbroken wall hold to a little less).
    public const float RoutAt = 12, RallyAt = 32;
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
    readonly List<Unit> nearArrow = new(32), nearKill = new(64);
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
        MapRadius = Math.Clamp(95 * MathF.Sqrt(Math.Min(total, Options.Cap) / 450f), 95, 230);
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
        // Lay out every role's lines first: how wide the centre ends up decides how far out the flanks stand.
        var layout = onField.GroupBy(Ironfield.Armies.RoleOf)
            .ToDictionary(g => g.Key, g => Rows(g.ToList(), g.Key == "infantry" ? MaxLine : MaxLine / 2));
        float Half(string role) => layout.TryGetValue(role, out var rows) ? rows.Max(r => r.Sum(x => x.w) + 4 * (r.Count - 1)) / 2 : 0;
        float Depth(string role) => layout.TryGetValue(role, out var rows) ? rows.Sum(r => r.Max(x => (x.c.Count + x.cols - 1) / x.cols * (x.t.Mount != null ? 3f : 1.3f)) + 5) - 5 : 0;
        float centre = MathF.Max(Half("infantry"), Half("archers")), infDepth = MathF.Max(0, Depth("infantry") - 2.6f);
        // Spears stand off the left of the line, horse off the right, archers behind it; a wide centre pushes them out.
        float spearsX = MathF.Max(22, centre + 6 + Half("spears")), horseX = MathF.Max(24, centre + 6 + Half("horse"));
        flankOut[team] = (spearsX - 22, horseX - 24);
        foreach (var (role, rows) in layout)
        {
            var spot = role switch
            {
                "archers" => new Vector2(0, -38 - infDepth), "spears" => new Vector2(-spearsX, -31), "horse" => new Vector2(horseX, -33), _ => new Vector2(0, -30),
            };
            float back = 0;
            foreach (var row in rows)
            {
                float total = row.Sum(x => x.w) + 4 * (row.Count - 1), at = -total / 2, deep = 0;
                foreach (var (c, t, cols, w) in row)
                {
                    float off = at + w / 2;
                    at += w + 4;
                    var pos = (spot + new Vector2(off, -back)) * f;
                    var s = AddSquad(team, t, c.Count, pos, cols, new Vector2(0, f), c);
                    Role(team, role).Add(s);
                    lateral[s] = off;
                    depth[s] = back;
                    deep = MathF.Max(deep, (c.Count + cols - 1) / cols * (t.Mount != null ? 3f : 1.3f));
                }
                back += deep + 5;
            }
        }
        rearLine[team] = MathF.Max(48, 44 + infDepth);
    }

    /// The widest a role's line may stand before its companies form a second line behind (the infantry's; flanks half).
    const float MaxLine = 110;
    /// How far each side's flanks were pushed out by a wide infantry line (the general keeps them there).
    readonly (float Spears, float Horse)[] flankOut = new (float, float)[2];
    /// How many metres behind its role's front line a company was deployed (0 = in the first line).
    readonly Dictionary<Squad, float> depth = new();
    /// How far behind the centre reserves march on from: just behind each side's deployment.
    readonly float[] rearLine = { 48, 48 };

    /// Ranks a company stands in: two for a small company, deeper for a big one, so a large army is not one thin line.
    public static int Ranks(int men) => men <= 30 ? 2 : men <= 80 ? 3 : men <= 160 ? 4 : 6;
    public static int ColumnsFor(int men) => (men + Ranks(men) - 1) / Ranks(men);

    /// Companies laid out left to right, wrapping into another line when the line would grow wider than maxWidth.
    static List<List<(Company c, UnitType t, int cols, float w)>> Rows(List<Company> list, float maxWidth)
    {
        var rows = new List<List<(Company, UnitType, int, float)>> { new() };
        float width = 0;
        foreach (var c in list)
        {
            var t = Troops.Get(c.Troop);
            int cols = ColumnsFor(c.Count);
            float w = cols * (t.Mount != null ? 3f : 1.3f);
            if (rows[^1].Count > 0 && width + 4 + w > maxWidth) { rows.Add(new()); width = 0; }
            width += (rows[^1].Count > 0 ? 4 : 0) + w;
            rows[^1].Add((c, t, cols, w));
        }
        return rows;
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
        SetupCaptain();
        Player.Facing = Player.Heading = MathHelper.PiOver2;
        eGuard = AddSquad(1, UnitType.Captain, 1, new(0, scripted ? 80 : 34), 1, -Vector2.UnitY);
        EnemyCaptain = eGuard.Units[0];
        EnemyCaptain.IsLeader = true;
        UpdateCentroids();
    }

    public IEnumerable<Squad> SquadsOf(int team) => Squads.Where(s => s.Team == team);
    public Vector2 Centroid(int team) => centroid[team];
    public void Log(string text) { Events.Add((text, Time)); if (!text.Contains(": ") || text.Contains("charge")) History.Add((text, Time)); }
    /// Every notable event of the battle, kept for the post-battle timeline (orders you give are left out).
    public readonly List<(string Text, float At)> History = new();

    public Squad AddSquad(int team, UnitType type, int n, Vector2 anchor, int cols, Vector2 front, Company? company = null)
    {
        var s = new Squad(type.Name, team) { Anchor = anchor, Columns = cols, Front = front, StartCount = n, Cavalry = type.Mount != null && !Options.Siege, Company = company,
            AiControlled = company?.Allied == true && team == 0 };
        // A mixed company spawns each stack in turn, largest first; a one-off squad is n men of `type`.
        var types = company?.Mix is { Length: > 1 } ? company.Stacks.SelectMany(x => Enumerable.Repeat(Troops.Get(x.Troop), x.Count)).Take(n).ToList() : null;
        for (int i = 0; i < n; i++)
        {
            var u = Spawn(types != null && i < types.Count ? types[i] : type, team, s, s.Slot(i, n));
            s.MarchSpeed = MathF.Min(s.MarchSpeed, u.Type.Speed);
            if (company != null) u.Morale = company.Morale;
            u.Face(front);
            u.Heading = u.Facing;
            s.Units.Add(u);
        }
        // A companion fights at the head of his company as a hero (CAMPAIGN_OVERHAUL.md 3.3): the captain's kit, the AI's hands.
        if (company is { Hero.Length: > 0 })
        {
            var h = Spawn(Troops.Get("captain"), team, s, s.Slot(0, n));
            h.Hero = true;
            h.Morale = company.Morale + 10;
            h.Face(front);
            h.Heading = h.Facing;
            s.Units.Add(h);
        }
        Squads.Add(s);
        Bounds(s);
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

    /// Set by the bench (`--bench`): called at the end of each tick stage (index into <see cref="Stages"/>; -1 = tick start).
    /// The sim itself never reads a clock, so replays stay exact.
    public Action<int>? Probe;
    public static readonly string[] Stages = { "grid", "general", "squads", "think", "move", "lances", "arrows", "morale", "end" };
    void Mark(int stage) => Probe?.Invoke(stage);

    public void Update(float dt)
    {
        Mark(-1);
        if (Recording != null) RecordFrame(dt);
        Sounds.Clear();
        Time += dt;
        tick++;
        BuildGrid();
        RallyCooldown = MathF.Max(0, RallyCooldown - dt);
        UpdateCentroids();
        Mark(0);
        if (!scripted && (generalTimer -= dt) <= 0)
        {
            generalTimer = 0.5f;
            General(1);
            if (Options.AutoPlayer || Squads.Any(q => q.AiControlled && q.Alive > 0)) General(0);
        }
        Batter(dt);
        if ((reinforceTimer -= dt) <= 0) { reinforceTimer = 1; Reinforce(); }
        Mark(1);
        UpdateCalledHorse(dt);
        Resupply(dt);
        foreach (var s in Squads) UpdateSquad(s, dt);
        Mark(2);
        foreach (var u in Units)
        {
            if (!u.Active) continue;
            u.HitFlash = MathF.Max(0, u.HitFlash - dt);
            u.Cooldown -= dt;
            u.ImpactCd -= dt;
            u.BraceCd -= dt;
            if (u.IsPlayer) UpdatePlayer(u, dt); else if (ThinkNow(u)) Think(u, dt);
            TickAction(u, dt);
            Tire(u, dt);
        }
        Mark(3);
        Move(dt);
        Mark(4);
        Braces();
        Lances();
        Mark(5);
        UpdateArrows(dt);
        Mark(6);
        UpdateMorale(dt);
        Mark(7);
        CheckEnd();
        Mark(8);
        Events.RemoveAll(e => Time - e.At > 6);
    }

    /// Each squad's bounding circle (Squad.Center, Radius), so distance questions can skip far squads.
    static void Bounds(Squad s)
    {
        var sum = Vector2.Zero;
        int n = 0;
        foreach (var u in s.Units) if (u.Active) { sum += u.Pos; n++; }
        if (n == 0) { s.Center = s.Anchor; s.Radius = 0; return; }
        s.Center = sum / n;
        float r = 0;
        foreach (var u in s.Units) if (u.Active) r = MathF.Max(r, Vector2.DistanceSquared(u.Pos, s.Center));
        s.Radius = MathF.Sqrt(r);
    }

    int tick;
    /// Men far from any enemy think every 4th tick (riders, and men within 25 m, every 2nd), spread by index so the work
    /// is even; men within 6 m of the enemy, or mid-action, think every tick. Deterministic, so replays still match.
    bool ThinkNow(Unit u) => u.EnemyDist < 6 || u.Action is not (UnitAction.None or UnitAction.Block) || u.Routing
        || (tick + u.Index) % (u.EnemyDist < 25 || u.Mounted ? 2 : 4) == 0;

    void UpdateCentroids()
    {
        foreach (var s in Squads) Bounds(s);
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
    (Unit? unit, float dist) Nearest(Unit u, Squad? only = null) => NearestFast(u, only);

    /// Spears set their points whenever they are standing (whatever the order); other foot need to be holding tight.
    static bool CanBrace(Unit b) =>
        !b.Mounted && !b.Routing && b.Squad is { } s && s.Formation != Formation.Loose
        && (b.Type.Class == UnitClass.Skirmisher || s.Formation is Formation.ShieldWall or Formation.Square
            || (b.Type.Class == UnitClass.Infantry && s.Order == Order.Hold));

    bool Braced(Unit b) => CanBrace(b) && b.Action is UnitAction.None or UnitAction.Block && b.Vel.LengthSquared() < 1 && Terrain.WaterDepth(b.Pos) < 0.5f;

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
        u.Stamina = MathF.Max(0, u.Stamina - 3);
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
            var s = AddSquad(team, t, c.Count, new Vector2(lateral.Count % 3 * 12 - 12, -rearLine[team]) * f, ColumnsFor(c.Count), new Vector2(0, f), c);
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
        {
            var men = s.Units.Where(u => !u.Hero).ToList();
            var stacks = men.GroupBy(u => u.Type.Id).Select(g => new StackResult(g.Key, g.Count(),
                Standing: g.Count(u => u.Active && !u.Routing), Fled: g.Count(u => u.Fled || (u.Active && u.Routing)), Fallen: g.Count(u => u.Dead))).ToList();
            int fled = men.Count(u => u.Fled || (u.Active && u.Routing));
            list.Add(new CompanyResult(s.Team, s.Company!.Id, s.StartCount,
                Standing: men.Count(u => u.Active && !u.Routing),
                Fled: fled,
                Fallen: men.Count(u => u.Dead),
                Kills: s.KillsByTier.Sum(), KillsByTier: (int[])s.KillsByTier.Clone(), Deployed: true, Stacks: stacks, HorseKills: s.HorseKills,
                Routed: fled * 2 > s.StartCount || s.RoutSounded, HeroFell: s.Units.Any(u => u.Hero && u.Dead)));
        }
        for (int team = 0; team < 2; team++)
            foreach (var (c, _) in reserves[team])
                list.Add(new CompanyResult(team, c.Id, c.Count, c.Count, 0, 0, 0, new int[8], Deployed: false,
                    Stacks: c.Stacks.Select(x => new StackResult(x.Troop, x.Count, x.Count, 0, 0)).ToList()));
        var captain = Player.Dead || Options.PlayerWounded ? (Winner == 1 ? LeaderFate.Captured : LeaderFate.Wounded) : LeaderFate.Unhurt;
        var foeLeader = EnemyCaptain.Dead ? LeaderFate.Slain : LeaderFate.Unhurt;
        int horses = LooseHorses.Count(h => h.Team == 1);
        return new BattleResult(Winner, Time, list, captain, foeLeader, Winner == 0 ? horses : 0, PlayerKills);
    }

    /// The gap between squad s and the nearest active man of `team`. Exact, but squads whose bounding circles
    /// are already further than the best gap found are skipped (this runs for every company, twice a second).
    float GapTo(Squad s, int team)
    {
        float best = float.MaxValue;
        void Pair(Unit b) { foreach (var a in s.Units) if (a.Active) best = MathF.Min(best, Vector2.DistanceSquared(a.Pos, b.Pos)); }
        foreach (var e in Squads.Where(e => e.Team == team && e.Units.Any(u => u.Active)).OrderBy(e => Vector2.Distance(e.Center, s.Center) - e.Radius))
        {
            float lb = Vector2.Distance(e.Center, s.Center) - e.Radius - s.Radius;
            if (lb > 0 && lb * lb >= best) break;
            foreach (var b in e.Units) if (b.Active) Pair(b);
        }
        if (Player.Team == team && Player.Active) Pair(Player);
        return MathF.Sqrt(best);
    }

    /// Give up the field: the battle ends as a defeat, and your men on the field count as fled (they live).
    public void Withdraw()
    {
        if (Winner >= 0) return;
        if (Recording != null) withdrawRequested = true;
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
