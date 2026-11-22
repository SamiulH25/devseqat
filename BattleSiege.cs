using Microsoft.Xna.Framework;

namespace Ironfield;

/// Assaults on walled towns, castles and villages (docs/pre-demo/SIEGES.md SE1-SE5). Team 1 holds the fortress (`Terrain.Wall`);
/// team 0 scales the walls by ladder or siege tower, breaches segments, or forces the gates, then fights through the streets to the heart.
public sealed partial class Battle
{
    /// Counts changes to the fortress: a gate opening, a breach, a ladder raised/pushed, a tower docking or changing hands.
    public int WallVersion;
    readonly Dictionary<Squad, Vector2> posts = new(); // where each defending company stands guard
    readonly Dictionary<Squad, int> defLayer = new();  // 0 = wall/gate, 1 = street mouth, 2 = heart
    Squad? defReserve;
    float ramSound;
    bool ramPhase;
    float siegeTickTimer;
    float stalledHorseTimer;

    public Fortress? Wall => Terrain.Wall;

    /// Time limit for a siege assault in seconds (SE3, owner decision 2026-11-21): 20-35 minutes by size, less when starving.
    public float SiegeTimeLimit
    {
        get
        {
            if (Wall is not Fortress w) return 0;
            float baseSec = w.Kind switch
            {
                FortressKind.WalledVillage or FortressKind.TowerHouse => 1200f,
                FortressKind.Castle => 1500f,
                FortressKind.GreatCastle => 1800f,
                _ => 2100f,
            };
            float supplyFactor = w.Supplies < 15 ? 0.75f + 0.25f * (w.Supplies / 15f) : 1f;
            return baseSec * supplyFactor;
        }
    }

    public float SiegeTimeLeft => Wall != null ? MathF.Max(0, SiegeTimeLimit - Time) : 0;

    /// Defenders: archers on the south-facing walkways; foot at the main gate, ladder tops and heart reserve; horse at the heart.
    void DeployDefenders(List<Company> onField)
    {
        var w = Wall!;
        var mainGate = w.MainGate;
        var guardPosts = new List<(Vector2 Pos, Vector2 Front)>
        {
            (mainGate.Pos - mainGate.Out * 7f, mainGate.Out),
        };
        foreach (var l in w.Ladders)
            guardPosts.Add((l.Top - l.Out * 2.5f, l.Out));
        guardPosts.Add((w.Heart, -Vector2.UnitY));

        // Walkways facing the attackers for archers.
        var archerSegs = w.Segments.Where(s => s.Ring == 0 && !s.IsGate && s.Out.Y < -0.2f)
            .OrderBy(s => MathF.Abs(s.Mid.X - mainGate.Pos.X))
            .ToList();
        if (archerSegs.Count == 0) archerSegs = w.Segments.Where(s => s.Ring == 0 && !s.IsGate).ToList();

        // Starting morale from supplies and walls (SE3).
        float moraleMod = (w.Supplies < 10 ? -12f : w.Supplies < 20 ? -5f : 0f) + (w.WallsLevel >= 2 ? 6f : 0f);

        int melee = 0, archIdx = 0;
        foreach (var c in onField)
        {
            var t = Troops.Get(c.Troop);
            string role = Ironfield.Armies.RoleOf(c);
            Vector2 at, front = -Vector2.UnitY;
            int cols;
            int layer = 0;
            if (role == "archers" && archerSegs.Count > 0)
            {
                var seg = archerSegs[archIdx++ % archerSegs.Count];
                at = seg.Mid - seg.Out * 2.1f;
                front = seg.Out;
                cols = Math.Min(c.Count, Math.Max(4, (int)(seg.Length / 1.3f)));
            }
            else if (role == "cavalry")
            {
                at = w.Heart + new Vector2((melee++ % 2 == 0 ? -1 : 1) * 6f, -3f);
                front = -Vector2.UnitY;
                cols = Math.Min(c.Count, 6);
                layer = 2;
            }
            else
            {
                var gp = guardPosts[melee++ % guardPosts.Count];
                at = gp.Pos;
                front = gp.Front;
                cols = Math.Min(c.Count, 8);
                if (Vector2.Distance(at, w.Heart) < 8f) layer = 2;
            }
            var s = AddSquad(1, t, c.Count, at, cols, front, c);
            if (moraleMod != 0) foreach (var u in s.Units) u.Morale = Math.Clamp(u.Morale + moraleMod, 15, 100);
            Role(1, role).Add(s);
            lateral[s] = 0;
            posts[s] = at;
            defLayer[s] = layer;
            if (defReserve == null && (role == "cavalry" || layer == 2)) defReserve = s;
        }
        eGuard.Anchor = w.Heart + new Vector2(0, 5f);
        eGuard.Front = -Vector2.UnitY;
        foreach (var u in eGuard.Units) u.Pos = eGuard.Anchor;
        MountWallEngines();
    }

    /// Unified routing for field battles and fortress sieges (SE2): uses the company's flow field over the fortress;
    /// when no passage is open yet, attacking foot head for the nearest closed gate to batter it.
    Vector2 Via(Unit u, Vector2 dest)
    {
        var w = Wall;
        if (w == null) return PathVia(u, dest);
        var f = u.Squad?.Path;
        byte mode = PathMode(u.Mounted, u.Team);
        if (f != null && !u.Routing && f.Mode == mode && Vector2.DistanceSquared(dest, f.Goal) <= 30 * 30)
        {
            float cost = f.CostFrom(u.Pos);
            if (!float.IsPositiveInfinity(cost))
            {
                if (Vector2.DistanceSquared(u.Pos, dest) < PathNear * PathNear && !w.CrossesWall(u.Pos, dest, out _))
                    return dest;
                var d = f.Dir(u.Pos);
                if (d != Vector2.Zero) return u.Pos + d * 3f;
            }
        }
        // Fallback if the flow field isn't built yet or the fortress is completely sealed.
        bool inU = w.Inside(u.Pos), inD = w.Inside(dest);
        if (inU == inD && !w.CrossesWall(u.Pos, dest, out _)) return dest;
        if (u.Team == 0 && !u.Mounted && !inU && inD)
        {
            Vector2? bestPt = null;
            float bestD = float.MaxValue;
            void Consider(Vector2 near, Vector2 far, float pen)
            {
                float d = Vector2.Distance(u.Pos, near) + Vector2.Distance(far, dest) + pen;
                if (d < bestD) { bestD = d; bestPt = Vector2.Distance(u.Pos, near) < 2.2f ? far : near; }
            }
            foreach (var g in w.Gates.Where(g => g.Ring == 0))
            {
                var near = g.Pos + g.Out * 1.5f;
                var far = g.Pos - g.Out * 2.5f;
                Consider(near, g.Open ? far : near, g.Open ? 0 : 28f + 0.08f * g.Hp);
            }
            foreach (var l in w.Ladders.Where(l => l.Up))
                Consider(l.Foot, l.Top, 4f);
            foreach (var r in w.Ramps)
                Consider(r.Foot, r.Top, 2f);
            foreach (var s in w.Segments.Where(s => s.Ring == 0 && s.Breached))
                Consider(s.Mid + s.Out * 2f, s.Mid - s.Out * 2.5f, 0);
            if (bestPt is Vector2 pt) return pt;
        }
        return PathVia(u, dest);
    }

    void EnforceWall(Vector2[] before)
    {
        if (Wall == null) return;
        ladderSound = MathF.Max(0, ladderSound - 1 / 60f);
        for (int i = 0; i < Units.Count; i++)
        {
            var u = Units[i];
            if (!u.Active || Airborne(u)) continue;
            if (Wall.MayCross(u, before[i], u.Pos, out bool climb))
            {
                if (climb && ladderSound <= 0)
                {
                    ladderSound = 1.6f;
                    Sound(Cue.LadderHook, u.Pos);
                }
                continue;
            }
            u.Pos = before[i];
            if (u.Mounted) u.Speed *= 0.5f;
        }
    }
    float ladderSound;

    /// Per-tick siege systems (SE3-SE5): capture points, tower shooting, gate oil/stones, dynamic ladders.
    void UpdateSiege(float dt)
    {
        var w = Wall;
        if (w == null || Winner >= 0) return;

        UpdateLadders(dt);
        UpdateTowers(dt);
        UpdateGateDefence(dt);

        if ((siegeTickTimer -= dt) <= 0)
        {
            siegeTickTimer = 0.5f;
            UpdatePoints(0.5f);
        }

        // Check if only mounted attackers remain outside a sealed fortress with no foot or engines left to breach.
        if (!w.AnyGateOpen && !w.AnyBreach)
        {
            bool anyFootOrEngine = Units.Any(u => u.Team == 0 && u.Active && !u.Routing && !u.Mounted && !u.IsLeader)
                || Engines.Any(e => e.Team == 0 && !e.Wrecked && e.Crew.Alive > 0 && (e.Def.Shot is "ram" or "stone"));
            if (!anyFootOrEngine && Units.Any(u => u.Team == 0 && u.Active && !u.Routing && u.Mounted && !w.Inside(u.Pos)))
                stalledHorseTimer += dt;
            else stalledHorseTimer = 0;
        }
        else stalledHorseTimer = 0;
    }

    /// Order a foot company to raise a ladder against a wall segment (SE2, SE5).
    public WallLadder? RaiseLadder(Squad carrier, WallSegment seg, Vector2 at)
    {
        if (Wall == null || carrier.Cavalry || seg.IsGate || seg.Breached) return null;
        var l = Wall.AddLadder(seg, at, raised: false, carrier: carrier);
        Log($"{carrier.Name} bring up a ladder!");
        return l;
    }

    /// Update ladders being raised: carriers raise them over 4 s; defenders at the top can push them off before they lock in.
    void UpdateLadders(float dt)
    {
        var w = Wall!;
        for (int i = w.Ladders.Count - 1; i >= 0; i--)
        {
            var l = w.Ladders[i];
            if (l.Up) continue;
            int raisers = 0, pushers = 0;
            foreach (var u in Units)
            {
                if (!u.Active || u.Routing || u.Mounted) continue;
                if (u.Team == 0 && Vector2.DistanceSquared(u.Pos, l.Foot) < 16f) raisers++;
                else if (u.Team == 1 && Vector2.DistanceSquared(u.Pos, l.Top) < 12f) pushers++;
            }
            if (pushers > 0 && raisers <= pushers * 2)
            {
                l.RaiseProgress -= 0.35f * dt;
                if (l.RaiseProgress < -0.5f)
                {
                    w.Ladders.RemoveAt(i);
                    Log("Defenders thrust a ladder off the wall!");
                    Sound(Cue.Crash, l.Foot);
                    continue;
                }
            }
            else if (raisers > 0)
            {
                l.RaiseProgress += MathF.Min(1f, raisers / 4f) * 0.25f * dt;
                if (l.Up)
                {
                    l.RaiseProgress = 1f;
                    WallVersion++;
                    InvalidatePath();
                    Log("A ladder is up against the wall!");
                    Sound(Cue.LadderHook, l.Pos, priority: true);
                }
            }
        }
    }

    /// Capture points (SE3): gatehouses, towers, and the heart.
    void UpdatePoints(float dt)
    {
        var w = Wall!;
        (int Att, int Def) CountNear(Vector2 pt, float radius, bool insideOnly = true)
        {
            int a = 0, d = 0;
            float r2 = radius * radius;
            foreach (var u in Units)
            {
                if (!u.Active || u.Routing || (insideOnly && !w.Inside(u.Pos)) || Vector2.DistanceSquared(u.Pos, pt) > r2) continue;
                if (u.Team == 0) a++; else d++;
            }
            return (a, d);
        }

        // Gatehouses: holding opens/shuts the gate.
        foreach (var g in w.Gates)
        {
            var insidePt = g.Pos - g.Out * 3.5f;
            var (att, def) = CountNear(insidePt, 8f);
            if (att > def && att >= 3)
            {
                g.Capture = MathF.Max(0, g.Capture - 0.12f * dt);
                if (g.Capture <= 0 && g.Owner != 0)
                {
                    g.Owner = 0;
                    if (!g.Open) { g.HeldOpen = true; GateGivesWay(g); }
                    else { WallVersion++; Sound(Cue.Horn, g.Pos); }
                }
            }
            else if (def > att && def >= 2)
            {
                g.Capture = MathF.Min(1, g.Capture + 0.12f * dt);
                if (g.Capture >= 1 && g.Owner != 1)
                {
                    g.Owner = 1;
                    if (g.HeldOpen && !g.Broken) { g.HeldOpen = false; WallVersion++; InvalidatePath(); Log("The defenders retake the gatehouse and shut the gate!"); }
                }
            }
        }

        // Towers: holding flips who the tower shoots for.
        foreach (var tw in w.Towers)
        {
            if (tw.Silenced) continue;
            var (att, def) = CountNear(tw.Pos, 6.5f);
            if (att > def && att >= 3)
            {
                tw.Capture = MathF.Max(0, tw.Capture - 0.14f * dt);
                if (tw.Capture <= 0 && tw.Owner != 0)
                {
                    tw.Owner = 0;
                    WallVersion++;
                    Log("A wall tower has fallen to the attackers!");
                    Sound(Cue.Horn, tw.Pos);
                }
            }
            else if (def > att && def >= 2)
            {
                tw.Capture = MathF.Min(1, tw.Capture + 0.14f * dt);
                if (tw.Capture >= 1 && tw.Owner != 1)
                {
                    tw.Owner = 1;
                    WallVersion++;
                }
            }
        }

        // The Heart: holding for 60 s wins the assault.
        var (hAtt, hDef) = CountNear(w.Heart, w.HeartRadius);
        if (hAtt > hDef && hAtt >= 3)
        {
            w.HeartCapture = MathF.Max(0, w.HeartCapture - 0.08f * dt);
            if (w.HeartCapture <= 0)
            {
                if (w.HeartOwner != 0)
                {
                    w.HeartOwner = 0;
                    Log("The heart of the fortress is taken! Hold it!");
                    Sound(Cue.Horn, w.Heart, priority: true);
                    foreach (var u in Units) if (u.Team == 1 && u.Active) u.Morale -= 18;
                }
                w.HeartHoldTimer += dt;
            }
        }
        else if (hDef > hAtt)
        {
            w.HeartCapture = MathF.Min(1, w.HeartCapture + 0.08f * dt);
            if (w.HeartCapture >= 1 && w.HeartOwner != 1)
            {
                w.HeartOwner = 1;
                Log("The defenders reclaim the heart!");
            }
            w.HeartHoldTimer = MathF.Max(0, w.HeartHoldTimer - dt * 0.5f);
        }
        else if (w.HeartOwner == 0 && hAtt > 0 && hDef == 0)
        {
            w.HeartHoldTimer += dt;
        }
    }

    /// Towers shoot arrows (or springald bolts at walls level >= 2) at enemies in reach (SE4).
    void UpdateTowers(float dt)
    {
        var w = Wall!;
        if (w.Towers.Count == 0) return;
        float range = w.WallsLevel >= 2 ? 62f : 48f;
        float dmg = w.WallsLevel >= 2 ? 26f : 14f;
        float reload = w.WallsLevel >= 2 ? 3.2f : 2.6f;

        foreach (var tw in w.Towers)
        {
            if (tw.Silenced) continue;
            if ((tw.ShootTimer -= dt) > 0) continue;
            tw.ShootTimer = reload * (0.85f + 0.3f * rng.NextSingle());
            // Find nearest enemy in reach (only shoot if the tower's side has men near the wall or defender owns it).
            Unit? target = null;
            float best = range * range;
            foreach (var u in Units)
            {
                if (!u.Active || u.Team == tw.Owner) continue;
                float d2 = Vector2.DistanceSquared(u.Pos, tw.Pos);
                if (d2 < best) { best = d2; target = u; }
            }
            if (target == null) continue;
            var shooter = tw.Owner == 1 ? EnemyCaptain : Player;
            float dist = MathF.Sqrt(best);
            float flight = dist / 28f;
            float y0 = Terrain.Height(tw.Pos) + 7.5f;
            float y1 = Terrain.Height(target.Pos) + 1.2f;
            var aim = target.Pos + target.Vel * flight + new Vector2((rng.NextSingle() - 0.5f) * 2.2f, (rng.NextSingle() - 0.5f) * 2.2f);
            var h = (aim - tw.Pos) / MathF.Max(0.2f, flight);
            Arrows.Add(new Projectile
            {
                Shooter = shooter,
                Pos = new(tw.Pos.X, y0, tw.Pos.Y),
                Vel = new(h.X, 0.5f * Gravity * flight + (y1 - y0) / MathF.Max(0.2f, flight), h.Y),
                Damage = dmg,
                Kind = w.WallsLevel >= 2 ? Grip.Crossbow : Grip.Bow,
            });
            Sound(w.WallsLevel >= 2 ? Cue.CrossbowSnap : Cue.Bow, tw.Pos);
        }
    }

    /// Burning oil and dropped stones over closed defender-held gates (SE4); a ram's hide roof shelters its crew.
    void UpdateGateDefence(float dt)
    {
        var w = Wall!;
        foreach (var g in w.Gates)
        {
            if (g.Open || g.Owner != 1) continue;
            if ((g.OilCooldown -= dt) > 0) continue;
            var outer = g.Pos + g.Out * 2.0f;
            var victims = new List<Unit>();
            foreach (var u in Units)
                if (u.Team == 0 && u.Active && !u.Routing && Vector2.DistanceSquared(u.Pos, outer) < 16f)
                    victims.Add(u);
            if (victims.Count == 0) continue;
            // Require at least one defender near the gatehouse to pour oil/drop stones.
            if (!Units.Any(u => u.Team == 1 && u.Active && !u.Routing && Vector2.DistanceSquared(u.Pos, g.Pos) < 144f)) continue;
            g.OilCooldown = w.WallsLevel >= 2 ? 5.0f : 7.0f;
            Flash("oil", outer, 3.2f, 1, "");
            Sound(Cue.Impact, outer);
            foreach (var v in victims)
            {
                float dmg = (w.WallsLevel >= 2 ? 14f : 7f) * (Sheltered(v) ? 0.25f : 1f);
                Damage(EnemyCaptain, v, dmg, 4f, 0.5f, ranged: true);
            }
        }
    }

    /// Siege-specific victory & timeout checks (SE3).
    bool CheckSiegeEnd()
    {
        var w = Wall;
        if (w == null) return false;
        if (w.HeartOwner == 0 && w.HeartHoldTimer >= 60f)
        {
            Winner = 0;
            Log("Victory! The heart of the fortress has fallen!");
            return true;
        }
        if (Time >= SiegeTimeLimit)
        {
            Winner = 1;
            Log("Defeat. The garrison held until nightfall.");
            return true;
        }
        if (stalledHorseTimer >= 12f)
        {
            Winner = 1;
            Log("The assault is repulsed — no foot remain to breach the walls.");
            return true;
        }
        return false;
    }

    /// Attackers on foot standing at a closed gate batter it down.
    void Batter(float dt)
    {
        var w = Wall;
        if (w == null) return;
        foreach (var g in w.Gates)
        {
            if (g.Open) continue;
            int rams = 0;
            var outer = g.Pos + g.Out * 1.4f;
            float reach2 = (g.HalfWidth + 1.8f) * (g.HalfWidth + 1.8f);
            foreach (var u in Units)
                if (u.Team == 0 && u.Active && !u.Routing && !u.Mounted && !u.IsPlayer
                    && Vector2.DistanceSquared(u.Pos, outer) < reach2 && Vector2.Dot(u.Pos - g.Pos, g.Out) > -0.3f)
                {
                    rams++;
                    u.Face(-g.Out);
                }
            if (Player.Active && !Player.Mounted && Vector2.DistanceSquared(Player.Pos, outer) < reach2
                && Vector2.Dot(Player.Pos - g.Pos, g.Out) > -0.3f && Player.Action == UnitAction.Recover)
                rams += 2;
            if (rams == 0) continue;
            g.Hp = MathF.Max(0, g.Hp - MathF.Min(12, rams) * 5 * dt);
            if ((ramSound -= dt) <= 0)
            {
                ramSound = 0.65f;
                ramPhase = !ramPhase;
                Sound(ramPhase ? Cue.Ram : Cue.RamSwing, g.Pos);
            }
            if (g.Open) GateGivesWay(g);
        }
    }

    /// A gate breaks (battered, or a stone from an engine) or is opened from inside: the defenders lose heart.
    internal void GateGivesWay(FortressGate g)
    {
        WallVersion++;
        InvalidatePath();
        Log(g.Broken ? "The gate gives way!" : "The gate is thrown open!");
        Sound(Cue.WallFall, g.Pos);
        Sound(Cue.Horn, g.Pos);
        foreach (var u in Units) if (u.Team == 1 && u.Active) u.Morale -= 10;
    }

    /// A section of the wall falls to stones or a sap: the defenders lose heart and the nearest foot company moves to plug it.
    internal void WallBreached(WallSegment seg)
    {
        WallVersion++;
        InvalidatePath();
        Log("The wall is breached!");
        Sound(Cue.WallFall, seg.Mid, priority: true);
        Sound(Cue.Horn, seg.Mid);
        foreach (var u in Units) if (u.Team == 1 && u.Active) u.Morale -= 6;
        var post = seg.Mid - seg.Out * 6f;
        var foot = SquadsOf(1).Where(s => s != eGuard && s.Works == null && s.Alive > 0 && !s.Cavalry && !s.Units.Any(u => u.Active && u.Ammo > 0) && posts.ContainsKey(s))
            .OrderBy(s => Vector2.Distance(posts[s], post)).FirstOrDefault();
        if (foot != null) posts[foot] = post;
    }

    /// Springalds on the south-facing walkways (BattleOptions.WallEngines, from the walls' level), each worked by a few townsmen.
    void MountWallEngines()
    {
        if (Options.WallEngines <= 0) return;
        var w = Wall!;
        var segs = w.Segments.Where(s => s.Ring == 0 && !s.IsGate && s.Out.Y < -0.2f && Vector2.Distance(s.Mid, w.MainGate.Pos) > 10f)
            .OrderBy(s => MathF.Abs(s.Mid.X - w.MainGate.Pos.X))
            .ToList();
        if (segs.Count == 0) segs = w.Segments.Where(s => s.Ring == 0 && !s.IsGate).ToList();
        if (segs.Count == 0) return;
        for (int k = 0; k < Options.WallEngines; k++)
        {
            var seg = segs[k % segs.Count];
            var at = seg.Mid - seg.Out * 2.0f;
            var def = Ironfield.Engines.Get("springald");
            var crew = AddSquad(1, Troops.Get("levies"), def.Crew, at - seg.Out * 2.2f, def.Crew, seg.Out);
            crew.Order = Order.Hold;
            AddEngine(1, "springald", at, seg.Out, crew);
        }
    }

    /// The defenders' general (SE4): hold the wall, fall back in layers (wall -> street mouth -> heart) when outmatched,
    /// and dispatch the reserve to whichever point is under the heaviest pressure.
    void SiegeDefend()
    {
        var w = Wall!;
        var inside = Units.Where(u => u.Team == 0 && u.Active && !u.Routing && w.Inside(u.Pos)).ToList();
        bool heartThreatened = inside.Any(u => Vector2.Distance(u.Pos, w.Heart) < w.HeartRadius + 16f);

        foreach (var s in SquadsOf(1))
        {
            if (s == eGuard || s.Alive == 0 || s.Works != null) continue;
            var post = posts.TryGetValue(s, out var p) ? p : w.MainGate.Pos - w.MainGate.Out * 7f;
            int layer = defLayer.TryGetValue(s, out int l) ? l : 0;
            bool archers = s.Units.Any(u => u.Active && u.Ammo > 0);

            // Layered fallback: if the heart is threatened or a wall company is badly outnumbered inside, fall back toward the heart.
            if (heartThreatened && layer < 2 && Vector2.Distance(s.Center, w.Heart) > 20f)
            {
                defLayer[s] = 2;
                posts[s] = w.Heart + new Vector2((s.Id % 3 - 1) * 5f, -4f);
                post = posts[s];
            }
            else if (layer == 0 && !archers)
            {
                int nearFoes = inside.Count(u => Vector2.Distance(u.Pos, post) < 16f);
                if (nearFoes > s.Alive * 1.8f && s.Alive < s.StartCount * 0.55f)
                {
                    defLayer[s] = 1;
                    posts[s] = Vector2.Lerp(post, w.Heart, 0.5f); // street mouth
                    post = posts[s];
                }
            }

            // Reserve: sent to the hottest breach/gate/heart threat.
            if (s == defReserve && inside.Count > 0)
            {
                var hot = inside.OrderBy(u => Vector2.Distance(u.Pos, w.Heart)).First();
                if (hot.Squad != null) { s.Order = Order.Charge; s.Target = hot.Squad; continue; }
            }

            if (archers)
            {
                if (inside.Any(u => Vector2.Distance(u.Pos, s.Center) < 10f) && layer < 2)
                {
                    defLayer[s] = 2;
                    posts[s] = w.Heart + new Vector2((s.Id % 2 == 0 ? -8f : 8f), 6f);
                    post = posts[s];
                }
                s.Order = Order.Hold;
                s.Anchor = post;
                continue;
            }

            bool horse = s.Cavalry;
            var threat = inside.Where(u => Vector2.Distance(u.Pos, post) < (horse ? 65 : 30)).OrderBy(u => Vector2.Distance(u.Pos, post)).FirstOrDefault();
            if (horse && (w.AnyGateOpen || w.AnyBreach))
                threat ??= Units.Where(u => u.Team == 0 && u.Active && Vector2.Distance(u.Pos, post) < 55).OrderBy(u => Vector2.Distance(u.Pos, post)).FirstOrDefault();
            if (threat?.Squad != null) { s.Order = Order.Charge; s.Target = threat.Squad; }
            else { s.Order = Order.Hold; s.Target = null; s.Anchor = post; }
        }
        eGuard.Front = -Vector2.UnitY;
        eGuard.Anchor = w.Heart + new Vector2(0, 5f);
    }

    /// The attackers' general (SE5): archers shoot at defenders on the wall, foot storm the walls/gate then push to the heart,
    /// horse wait outside for an open gate or breach then ride into the streets.
    void SiegeAttack()
    {
        var w = Wall!;
        bool foesOnWall = Units.Any(u => u.Team == 1 && u.Active && !u.Routing && w.OnWalkway(u.Pos, out _));
        bool horseWayIn = w.AnyGateOpen || w.AnyBreach;
        bool anyInside = Units.Any(u => u.Team == 0 && u.Active && !u.Routing && w.Inside(u.Pos));
        foreach (var s in SquadsOf(0))
        {
            if (s.Company == null || s.Alive == 0 || s.Works != null || !(Options.AutoPlayer || s.AiControlled)) continue;
            bool archers = s.Units.Any(u => u.Active && u.Ammo > 0);
            if (archers && foesOnWall)
            {
                s.Order = Order.Hold;
                s.Front = Vector2.UnitY;
                s.Anchor = new Vector2(s.Anchor.X, w.MainGate.Pos.Y - s.Units[0].Type.Range * 0.6f);
            }
            else if (s.Cavalry && !horseWayIn)
            {
                s.Order = Order.Hold;
                s.Front = Vector2.UnitY;
                s.Anchor = w.MainGate.Pos + w.MainGate.Out * 28f + w.MainGate.Along * 22f;
            }
            else if (anyInside && w.Inside(s.Center) && !Units.Any(u => u.Team == 1 && u.Active && !u.Routing && Vector2.Distance(u.Pos, s.Center) < 18f))
            {
                // Push for the heart once clear of wall fighting.
                s.Order = Order.Hold;
                s.Anchor = w.Heart;
                s.Front = Vector2.Normalize(w.Heart - s.Center + new Vector2(0, 0.01f));
            }
            else if (Time > 3) s.Order = Order.Charge;
        }
    }
}
