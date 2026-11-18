using Microsoft.Xna.Framework;

namespace Ironfield;

/// World: roads and water (docs/WORLD_LIFE.md WL1-WL2). Roads are ribbons of packed earth laid over the ground with ruts
/// and a paler crown; rivers carry their flow in their UVs (x = across, -1..1; y = -10 - metres along) so the world shader
/// can run ripples downstream, pale the shallows and foam the edges; still water (lakes, ponds) carries x = 5. Muddy banks,
/// reeds, stepping stones at the fords, and bridges (stone between towns, planks out to a village) finish them.
sealed partial class World
{
    static readonly Color WaterDeep = new(52, 88, 112), WaterShallow = new(86, 124, 132), LakeDeep = new(44, 78, 100);
    static readonly Vector3 Verge = new(0.34f, 0.31f, 0.21f), RoadEarth = new(0.6f, 0.51f, 0.37f), Rut = new(0.4f, 0.33f, 0.24f), Crown = new(0.68f, 0.6f, 0.45f);
    static readonly Vector3 Mud = new(0.22f, 0.19f, 0.14f), Shore = new(0.5f, 0.46f, 0.36f);
    static readonly Color BridgeStone = new(140, 136, 126), ReedLow = new(92, 104, 52), ReedTip = new(170, 162, 96), Cattail = new(96, 64, 40);

    /// The UV tag of trodden ground (roads): the shader lets less snow lie on it and darkens it to slush in winter.
    static readonly Vector2 Trodden = new(0, -0.25f);

    /// The ground's normal at p (for ribbons lit like the land under them).
    Vector3 NormalAt(Vector2 p)
    {
        var g = t.Gradient(p);
        return Vector3.Normalize(new Vector3(-g.X, 1, -g.Y));
    }

    /// Points along a line every `step` metres, each with the line's unit direction there.
    static List<(Vector2 P, Vector2 D)> Resample(Vector2[] line, float step)
    {
        var o = new List<(Vector2, Vector2)>();
        for (int i = 0; i + 1 < line.Length; i++)
        {
            var a = line[i]; var ab = line[i + 1] - a;
            float len = ab.Length();
            if (len < 1e-3f) continue;
            var d = ab / len;
            for (float s = 0; s < len; s += step) o.Add((a + d * s, d));
        }
        if (line.Length > 1) o.Add((line[^1], Vector2.Normalize(line[^1] - line[^2])));
        // Smooth the directions so the ribbon's edges do not pinch at the corners.
        var sm = new List<(Vector2 P, Vector2 D)>(o.Count);
        for (int i = 0; i < o.Count; i++)
        {
            var d = o[Math.Max(0, i - 1)].Item2 + o[i].Item2 * 2 + o[Math.Min(o.Count - 1, i + 1)].Item2;
            sm.Add((o[i].Item1, d.LengthSquared() > 1e-6f ? Vector2.Normalize(d) : o[i].Item2));
        }
        return sm;
    }

    // ---------- roads ----------

    /// Each map road as a ribbon of earth across seven columns: verge, earth, rut, crown, rut, earth, verge. Built in
    /// stretches of about 40 m, each its own tile, so a frame draws only the roads in view.
    void Roads(Mesh m)
    {
        if (t.RoadLines.Count == 0) return;
        float[] cols = { -1, -0.62f, -0.3f, 0, 0.3f, 0.62f, 1 };
        // Each point of a ribbon (sample x column) is worked out once, the roads in parallel; then the quads take their
        // corners from those points, road by road in order.
        var ribbons = new (int Samples, VertexPNCT[] At)[t.RoadLines.Count];
        Parallel.For(0, t.RoadLines.Count, k =>
        {
            var road = t.RoadLines[k];
            var s = Resample(road.Points, 2f);
            var at = new VertexPNCT[s.Count * cols.Length];
            for (int i = 0; i < s.Count; i++)
            {
                var (p, d) = s[i];
                Vector2 nrm = new(-d.Y, d.X);
                for (int c = 0; c < cols.Length; c++) at[i * cols.Length + c] = Vertex(p + nrm * cols[c] * road.Half, c);
            }
            ribbons[k] = (s.Count, at);

            VertexPNCT Vertex(Vector2 p, int col)
            {
                var earth = col is 0 or 6 ? Verge : col is 2 or 4 ? Rut : col == 3 ? Crown : RoadEarth;
                if (road.Village) earth = Vector3.Lerp(earth, Verge, 0.25f); // a lane: less worn
                var c = Vector3.Lerp(earth, GroundColor(p), 0.1f); // the ground's season shows through (snow, dry summer)
                return new VertexPNCT(new Vector3(p.X, t.Height(p) + 0.32f, p.Y), NormalAt(p), new Color(c), Trodden);
            }
        });
        foreach (var (count, at) in ribbons)
        {
            for (int from = 0; from + 1 < count; from += 20)
            {
                int start = m.V.Count, to = Math.Min(count - 1, from + 20);
                for (int i = from; i < to; i++)
                    for (int c = 0; c + 1 < cols.Length; c++)
                    {
                        int a0 = i * cols.Length + c, b0 = a0 + 1, a1 = a0 + cols.Length, b1 = a1 + 1;
                        m.V.Add(at[a0]); m.V.Add(at[b0]); m.V.Add(at[b1]);
                        m.V.Add(at[a0]); m.V.Add(at[b1]); m.V.Add(at[a1]);
                    }
                AddTile(m, start);
            }
        }
    }

    /// The vertices from `start` on as one more culled tile.
    void AddTile(Mesh m, int start)
    {
        if (m.V.Count == start) return;
        var min = new Vector3(float.MaxValue); var max = new Vector3(float.MinValue);
        for (int i = start; i < m.V.Count; i++) { min = Vector3.Min(min, m.V[i].Position); max = Vector3.Max(max, m.V[i].Position); }
        tiles.Add((start, (m.V.Count - start) / 3, new BoundingBox(min - Vector3.One, max + Vector3.One)));
    }

    // ---------- water ----------

    /// The sea of a designed map (docs/WORLDMAP.md WM2): a still sheet at sea level over every cell whose ground dips under
    /// it, deep blue far out and paler over the shelf (the still-water shading of a lake: x = 5 + how near the shore).
    /// Laid in culled blocks, as most of it is out of view.
    void Sea(Mesh m, float level)
    {
        const float C = 8;
        const int Block = 16;
        int n = (int)MathF.Ceiling(2 * t.Half / C);
        float o = -t.Half;
        var depth = new float[(n + 1) * (n + 1)];
        Parallel.For(0, n + 1, z => { for (int x = 0; x <= n; x++) depth[z * (n + 1) + x] = level - t.Height(o + x * C, o + z * C); });
        Vector2 Still(float d) => new(5 + 1.12f * (1 - Math.Clamp(d / 4.5f, 0, 1)), -10);
        Color Tone(float d) => Color.Lerp(SeaDeep, WaterShallow, 1 - Math.Clamp(d / 5, 0, 1));
        for (int bz = 0; bz < n; bz += Block)
            for (int bx = 0; bx < n; bx += Block)
            {
                int start = m.V.Count;
                for (int z = bz; z < Math.Min(n, bz + Block); z++)
                    for (int x = bx; x < Math.Min(n, bx + Block); x++)
                    {
                        float d00 = depth[z * (n + 1) + x], d10 = depth[z * (n + 1) + x + 1], d01 = depth[(z + 1) * (n + 1) + x], d11 = depth[(z + 1) * (n + 1) + x + 1];
                        if (MathF.Max(MathF.Max(d00, d10), MathF.Max(d01, d11)) <= -0.2f) continue; // dry land all round
                        Vector3 P(int ix, int iz) => new(o + ix * C, level, o + iz * C);
                        m.Raw(P(x, z), Vector3.Up, Tone(d00), Still(d00)); m.Raw(P(x + 1, z + 1), Vector3.Up, Tone(d11), Still(d11)); m.Raw(P(x + 1, z), Vector3.Up, Tone(d10), Still(d10));
                        m.Raw(P(x, z), Vector3.Up, Tone(d00), Still(d00)); m.Raw(P(x, z + 1), Vector3.Up, Tone(d01), Still(d01)); m.Raw(P(x + 1, z + 1), Vector3.Up, Tone(d11), Still(d11));
                    }
                AddTile(m, start);
            }
        // Past the map's edge the sea runs on to the horizon over the skirt's sea floor.
        const float Far = 1700, Big = 100;
        var open = new Vector2(5, -10); // still water, far from any shore
        for (float z = -t.Half - Far; z < t.Half + Far; z += Big * 8)
        {
            int start = m.V.Count;
            for (float zz = z; zz < MathF.Min(z + Big * 8, t.Half + Far); zz += Big)
                for (float x = -t.Half - Far; x < t.Half + Far; x += Big)
                {
                    if (x >= -t.Half - 0.01f && x + Big <= t.Half + 0.01f && zz >= -t.Half - 0.01f && zz + Big <= t.Half + 0.01f) continue;
                    // Only off a sea edge: a shore battlefield has its high rim on the other three sides.
                    float ex = Math.Clamp(x + Big / 2, -t.Half + 1, t.Half - 1), ez = Math.Clamp(zz + Big / 2, -t.Half + 1, t.Half - 1);
                    if (t.Height(ex, ez) > level) continue;
                    float x0 = x, z0 = zz, x1 = x + Big, z1 = zz + Big;
                    // Cells straddling the map's edge keep to the outside of it.
                    if (z0 < t.Half && z1 > -t.Half && x0 < t.Half && x1 > -t.Half)
                    {
                        if (x0 < -t.Half) x1 = -t.Half; else if (x1 > t.Half) x0 = t.Half;
                        else if (z0 < -t.Half) z1 = -t.Half; else z0 = t.Half;
                    }
                    Vector3 P(float px, float pz) => new(px, level, pz);
                    m.Raw(P(x0, z0), Vector3.Up, SeaDeep, open); m.Raw(P(x1, z1), Vector3.Up, SeaDeep, open); m.Raw(P(x1, z0), Vector3.Up, SeaDeep, open);
                    m.Raw(P(x0, z0), Vector3.Up, SeaDeep, open); m.Raw(P(x0, z1), Vector3.Up, SeaDeep, open); m.Raw(P(x1, z1), Vector3.Up, SeaDeep, open);
                }
            AddTile(m, start);
        }
    }

    static readonly Color SeaDeep = new(36, 76, 100);

    /// Rivers, lakes and the works on them.
    void Water(Mesh m)
    {
        m.Tag = Vector2.Zero;
        var rng = new Random(t.Seed + 91);
        foreach (var r in t.Rivers) RiverWater(m, r, rng);
        foreach (var l in t.Lakes) Lake(m, l, rng);
        foreach (var b in t.MapBridges) Bridge(m, b, rng);
    }

    /// A river: its surface (flow in the UVs), muddy banks rising to the land, reeds in the quiet stretches, and stepping
    /// stones across the fords that have no bridge.
    void RiverWater(Mesh m, River r, Random rng)
    {
        float half = r.Width / 2 + 1.2f, bankOut = r.Width / 2 + 4.2f + 2 * r.Widen, bank = r.Width / 2 + 3.5f + 2 * r.Widen;
        var path = r.Path;
        // The course every 2 m, its direction averaged over some metres either way: a river turned hard round a town
        // otherwise folds its ribbons over themselves at the kink, leaving flaps of bank standing out over the land.
        var smp = Resample(path, 2f);
        int count = smp.Count;
        if (count < 2) return;
        var nrm = new Vector2[count];
        var surf = new float[count];
        for (int i = 0; i < count; i++)
        {
            var d = Vector2.Zero;
            for (int j = Math.Max(0, i - 4); j <= Math.Min(count - 1, i + 4); j++) d += smp[j].D;
            d = d.LengthSquared() > 1e-6f ? Vector2.Normalize(d) : smp[i].D;
            nrm[i] = new Vector2(-d.Y, d.X);
            surf[i] = MathF.Min(t.Height(smp[i].P + nrm[i] * bank), t.Height(smp[i].P - nrm[i] * bank)) - 0.3f;
        }
        // The surface eased along the course (never above the lower bank it had), so it does not step at every sample.
        var eased = new float[count];
        for (int i = 0; i < count; i++)
        {
            float sum = 0; int k = 0;
            for (int j = Math.Max(0, i - 3); j <= Math.Min(count - 1, i + 3); j++) { sum += surf[j]; k++; }
            eased[i] = MathF.Min(surf[i], sum / k);
        }
        surf = eased;
        Vector3 V(Vector2 p, float y) => new(p.X, y, p.Y);
        // Across a bank, from the water's edge out to the grass: each point lies on the ground (or just over the water).
        float[] cols = { 0, 0.25f, 0.5f, 0.75f, 1 };
        float along = 0;
        for (int i = 0; i + 1 < count; i++)
        {
            Vector2 a = smp[i].P, b = smp[i + 1].P, na = nrm[i], nb = nrm[i + 1];
            float len = Vector2.Distance(a, b), la = surf[i], lb = surf[i + 1];
            Color ca = r.AtFord(a) ? WaterShallow : WaterDeep, cb = r.AtFord(b) ? WaterShallow : WaterDeep;
            float ya = -10 - along, yb = -10 - along - len;
            m.Raw(V(a - na * half, la), Vector3.Up, ca, new(-1, ya)); m.Raw(V(a + na * half, la), Vector3.Up, ca, new(1, ya)); m.Raw(V(b + nb * half, lb), Vector3.Up, cb, new(1, yb));
            m.Raw(V(a - na * half, la), Vector3.Up, ca, new(-1, ya)); m.Raw(V(b + nb * half, lb), Vector3.Up, cb, new(1, yb)); m.Raw(V(b - nb * half, lb), Vector3.Up, cb, new(-1, yb));
            // The banks: wet mud at the water's edge drying toward the grass, draped over the cut.
            foreach (int side in new[] { -1, 1 })
            {
                for (int c = 0; c + 1 < cols.Length; c++)
                {
                    var (pa0, ya0, ka0) = BankPoint(a, na, la, c);
                    var (pa1, ya1, ka1) = BankPoint(a, na, la, c + 1);
                    var (pb0, yb0, kb0) = BankPoint(b, nb, lb, c);
                    var (pb1, yb1, kb1) = BankPoint(b, nb, lb, c + 1);
                    m.Raw(V(pa0, ya0), NormalAt(pa0), ka0, Vector2.Zero); m.Raw(V(pa1, ya1), NormalAt(pa1), ka1, Vector2.Zero); m.Raw(V(pb1, yb1), NormalAt(pb1), kb1, Vector2.Zero);
                    m.Raw(V(pa0, ya0), NormalAt(pa0), ka0, Vector2.Zero); m.Raw(V(pb1, yb1), NormalAt(pb1), kb1, Vector2.Zero); m.Raw(V(pb0, yb0), NormalAt(pb0), kb0, Vector2.Zero);
                }
                // Reeds in the quiet water by the bank (not at fords or bridges, and not everywhere), about one stretch in two.
                if (i % 2 == 0 && !r.AtFord(a) && !r.AtBridge(a) && Terrain.Noise(a.X * 0.05f, a.Y * 0.05f, t.Seed + 31 + side) > 0.55f)
                    Reeds(m, a + na * side * (half - 0.2f + rng.NextSingle() * 1.2f) + smp[i].D * rng.NextSingle() * len * 2, la, rng);

                (Vector2 P, float Y, Color C) BankPoint(Vector2 p, Vector2 n, float lvl, int c)
                {
                    float k = cols[c];
                    var q = p + n * side * MathHelper.Lerp(half - 0.3f, bankOut, k);
                    float y = c == 0 ? lvl + 0.03f : MathF.Max(t.Height(q), lvl + 0.03f) + 0.06f;
                    var col = new Color(Vector3.Lerp(Mud, Vector3.Lerp(Shore, GroundColor(q), 0.5f + 0.5f * k * k), MathHelper.SmoothStep(0, 1, k)));
                    return (q, y, col);
                }
            }
            along += len;
        }
        // Stepping stones across each ford a road does not bridge.
        foreach (var f in r.Fords)
        {
            if (r.AtBridge(f) || t.MapBridges.Any(b => Vector2.DistanceSquared(b.At, f) < 64)) continue;
            int k = Nearest(f);
            var d = Vector2.Normalize(path[Math.Min(k + 1, path.Length - 1)] - path[Math.Max(k - 1, 0)]);
            var across = new Vector2(-d.Y, d.X);
            float level = MathF.Min(t.Height(f + across * (r.Width / 2 + 3.5f + 2 * r.Widen)), t.Height(f - across * (r.Width / 2 + 3.5f + 2 * r.Widen))) - 0.3f;
            for (float s = -half + 0.6f; s < half - 0.4f; s += 1.1f + rng.NextSingle() * 0.4f)
            {
                var p = f + across * s + d * (rng.NextSingle() - 0.5f) * 1.2f;
                var stone = Jitter(new Color(122, 118, 108), rng, 0.12f);
                m.Blob(new Vector3(p.X, level - 0.05f, p.Y), new Vector3(0.45f, 0.22f, 0.4f), stone, rng, 0.25f);
            }
        }

        int Nearest(Vector2 p)
        {
            int best = 0;
            for (int i = 1; i < path.Length; i++) if (Vector2.DistanceSquared(path[i], p) < Vector2.DistanceSquared(path[best], p)) best = i;
            return best;
        }
    }

    /// A clump of reeds standing in shallow water at `p` (water surface `level`); now and then a brown cattail head.
    void Reeds(Mesh m, Vector2 p, float level, Random rng)
    {
        int blades = 5 + rng.Next(5);
        var b0 = new Vector3(p.X, level - 0.1f, p.Y);
        for (int k = 0; k < blades; k++)
        {
            float ang = rng.NextSingle() * MathF.Tau, w = 0.05f + rng.NextSingle() * 0.04f, h = 0.7f + rng.NextSingle() * 0.6f;
            var o = new Vector3(rng.NextSingle() - 0.5f, 0, rng.NextSingle() - 0.5f) * 0.7f;
            var side = new Vector3(MathF.Cos(ang), 0, MathF.Sin(ang)) * w;
            var lean = new Vector3(rng.NextSingle() - 0.5f, 0, rng.NextSingle() - 0.5f) * 0.3f;
            var low = Jitter(ReedLow, rng, 0.1f);
            m.TriUp(b0 + o - side, b0 + o + side, b0 + o + Vector3.Up * h + lean, low, low, Jitter(ReedTip, rng, 0.1f), 0.6f);
            if (rng.Next(5) == 0)
            {
                var top = b0 + o + Vector3.Up * h * 0.85f + lean * 0.85f;
                m.TriUp(top - side * 1.4f, top + side * 1.4f, top + Vector3.Up * 0.22f, Cattail, Cattail, Cattail, 0.6f);
            }
        }
    }

    /// A lake or pond: a disc of still water a little below the lowest point of its shore, a sandy-muddy shore ring,
    /// and reeds here and there round the edge.
    void Lake(Mesh m, Lake l, Random rng)
    {
        const int Around = 36;
        float level = float.MaxValue;
        for (int i = 0; i < Around; i++)
        {
            float a = i * MathF.Tau / Around;
            level = MathF.Min(level, t.Height(l.Center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * l.Edge(a)));
        }
        level -= 0.12f;
        Vector2 Ring(int i, float k) { float a = i * MathF.Tau / Around; return l.Center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * l.Edge(a) * k; }
        Vector3 W(Vector2 p) => new(p.X, level, p.Y);
        Vector2 Still(float k) => new(5 + k, -10); // still water; x - 5 = the share of the way out to the shore
        Color edge = Color.Lerp(LakeDeep, WaterShallow, 0.8f);
        for (int i = 0; i < Around; i++)
        {
            int j = (i + 1) % Around;
            Vector2 i0 = Ring(i, 0.55f), j0 = Ring(j, 0.55f), i1 = Ring(i, 1.12f), j1 = Ring(j, 1.12f);
            m.Raw(W(l.Center), Vector3.Up, LakeDeep, Still(0)); m.Raw(W(j0), Vector3.Up, LakeDeep, Still(0.55f)); m.Raw(W(i0), Vector3.Up, LakeDeep, Still(0.55f));
            m.Raw(W(i0), Vector3.Up, LakeDeep, Still(0.55f)); m.Raw(W(j0), Vector3.Up, LakeDeep, Still(0.55f)); m.Raw(W(j1), Vector3.Up, edge, Still(1.12f));
            m.Raw(W(i0), Vector3.Up, LakeDeep, Still(0.55f)); m.Raw(W(j1), Vector3.Up, edge, Still(1.12f)); m.Raw(W(i1), Vector3.Up, edge, Still(1.12f));
            // The shore: from just under the water out onto the grass.
            Vector2 si = Ring(i, 0.95f), sj = Ring(j, 0.95f), oi = Ring(i, 1.3f), oj = Ring(j, 1.3f);
            Vector3 Si = new(si.X, MathF.Max(level + 0.02f, t.Height(si) + 0.04f), si.Y), Sj = new(sj.X, MathF.Max(level + 0.02f, t.Height(sj) + 0.04f), sj.Y);
            Vector3 Oi = new(oi.X, t.Height(oi) + 0.05f, oi.Y), Oj = new(oj.X, t.Height(oj) + 0.05f, oj.Y);
            Color wet = new(Vector3.Lerp(Mud, Shore, 0.4f)), dry = new(Vector3.Lerp(Shore, GroundColor(oi), 0.6f));
            m.Raw(Si, NormalAt(si), wet, Vector2.Zero); m.Raw(Oi, NormalAt(oi), dry, Vector2.Zero); m.Raw(Oj, NormalAt(oj), dry, Vector2.Zero);
            m.Raw(Si, NormalAt(si), wet, Vector2.Zero); m.Raw(Oj, NormalAt(oj), dry, Vector2.Zero); m.Raw(Sj, NormalAt(sj), wet, Vector2.Zero);
            if (Terrain.Noise(si.X * 0.3f, si.Y * 0.3f, l.Seed) > 0.55f) Reeds(m, Ring(i, 0.9f + rng.NextSingle() * 0.12f), level, rng);
        }
    }

    /// A bridge over a river on the map: a stone bridge (piers, a deck, low parapets) between towns, a plank bridge with
    /// rails out to a village. The deck runs level across from bank to bank.
    void Bridge(Mesh m, MapBridge b, Random rng)
    {
        float yaw = MathF.Atan2(b.Along.Y, b.Along.X), reach = b.Reach + 1.2f;
        var side = new Vector2(-b.Along.Y, b.Along.X);
        float deck = MathF.Max(t.Height(b.At - b.Along * reach), t.Height(b.At + b.Along * reach)) + 0.25f;
        float water = MathF.Min(t.Height(b.At + side * 4), t.Height(b.At - side * 4)) - 0.6f;
        Vector3 At(float s, Vector2 off, float y) { var p = b.At + b.Along * s + off; return new Vector3(p.X, y, p.Y); }
        if (b.Stone)
        {
            var stone = Jitter(BridgeStone, rng, 0.05f);
            m.Box(At(0, Vector2.Zero, deck - 0.35f), reach * 2, 3.0f, 0.35f, yaw, stone);                         // the deck
            foreach (float s in new[] { -reach * 0.45f, reach * 0.45f }) m.Box(At(s, Vector2.Zero, water), 1.0f, 2.6f, deck - water - 0.3f, yaw, new Color(stone.ToVector3() * 0.82f)); // piers
            foreach (int k in new[] { -1, 1 }) m.Box(At(0, side * k * 1.38f, deck), reach * 2, 0.26f, 0.45f, yaw, Jitter(BridgeStone, rng, 0.06f)); // parapets
        }
        else
        {
            for (float s = -reach; s < reach; s += 0.6f)
                m.Box(At(s + 0.3f, Vector2.Zero, deck - 0.18f), 0.56f, 2.2f, 0.18f, yaw, Jitter(DeckWood, rng, 0.08f)); // planks
            foreach (int k in new[] { -1, 1 })
            {
                for (float s = -reach; s <= reach + 0.01f; s += reach / 2) m.Box(At(s, side * k * 1.05f, water), 0.16f, 0.16f, deck - water + 0.55f, yaw, Jitter(FenceWood, rng, 0.08f));
                m.Box(At(0, side * k * 1.05f, deck + 0.45f), reach * 2, 0.08f, 0.08f, yaw, Jitter(FenceWood, rng, 0.06f));
            }
        }
    }
}
