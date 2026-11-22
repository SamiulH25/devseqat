using Microsoft.Xna.Framework;

namespace Ironfield;

/// What kind of stronghold an assault is fought over (docs/pre-demo/SIEGES.md SE1, SE7).
/// Ports fight as regular walled towns for the demo (owner, 2026-11-21).
public enum FortressKind : byte { WalledVillage, TowerHouse, Castle, GreatCastle, Town }

/// A gate in a fortress specification (serializable for replays).
public readonly record struct FortressGateSpec(Vector2 At, Vector2 Out, int Edge);

/// A building or camp lot in a fortress specification (serializable for replays).
public readonly record struct FortressLotSpec(Vector2 Pos, float Yaw, float W, float D, LotUse Use);

/// Plain, JSON-serializable description of a fortress for `BattleOptions.Fortress`, built either from a campaign
/// settlement's `PlacePlan` (`FromPlan`) or procedurally for quick battles, tests and `--bench` (`Procedural`).
public sealed class FortressSpec
{
    public FortressKind Kind = FortressKind.Town;
    public int WallsLevel = 1;
    public int Supplies = 30;
    public bool HasDitch;
    public Vector2[] Ring = Array.Empty<Vector2>();
    public Vector2[] InnerRing = Array.Empty<Vector2>();
    public FortressGateSpec[] Gates = Array.Empty<FortressGateSpec>();
    public Vector2 Heart;
    public FortressLotSpec[] Lots = Array.Empty<FortressLotSpec>();
    public FortressLotSpec[] CampLots = Array.Empty<FortressLotSpec>();

    /// Convert a campaign settlement's `PlacePlan` into a battle-scale `FortressSpec` (SE1):
    /// rotated so `plan.Front` faces south (-Y, toward the attackers) and translated so the south wall sits near y = 12.
    public static FortressSpec FromPlan(PlacePlan plan, Settlement s, int wallsLevel)
    {
        var kind = s.Kind switch
        {
            SettlementKind.Village => FortressKind.WalledVillage,
            SettlementKind.Town => FortressKind.Town,
            _ => s.Size switch { 1 => FortressKind.TowerHouse, 3 => FortressKind.GreatCastle, _ => FortressKind.Castle },
        };
        if (plan.Kind != SettlementKind.Town || plan.Ring.Length < 3)
        {
            var spec = Procedural(plan.Seed, kind, wallsLevel, (int)MathF.Round(s.Supplies));
            // Carry camp lots from LayCamp if present.
            if (plan.Lots.Length > 0)
            {
                float rot = -MathF.PI / 2 - plan.Front;
                float scale = kind switch { FortressKind.TowerHouse => 5.5f, FortressKind.GreatCastle => 6.8f, FortressKind.WalledVillage => 6.2f, _ => 6.4f };
                Vector2 Xform(Vector2 p)
                {
                    var d = (p - plan.Pos) * scale;
                    float c = MathF.Cos(rot), sn = MathF.Sin(rot);
                    return new Vector2(d.X * c - d.Y * sn, d.X * sn + d.Y * c) + spec.Heart;
                }
                var camp = new List<FortressLotSpec>();
                foreach (var l in plan.Lots)
                    if (l.Use is LotUse.Camp or LotUse.Fire)
                    {
                        var pos = Xform(l.Pos);
                        if (pos.Y < 8 && MathF.Abs(pos.X) < 120 && pos.Y > -120)
                            camp.Add(new FortressLotSpec(pos, l.Yaw + rot, MathF.Max(2.5f, l.W * 2.2f), MathF.Max(2.5f, l.D * 2.2f), l.Use));
                    }
                if (camp.Count > 0) spec.CampLots = camp.ToArray();
            }
            return spec;
        }

        // Town with a laid-out ring from WorldMap.LayTown: scale to ~72-85 m radius (fits cleanly inside ±256 m).
        float mapR = MathF.Max(4f, plan.Ring.Max(v => Vector2.Distance(v, plan.Pos)));
        float sc = Math.Clamp(76f / mapR, 4.8f, 7.2f);
        float angle = -MathF.PI / 2 - plan.Front;
        float cos = MathF.Cos(angle), sin = MathF.Sin(angle);
        Vector2 Rot(Vector2 v) => new(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);

        var ring = new Vector2[plan.Ring.Length];
        for (int i = 0; i < plan.Ring.Length; i++) ring[i] = Rot((plan.Ring[i] - plan.Pos) * sc);
        EnsureCcw(ring);
        float minY = ring.Min(v => v.Y);
        var shift = new Vector2(0, 12f - minY);
        for (int i = 0; i < ring.Length; i++) ring[i] += shift;

        Vector2 MapPt(Vector2 p) => Rot((p - plan.Pos) * sc) + shift;
        var heart = MapPt(plan.Square);
        if (!Fortress.PointInPoly(ring, heart)) heart = Centroid(ring);

        var gates = new List<FortressGateSpec>();
        foreach (var g in plan.Gates)
        {
            var at = MapPt(g.At);
            var outDir = Vector2.Normalize(Rot(PlacePlan.Dir(g.Out)));
            gates.Add(new FortressGateSpec(at, outDir, NearestEdge(ring, at)));
        }
        // Put the southernmost gate first (MainGate).
        gates.Sort((a, b) => a.At.Y.CompareTo(b.At.Y));
        if (gates.Count == 0) gates.Add(SouthGate(ring));

        var lots = new List<FortressLotSpec>();
        var campLots = new List<FortressLotSpec>();
        foreach (var l in plan.Lots)
        {
            if (l.W <= 0 || l.D <= 0) continue;
            var pos = MapPt(l.Pos);
            float w = Math.Clamp(l.W * 2.2f, 3.5f, 14f), d = Math.Clamp(l.D * 2.2f, 3.0f, 12f);
            if (l.Use is LotUse.Camp or LotUse.Fire)
            {
                if (pos.Y < 8 && MathF.Abs(pos.X) < 130 && pos.Y > -130)
                    campLots.Add(new FortressLotSpec(pos, l.Yaw + angle, w, d, l.Use));
                continue;
            }
            if (l.Use is LotUse.Suburb or LotUse.Works or LotUse.Engine) continue;
            // Keep buildings strictly inside the ring, clear of the wall walkway (7 m), gates (10 m) and heart (11 m).
            if (!Fortress.PointInPoly(ring, pos) || Fortress.PolyDist(ring, pos) < 7.5f + MathF.Max(w, d) * 0.45f) continue;
            if (Vector2.Distance(pos, heart) < 11f + MathF.Max(w, d) * 0.4f) continue;
            if (gates.Any(g => Vector2.Distance(pos, g.At) < 11f)) continue;
            lots.Add(new FortressLotSpec(pos, l.Yaw + angle, w, d, l.Use));
        }

        // Ensure a street corridor from each gate to the heart by dropping any lot blocking the main avenues.
        lots.RemoveAll(l => gates.Any(g => Fortress.SegDist(g.At, heart, l.Pos) < 5.5f + MathF.Max(l.W, l.D) * 0.4f));

        return new FortressSpec
        {
            Kind = kind,
            WallsLevel = wallsLevel,
            Supplies = (int)MathF.Round(s.Supplies),
            HasDitch = wallsLevel >= 2,
            Ring = ring,
            Gates = gates.ToArray(),
            Heart = heart,
            Lots = lots.ToArray(),
            CampLots = campLots.Count > 0 ? campLots.ToArray() : DefaultCamp(ring),
        };
    }

    /// Build a canonical fortress from seed and kind (used for quick battles, tests, `--bench`, and non-town settlements).
    public static FortressSpec Procedural(int seed, FortressKind kind, int wallsLevel = 1, int supplies = 30)
    {
        var r = new Random(seed ^ 0x4f07);
        return kind switch
        {
            FortressKind.TowerHouse => BuildTowerHouse(r, wallsLevel, supplies),
            FortressKind.Castle => BuildCastle(r, wallsLevel, supplies),
            FortressKind.GreatCastle => BuildGreatCastle(r, wallsLevel, supplies),
            FortressKind.WalledVillage => BuildVillage(r, wallsLevel, supplies),
            _ => BuildTown(r, wallsLevel, supplies),
        };
    }

    static FortressSpec BuildTown(Random r, int wallsLevel, int supplies)
    {
        // Octagonal walled town: radius ~74 m, centre (0, 86), south wall at y = 12.
        float rad = 74f;
        var center = new Vector2(0, 12f + rad);
        var ring = RegularRing(center, rad, 8, -MathF.PI / 2 - MathF.PI / 8, r, jitter: 3.5f);
        // Flatten south face slightly so the main approach is clean.
        float minY = ring.Min(v => v.Y);
        var shift = new Vector2(0, 12f - minY);
        for (int i = 0; i < ring.Length; i++) ring[i] += shift;
        center += shift;

        var gates = new List<FortressGateSpec> { SouthGate(ring) };
        // East and west gates on the edges closest to +X and -X.
        int eEdge = BestEdge(ring, Vector2.UnitX), wEdge = BestEdge(ring, -Vector2.UnitX);
        if (eEdge != gates[0].Edge) gates.Add(EdgeGate(ring, eEdge));
        if (wEdge != gates[0].Edge && wEdge != eEdge) gates.Add(EdgeGate(ring, wEdge));

        var heart = center;
        var lots = new List<FortressLotSpec>();
        // Landmark on the north side of the market square.
        lots.Add(new FortressLotSpec(heart + new Vector2(0, 16), 0, 10, 8, LotUse.Landmark));

        // Houses arranged in blocks along the streets, leaving 11 m avenues from every gate to the square and a 9 m wall lane.
        for (float y = center.Y - rad + 14; y <= center.Y + rad - 14; y += 13.5f)
            for (float x = -rad + 14; x <= rad - 14; x += 13.5f)
            {
                var p = new Vector2(x + (r.NextSingle() - 0.5f) * 2.2f, y + (r.NextSingle() - 0.5f) * 2.2f);
                if (!Fortress.PointInPoly(ring, p) || Fortress.PolyDist(ring, p) < 11.5f) continue;
                if (Vector2.Distance(p, heart) < 17f) continue;
                if (gates.Any(g => Fortress.SegDist(g.At, heart, p) < 8.5f)) continue;
                if (lots.Any(o => Vector2.Distance(o.Pos, p) < 9.5f)) continue;
                float w = 5.5f + r.NextSingle() * 2.5f, d = 4.2f + r.NextSingle() * 1.8f;
                float yaw = r.Next(4) * MathHelper.PiOver2;
                lots.Add(new FortressLotSpec(p, yaw, w, d, LotUse.House));
            }

        return new FortressSpec
        {
            Kind = FortressKind.Town,
            WallsLevel = wallsLevel,
            Supplies = supplies,
            HasDitch = wallsLevel >= 2,
            Ring = ring,
            Gates = gates.ToArray(),
            Heart = heart,
            Lots = lots.ToArray(),
            CampLots = DefaultCamp(ring),
        };
    }

    static FortressSpec BuildCastle(Random r, int wallsLevel, int supplies)
    {
        float rad = 46f;
        var center = new Vector2(0, 12f + rad);
        var ring = RegularRing(center, rad, 6, -MathF.PI / 2 - MathF.PI / 6, r, jitter: 1.5f);
        float minY = ring.Min(v => v.Y);
        var shift = new Vector2(0, 12f - minY);
        for (int i = 0; i < ring.Length; i++) ring[i] += shift;
        center += shift;

        var gates = new[] { SouthGate(ring) };
        var keepPos = center + new Vector2(0, 14f);
        var heart = keepPos + new Vector2(0, -10f); // keep's door in the bailey
        var lots = new List<FortressLotSpec>
        {
            new(keepPos, 0, 11, 11, LotUse.Landmark), // drawn as FieldKeep
            new(center + new Vector2(-19, -4), MathHelper.PiOver2, 7.5f, 4.5f, LotUse.House),
            new(center + new Vector2(19, -4), -MathHelper.PiOver2, 7.5f, 4.5f, LotUse.House),
            new(center + new Vector2(-17, 10), MathHelper.PiOver2, 6.5f, 4.2f, LotUse.House),
            new(center + new Vector2(17, 10), -MathHelper.PiOver2, 6.5f, 4.2f, LotUse.House),
        };

        return new FortressSpec
        {
            Kind = FortressKind.Castle,
            WallsLevel = wallsLevel,
            Supplies = supplies,
            HasDitch = wallsLevel >= 2,
            Ring = ring,
            Gates = gates,
            Heart = heart,
            Lots = lots.ToArray(),
            CampLots = DefaultCamp(ring),
        };
    }

    static FortressSpec BuildGreatCastle(Random r, int wallsLevel, int supplies)
    {
        float outerR = 64f, innerR = 32f;
        var center = new Vector2(0, 12f + outerR);
        var outer = RegularRing(center, outerR, 6, -MathF.PI / 2 - MathF.PI / 6, r, jitter: 1.5f);
        float minY = outer.Min(v => v.Y);
        var shift = new Vector2(0, 12f - minY);
        for (int i = 0; i < outer.Length; i++) outer[i] += shift;
        center += shift;

        var inner = RegularRing(center + new Vector2(0, 6f), innerR, 6, -MathF.PI / 2 - MathF.PI / 6, r, jitter: 0.8f);
        var gates = new[] { SouthGate(outer), SouthGate(inner) };
        var keepPos = center + new Vector2(0, 14f);
        var heart = keepPos + new Vector2(0, -9f);
        var lots = new List<FortressLotSpec>
        {
            new(keepPos, 0, 12, 12, LotUse.Landmark),
            new(center + new Vector2(-38, -10), MathHelper.PiOver2, 7.5f, 4.5f, LotUse.House),
            new(center + new Vector2(38, -10), -MathHelper.PiOver2, 7.5f, 4.5f, LotUse.House),
            new(center + new Vector2(-36, 12), MathHelper.PiOver2, 7.0f, 4.5f, LotUse.House),
            new(center + new Vector2(36, 12), -MathHelper.PiOver2, 7.0f, 4.5f, LotUse.House),
        };

        return new FortressSpec
        {
            Kind = FortressKind.GreatCastle,
            WallsLevel = wallsLevel,
            Supplies = supplies,
            HasDitch = wallsLevel >= 1,
            Ring = outer,
            InnerRing = inner,
            Gates = gates,
            Heart = heart,
            Lots = lots.ToArray(),
            CampLots = DefaultCamp(outer),
        };
    }

    static FortressSpec BuildTowerHouse(Random r, int wallsLevel, int supplies)
    {
        float rad = 28f;
        var center = new Vector2(0, 12f + rad);
        var ring = RegularRing(center, rad, 4, -MathF.PI / 2 - MathF.PI / 4, r, jitter: 0.6f);
        float minY = ring.Min(v => v.Y);
        var shift = new Vector2(0, 12f - minY);
        for (int i = 0; i < ring.Length; i++) ring[i] += shift;
        center += shift;

        var keepPos = center + new Vector2(0, 7f);
        var heart = keepPos + new Vector2(0, -8f);
        var lots = new[] { new FortressLotSpec(keepPos, 0, 9, 9, LotUse.Landmark) };
        return new FortressSpec
        {
            Kind = FortressKind.TowerHouse,
            WallsLevel = wallsLevel,
            Supplies = supplies,
            HasDitch = false,
            Ring = ring,
            Gates = new[] { SouthGate(ring) },
            Heart = heart,
            Lots = lots,
            CampLots = DefaultCamp(ring),
        };
    }

    static FortressSpec BuildVillage(Random r, int wallsLevel, int supplies)
    {
        float rad = 40f;
        var center = new Vector2(0, 12f + rad);
        var ring = RegularRing(center, rad, 8, -MathF.PI / 2 - MathF.PI / 8, r, jitter: 2.5f);
        float minY = ring.Min(v => v.Y);
        var shift = new Vector2(0, 12f - minY);
        for (int i = 0; i < ring.Length; i++) ring[i] += shift;
        center += shift;

        var heart = center;
        var lots = new List<FortressLotSpec>();
        for (int i = 0; i < 8; i++)
        {
            float a = i * MathF.Tau / 8 + 0.3f;
            var p = center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * 21f;
            if (Fortress.SegDist(SouthGate(ring).At, heart, p) < 7.5f) continue;
            lots.Add(new FortressLotSpec(p, a + MathF.PI / 2, 5.5f, 4.0f, LotUse.House));
        }

        return new FortressSpec
        {
            Kind = FortressKind.WalledVillage,
            WallsLevel = 0,
            Supplies = supplies,
            HasDitch = false,
            Ring = ring,
            Gates = new[] { SouthGate(ring) },
            Heart = heart,
            Lots = lots.ToArray(),
            CampLots = DefaultCamp(ring),
        };
    }

    static Vector2[] RegularRing(Vector2 center, float radius, int sides, float startAngle, Random r, float jitter)
    {
        var pts = new Vector2[sides];
        for (int i = 0; i < sides; i++)
        {
            float a = startAngle + i * MathF.Tau / sides;
            float rad = radius + (r.NextSingle() - 0.5f) * 2 * jitter;
            pts[i] = center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * rad;
        }
        EnsureCcw(pts);
        return pts;
    }

    static void EnsureCcw(Vector2[] poly)
    {
        float area = 0;
        for (int i = 0; i < poly.Length; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % poly.Length];
            area += a.X * b.Y - b.X * a.Y;
        }
        if (area < 0) Array.Reverse(poly);
    }

    static Vector2 Centroid(Vector2[] poly)
    {
        var c = Vector2.Zero;
        foreach (var v in poly) c += v;
        return c / Math.Max(1, poly.Length);
    }

    static int NearestEdge(Vector2[] ring, Vector2 p)
    {
        int best = 0;
        float bd = float.MaxValue;
        for (int i = 0; i < ring.Length; i++)
        {
            float d = Fortress.SegDist(ring[i], ring[(i + 1) % ring.Length], p);
            if (d < bd) { bd = d; best = i; }
        }
        return best;
    }

    static int BestEdge(Vector2[] ring, Vector2 dir)
    {
        int best = 0;
        float bd = float.MinValue;
        for (int i = 0; i < ring.Length; i++)
        {
            var a = ring[i];
            var b = ring[(i + 1) % ring.Length];
            var e = Vector2.Normalize(b - a);
            var outN = new Vector2(e.Y, -e.X); // CCW polygon: outward normal is right-hand
            float dot = Vector2.Dot(outN, dir);
            if (dot > bd) { bd = dot; best = i; }
        }
        return best;
    }

    static FortressGateSpec SouthGate(Vector2[] ring) => EdgeGate(ring, BestEdge(ring, -Vector2.UnitY));

    static FortressGateSpec EdgeGate(Vector2[] ring, int edge)
    {
        var a = ring[edge];
        var b = ring[(edge + 1) % ring.Length];
        var mid = (a + b) * 0.5f;
        var e = Vector2.Normalize(b - a);
        return new FortressGateSpec(mid, new Vector2(e.Y, -e.X), edge);
    }

    static FortressLotSpec[] DefaultCamp(Vector2[] ring)
    {
        float southY = ring.Min(v => v.Y);
        var camp = new List<FortressLotSpec>();
        for (int i = -3; i <= 3; i++)
        {
            if (i == 0) continue;
            camp.Add(new FortressLotSpec(new Vector2(i * 18, southY - 68 - MathF.Abs(i) * 4), 0, 4.5f, 3.5f, LotUse.Camp));
            if (i % 2 != 0) camp.Add(new FortressLotSpec(new Vector2(i * 18 + 3, southY - 63 - MathF.Abs(i) * 4), 0, 1.5f, 1.5f, LotUse.Fire));
        }
        return camp.ToArray();
    }
}

/// One straight section of fortress wall (destructible, ~10-16 m long, or a gate opening).
public sealed class WallSegment
{
    public int Id, Ring; // Ring 0 = outer, 1 = inner
    public Vector2 A, B, Mid, Along, Out;
    public float Length, Height, Yaw;
    public float Hp, MaxHp;
    public bool IsGate, IsPalisade;
    public int GateIndex = -1;
    public bool Breached => !IsGate && Hp <= 0;
}

/// A tower on the fortress ring: shoots arrows/bolts, acts as a capture point and stairs down into the town.
public sealed class WallTower
{
    public int Id, Ring;
    public Vector2 Pos, Out;
    public float Radius = 2.6f;
    public bool IsGateTower, HasStairs;
    public int GateIndex = -1;
    public int Owner = 1; // 1 = defender, 0 = attacker
    public float Capture = 1f; // 1 = defender, 0 = attacker
    public float Hp = 300, MaxHp = 300;
    public float ShootTimer;
    public bool Silenced => Hp <= 0;
}

/// A gatehouse in the fortress wall: breaks to rams/stones, or opens from inside when attackers capture it.
public sealed class FortressGate
{
    public int Id, Ring;
    public Vector2 Pos, Out, Along;
    public float HalfWidth = 3.0f;
    public float Hp, MaxHp;
    public bool Broken => Hp <= 0;
    public bool HeldOpen; // opened from inside by attackers capturing the gatehouse
    public bool Open => Broken || HeldOpen;
    public int Owner = 1;
    public float Capture = 1f;
    public float OilCooldown;
}

/// A ladder raised (or being raised) against a wall segment.
public sealed class WallLadder
{
    public int Id, SegmentId;
    public Vector2 Pos, Foot, Top, Out;
    public float HalfWidth = 1.4f;
    public float RaiseProgress = 1f; // 0..1; >= 1 is up and climbable
    public bool Up => RaiseProgress >= 1f;
    public Squad? Carrier;
}

/// A docked siege tower's bridge onto a wall segment.
public sealed class WallRamp
{
    public int SegmentId;
    public Vector2 Pos, Foot, Top, Out;
    public float HalfWidth = 2.0f;
    public Engine? Tower;
}

/// A solid rectangular building inside the fortress (house, keep, landmark): men flow through the streets between them.
public sealed class FortressBuilding
{
    public Vector2 Pos, Along, Across;
    public float Yaw, W, D, Radius;
    public LotUse Use;
    public int Seed;

    public bool Contains(Vector2 p, float pad)
    {
        var d = p - Pos;
        if (d.LengthSquared() > (Radius + pad) * (Radius + pad)) return false;
        return MathF.Abs(Vector2.Dot(d, Along)) <= W * 0.5f + pad && MathF.Abs(Vector2.Dot(d, Across)) <= D * 0.5f + pad;
    }

    public bool PushOut(ref Vector2 p, float r)
    {
        var d = p - Pos;
        float maxR = Radius + r;
        if (d.LengthSquared() >= maxR * maxR) return false;
        float lx = Vector2.Dot(d, Along), ly = Vector2.Dot(d, Across);
        float hx = W * 0.5f + r, hy = D * 0.5f + r;
        if (MathF.Abs(lx) >= hx || MathF.Abs(ly) >= hy) return false;
        float px = hx - MathF.Abs(lx), py = hy - MathF.Abs(ly);
        if (px < py) p += Along * (lx >= 0 ? px : -px);
        else p += Across * (ly >= 0 ? py : -py);
        return true;
    }
}

/// The complete headless siege fortress (docs/pre-demo/SIEGES.md SE1-SE2): closed wall ring(s) of destructible segments,
/// towers, gatehouses, dynamic ladders, siege-tower ramps, solid interior buildings with streets between them, and the heart.
public sealed class Fortress
{
    public const float WallHeight = 4.5f, PalisadeHeight = 3.2f;
    public const float WalkwayDepth = 4.2f, GateHalfWidth = 3.0f, LadderHalfWidth = 1.4f, RampHalfWidth = 2.0f;

    public readonly FortressKind Kind;
    public readonly int WallsLevel;
    public readonly int Supplies;
    public readonly bool HasDitch;
    public readonly Vector2 Center;
    public readonly float Radius;
    public readonly Vector2[] OuterRing;
    public readonly Vector2[] InnerRing;
    public readonly Vector2 Heart;
    public readonly float HeartRadius;
    public readonly List<WallSegment> Segments = new();
    public readonly List<WallTower> Towers = new();
    public readonly List<FortressGate> Gates = new();
    public readonly List<WallLadder> Ladders = new();
    public readonly List<WallRamp> Ramps = new();
    public readonly List<FortressBuilding> Buildings = new();
    public readonly List<FortressLotSpec> CampLots = new();

    /// Capture state of the heart (SE3): 1 = defender, 0 = attacker; HoldTimer counts up to 60 s when attacker holds it.
    public int HeartOwner = 1;
    public float HeartCapture = 1f;
    public float HeartHoldTimer;

    /// The primary south-facing gate (the main approach).
    public FortressGate MainGate => Gates[0];
    /// True if any outer gate is open.
    public bool AnyGateOpen => Gates.Any(g => g.Ring == 0 && g.Open);
    /// True if any outer wall segment is breached.
    public bool AnyBreach => Segments.Any(s => s.Ring == 0 && s.Breached);

    public Fortress(FortressSpec spec, int ladders, float gateHp, float wallHp, int seed)
    {
        Kind = spec.Kind;
        WallsLevel = spec.WallsLevel;
        Supplies = spec.Supplies;
        HasDitch = spec.HasDitch;
        OuterRing = spec.Ring.Length >= 3 ? spec.Ring : FortressSpec.Procedural(seed, spec.Kind, spec.WallsLevel).Ring;
        InnerRing = spec.InnerRing;
        Heart = spec.Heart;
        HeartRadius = Kind is FortressKind.Town or FortressKind.WalledVillage ? 10f : 7f;
        CampLots.AddRange(spec.CampLots);

        var c = Vector2.Zero;
        foreach (var v in OuterRing) c += v;
        Center = c / OuterRing.Length;
        Radius = OuterRing.Max(v => Vector2.Distance(v, Center));

        float segHp = wallHp > 0 ? wallHp : gateHp * 1.5f;
        float towerHp = segHp * 1.4f;
        bool palisade = Kind == FortressKind.WalledVillage;
        float h = palisade ? PalisadeHeight : WallHeight;

        // Build outer ring segments, gates and towers.
        var outerGates = spec.Gates.Where(g => PolyDist(OuterRing, g.At) < 12f).ToList();
        if (outerGates.Count == 0) outerGates.Add(new FortressGateSpec((OuterRing[0] + OuterRing[1]) * 0.5f, OutwardOf(OuterRing, 0), 0));
        BuildRing(0, OuterRing, outerGates, gateHp, segHp, towerHp, h, palisade);

        // Build inner ring for concentric great castles.
        if (InnerRing.Length >= 3)
        {
            var innerGates = spec.Gates.Where(g => PolyDist(InnerRing, g.At) < 12f).ToList();
            if (innerGates.Count == 0)
            {
                int south = 0;
                float best = float.MinValue;
                for (int i = 0; i < InnerRing.Length; i++)
                {
                    float dot = Vector2.Dot(OutwardOf(InnerRing, i), -Vector2.UnitY);
                    if (dot > best) { best = dot; south = i; }
                }
                innerGates.Add(new FortressGateSpec((InnerRing[south] + InnerRing[(south + 1) % InnerRing.Length]) * 0.5f, OutwardOf(InnerRing, south), south));
            }
            BuildRing(1, InnerRing, innerGates, gateHp * 0.85f, segHp * 0.9f, towerHp, h + 0.8f, false);
        }

        // Solid interior buildings.
        var rng = new Random(seed ^ 0x6b1d);
        foreach (var l in spec.Lots)
        {
            var along = new Vector2(MathF.Cos(l.Yaw), MathF.Sin(l.Yaw));
            var across = new Vector2(-along.Y, along.X);
            Buildings.Add(new FortressBuilding
            {
                Pos = l.Pos, Yaw = l.Yaw, W = l.W, D = l.D, Along = along, Across = across,
                Radius = MathF.Sqrt(l.W * l.W + l.D * l.D) * 0.5f, Use = l.Use, Seed = rng.Next(),
            });
        }

        // Place initial ladders on south-facing outer wall segments (away from the gate).
        PlaceInitialLadders(ladders);
    }

    void BuildRing(int ringId, Vector2[] ring, List<FortressGateSpec> ringGates, float gateHp, float segHp, float towerHp, float height, bool palisade)
    {
        for (int i = 0; i < ring.Length; i++)
        {
            var a = ring[i];
            var b = ring[(i + 1) % ring.Length];
            var diff = b - a;
            float len = diff.Length();
            if (len < 1f) continue;
            var along = diff / len;
            var outN = new Vector2(along.Y, -along.X);
            float yaw = MathF.Atan2(along.Y, along.X);

            // Corner tower at vertex a (palisade villages have no stone towers).
            if (!palisade)
            {
                var prev = ring[(i + ring.Length - 1) % ring.Length];
                var prevAlong = Vector2.Normalize(a - prev);
                var prevOut = new Vector2(prevAlong.Y, -prevAlong.X);
                var cornerOut = Vector2.Normalize(outN + prevOut + new Vector2(1e-4f, 0));
                Towers.Add(new WallTower
                {
                    Id = Towers.Count, Ring = ringId, Pos = a - cornerOut * 1.0f, Out = cornerOut,
                    HasStairs = true, Hp = towerHp, MaxHp = towerHp,
                });
            }

            // Does a gate sit on this edge?
            int gateIdx = -1;
            float gateT = -1;
            foreach (var gs in ringGates)
            {
                if (SegDist(a, b, gs.At) > 6f) continue;
                float t = Math.Clamp(Vector2.Dot(gs.At - a, along), GateHalfWidth + 4f, len - GateHalfWidth - 4f);
                if (len < GateHalfWidth * 2 + 6f) t = len * 0.5f;
                gateIdx = Gates.Count;
                gateT = t;
                var gp = a + along * t;
                Gates.Add(new FortressGate
                {
                    Id = gateIdx, Ring = ringId, Pos = gp, Out = outN, Along = along,
                    HalfWidth = GateHalfWidth, Hp = gateHp, MaxHp = gateHp,
                });
                if (!palisade)
                {
                    Towers.Add(new WallTower { Id = Towers.Count, Ring = ringId, Pos = gp - along * (GateHalfWidth + 2.2f) - outN * 0.8f, Out = outN, IsGateTower = true, GateIndex = gateIdx, HasStairs = true, Hp = towerHp, MaxHp = towerHp });
                    Towers.Add(new WallTower { Id = Towers.Count, Ring = ringId, Pos = gp + along * (GateHalfWidth + 2.2f) - outN * 0.8f, Out = outN, IsGateTower = true, GateIndex = gateIdx, HasStairs = true, Hp = towerHp, MaxHp = towerHp });
                }
                break;
            }

            void AddWallRun(float s0, float s1)
            {
                float run = s1 - s0;
                if (run < 1.5f) return;
                int pieces = Math.Max(1, (int)MathF.Round(run / 12f));
                float step = run / pieces;
                for (int k = 0; k < pieces; k++)
                {
                    var p0 = a + along * (s0 + k * step);
                    var p1 = a + along * (s0 + (k + 1) * step);
                    Segments.Add(new WallSegment
                    {
                        Id = Segments.Count, Ring = ringId, A = p0, B = p1, Mid = (p0 + p1) * 0.5f,
                        Along = along, Out = outN, Length = step, Height = height, Yaw = yaw,
                        Hp = segHp, MaxHp = segHp, IsPalisade = palisade,
                    });
                    // Intermediate tower on long runs.
                    if (!palisade && k > 0 && k % 3 == 0 && Vector2.Distance(p0, a) > 16f && Vector2.Distance(p0, b) > 16f)
                        Towers.Add(new WallTower { Id = Towers.Count, Ring = ringId, Pos = p0 - outN * 0.8f, Out = outN, HasStairs = false, Hp = towerHp, MaxHp = towerHp });
                }
            }

            if (gateIdx >= 0)
            {
                AddWallRun(0, gateT - GateHalfWidth);
                var g0 = a + along * (gateT - GateHalfWidth);
                var g1 = a + along * (gateT + GateHalfWidth);
                Segments.Add(new WallSegment
                {
                    Id = Segments.Count, Ring = ringId, A = g0, B = g1, Mid = (g0 + g1) * 0.5f,
                    Along = along, Out = outN, Length = GateHalfWidth * 2, Height = height, Yaw = yaw,
                    Hp = gateHp, MaxHp = gateHp, IsGate = true, GateIndex = gateIdx, IsPalisade = palisade,
                });
                AddWallRun(gateT + GateHalfWidth, len);
            }
            else AddWallRun(0, len);
        }
    }

    void PlaceInitialLadders(int count)
    {
        if (count <= 0) return;
        // Prefer south-facing outer wall segments (facing the attackers' deployment).
        var candidates = Segments.Where(s => s.Ring == 0 && !s.IsGate && s.Out.Y < -0.25f)
            .OrderBy(s => MathF.Abs(s.Mid.X - MainGate.Pos.X) < 10f ? 1 : 0)
            .ThenBy(s => s.Mid.Y)
            .ToList();
        if (candidates.Count == 0) candidates = Segments.Where(s => s.Ring == 0 && !s.IsGate).ToList();
        if (candidates.Count == 0) return;

        // Spread ladders left and right of the main gate.
        var picked = new List<WallSegment>();
        for (int i = 0; i < count; i++)
        {
            WallSegment? best = null;
            float bestScore = float.MinValue;
            foreach (var seg in candidates)
            {
                float minSep = picked.Count == 0 ? 100f : picked.Min(p => Vector2.Distance(p.Mid, seg.Mid));
                float gateSep = Vector2.Distance(seg.Mid, MainGate.Pos);
                if (gateSep < 9f) continue;
                float score = MathF.Min(minSep, 36f) - MathF.Abs(seg.Mid.X) * 0.12f - seg.Mid.Y * 0.25f;
                if (score > bestScore) { bestScore = score; best = seg; }
            }
            if (best == null) break;
            picked.Add(best);
            AddLadder(best, best.Mid, raised: true);
        }
    }

    /// Raise or start raising a ladder against segment `seg` at `pos` (projected onto the segment).
    public WallLadder AddLadder(WallSegment seg, Vector2 pos, bool raised = true, Squad? carrier = null)
    {
        float t = Math.Clamp(Vector2.Dot(pos - seg.A, seg.Along), 1.5f, MathF.Max(1.5f, seg.Length - 1.5f));
        var onFace = seg.A + seg.Along * t;
        var l = new WallLadder
        {
            Id = Ladders.Count,
            SegmentId = seg.Id,
            Pos = onFace,
            Foot = onFace + seg.Out * 2.4f,
            Top = onFace - seg.Out * 2.2f,
            Out = seg.Out,
            HalfWidth = LadderHalfWidth,
            RaiseProgress = raised ? 1f : 0f,
            Carrier = carrier,
        };
        Ladders.Add(l);
        return l;
    }

    /// Dock a siege tower's ramp onto segment `seg` at `pos`.
    public WallRamp AddRamp(WallSegment seg, Vector2 pos, Engine? tower = null)
    {
        float t = Math.Clamp(Vector2.Dot(pos - seg.A, seg.Along), 2f, MathF.Max(2f, seg.Length - 2f));
        var onFace = seg.A + seg.Along * t;
        var r = new WallRamp
        {
            SegmentId = seg.Id,
            Pos = onFace,
            Foot = onFace + seg.Out * 2.6f,
            Top = onFace - seg.Out * 2.2f,
            Out = seg.Out,
            HalfWidth = RampHalfWidth,
            Tower = tower,
        };
        Ramps.Add(r);
        return r;
    }

    // ---------- geometry queries ----------

    public bool InsideOuter(Vector2 p) => Vector2.DistanceSquared(p, Center) <= (Radius + 4f) * (Radius + 4f) && PointInPoly(OuterRing, p);
    public bool InsideInner(Vector2 p) => InnerRing.Length >= 3 && PointInPoly(InnerRing, p);
    public bool Inside(Vector2 p) => InsideOuter(p);

    /// True when p stands on a non-breached wall walkway (0.2..WalkwayDepth behind the outer face).
    public bool OnWalkway(Vector2 p, out WallSegment? onSeg)
    {
        onSeg = null;
        foreach (var s in Segments)
        {
            if (s.IsGate || s.Breached) continue;
            float along = Vector2.Dot(p - s.A, s.Along);
            if (along < -0.5f || along > s.Length + 0.5f) continue;
            float back = -Vector2.Dot(p - s.A, s.Out); // positive inside the wall
            if (back >= 0.2f && back <= WalkwayDepth) { onSeg = s; return true; }
        }
        return false;
    }

    /// Terrain vertex height boost for the fortress wall walkways (smooth inner bank, sheer outer face, zero at gates).
    public float Raise(float x, float z)
    {
        var p = new Vector2(x, z);
        if (Vector2.DistanceSquared(p, Center) > (Radius + 14f) * (Radius + 14f)) return 0;
        // Never raise inside a gate opening.
        foreach (var g in Gates)
        {
            float along = MathF.Abs(Vector2.Dot(p - g.Pos, g.Along));
            float perp = MathF.Abs(Vector2.Dot(p - g.Pos, g.Out));
            if (along < g.HalfWidth + 0.5f && perp < WalkwayDepth + 2.5f) return 0;
        }
        float best = 0;
        foreach (var s in Segments)
        {
            if (s.IsGate) continue;
            float along = Vector2.Dot(p - s.A, s.Along);
            if (along < -1.2f || along > s.Length + 1.2f) continue;
            float back = -Vector2.Dot(p - s.A, s.Out); // 0 at outer face, positive inward
            if (back < 0.8f || back > WalkwayDepth + 3.2f) continue;
            float h = back <= WalkwayDepth ? s.Height : s.Height * Math.Clamp(1f - (back - WalkwayDepth) / 3.2f, 0, 1);
            if (h > best) best = h;
        }
        return best;
    }

    /// True if p is in the dry ditch / moat outside the outer wall (not on a gate causeway).
    public bool InDitch(Vector2 p)
    {
        if (!HasDitch) return false;
        if (InsideOuter(p)) return false;
        float d = PolyDist(OuterRing, p);
        if (d < 3.5f || d > 9.5f) return false;
        foreach (var g in Gates)
            if (g.Ring == 0 && MathF.Abs(Vector2.Dot(p - g.Pos, g.Along)) < g.HalfWidth + 2.5f && Vector2.Dot(p - g.Pos, g.Out) > 0)
                return false; // causeway in front of gate
        return true;
    }

    /// Does the step a -> b cross any fortress wall segment?
    public bool CrossesWall(Vector2 a, Vector2 b, out WallSegment? hit)
    {
        hit = null;
        foreach (var s in Segments)
            if (Terrain.SegmentsCross(a, b, s.A, s.B)) { hit = s; return true; }
        return false;
    }

    /// Can a step a -> b pass through segment `s` for this unit (foot/horse, team)?
    public bool CanPassSegment(WallSegment s, Vector2 mid, bool horse, int team, out bool isClimb)
    {
        isClimb = false;
        if (s.IsGate) return Gates[s.GateIndex].Open;
        if (s.Breached) return true; // both foot and horse can pass a breach (horse slower)
        if (!horse)
        {
            foreach (var r in Ramps)
                if (r.SegmentId == s.Id && Vector2.Distance(mid, r.Pos) <= r.HalfWidth + 0.8f) { isClimb = true; return true; }
            if (team == 0)
                foreach (var l in Ladders)
                    if (l.Up && l.SegmentId == s.Id && Vector2.Distance(mid, l.Pos) <= l.HalfWidth + 0.8f) { isClimb = true; return true; }
        }
        return false;
    }

    /// Unified wall-crossing rule for movement enforcement (SE2).
    public bool MayCross(Unit u, Vector2 from, Vector2 to, out bool isClimb)
    {
        isClimb = false;
        foreach (var s in Segments)
        {
            if (!Terrain.SegmentsCross(from, to, s.A, s.B)) continue;
            var mid = (from + to) * 0.5f;
            if (!CanPassSegment(s, mid, u.Mounted, u.Team, out bool c)) return false;
            isClimb |= c;
        }
        return true;
    }

    /// Push a circle of radius r out of any solid interior building.
    public bool PushOutBuildings(ref Vector2 p, float r)
    {
        bool hit = false;
        foreach (var b in Buildings)
            if (b.PushOut(ref p, r)) hit = true;
        return hit;
    }

    /// Is p inside (or within pad of) any solid building?
    public bool InBuilding(Vector2 p, float pad = 0.5f)
    {
        foreach (var b in Buildings)
            if (b.Contains(p, pad)) return true;
        return false;
    }

    /// Nearest non-gate wall segment to p (within maxDist).
    public WallSegment? NearestSegment(Vector2 p, float maxDist = float.MaxValue, int ring = -1)
    {
        WallSegment? best = null;
        float bd = maxDist;
        foreach (var s in Segments)
        {
            if (s.IsGate || (ring >= 0 && s.Ring != ring)) continue;
            float d = SegDist(s.A, s.B, p);
            if (d < bd) { bd = d; best = s; }
        }
        return best;
    }

    /// Nearest gate to p (optionally filtered by ring).
    public FortressGate? NearestGate(Vector2 p, int ring = -1)
    {
        FortressGate? best = null;
        float bd = float.MaxValue;
        foreach (var g in Gates)
        {
            if (ring >= 0 && g.Ring != ring) continue;
            float d = Vector2.Distance(p, g.Pos);
            if (d < bd) { bd = d; best = g; }
        }
        return best;
    }

    /// Nearest non-silenced tower to p.
    public WallTower? NearestTower(Vector2 p, float maxDist = float.MaxValue)
    {
        WallTower? best = null;
        float bd = maxDist;
        foreach (var t in Towers)
        {
            float d = Vector2.Distance(p, t.Pos);
            if (d < bd) { bd = d; best = t; }
        }
        return best;
    }

    /// Point-in-polygon test (ray cast).
    public static bool PointInPoly(Vector2[] poly, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if ((poly[i].Y > p.Y) != (poly[j].Y > p.Y) && p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X)
                inside = !inside;
        return inside;
    }

    public static float PolyDist(Vector2[] poly, Vector2 p)
    {
        float best = float.MaxValue;
        for (int i = 0; i < poly.Length; i++) best = MathF.Min(best, SegDist(poly[i], poly[(i + 1) % poly.Length], p));
        return best;
    }

    public static float SegDist(Vector2 a, Vector2 b, Vector2 p)
    {
        var ab = b - a;
        float l2 = ab.LengthSquared();
        float k = l2 < 1e-6f ? 0 : Math.Clamp(Vector2.Dot(p - a, ab) / l2, 0, 1);
        return Vector2.Distance(p, a + ab * k);
    }

    static Vector2 OutwardOf(Vector2[] ring, int edge)
    {
        var e = Vector2.Normalize(ring[(edge + 1) % ring.Length] - ring[edge]);
        return new Vector2(e.Y, -e.X);
    }
}
