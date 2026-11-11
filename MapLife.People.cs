using Microsoft.Xna.Framework;

namespace Ironfield;

/// People and traffic on the campaign map (docs/WORLD_LIFE.md WL5): villagers at work in the fields who walk home at
/// dusk, folk about the villages and towns by day, walkers, riders and ox carts on the roads, boats on the wide
/// rivers, smoke from the chimneys, and the real caravans and pilgrims drawn as wagon trains and files of walkers.
/// At night the roads and fields empty and the windows light (the shader, `World.Windows`). Drawing only, like the rest
/// of `MapLife`: its own dice, nothing read back by the campaign.
sealed partial class MapLife
{
    sealed class Person
    {
        public Vector2 Pos, Goal, Home;
        public float Heading, Phase, Timer, Speed;
        public bool Working, Gone, Hat;
        public Color Shirt, Hose;
    }

    sealed class Traveller
    {
        public int Kind, Road; // kind: 0 walker, 1 rider, 2 ox cart
        public float S, Dir, Speed, Phase;
        public Vector2 Pos;
        public float Heading;
        public Person Who = new();
    }

    sealed class Boat
    {
        public River River = null!;
        public float S, Speed, Phase;
        public Color Hull;
    }

    /// The villages and towns (from the map's foliage): centre and how far their folk wander.
    readonly List<(Vector2 Pos, float Radius, bool Town)> places = new();
    readonly Dictionary<int, List<Person>> crowds = new();
    readonly List<Traveller> travellers = new();
    readonly List<Boat> boats = new();
    readonly Dictionary<int, List<Vector2>> trails = new();
    readonly Dictionary<int, float[]> roadLengths = new();
    readonly List<(int Id, Vector2 Pos, float Facing, bool Pilgrims)> caravans = new();
    readonly List<Vector3> smoking = new();
    float peopleWait;
    /// How much bigger than life the map draws its folk: a man stands about as high as a cottage's eaves.
    const float Folk = 1.7f;

    static readonly Color[] Shirts = { new(176, 150, 106), new(128, 140, 92), new(196, 182, 150), new(150, 84, 62), new(104, 120, 150), new(206, 196, 168), new(170, 120, 70) };
    static readonly Color Skin = new(196, 158, 126), Straw = new(200, 178, 112), Ox = new(150, 118, 84), Canvas = new(214, 204, 178), Plank = new(110, 84, 58);

    /// Where smoke rises: `World.Chimneys`, set by the map once the world is built.
    public IReadOnlyList<Vector3> Chimneys = Array.Empty<Vector3>();

    void FindPlaces()
    {
        foreach (var f in t.Foliage)
            if (f.Kind is FoliageKind.Village or FoliageKind.Town)
                places.Add((f.Pos, f.Kind == FoliageKind.Town ? 3.0f * f.Scale : 2.4f * f.Scale, f.Kind == FoliageKind.Town));
    }

    /// The caravans and pilgrims on the map this frame (their parties' places), for the wagons and walkers behind them.
    public void SetCaravans(IEnumerable<(int Id, Vector2 Pos, float Facing, bool Pilgrims)> list)
    {
        caravans.Clear();
        caravans.AddRange(list);
        foreach (var (id, pos, _, _) in caravans)
        {
            if (!trails.TryGetValue(id, out var trail)) trails[id] = trail = new List<Vector2> { pos };
            if (Vector2.Distance(trail[^1], pos) > 30) { trail.Clear(); trail.Add(pos); } // it jumped (a load, a town)
            else if (Vector2.Distance(trail[^1], pos) > 0.8f) { trail.Add(pos); if (trail.Count > 60) trail.RemoveAt(0); }
        }
        foreach (int id in trails.Keys.Where(k => !caravans.Any(c => c.Id == k)).ToList()) trails.Remove(id);
    }

    void UpdatePeople(float dt, Vector2 focus, float zoom, float reach, float hour, bool high)
    {
        if (zoom > 260) { crowds.Clear(); travellers.Clear(); boats.Clear(); return; }
        bool day = hour > 5.5f && hour < 21f, work = hour > 6.5f && hour < 18f && t.MapSeason != 3;
        peopleWait -= dt;
        if (peopleWait <= 0)
        {
            peopleWait = 0.5f;
            foreach (var key in crowds.Keys.Where(k => crowds[k].Count == 0 || crowds[k].All(p => p.Gone) || Vector2.Distance(crowds[k][0].Home, focus) > reach * 1.3f).ToList()) crowds.Remove(key);
            PopulatePeople(focus, reach, day, work, high ? 18 : 9);
            travellers.RemoveAll(v => Vector2.Distance(v.Pos, focus) > reach * 1.5f);
            int roadCap = day ? (high ? 14 : 7) : 2;
            var roads = NearRoads(focus, reach);
            for (int tries = 0; tries < 12 && travellers.Count < roadCap && roads.Count > 0; tries++) SpawnTraveller(roads, focus, reach, day);
            boats.RemoveAll(b => Vector2.Distance(At(b.River.Path, b.S, out _), focus) > reach * 1.5f);
            if (day && boats.Count < (high ? 3 : 1)) SpawnBoat(focus, reach);
        }
        foreach (var crowd in crowds.Values)
            foreach (var p in crowd) if (!p.Gone) StepPerson(p, dt, hour);
        for (int i = travellers.Count - 1; i >= 0; i--)
            if (!StepTraveller(travellers[i], dt)) travellers.RemoveAt(i);
        for (int i = boats.Count - 1; i >= 0; i--)
        {
            var b = boats[i];
            b.S += b.Speed * dt;
            b.Phase += dt;
            if (b.S > Length(b.River.Path) - 2) boats.RemoveAt(i);
        }
        smoking.Clear();
        smoking.AddRange(Chimneys.Where(c => Vector2.DistanceSquared(new Vector2(c.X, c.Z), focus) < reach * reach)
            .OrderBy(c => Vector2.DistanceSquared(new Vector2(c.X, c.Z), focus)).Take(high ? 40 : 16));
    }

    void PopulatePeople(Vector2 focus, float reach, bool day, bool work, int cap)
    {
        // Folk about the villages first (they are what a close view looks at), then the field hands.
        if (day)
            for (int i = 0; i < places.Count; i++)
            {
                if (crowds.Count >= cap) break;
                var (pos, radius, town) = places[i];
                int key = -1 - i;
                if (crowds.ContainsKey(key) || Vector2.DistanceSquared(pos, focus) > reach * reach) continue;
                var r = new Random(key * 31 + 7);
                var crowd = new List<Person>();
                for (int n = (town ? 4 : 2) + r.Next(3); n > 0; n--)
                {
                    float a = r.NextSingle() * MathF.Tau;
                    crowd.Add(NewPerson(pos + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius * (n % 2 == 0 ? 0.1f + r.NextSingle() * 0.25f : 1.15f + r.NextSingle() * 0.4f), pos, r, false)); // on the green or round the edge
                }
                crowds[key] = crowd;
            }
        if (work)
            foreach (var f in t.Fields)
            {
                if (crowds.Count >= cap) break;
                if (f.Kind == FieldKind.Pasture || f.Seed % 2 != 0 || crowds.ContainsKey(f.Seed) || Vector2.DistanceSquared(f.Center, focus) > reach * reach) continue;
                var r = new Random(f.Seed + 3);
                var home = places.Count > 0 ? places.MinBy(p => Vector2.DistanceSquared(p.Pos, f.Center)).Pos : f.Center;
                var crowd = new List<Person>();
                for (int i = 1 + r.Next(3); i > 0; i--)
                {
                    var p = f.At((r.NextSingle() * 2 - 1) * f.Half.X * 0.8f, (r.NextSingle() * 2 - 1) * f.Half.Y * 0.75f);
                    crowd.Add(NewPerson(p, home, r, true));
                }
                crowds[f.Seed] = crowd;
            }
    }

    static Person NewPerson(Vector2 at, Vector2 home, Random r, bool working) => new()
    {
        Pos = at, Goal = at, Home = home, Heading = r.NextSingle() * MathF.Tau, Phase = r.NextSingle() * 10, Timer = r.NextSingle() * 3,
        Working = working, Hat = r.Next(2) == 0, Shirt = Jitter(Shirts[r.Next(Shirts.Length)], r), Hose = Jitter(new Color(84, 74, 60), r),
    };

    static Color Jitter(Color c, Random r) { float k = 0.9f + r.NextSingle() * 0.2f; return new Color(c.ToVector3() * k); }

    /// A villager's moment: bent to the work, a few steps to the next row; at dusk the walk home, and gone indoors.
    /// Folk in a village or town amble between spots near its middle and go in at nightfall.
    void StepPerson(Person p, float dt, float hour)
    {
        bool home = p.Working ? hour >= 18f || hour < 6.5f : hour >= 21f || hour < 5.5f;
        if (home && p.Speed < 1)
        {
            float a = rng.NextSingle() * MathF.Tau;
            p.Goal = p.Working ? p.Home + new Vector2(MathF.Cos(a), MathF.Sin(a)) * 3 : p.Home;
            p.Speed = 1.1f;
            p.Timer = 999;
        }
        p.Timer -= dt;
        if (!home && p.Timer <= 0)
        {
            if (p.Speed > 0 || rng.Next(3) > 0) { p.Speed = 0; p.Timer = p.Working ? 4 + rng.NextSingle() * 6 : 2 + rng.NextSingle() * 4; }
            else
            {
                var step = new Vector2(rng.NextSingle() - 0.5f, rng.NextSingle() - 0.5f) * (p.Working ? 5 : 9);
                var to = p.Pos + step;
                if (!p.Working && Vector2.Distance(to, p.Home) > 9) to = p.Home + step * 0.3f;
                p.Goal = to;
                p.Speed = p.Working ? 0.5f : 0.7f;
                p.Timer = 3 + rng.NextSingle() * 3;
            }
        }
        var d = p.Goal - p.Pos;
        float len = d.Length();
        if (p.Speed > 0 && len > 0.15f)
        {
            var next = p.Pos + d / len * MathF.Min(len, p.Speed * dt);
            if (t.WaterDepth(next) <= 0) p.Pos = next;
            p.Heading += MathHelper.WrapAngle(MathF.Atan2(d.Y, d.X) - p.Heading) * MathF.Min(1, dt * 5);
            p.Phase += dt * p.Speed * 6;
        }
        else if (home) p.Gone = true;
        else p.Speed = 0;
    }

    float[] Lengths(int road)
    {
        if (roadLengths.TryGetValue(road, out var l)) return l;
        var pts = t.RoadLines[road].Points;
        l = new float[pts.Length];
        for (int i = 1; i < pts.Length; i++) l[i] = l[i - 1] + Vector2.Distance(pts[i - 1], pts[i]);
        return roadLengths[road] = l;
    }

    static float Length(Vector2[] path)
    {
        float s = 0;
        for (int i = 1; i < path.Length; i++) s += Vector2.Distance(path[i - 1], path[i]);
        return s;
    }

    /// The point `s` metres along a path, and the way it runs there.
    static Vector2 At(Vector2[] path, float s, out Vector2 dir)
    {
        dir = Vector2.UnitX;
        for (int i = 1; i < path.Length; i++)
        {
            float seg = Vector2.Distance(path[i - 1], path[i]);
            if (seg < 1e-4f) continue;
            dir = (path[i] - path[i - 1]) / seg;
            if (s <= seg || i == path.Length - 1) return path[i - 1] + dir * MathF.Min(s, seg);
            s -= seg;
        }
        return path[^1];
    }

    /// The roads with a stretch in view.
    List<int> NearRoads(Vector2 focus, float reach)
    {
        var near = new List<int>();
        for (int i = 0; i < t.RoadLines.Count; i++)
            if (t.RoadLines[i].Points.Any(p => Vector2.DistanceSquared(p, focus) < reach * reach * 0.8f)) near.Add(i);
        return near;
    }

    void SpawnTraveller(List<int> roads, Vector2 focus, float reach, bool day)
    {
        int road = roads[rng.Next(roads.Count)];
        var pts = t.RoadLines[road].Points;
        var near = Enumerable.Range(0, pts.Length).Where(i => Vector2.DistanceSquared(pts[i], focus) < reach * reach * 0.8f).ToList();
        if (near.Count == 0) return;
        float s = Lengths(road)[near[rng.Next(near.Count)]] + rng.NextSingle() * 4;
        int kind = !day ? 1 : rng.Next(10) switch { < 5 => 0, < 8 => 1, _ => 2 };
        var v = new Traveller
        {
            Kind = kind, Road = road, S = s, Dir = rng.Next(2) == 0 ? 1 : -1, Phase = rng.NextSingle() * 10,
            Speed = kind switch { 0 => 1.0f + rng.NextSingle() * 0.4f, 1 => 3.2f + rng.NextSingle(), _ => 0.8f },
        };
        v.Who = NewPerson(Vector2.Zero, Vector2.Zero, rng, false);
        travellers.Add(v);
        StepTraveller(v, 0);
    }

    /// Moves a traveller along its road, keeping to its side; false once it has gone off the road's end.
    bool StepTraveller(Traveller v, float dt)
    {
        v.S += v.Dir * v.Speed * dt;
        v.Phase += dt * v.Speed * (v.Kind == 1 ? 2.5f : 6);
        var len = Lengths(v.Road);
        if (v.S < 0 || v.S > len[^1]) return false;
        var line = t.RoadLines[v.Road];
        var p = At(line.Points, v.S, out var dir);
        dir *= v.Dir;
        v.Pos = p + new Vector2(-dir.Y, dir.X) * line.Half * 0.4f; // keeps to one side, so two can pass
        v.Heading = MathF.Atan2(dir.Y, dir.X);
        return true;
    }

    void SpawnBoat(Vector2 focus, float reach)
    {
        var wide = t.Rivers.Where(r => r.Width >= 4).ToList();
        if (wide.Count == 0) return;
        var river = wide[rng.Next(wide.Count)];
        var near = new List<float>();
        float s = 0;
        for (int i = 0; i < river.Path.Length; i++)
        {
            if (i > 0) s += Vector2.Distance(river.Path[i - 1], river.Path[i]);
            if (Vector2.DistanceSquared(river.Path[i], focus) < reach * reach * 0.6f) near.Add(s);
        }
        if (near.Count == 0) return;
        float at = near[rng.Next(near.Count)];
        var p = At(river.Path, at, out _);
        if (t.WaterDepth(p) < 0.5f || boats.Any(b => Vector2.Distance(At(b.River.Path, b.S, out _), p) < 30)) return; // not at a ford or bridge
        boats.Add(new Boat { River = river, S = at, Speed = 0.5f + rng.NextSingle() * 0.4f, Phase = rng.NextSingle() * 10, Hull = Jitter(Plank, rng) });
    }

    // ---------- drawing ----------

    void BuildPeople()
    {
        foreach (var crowd in crowds.Values)
            foreach (var p in crowd)
                if (!p.Gone) DrawPerson(p.Pos, p.Heading, p.Phase, p.Speed > 0, p.Working && p.Speed == 0 ? 1 : 0, p, Folk);
        foreach (var v in travellers) DrawTraveller(v);
        foreach (var b in boats) DrawBoat(b);
        foreach (var c in caravans) DrawCaravan(c.Id, c.Pilgrims);
        DrawSmoke();
    }

    /// A figure about as tall as a cottage's eaves: legs that step, a tunic, a head, a straw hat for some. `stoop`
    /// bends him to the work (hoeing, reaping).
    void DrawPerson(Vector2 pos, float heading, float phase, bool moving, float stoop, Person who, float k, float lift = 0)
    {
        var f = new Vector2(MathF.Cos(heading), MathF.Sin(heading));
        var s = new Vector2(-f.Y, f.X);
        float ground = t.Height(pos) + lift;
        Vector3 At(float fwd, float side, float up) { var p = pos + f * fwd * k + s * side * k; return new Vector3(p.X, ground + up * k, p.Y); }
        if (lift == 0)
            foreach (var (side, ph) in new[] { (0.08f, 0f), (-0.08f, MathF.PI) })
            {
                float swing = moving ? MathF.Sin(phase + ph) * 0.12f : 0;
                mesh.Box(At(swing, side, 0), 0.11f * k, 0.11f * k, 0.5f * k, heading, who.Hose);
            }
        float bend = stoop * (0.5f + 0.5f * MathF.Sin(clock * 1.7f + phase)); // the swing of the hoe
        mesh.Box(At(0.1f * bend, 0, 0.48f), 0.26f * k, 0.36f * k, (0.5f - 0.12f * bend) * k, heading, who.Shirt);
        mesh.Box(At(0.08f + 0.22f * bend, 0, 0.98f - 0.3f * bend), 0.18f * k, 0.18f * k, 0.2f * k, heading, Skin);
        if (who.Hat) mesh.Box(At(0.08f + 0.22f * bend, 0, 1.16f - 0.3f * bend), 0.34f * k, 0.34f * k, 0.05f * k, heading, Straw);
        if (stoop > 0) mesh.Box(At(0.3f + 0.1f * bend, 0.12f, 0.1f + 0.4f * (1 - bend)), 0.05f * k, 0.05f * k, 0.6f * k, heading, Plank); // the hoe
    }

    void DrawTraveller(Traveller v)
    {
        switch (v.Kind)
        {
            case 0: DrawPerson(v.Pos, v.Heading, v.Phase, true, 0, v.Who, Folk); break;
            case 1:
                DrawHorse(v.Pos, v.Heading, v.Phase, 1.4f, new Color(96, 70, 50));
                DrawPerson(v.Pos, v.Heading, 0, false, 0, v.Who, 1.4f, 0.62f * 1.4f);
                break;
            default:
                DrawCart(v.Pos, v.Heading, v.Phase, false, 1.4f);
                break;
        }
    }

    /// A horse as boxes, legs swinging with `phase`.
    void DrawHorse(Vector2 pos, float heading, float phase, float k, Color coat)
    {
        var f = new Vector2(MathF.Cos(heading), MathF.Sin(heading));
        var s = new Vector2(-f.Y, f.X);
        float ground = t.Height(pos);
        Vector3 At(float fwd, float side, float up) { var p = pos + f * fwd * k + s * side * k; return new Vector3(p.X, ground + up * k, p.Y); }
        foreach (var (lf, ls, ph) in new[] { (0.36f, 0.12f, 0f), (0.36f, -0.12f, MathF.PI), (-0.36f, 0.12f, MathF.PI), (-0.36f, -0.12f, 0f) })
            mesh.Box(At(lf + MathF.Sin(phase + ph) * 0.1f, ls, 0), 0.09f * k, 0.09f * k, 0.6f * k, heading, new Color(coat.ToVector3() * 0.6f));
        mesh.Box(At(0, 0, 0.55f), 1.0f * k, 0.34f * k, 0.4f * k, heading, coat);
        mesh.Box(At(0.55f, 0, 0.85f), 0.2f * k, 0.18f * k, 0.4f * k, heading, coat);
        mesh.Box(At(0.7f, 0, 1.1f), 0.36f * k, 0.16f * k, 0.18f * k, heading, coat);
    }

    /// An ox cart (or with `covered`, a merchant's wagon under canvas) drawn by one ox, a driver walking beside it.
    void DrawCart(Vector2 pos, float heading, float phase, bool covered, float k)
    {
        var f = new Vector2(MathF.Cos(heading), MathF.Sin(heading));
        var s = new Vector2(-f.Y, f.X);
        float ground = t.Height(pos);
        Vector3 At(Vector2 p, float fwd, float side, float up) { var q = p + f * fwd * k + s * side * k; return new Vector3(q.X, ground + up * k, q.Y); }
        // The ox ahead of the cart.
        var ox = pos + f * 1.5f * k;
        foreach (var (lf, ls, ph) in new[] { (0.3f, 0.15f, 0f), (0.3f, -0.15f, MathF.PI), (-0.3f, 0.15f, MathF.PI), (-0.3f, -0.15f, 0f) })
            mesh.Box(At(ox, lf + MathF.Sin(phase + ph) * 0.06f, ls, 0), 0.12f * k, 0.12f * k, 0.42f * k, heading, new Color(90, 70, 52));
        mesh.Box(At(ox, 0, 0, 0.4f), 1.0f * k, 0.5f * k, 0.48f * k, heading, Ox);
        mesh.Box(At(ox, 0.6f, 0, 0.62f), 0.32f * k, 0.3f * k, 0.28f * k, heading, new Color(118, 92, 66));
        mesh.Box(At(pos, 0.75f, 0, 0.42f), 0.8f * k, 0.06f * k, 0.06f * k, heading, Plank); // the shaft
        // The cart: a bed on two wheels, a load or a canvas tilt.
        foreach (float side in new[] { -0.5f, 0.5f })
            mesh.Box(At(pos, 0, side, 0), 0.62f * k, 0.08f * k, 0.62f * k, heading, new Color(70, 56, 42));
        mesh.Box(At(pos, 0, 0, 0.36f), 1.3f * k, 0.9f * k, 0.22f * k, heading, Plank);
        if (covered) mesh.Gable(At(pos, 0, 0, 0.58f), 1.3f * k, 0.95f * k, 0.6f * k, heading, Canvas);
        else mesh.Box(At(pos, -0.1f, 0, 0.58f), 0.8f * k, 0.6f * k, 0.25f * k, heading, new Color(176, 150, 96)); // sacks, hay
    }

    void DrawBoat(Boat b)
    {
        var p = At(b.River.Path, b.S, out var dir);
        var n = new Vector2(-dir.Y, dir.X);
        float bank = b.River.Width / 2 + 3.5f;
        float level = MathF.Min(t.Height(p + n * bank), t.Height(p - n * bank)) - 0.3f + 0.05f + MathF.Sin(b.Phase * 1.3f) * 0.04f;
        float yaw = MathF.Atan2(dir.Y, dir.X);
        var at = new Vector3(p.X, level - 0.15f, p.Y);
        mesh.Box(at, 2.6f, 0.9f, 0.35f, yaw, b.Hull);
        mesh.Box(at + new Vector3(dir.X, 0, dir.Y) * 1.45f, 0.4f, 0.5f, 0.42f, yaw, b.Hull);           // the prow
        mesh.Box(at + Vector3.Up * 0.35f, 2.2f, 0.7f, 0.04f, yaw, new Color(150, 120, 84));             // thwarts
        var who = new Person { Shirt = Shirts[(int)(b.Phase * 10) % Shirts.Length], Hose = new Color(84, 74, 60), Hat = true };
        DrawPerson(p - dir * 0.8f, yaw, 0, false, 0, who, 1, level + 0.2f - t.Height(p - dir * 0.8f));
        // The pole, dipping as the boatman pushes.
        float push = MathF.Sin(b.Phase * 1.1f);
        var hand = new Vector3(p.X - dir.X * 0.6f, level + 1.1f, p.Y - dir.Y * 0.6f);
        var foot = new Vector3(p.X - dir.X * (1.6f + push * 0.5f) + n.X * 0.5f, level - 0.6f, p.Y - dir.Y * (1.6f + push * 0.5f) + n.Y * 0.5f);
        Pole(hand, foot);
        void Pole(Vector3 a, Vector3 c)
        {
            var side = Vector3.Normalize(Vector3.Cross(c - a, Vector3.Up)) * 0.04f;
            var col = new Color(120, 96, 66);
            mesh.Raw(a - side, Vector3.Up, col, Vector2.Zero); mesh.Raw(a + side, Vector3.Up, col, Vector2.Zero); mesh.Raw(c + side, Vector3.Up, col, Vector2.Zero);
            mesh.Raw(a - side, Vector3.Up, col, Vector2.Zero); mesh.Raw(c + side, Vector3.Up, col, Vector2.Zero); mesh.Raw(c - side, Vector3.Up, col, Vector2.Zero);
        }
    }

    /// A caravan's wagons, or a pilgrims' file of walkers, strung out along the way the party came.
    void DrawCaravan(int id, bool pilgrims)
    {
        if (!trails.TryGetValue(id, out var trail) || trail.Count < 2) return;
        int count = pilgrims ? 6 : 2 + id % 2;
        float gap = pilgrims ? 1.6f : 4.2f, first = pilgrims ? 2.6f : 5f;
        var r = new Random(id);
        for (int i = 0; i < count; i++)
        {
            float back = first + i * gap;
            if (!Behind(trail, back, out var pos, out var dir)) break;
            float heading = MathF.Atan2(dir.Y, dir.X), phase = clock * 3 + i;
            if (pilgrims)
            {
                var who = new Person { Shirt = Jitter(i % 3 == 0 ? new Color(120, 110, 96) : new Color(150, 140, 120), r), Hose = new Color(96, 86, 72), Hat = i % 2 == 0 };
                DrawPerson(pos, heading, phase * 2, true, 0, who, 1.15f);
            }
            else DrawCart(pos, heading, phase, true, 1.6f);
        }
    }

    /// The place `back` metres behind the head of a trail, and the way it runs there.
    static bool Behind(List<Vector2> trail, float back, out Vector2 pos, out Vector2 dir)
    {
        pos = trail[^1]; dir = Vector2.UnitX;
        for (int i = trail.Count - 1; i > 0; i--)
        {
            float seg = Vector2.Distance(trail[i], trail[i - 1]);
            if (seg < 1e-4f) continue;
            dir = (trail[i] - trail[i - 1]) / seg;
            if (back <= seg) { pos = trail[i] - dir * back; return true; }
            back -= seg;
        }
        return false;
    }

    /// One puff of smoke: an eight-faced lump with normals from its middle, so it shades soft (a sphere would cost forty
    /// times the triangles, and there are up to two hundred puffs).
    void Puff(Vector3 c, float r, Color col)
    {
        Span<Vector3> d = stackalloc Vector3[] { Vector3.UnitX, Vector3.UnitZ, -Vector3.UnitX, -Vector3.UnitZ };
        var top = c + Vector3.Up * r * 0.85f; var bottom = c - Vector3.Up * r * 0.7f;
        for (int i = 0; i < 4; i++)
        {
            Vector3 a = c + d[i] * r, b = c + d[(i + 1) % 4] * r;
            mesh.Raw(top, Vector3.Up, col, Vector2.Zero); mesh.Raw(a, d[i], col, Vector2.Zero); mesh.Raw(b, d[(i + 1) % 4], col, Vector2.Zero);
            mesh.Raw(bottom, -Vector3.Up, col, Vector2.Zero); mesh.Raw(b, d[(i + 1) % 4], col, Vector2.Zero); mesh.Raw(a, d[i], col, Vector2.Zero);
        }
    }

    /// Smoke from the chimneys: a few puffs each, rising, drifting with the wind and swelling as they go.
    void DrawSmoke()
    {
        for (int c = 0; c < smoking.Count; c++)
        {
            var at = smoking[c];
            float seed = (c * 0.618f + at.X * 0.013f) % 1f;
            for (int j = 0; j < 5; j++)
            {
                float age = clock * 0.18f + seed + j / 5f; age -= MathF.Floor(age); // seed can be negative: keep the age in 0..1
                var p = at + new Vector3(2.4f, 0, 0.9f) * age * age * 2.2f + Vector3.Up * age * 5f;
                float size = 0.4f + age * 1.0f;
                var col = Color.Lerp(new Color(128, 126, 122), new Color(196, 198, 202), age);
                Puff(p, size, col);
            }
        }
    }
}
