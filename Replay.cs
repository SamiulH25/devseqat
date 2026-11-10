using System.Text.Json;
using Microsoft.Xna.Framework;

namespace Ironfield;

/// Roadmap E5: a battle's seed, armies and options plus every tick's input reproduce it exactly.
/// The screen records each battle and keeps the last one; `--replay file` plays it back headless and checks the outcome.
public sealed class Replay
{
    public const int CurrentVersion = 2;
    public int Version = CurrentVersion, Seed;
    /// The battlefield generation it was recorded on (`Terrain.Version`); 0 in replays from before it was kept.
    public int TerrainVersion;
    public ArmySpec Player = null!, Enemy = null!;
    public BattleOptions Options = new();
    /// Where every man stood when the fighting began (after deployment).
    public List<Vector2> Start = new();
    public List<float> StartFacing = new();
    public List<Frame> Frames = new();
    /// The outcome when recorded (to check a playback against).
    public int Winner = -1, Dead;
    public float Time;

    public sealed class Frame
    {
        public float Dt;
        public PlayerInput Input;
        public bool Commanding, Withdraw;
        public List<SquadOrder>? Orders;
        public List<GoalOrder>? Goals;
        /// Companies (by index) that planted stakes this tick.
        public List<int>? Stakes;
        /// Companies folded into others this tick (from, into; Battle.Merge).
        public List<int[]>? Merges;
    }

    /// A goal you sent one of your divisions (it then rides to him inside the sim, so it replays exactly).
    public sealed class GoalOrder
    {
        public int Division, Target = -1, Side = 1;
        public GoalKind Kind;
        public Vector2 Point;
    }

    /// One of your companies' orders, recorded when it changes.
    public sealed class SquadOrder
    {
        public int Squad, Target = -1, Columns;
        public Order Order;
        public Vector2 Anchor, Front;
        public Formation Formation;
        public bool FireAtWill, Override, Pursue, HoldSpells;
        public int CastSpell = -1;
        public Vector2 CastAt;
        public float CastSince;
    }

    static readonly JsonSerializerOptions Json = new() { IncludeFields = true };

    public string Serialize() => JsonSerializer.Serialize(this, Json);
    public static Replay Deserialize(string json) => JsonSerializer.Deserialize<Replay>(json, Json) ?? throw new InvalidDataException("empty replay");

    public static string Folder => Path.Combine(Settings.Folder, "replays");

    public void Save(string name)
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, name + ".json"), Serialize());
    }

    /// Play it back from the start; returns the battle as it ended.
    public Battle Play()
    {
        var b = new Battle(Seed, Player, Enemy, Options);
        for (int i = 0; i < Start.Count && i < b.Units.Count; i++)
        {
            b.Units[i].Pos = Start[i];
            b.Units[i].Facing = b.Units[i].Heading = StartFacing[i];
        }
        foreach (var f in Frames)
        {
            if (f.Orders != null)
                foreach (var o in f.Orders)
                {
                    var s = b.Squads[o.Squad];
                    s.Order = o.Order; s.Anchor = o.Anchor; s.Front = o.Front; s.Columns = o.Columns; s.Formation = o.Formation; s.FireAtWill = o.FireAtWill; s.Override = o.Override; s.Pursue = o.Pursue;
                    s.HoldSpells = o.HoldSpells; s.CastSpell = o.CastSpell; s.CastAt = o.CastAt; s.CastSince = o.CastSince;
                    s.Target = o.Target >= 0 ? b.Squads[o.Target] : null;
                }
            if (f.Goals != null)
                foreach (var g in f.Goals)
                    b.IssueGoal(b.Divisions[g.Division], new Goal(g.Kind, g.Point, g.Target >= 0 ? b.Squads[g.Target] : null, g.Side));
            if (f.Stakes != null) foreach (int i in f.Stakes) b.PlantStakes(b.Squads[i]);
            if (f.Merges != null) foreach (var m in f.Merges) b.ReplayMerge(m[0], m[1]);
            b.Input = f.Input;
            b.Commanding = f.Commanding;
            if (f.Withdraw) b.Withdraw();
            b.Update(f.Dt);
        }
        return b;
    }

    /// Does a playback end as the recording did?
    public bool Matches(Battle b) => b.Winner == Winner && MathF.Abs(b.Time - Time) < 1e-3f && b.Units.Count(u => u.Dead) == Dead;

    /// `--replay file`: play a saved battle headless and report whether it reproduces.
    public static void Run(string path)
    {
        var r = Deserialize(File.ReadAllText(path));
        if (r.TerrainVersion != Terrain.Version)
        {
            Console.WriteLine($"replay {Path.GetFileName(path)} was recorded on older ground (version {r.TerrainVersion}, now {Terrain.Version}): it cannot be played back");
            return;
        }
        var b = r.Play();
        Console.WriteLine($"replay {Path.GetFileName(path)}: {r.Frames.Count} ticks, winner {b.Winner} after {b.Time:0.0}s, {b.Units.Count(u => u.Dead)} dead");
        Console.WriteLine(r.Matches(b) ? "replay matches the recording" : $"REPLAY DIVERGED: recorded winner {r.Winner} after {r.Time:0.0}s, {r.Dead} dead");
    }
}

public sealed partial class Battle
{
    /// When set, every Update is recorded into it (the screen sets it for each battle).
    public Replay? Recording;
    readonly Dictionary<int, string> lastOrders = new();
    bool withdrawRequested;

    public void StartRecording(int seed)
    {
        Recording = new Replay { Seed = seed, TerrainVersion = Terrain.Version, Player = Armies[0]!, Enemy = Armies[1]!, Options = Options };
    }

    void RecordFrame(float dt)
    {
        var r = Recording!;
        if (r.Frames.Count == 0)
        {
            r.Start = Units.Select(u => u.Pos).ToList();
            r.StartFacing = Units.Select(u => u.Facing).ToList();
        }
        var f = new Replay.Frame { Dt = dt, Input = Input, Commanding = Commanding, Withdraw = withdrawRequested, Goals = goalRecords, Stakes = stakeRecords, Merges = mergeRecords?.Select(m => new[] { m.From, m.Into }).ToList() };
        mergeRecords = null;
        goalRecords = null;
        stakeRecords = null;
        withdrawRequested = false;
        for (int i = 0; i < Squads.Count; i++)
        {
            var s = Squads[i];
            if (s.Team != 0 || s.AiControlled || Options.AutoPlayer) continue;
            int target = s.Target != null ? Squads.IndexOf(s.Target) : -1;
            string sig = $"{s.Order}|{target}|{s.Anchor.X:R},{s.Anchor.Y:R}|{s.Front.X:R},{s.Front.Y:R}|{s.Columns}|{s.Formation}|{s.FireAtWill}|{s.Override}|{s.Pursue}|{s.HoldSpells}|{s.CastSpell}|{s.CastAt.X:R},{s.CastAt.Y:R}|{s.CastSince:R}";
            if (lastOrders.TryGetValue(i, out var last) && last == sig) continue;
            lastOrders[i] = sig;
            (f.Orders ??= new()).Add(new Replay.SquadOrder
            {
                Squad = i, Target = target, Order = s.Order, Anchor = s.Anchor, Front = s.Front, Columns = s.Columns, Formation = s.Formation, FireAtWill = s.FireAtWill, Override = s.Override, Pursue = s.Pursue,
                HoldSpells = s.HoldSpells, CastSpell = s.CastSpell, CastAt = s.CastAt, CastSince = s.CastSince,
            });
        }
        r.Frames.Add(f);
    }

    /// Close the recording with the outcome so far.
    public Replay? FinishRecording()
    {
        if (Recording == null) return null;
        Recording.Winner = Winner;
        Recording.Time = Time;
        Recording.Dead = Units.Count(u => u.Dead);
        return Recording;
    }
}
