using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;

namespace Ironfield;

/// Ambient life on the campaign map (docs/WORLD_LIFE.md WL4): flocks of small birds wandering over the land, crows
/// circling grave fields and old battlefields, a hawk wheeling alone; sheep and cattle in the pastures, deer at the
/// edges of the woods, rabbits in the fields. Beasts scatter from any party that comes near. Drawing only: its own
/// dice, nothing read back by the campaign. Spawned round the camera, thinned by zoom and quality, rebuilt as one vertex
/// list each frame and drawn with the foliage (`World.Extra`), so it is lit, shadowed and fogged like the land.
sealed partial class MapLife
{
    enum Beast { Sheep, Cow, Deer, Rabbit }

    sealed class Animal
    {
        public Vector2 Pos, Goal;
        public float Heading, Phase, Timer, Speed;
        public bool Grazing, Fleeing, Gone, Stag;
        public Color Coat;
    }

    sealed class Herd
    {
        public Beast Kind;
        public Vector2 Home;
        public float Radius;
        public FieldPlot? Plot;
        public readonly List<Animal> Members = new();
    }

    sealed class Flock
    {
        public Vector2 Pos, Dir, Circle;
        public float Alt, Speed, Size, Angle, Radius, Wander;
        public bool Circling, Glides;
        public float Climb; // a flock flushed from a wood: how fast it climbs (WL6)
        public Color Col;
        public Vector3[] Offsets = Array.Empty<Vector3>();
        public float[] Phases = Array.Empty<float>();
    }

    /// How much bigger than life the map draws its creatures (the parties stand about two and a half times life size).
    const float Big = 2.0f;
    /// Birds are drawn bigger still: at life size they would be specks at any height the map is seen from.
    const float BirdBig = 3.6f;

    readonly Terrain t;
    readonly Random rng;
    readonly List<(Vector2 Pos, Beast Kind, FieldPlot? Plot, int Key)> homes = new();
    readonly Dictionary<int, Herd> herds = new();
    readonly List<Flock> flocks = new();
    readonly Dictionary<Vector2, Flock> crows = new();
    Flock? hawk;
    readonly World.Mesh mesh = new();
    VertexPNCT[] buf = new VertexPNCT[3];
    readonly Random woolRng = new(5);
    float clock, spawnWait;

    /// On a battlefield (WL6): herds grazing out at the field's edge, birds roosting in the woods until the armies come
    /// near, crows over the dead; no folk or traffic, and birds at nearer life size.
    readonly bool battle;

    public MapLife(Terrain terrain, int seed, bool battle = false)
    {
        t = terrain;
        this.battle = battle;
        rng = new Random(seed * 101 + 17);
        if (battle) { BattleHomes(); WoodEdges(); FindRoosts(); return; }
        foreach (var f in t.Fields)
        {
            if (f.Kind == FieldKind.Pasture) homes.Add((f.Center, f.Seed % 3 == 0 ? Beast.Cow : Beast.Sheep, f, f.Seed));
            else if (f.Kind is FieldKind.Grain or FieldKind.Green or FieldKind.Fallow && f.Seed % 3 == 0) homes.Add((f.Center, Beast.Rabbit, f, f.Seed));
        }
        WoodEdges();
        FindPlaces();
    }

    /// Deer homes: open ground beside a wood (an empty 8 m cell next to a cell of trees), not on roads or water, one in nine.
    void WoodEdges()
    {
        var count = new Dictionary<(int, int), int>();
        foreach (var f in t.Foliage)
            if (f.Kind is FoliageKind.Broadleaf or FoliageKind.Pine or FoliageKind.Birch)
            {
                var k = ((int)MathF.Floor(f.Pos.X / 8), (int)MathF.Floor(f.Pos.Y / 8));
                count[k] = count.GetValueOrDefault(k) + 1;
            }
        var seen = new HashSet<(int, int)>();
        foreach (var ((x, z), n) in count)
        {
            if (n < 2) continue;
            foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var c = (x + dx, z + dz);
                if (count.ContainsKey(c) || !seen.Add(c) || Hash(c.Item1, c.Item2) % 9 != 0) continue;
                var p = new Vector2(c.Item1 * 8 + 4, c.Item2 * 8 + 4);
                if (t.RoadDist(p) < 5 || t.WaterDepth(p) > 0 || t.Lakes.Any(l => l.Inside(p, 3)) || t.Fields.Any(f => f.Inside(p, 1))) continue;
                homes.Add((p, Beast.Deer, null, Hash(c.Item1, c.Item2)));
            }
        }
    }

    static int Hash(int x, int z) => Math.Abs(x * 73856093 ^ z * 19349663);

    /// Moves everything on: spawns herds and birds near the camera's focus and lets go of those left behind.
    /// `parties` are the positions of the parties on the map (beasts flee them); `carrion` the grave fields and old
    /// battlefields (crows circle them); `hour` the clock (birds roost at night).
    public void Update(float dt, Vector2 focus, float zoom, IReadOnlyList<Vector2> parties, IEnumerable<Vector2> carrion, float hour, Quality quality)
    {
        clock += dt;
        if (quality == Quality.Low) { herds.Clear(); flocks.Clear(); crows.Clear(); hawk = null; crowds.Clear(); travellers.Clear(); boats.Clear(); smoking.Clear(); return; }
        bool high = quality == Quality.High;
        float reach = battle ? 420 : Math.Clamp(zoom * 0.9f, 50, 260);
        var near = parties.Where(p => Vector2.DistanceSquared(p, focus) < (reach + 40) * (reach + 40)).ToList();
        UpdateHerds(dt, focus, zoom, reach, near, high ? 40 : 20);
        bool day = hour > 5.5f && hour < 20.5f;
        UpdateBirds(dt, focus, battle ? 100 : zoom, reach, day, carrion, battle ? 2 : high ? 5 : 3);
        if (battle) UpdateRoosts(dt, focus, reach, parties);
        else UpdatePeople(dt, focus, zoom, reach, hour, high);
    }

    void UpdateHerds(float dt, Vector2 focus, float zoom, float reach, List<Vector2> parties, int cap)
    {
        if (zoom > 300) { herds.Clear(); return; }
        spawnWait -= dt;
        if (spawnWait <= 0 || herds.Count == 0) // the search for new homes in reach runs twice a second, not every frame
        {
            spawnWait = 0.5f;
            foreach (var key in herds.Keys.Where(k => Vector2.Distance(herds[k].Home, focus) > reach * 1.3f).ToList()) herds.Remove(key);
            Populate(focus, zoom, reach, cap);
        }
        foreach (var h in herds.Values)
            foreach (var a in h.Members) if (!a.Gone) Step(h, a, dt, parties);
    }

    void Populate(Vector2 focus, float zoom, float reach, int cap)
    {
        foreach (var (pos, kind, plot, key) in homes.Where(h => Vector2.DistanceSquared(h.Pos, focus) < reach * reach).OrderBy(h => Vector2.DistanceSquared(h.Pos, focus)))
        {
            if (herds.Count >= cap || Vector2.Distance(pos, focus) > reach) break;
            if (herds.ContainsKey(key)) continue;
            if (kind == Beast.Rabbit && (zoom > 140 || t.MapSeason == 3)) continue;
            herds[key] = Spawn(pos, kind, plot, key);
        }
    }

    Herd Spawn(Vector2 home, Beast kind, FieldPlot? plot, int key)
    {
        var r = new Random(key);
        var h = new Herd { Kind = kind, Home = home, Plot = plot, Radius = plot is { } f ? MathF.Min(f.Half.X, f.Half.Y) * 0.8f : 9 };
        int n = kind switch { Beast.Sheep => 6 + r.Next(5), Beast.Cow => 3 + r.Next(3), Beast.Deer => 3 + r.Next(4), _ => 2 + r.Next(2) };
        for (int i = 0; i < n; i++)
        {
            var p = Within(h, r);
            h.Members.Add(new Animal
            {
                Pos = p, Goal = p, Heading = r.NextSingle() * MathF.Tau, Phase = r.NextSingle() * 10, Timer = r.NextSingle() * 4, Grazing = r.Next(2) == 0,
                Stag = kind == Beast.Deer && i == 0 && r.Next(2) == 0,
                Coat = kind switch
                {
                    Beast.Sheep => r.Next(9) == 0 ? new Color(58, 52, 48) : new Color(226, 220, 204),
                    Beast.Cow => r.Next(3) switch { 0 => new Color(122, 80, 50), 1 => new Color(54, 48, 44), _ => new Color(196, 168, 120) },
                    Beast.Deer => new Color(128, 88, 54),
                    _ => new Color(130, 114, 92),
                },
            });
        }
        return h;
    }

    /// A random spot in a herd's ground: inside its pasture or field, or round its home at the wood's edge.
    static Vector2 Within(Herd h, Random r) => h.Plot is { } f
        ? f.At((r.NextSingle() * 2 - 1) * f.Half.X * 0.85f, (r.NextSingle() * 2 - 1) * f.Half.Y * 0.8f)
        : h.Home + new Vector2(r.NextSingle() * 2 - 1, r.NextSingle() * 2 - 1) * h.Radius;

    /// One beast's moment: run from a party close by, else graze a while and amble to another spot.
    void Step(Herd h, Animal a, float dt, List<Vector2> parties)
    {
        float fear = h.Kind switch { Beast.Deer => 24, Beast.Rabbit => 10, _ => 11 };
        Vector2? threat = null;
        foreach (var p in parties) if (Vector2.DistanceSquared(p, a.Pos) < fear * fear && (threat is not Vector2 q || Vector2.DistanceSquared(p, a.Pos) < Vector2.DistanceSquared(q, a.Pos))) threat = p;
        a.Timer -= dt;
        if (threat is Vector2 th)
        {
            var away = a.Pos - th;
            away = away.LengthSquared() > 1e-4f ? Vector2.Normalize(away) : Vector2.UnitX;
            a.Goal = a.Pos + away * 6;
            a.Speed = h.Kind switch { Beast.Deer => 9, Beast.Rabbit => 7, Beast.Sheep => 3.2f, _ => 2.6f };
            a.Grazing = false;
            if (h.Kind == Beast.Rabbit && !a.Fleeing) a.Timer = 1.2f; // a dash, then down its hole
            a.Fleeing = true;
            if (h.Kind == Beast.Rabbit && a.Timer <= 0) { a.Gone = true; return; }
        }
        else if (a.Timer <= 0)
        {
            a.Fleeing = false;
            if (a.Grazing || rng.Next(3) == 0)
            {
                a.Grazing = false;
                a.Goal = h.Kind == Beast.Deer && Vector2.Distance(a.Pos, h.Home) > h.Radius * 2.5f ? h.Home : Within(h, rng);
                a.Speed = h.Kind switch { Beast.Deer => 1.3f, Beast.Rabbit => 1.6f, Beast.Sheep => 0.8f, _ => 0.6f };
                a.Timer = 2 + rng.NextSingle() * 3;
            }
            else { a.Grazing = true; a.Speed = 0; a.Timer = 3 + rng.NextSingle() * 6; }
        }
        var to = a.Goal - a.Pos;
        float dist = to.Length();
        if (a.Speed > 0 && dist > 0.2f)
        {
            var next = a.Pos + to / dist * MathF.Min(dist, a.Speed * dt);
            if (h.Plot is { } f && !f.Inside(next, -0.4f)) next = a.Pos + (f.Center - a.Pos) * 0.02f; // the fence holds them
            if (t.WaterDepth(next) <= 0) a.Pos = next;
            float want = MathF.Atan2(to.Y, to.X);
            a.Heading += MathHelper.WrapAngle(want - a.Heading) * MathF.Min(1, dt * 6);
            a.Phase += dt * a.Speed * (h.Kind == Beast.Rabbit ? 3.5f : 4.5f);
        }
        else if (!a.Grazing && a.Timer > 0.5f && !a.Fleeing) { a.Grazing = true; a.Speed = 0; }
    }

    void UpdateBirds(float dt, Vector2 focus, float zoom, float reach, bool day, IEnumerable<Vector2> carrion, int wanderers)
    {
        if (!day || zoom > 520) { flocks.Clear(); crows.Clear(); hawk = null; return; }
        bool first = flocks.Count == 0; // on first sight the birds are already about; later ones fly in from the edge
        while (flocks.Count < wanderers) flocks.Add(NewFlock(focus, reach, first ? 0 : 1));
        foreach (var f in flocks)
        {
            f.Wander += (rng.NextSingle() - 0.5f) * dt * 1.2f;
            f.Wander *= 1 - dt * 0.4f;
            f.Dir = Rotate(f.Dir, f.Wander * dt);
            f.Pos += f.Dir * f.Speed * dt;
        }
        for (int i = 0; i < flocks.Count; i++)
            if (Vector2.Distance(flocks[i].Pos, focus) > reach * 1.5f) flocks[i] = NewFlock(focus, reach, 1);
        // Crows over the dead (at most three fields at a time), and one hawk while the view is close enough to see it.
        var spots = carrion.Where(c => Vector2.Distance(c, focus) < reach).Take(3).ToList();
        foreach (var k in crows.Keys.Where(k => !spots.Contains(k)).ToList()) crows.Remove(k);
        foreach (var s in spots)
            if (!crows.ContainsKey(s)) crows[s] = MakeFlock(s, 5 + rng.Next(5), new Color(30, 30, 34), 0.75f, 9 + rng.NextSingle() * 3, true, 9 + rng.NextSingle() * 6, 2.2f, false);
        if (zoom < 360 && !battle) hawk ??= MakeFlock(focus + new Vector2(rng.NextSingle() - 0.5f, rng.NextSingle() - 0.5f) * reach, 1, new Color(92, 70, 50), 1.6f, 30, true, 26, 3.2f, true);
        else hawk = null;
        if (hawk != null && Vector2.Distance(hawk.Circle, focus) > reach * 1.5f) hawk.Circle = focus;
        foreach (var f in crows.Values.Append(hawk))
            if (f != null) f.Angle += f.Speed / f.Radius * dt;
    }

    Flock NewFlock(Vector2 focus, float reach, int edge)
    {
        float a = rng.NextSingle() * MathF.Tau;
        var pos = focus + new Vector2(MathF.Cos(a), MathF.Sin(a)) * reach * (edge == 1 ? 1.2f : rng.NextSingle());
        var f = MakeFlock(pos, 6 + rng.Next(9), rng.Next(3) == 0 ? new Color(70, 62, 54) : new Color(44, 42, 44), 0.6f, 8 + rng.NextSingle() * 8, false, 0, 7 + rng.NextSingle() * 4, false);
        var inward = Vector2.Normalize(focus - pos + new Vector2(rng.NextSingle() - 0.5f, rng.NextSingle() - 0.5f) * reach * 0.6f);
        f.Dir = edge == 1 ? inward : new Vector2(MathF.Cos(a + 1.3f), MathF.Sin(a + 1.3f));
        return f;
    }

    Flock MakeFlock(Vector2 at, int n, Color col, float size, float alt, bool circling, float radius, float speed, bool glides)
    {
        var f = new Flock
        {
            Pos = at, Circle = at, Dir = Vector2.UnitX, Alt = alt, Speed = speed, Size = size * (battle ? 1.4f : BirdBig), Col = col, Circling = circling, Radius = MathF.Max(radius, 1),
            Glides = glides, Angle = rng.NextSingle() * MathF.Tau, Offsets = new Vector3[n], Phases = new float[n],
        };
        for (int i = 0; i < n; i++)
        {
            f.Offsets[i] = i == 0 ? Vector3.Zero : new Vector3(rng.NextSingle() - 0.5f, (rng.NextSingle() - 0.5f) * 0.4f, rng.NextSingle() - 0.5f) * (circling ? 7 : 9);
            f.Phases[i] = rng.NextSingle() * 10;
        }
        return f;
    }

    static Vector2 Rotate(Vector2 v, float a) => new(v.X * MathF.Cos(a) - v.Y * MathF.Sin(a), v.X * MathF.Sin(a) + v.Y * MathF.Cos(a));

    /// The frame's geometry; returns the vertices and the triangle count for `World.Extra`.
    public (VertexPNCT[] V, int Tris) Build()
    {
        mesh.V.Clear();
        mesh.Tag = Vector2.Zero;
        foreach (var h in herds.Values)
            foreach (var a in h.Members) if (!a.Gone) DrawBeast(h.Kind, a);
        foreach (var f in flocks) DrawFlock(f);
        foreach (var f in crows.Values) DrawFlock(f);
        foreach (var f in flushed) DrawFlock(f);
        if (hawk != null) DrawFlock(hawk);
        BuildPeople();
        if (buf.Length < mesh.V.Count) buf = new VertexPNCT[Math.Max(mesh.V.Count, buf.Length * 2)];
        CollectionsMarshal.AsSpan(mesh.V).CopyTo(buf);
        return (buf, mesh.V.Count / 3);
    }

    void DrawFlock(Flock f)
    {
        for (int i = 0; i < f.Offsets.Length; i++)
        {
            Vector2 at, dir;
            if (f.Circling)
            {
                float a = f.Angle + i * 0.35f;
                at = f.Circle + new Vector2(MathF.Cos(a), MathF.Sin(a)) * (f.Radius + f.Offsets[i].X * 0.6f);
                dir = new Vector2(-MathF.Sin(a), MathF.Cos(a));
            }
            else
            {
                // A loose flock: each bird keeps its place in the flock but drifts round it.
                var o = f.Offsets[i] + new Vector3(MathF.Sin(clock * 0.9f + f.Phases[i]), 0, MathF.Cos(clock * 0.7f + f.Phases[i] * 1.3f)) * 1.6f;
                at = f.Pos + Rotate(new Vector2(o.X, o.Z), MathF.Atan2(f.Dir.Y, f.Dir.X));
                dir = f.Dir;
            }
            float y = t.Height(at) + f.Alt + f.Offsets[i].Y + MathF.Sin(clock * 0.8f + f.Phases[i]) * 0.6f;
            float beat = f.Glides && MathF.Sin(clock * 0.4f + f.Phases[i]) < 0.6f ? 0.15f : MathF.Sin(clock * (f.Glides ? 6 : 13) + f.Phases[i] * 5);
            Bird(new Vector3(at.X, y, at.Y), dir, f.Size, beat, f.Col);
        }
    }

    /// A bird seen from below or above: two wings that beat, a small body and tail.
    void Bird(Vector3 p, Vector2 dir, float span, float beat, Color col)
    {
        var fw = new Vector3(dir.X, 0, dir.Y) * span * 0.32f;
        var side = new Vector3(-dir.Y, 0, dir.X) * span * 0.5f;
        var lift = Vector3.Up * beat * span * 0.32f;
        var root = p + fw * 0.2f;
        Wing(root, p - fw * 0.35f, p + side + lift - fw * 0.15f);
        Wing(root, p - fw * 0.35f, p - side + lift - fw * 0.15f);
        Wing(p + fw * 0.9f, p - fw * 0.9f, p + side * 0.12f);
        Wing(p + fw * 0.9f, p - fw * 0.9f, p - side * 0.12f);
        void Wing(Vector3 a, Vector3 b, Vector3 c)
        {
            mesh.Raw(a, Vector3.Up, col, Vector2.Zero); mesh.Raw(b, Vector3.Up, col, Vector2.Zero); mesh.Raw(c, Vector3.Up, col, Vector2.Zero);
        }
    }

    /// A beast as a few boxes: legs that step while it walks, a body, a head that dips to graze.
    void DrawBeast(Beast kind, Animal a)
    {
        var f = new Vector2(MathF.Cos(a.Heading), MathF.Sin(a.Heading));
        var s = new Vector2(-f.Y, f.X);
        float k = kind switch { Beast.Sheep => 1.1f, Beast.Cow => 1.25f, Beast.Deer => 1.4f, _ => 1.3f }, ground = t.Height(a.Pos), yaw = a.Heading; // about one fence post high
        bool moving = a.Speed > 0 && !a.Grazing;
        float stride = moving ? MathF.Sin(a.Phase) : 0;
        Vector3 At(float fwd, float side, float up) { var p = a.Pos + f * fwd * k + s * side * k; return new Vector3(p.X, ground + up * k, p.Y); }
        var dark = new Color(a.Coat.ToVector3() * 0.55f);
        switch (kind)
        {
            case Beast.Sheep:
                Legs(0.26f, 0.13f, 0.09f, 0.3f, new Color(60, 54, 48));
                mesh.Blob(At(0, 0, 0.5f), new Vector3(0.42f, 0.27f, 0.42f) * k, a.Coat, woolRng, 0.12f); // a woolly bundle
                mesh.Box(At(0.44f, 0, a.Grazing ? 0.12f : 0.42f), 0.32f * k, 0.24f * k, 0.26f * k, yaw, new Color(88, 80, 72)); // a grey face that reads
                break;
            case Beast.Cow:
                Legs(0.42f, 0.2f, 0.12f, 0.45f, dark);
                mesh.Box(At(0, 0, 0.42f), 1.2f * k, 0.6f * k, 0.58f * k, yaw, a.Coat);
                mesh.Box(At(0.72f, 0, a.Grazing ? 0.18f : 0.7f), 0.38f * k, 0.3f * k, 0.32f * k, yaw, dark);
                break;
            case Beast.Deer:
                Legs(0.3f, 0.11f, 0.07f, 0.62f, dark);
                mesh.Box(At(0, 0, 0.6f), 0.85f * k, 0.34f * k, 0.36f * k, yaw, a.Coat);
                mesh.Box(At(-0.44f, 0, 0.8f), 0.1f * k, 0.12f * k, 0.12f * k, yaw, new Color(232, 226, 214)); // the white scut
                if (a.Grazing) mesh.Box(At(0.52f, 0, 0.12f), 0.26f * k, 0.16f * k, 0.18f * k, yaw, a.Coat);
                else
                {
                    mesh.Box(At(0.36f, 0, 0.82f), 0.14f * k, 0.13f * k, 0.36f * k, yaw, a.Coat);           // neck
                    mesh.Box(At(0.48f, 0, 1.12f), 0.28f * k, 0.15f * k, 0.16f * k, yaw, a.Coat);           // head
                    if (a.Stag)
                        foreach (float sd in new[] { -0.07f, 0.07f })
                            mesh.Box(At(0.42f, sd, 1.26f), 0.05f * k, 0.05f * k, 0.3f * k, yaw + sd * 6, new Color(196, 178, 150));
                }
                break;
            default:
            {
                float hop = moving ? MathF.Abs(MathF.Sin(a.Phase)) * 0.35f : 0;
                mesh.Box(At(0, 0, hop), 0.32f * k, 0.2f * k, 0.2f * k, yaw, a.Coat);
                mesh.Box(At(0.16f, 0, hop + 0.12f), 0.14f * k, 0.13f * k, 0.14f * k, yaw, a.Coat);
                foreach (float sd in new[] { -0.03f, 0.03f }) mesh.Box(At(0.12f, sd, hop + 0.26f), 0.04f * k, 0.03f * k, 0.14f * k, yaw, a.Coat);
                break;
            }
        }
        void Legs(float fx, float sx, float w, float h, Color col)
        {
            foreach (var (lf, ls, ph) in new[] { (fx, sx, 0f), (fx, -sx, MathF.PI), (-fx, sx, MathF.PI), (-fx, -sx, 0f) })
            {
                float swing = moving ? MathF.Sin(a.Phase + ph) * 0.07f : 0;
                mesh.Box(At(lf + swing, ls, 0), w * k, w * k, h * k, yaw, col);
            }
        }
    }
}
