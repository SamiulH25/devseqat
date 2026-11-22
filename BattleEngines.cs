using Microsoft.Xna.Framework;

namespace Ironfield;

/// An engine of war on the field (docs/archive/SIEGECRAFT.md A1): it stands where it was set up and its crew company works it.
/// Crew within reach of it wind it (a short crew is slower; none and it stands silent); enemies who reach it hack it apart.
public sealed class Engine
{
    public EngineDef Def = null!;
    public int Team, Index;
    public Vector2 Pos, Front = Vector2.UnitY;
    public Squad Crew = null!;
    public float Hp;
    /// Seconds of winding left before it can loose (counted down at the crew's rate).
    public float Reload;
    /// Shots loosed, men it has killed, and what it has taken off gates.
    public int Shots, Kills;
    public float GateDamage, WallDamage;
    /// Where it last aimed (the screen and the HUD read it).
    public Vector2 Aim;
    /// A point the player ordered it to keep shooting at (a spot of ground or a place on the wall: docs/archive/SIEGECRAFT.md A5).
    public Vector2? Mark;
    /// Whether this winding has been heard yet (A6).
    public bool Winched;
    /// A rolling engine's goal (the gate, or its lane at the wall's face) and whether a tower stands at the face now.
    public Vector2 Goal;
    public bool Docked;
    public bool Wrecked => Hp <= 0;
}

/// A shot in flight from an engine: a bolt flies flat and runs through men; a stone falls and splashes.
public sealed class EngineShot
{
    public Engine From = null!;
    public Unit Shooter = null!;
    public Vector3 Pos, Vel;
    public float Damage;
    public int PierceLeft;
    public readonly HashSet<Unit> Struck = new();
    public bool Done;
}

public sealed partial class Battle
{
    public readonly List<Engine> Engines = new();
    public readonly List<EngineShot> EngineShots = new();
    /// How near a crewman must stand to work his engine, and how near a foe must come to hack at it.
    public const float CrewReach = 5, WreckReach = 2.5f;
    readonly List<Unit> nearEngine = new(64);
    /// The engine you work yourself (docs/archive/SIEGECRAFT.md A5): stand by it, press the use key, and it looses where you aim when you attack.
    public Engine? Manned { get; private set; }
    public const float ManReach = 3.5f;

    /// Your own light or stone engine within reach of the player, if any.
    public Engine? EngineNear(Unit p)
    {
        Engine? best = null;
        float bd = ManReach * ManReach;
        foreach (var e in Engines)
        {
            if (e.Team != p.Team || e.Wrecked || e.Def.Rolls) continue;
            float d = Vector2.DistanceSquared(e.Pos, p.Pos);
            if (d < bd) { bd = d; best = e; }
        }
        return best;
    }

    /// Take over an engine (it stops firing on its own: the crew wind it, you choose the mark), or step away from it.
    public void Man(Engine? e)
    {
        if (Manned == e) return;
        if (e == null) Log($"You leave the {Manned!.Def.Name.ToLowerInvariant()}.");
        else { e.Mark = null; e.Crew.Target = null; Log($"You take the {e.Def.Name.ToLowerInvariant()}: aim and attack to loose."); }
        Manned = e;
    }

    /// Order an engine's crew to shoot at a company, or at a point (a place on the wall face if it is near it); neither clears the order.
    public void AimEngine(Engine e, Squad? foe, Vector2? at)
    {
        e.Mark = null;
        e.Crew.Target = null;
        string n = e.Def.Name.ToLowerInvariant();
        if (foe != null) { e.Crew.Target = foe; Log($"The {n}: shoot at {foe.Name}!"); return; }
        if (at is not Vector2 p) { Log($"The {n}: shoot as you see fit."); return; }
        if (Wall is Fortress fort)
        {
            var g = fort.NearestGate(p);
            if (g != null && Vector2.Distance(p, g.Pos) < 8f) p = g.Pos + g.Out * 0.5f;
            else if (fort.NearestSegment(p, 8f) is WallSegment seg) p = seg.Mid + seg.Out * 0.5f;
        }
        float r = Vector2.Distance(e.Pos, p);
        if (r > e.Def.Range) { Log($"The {n} cannot reach that far ({e.Def.Range:0} m)."); return; }
        if (r < e.Def.MinRange) { Log($"That is too close for the {n} ({e.Def.MinRange:0} m at least)."); return; }
        e.Mark = p;
        Log(Wall != null && (Wall.NearestSegment(p, 4f) != null || (Wall.NearestGate(p) is { } ng && Vector2.Distance(p, ng.Pos) < 5f))
            ? $"The {n}: shoot at the wall!" : $"The {n}: shoot there!");
    }

    /// Set up an engine at `at` facing `front`, worked by `crew` (who form up just behind it and hold there; the AI general
    /// leaves them be). The engine starts wound.
    public Engine AddEngine(int team, string id, Vector2 at, Vector2 front, Squad crew)
    {
        var def = Ironfield.Engines.Get(id);
        var e = new Engine { Def = def, Team = team, Index = Engines.Count, Pos = at, Front = Vector2.Normalize(front), Crew = crew, Hp = def.Hp, Aim = at, Goal = at };
        if (def.Rolls && Wall is Fortress fw)
        {
            if (def.Shot == "ram") e.Goal = fw.MainGate.Pos + fw.MainGate.Out * 1.6f;
            else
            {
                var seg = fw.NearestSegment(at, ring: 0);
                if (seg != null)
                {
                    float t = Math.Clamp(Vector2.Dot(at - seg.A, seg.Along), 2f, MathF.Max(2f, seg.Length - 2f));
                    e.Goal = seg.A + seg.Along * t + seg.Out * 1.6f;
                }
            }
        }
        Engines.Add(e);
        crew.Works = e;
        crew.Order = Order.Hold;
        crew.Target = null;
        crew.Front = e.Front;
        crew.Anchor = at - e.Front * 2.5f;
        foreach (var l in roles[team].Values) l.Remove(crew); // the general does not send the crew into the line
        return e;
    }

/// Set up the engines in BattleOptions (docs/archive/SIEGECRAFT.md A3, A4) after both sides deploy. Each takes its crew from one of
    /// its side's foot companies (the smallest-tier one that can spare them; the crew reports with that company). In a field
    /// battle only bolt-throwers come, behind the line; in an assault the attackers bring their works before the wall and the
    /// defenders' are on it already (MountWallEngines). Breached sections start down.
    void DeployEngines()
    {
        if (Wall is Fortress fort)
        {
            var southSegs = fort.Segments.Where(s => s.Ring == 0 && !s.IsGate && s.Out.Y < -0.2f && Vector2.Distance(s.Mid, fort.MainGate.Pos) > 10f)
                .OrderBy(s => Vector2.Distance(s.Mid, fort.MainGate.Pos)).ToList();
            for (int k = 0; k < Options.Breaches && k < southSegs.Count; k++)
                southSegs[k].Hp = 0;
            if (Options.Breaches > 0) InvalidatePath();
        }
        var placed = new int[2];
        foreach (var spec in Options.Engines)
        {
            if (!Ironfield.Engines.Has(spec.Id) || spec.Team is < 0 or > 1) continue;
            var def = Ironfield.Engines.Get(spec.Id);
            if (Wall == null ? def.Shot != "bolt" : spec.Team == 1) continue;
            var from = Squads.Where(s => s.Team == spec.Team && s.Company != null && s.Works == null && s.SplitFrom == null && !s.Cavalry && !s.Swarm && s.AiControlled == spec.Ally
                                         && !CasterCompany(s) && s.Alive >= def.Crew + 4)
                .OrderBy(s => s.Units.Any(u => u.Ammo > 0) ? 1 : 0).ThenBy(s => s.Units[0].Type.Tier).ThenByDescending(s => s.Alive).ThenBy(s => s.Id).FirstOrDefault();
            if (from == null) continue;
            int k = placed[spec.Team]++;
            float f = spec.Team == 0 ? 1 : -1, side = k % 2 == 0 ? -1 : 1;
            Vector2 at;
            if (Wall == null)
                at = new Vector2(side * (10 + 16 * (k / 2)), -(rearLine[spec.Team] + 4)) * f; // behind the line, where the archers stand
            else
            {
                var mg = Wall.MainGate;
                at = def.Shot switch
                {
                    "ram" => mg.Pos + mg.Out * 32f + mg.Along * 6f,
                    "tower" => mg.Pos + mg.Out * 40f + mg.Along * (side * (24 + 14 * (k / 2))),
                    _ => mg.Pos + mg.Out * MathF.Min(def.Range * 0.7f, 95) + mg.Along * (side * (24 + 16 * (k / 2))),
                };
            }
            var crew = SplitNow(from, def.Crew, quiet: true);
            crew.Division?.Squads.Remove(crew);
            crew.Division = null;
            for (int i = 0; i < crew.Units.Count; i++) crew.Units[i].Pos = at + new Vector2((i % 4 - 1.5f) * 1.1f, -2 - i / 4 * 0.9f) * f; // they stand at it from the first
            AddEngine(spec.Team, def.Id, at, new Vector2(0, f), crew);
        }
    }

    /// Horse with no company to hunt rides for an enemy engine it can see near it (docs/archive/SIEGECRAFT.md A4): a crew is soft.
    internal Squad? EnginePrey(Squad horse, int foe)
    {
        Squad? best = null;
        float bestD = 90;
        foreach (var e in Engines)
        {
            if (e.Team != foe || e.Wrecked || e.Crew.Alive == 0 || !Visible(e.Crew, horse.Team)) continue;
            float d = Vector2.Distance(horse.Center, e.Pos);
            if (d < bestD) { bestD = d; best = e.Crew; }
        }
        return best;
    }

    /// Men of its crew close enough to work it.
    public int CrewAt(Engine e)
    {
        int n = 0;
        foreach (var u in e.Crew.Units)
            if (u.Active && !u.Routing && Vector2.DistanceSquared(u.Pos, e.Pos) < CrewReach * CrewReach) n++;
        return n;
    }

    void UpdateEngines(float dt)
    {
        if (Engines.Count == 0) return;
        foreach (var e in Engines)
        {
            if (e.Wrecked) continue;
            Hack(e, dt);
            if (e.Wrecked) continue;
            bool mine = e == Manned && Player.Active && Vector2.DistanceSquared(Player.Pos, e.Pos) < (ManReach + 1) * (ManReach + 1);
            if (e == Manned && !mine) Man(null); // walked off, or fell
            int crew = CrewAt(e) + (mine ? 1 : 0);
            if (crew == 0) continue; // nobody to wind it
            if (e.Def.Rolls) { Roll(e, crew, dt); continue; }
            e.Reload -= dt * MathF.Min(1, crew / (float)e.Def.Crew);
            if (!e.Winched && e.Reload < e.Def.Reload * 0.85f && e.Reload > 0) { e.Winched = true; Sound(Cue.EngineWinch, e.Pos); }
            if (e.Reload > 0) continue;
            if (mine)
            {
                // Your engine looses when you attack, where you look; the range is held to what it can throw.
                if (Commanding || Input.EngineAim is not Vector2 want || !Input.Attack) continue;
                var to = want - e.Pos;
                float r = to.Length();
                if (r < e.Def.MinRange) { if (Time - lastTooNear > 2) { lastTooNear = Time; Log("Too near for the " + e.Def.Name.ToLowerInvariant() + "."); } continue; }
                if (r > e.Def.Range) want = e.Pos + to / r * e.Def.Range;
                Launch(e, want, Player);
                continue;
            }
            if (EngineAim(e) is not Vector2 at) continue;
            Launch(e, at);
        }
        UpdateEngineShots(dt);
    }

    /// Foes standing at it hack it apart (crew or no crew: the crew must drive them off).
    void Hack(Engine e, float dt)
    {
        int foes = 0;
        foreach (var u in Near(e.Pos, WreckReach + 0.5f, nearEngine))
            if (u.Active && !u.Routing && u.Team != e.Team && Vector2.DistanceSquared(u.Pos, e.Pos) < WreckReach * WreckReach) foes++;
        if (foes == 0) return;
        Damage(e, Math.Min(foes, 6) * 15 * dt);
    }

    internal void Damage(Engine e, float dmg)
    {
        if (e.Wrecked) return;
        e.Hp -= dmg;
        if (!e.Wrecked) return;
        Sound(Cue.Crash, e.Pos);
        Log(e.Team == 0 ? $"Your {e.Def.Name.ToLowerInvariant()} is wrecked!" : $"The enemy {e.Def.Name.ToLowerInvariant()} is wrecked!");
        if (Manned == e) Man(null);
        e.Crew.Works = null; // the crew is a company again
        if (e.Docked && Wall != null) { Wall.Ramps.RemoveAll(r => r.Tower == e); e.Docked = false; WallVersion++; InvalidatePath(); } // the tower's way up is gone
    }

    /// A ram or a tower: its crew push it toward its goal at their pace (a short crew, slower); a ram at the gate beats on it,
    /// a tower at the face drops its bridge.
    void Roll(Engine e, int crew, float dt)
    {
        var w = Wall;
        float k = MathF.Min(1, crew / (float)e.Def.Crew);
        var to = e.Goal - e.Pos;
        float d = to.Length();
        if (d > 0.3f)
        {
            e.Front = to / d;
            e.Pos += e.Front * MathF.Min(d, e.Def.Move * k * dt);
            e.Crew.Anchor = e.Pos - e.Front * (e.Def.Shot == "ram" ? 1.5f : 2.5f);
            e.Crew.Front = e.Front;
            return;
        }
        if (w == null) return;
        if (e.Def.Shot == "tower" && !e.Docked)
        {
            var seg = w.NearestSegment(e.Pos, 8f, ring: 0);
            if (seg != null)
            {
                e.Docked = true;
                w.AddRamp(seg, e.Pos, e);
                WallVersion++;
                InvalidatePath();
                Log(e.Team == 0 ? "Our siege tower is at the wall!" : "A siege tower reaches the wall!");
                Sound(Cue.LadderHook, e.Pos, priority: true);
            }
        }
        if (e.Def.Shot == "ram")
        {
            var g = w.NearestGate(e.Pos);
            if (g != null && !g.Open && Vector2.Distance(e.Pos, g.Pos) < g.HalfWidth + 3.5f)
            {
                float before = g.Hp;
                g.Hp = MathF.Max(0, g.Hp - e.Def.Damage * k * dt);
                e.GateDamage += before - g.Hp;
                if ((e.Reload -= dt) <= 0) { e.Reload = 1.4f; Sound(Cue.Ram, e.Pos); }
                if (g.Open) GateGivesWay(g);
            }
        }
    }

    /// Men of a side under the roof of their ram or behind their tower: arrows rarely find them.
    bool Sheltered(Unit t)
    {
        foreach (var e in Engines)
            if (e.Team == t.Team && e.Def.Rolls && !e.Wrecked && Vector2.DistanceSquared(e.Pos, t.Pos) < 9) return true;
        return false;
    }

    /// Where it shoots: a closed gate before it (an engine that breaks walls, attacking), else its crew's chosen company, else
    /// the best company in its reach that its side can see: for a stone the biggest body of foot, for a bolt the nearest.
    Vector2? EngineAim(Engine e)
    {
        var d = e.Def;
        bool InReach(Vector2 p) { float r = Vector2.Distance(e.Pos, p); return r >= d.MinRange && r <= d.Range; }
        if (e.Mark is Vector2 spot && InReach(spot)) return spot; // the player's chosen spot (A5)
        if (Wall is Fortress w && e.Team == 0 && d.Shot == "stone" && d.WallDamage >= 20 && !w.AnyBreach)
        {
            // A great engine (or any, once the gate is down) makes a breach in the nearest outer segment; a lesser one hits the gate.
            var mg = w.MainGate;
            var gate = mg.Pos - mg.Out * 0.6f;
            if (!mg.Open && d.WallDamage < 100 && InReach(gate)) return gate;
            if (mg.Open || d.WallDamage >= 100)
            {
                WallSegment? best = null;
                float bd = float.MaxValue;
                foreach (var seg in w.Segments)
                {
                    if (seg.Ring != 0 || seg.IsGate || seg.Breached || !InReach(seg.Mid)) continue;
                    float dist = Vector2.Distance(e.Pos, seg.Mid);
                    if (dist < bd) { bd = dist; best = seg; }
                }
                if (best != null) return best.Mid + best.Out * 0.5f;
            }
        }
        Squad? Fit(Squad? s) => s is { Alive: > 0 } && s.Team != e.Team && Visible(s, e.Team) && InReach(s.Center) ? s : null;
        var pick = Fit(e.Crew.Target);
        if (pick == null)
        {
            float best = float.MaxValue;
            foreach (var s in Squads)
            {
                if (Fit(s) == null) continue;
                float r = Vector2.Distance(e.Pos, s.Center);
                float score = d.Shot == "stone" ? r - s.Alive * 2 + (s.Cavalry ? 60 : 0) : r; // stones go where men stand thick and still
                if (score < best) { best = score; pick = s; }
            }
        }
        if (pick == null) return null;
        // Aim at the man nearest the company's middle, led by its pace.
        Unit? mark = null;
        float near = float.MaxValue;
        foreach (var u in pick.Units)
        {
            if (!u.Active || u.Routing) continue;
            float r = Vector2.DistanceSquared(u.Pos, pick.Center);
            if (r < near) { near = r; mark = u; }
        }
        if (mark == null) return null;
        float flight = Vector2.Distance(e.Pos, mark.Pos) / d.Speed;
        return mark.Pos + mark.Vel * flight;
    }

    float lastTooNear = -10;

    void Launch(Engine e, Vector2 at, Unit? by = null)
    {
        var d = e.Def;
        by ??= e.Crew.Units.FirstOrDefault(u => u.Active && !u.Routing && Vector2.DistanceSquared(u.Pos, e.Pos) < CrewReach * CrewReach);
        if (by == null) return;
        e.Winched = false;
        e.Reload = d.Reload * (0.9f + 0.2f * rng.NextSingle());
        e.Shots++;
        e.Aim = at;
        float dist = Vector2.Distance(e.Pos, at);
        at += new Vector2(Gauss(), Gauss()) * dist * d.Spread * Weather.Spread;
        dist = MathF.Max(1, Vector2.Distance(e.Pos, at));
        float time = dist / d.Speed;
        bool stone = d.Shot == "stone";
        float from = Terrain.Height(e.Pos) + (stone ? 4 : 1.4f), land = Terrain.Height(at) + (stone ? 0 : 1.1f);
        var h = (at - e.Pos) / time;
        EngineShots.Add(new EngineShot
        {
            From = e,
            Shooter = by,
            Pos = new(e.Pos.X, from, e.Pos.Y),
            Vel = new(h.X, 0.5f * Gravity * time + (land - from) / time, h.Y),
            Damage = d.Damage,
            PierceLeft = stone ? 0 : d.Pierce,
        });
        Sound(stone || d.Damage >= 80 ? Cue.EngineRelease : Cue.CrossbowSnap, e.Pos, priority: by == Player);
    }

    void UpdateEngineShots(float dt)
    {
        for (int i = EngineShots.Count - 1; i >= 0; i--)
        {
            var s = EngineShots[i];
            s.Vel.Y -= Gravity * dt;
            s.Pos += s.Vel * dt;
            var flat = new Vector2(s.Pos.X, s.Pos.Z);
            float ground = Terrain.Height(flat);
            bool stone = s.From.Def.Shot == "stone";
            if (!stone && s.Pos.Y <= ground + 2.4f) Pierce(s, flat, ground);
            if (s.Pos.Y <= ground || s.Done || flat.LengthSquared() > 600 * 600)
            {
                if (stone && !s.Done && s.Pos.Y <= ground) Land(s, flat);
                EngineShots.RemoveAt(i);
            }
        }
    }

    /// A bolt runs through the foes in its path, losing some force with each.
    void Pierce(EngineShot s, Vector2 flat, float ground)
    {
        var d = s.From.Def;
        foreach (var t in Near(flat, 1.5f, nearEngine))
        {
            if (s.PierceLeft <= 0) break;
            if (!t.Active || t.Team == s.From.Team || s.Struck.Contains(t)) continue;
            float r = t.Mounted ? 0.9f : 0.45f;
            if ((!t.Mounted && s.Pos.Y > ground + 2) || Vector2.DistanceSquared(t.Pos, flat) > r * r) continue;
            s.Struck.Add(t);
            s.PierceLeft--;
            Strike(s, t, s.Damage, d.Shock);
            s.Damage *= 0.75f;
            Sound(Cue.ArrowFlesh, t.Pos);
        }
        if (s.PierceLeft <= 0) s.Done = true;
    }

    /// A stone lands: everyone near takes it (friends too), the nearest worst; men are thrown down; the rest are shaken;
    /// a gate or an engine under it takes the blow.
    void Land(EngineShot s, Vector2 at)
    {
        var d = s.From.Def;
        Sound(Cue.Impact, at, priority: true);
        Flash("stone", at, d.Splash, s.From.Team, "");
        foreach (var t in Near(at, d.Splash * 2 + 0.5f, nearEngine).ToList())
        {
            if (!t.Active) continue;
            float r = Vector2.Distance(t.Pos, at);
            if (r < d.Splash)
            {
                float k = 1 - 0.6f * r / d.Splash;
                Strike(s, t, s.Damage * k, d.Shock);
                if (t.Active && !t.Mounted && !t.Type.Huge && Wall == null)
                {
                    var away = r > 0.01f ? (t.Pos - at) / r : t.Dir;
                    t.Pos += away * 1.2f * k;
                    Stagger(t, 0.8f * k);
                }
            }
            else if (r < d.Splash * 2) t.Morale -= d.Shock * 0.5f * (1 - (r - d.Splash) / d.Splash);
        }
        foreach (var e in Engines)
            if (e != s.From && !e.Wrecked && Vector2.Distance(e.Pos, at) < d.Splash + 1.5f) Damage(e, s.Damage * 0.8f);
        if (Wall is Fortress w && d.WallDamage > 0)
        {
            var g = w.NearestGate(at);
            if (g != null && !g.Open && Vector2.Distance(at, g.Pos) < g.HalfWidth + 2.5f)
            {
                float before = g.Hp;
                g.Hp = MathF.Max(0, g.Hp - d.WallDamage);
                s.From.GateDamage += before - g.Hp;
                Sound(Cue.Ram, at);
                if (g.Open) GateGivesWay(g);
            }
            else if (w.NearestTower(at, 4.5f) is { Silenced: false } tw)
            {
                tw.Hp = MathF.Max(0, tw.Hp - d.WallDamage);
                s.From.WallDamage += d.WallDamage;
                if (tw.Silenced) { WallVersion++; Log("A tower falls silent!"); Sound(Cue.WallFall, tw.Pos); }
            }
            else if (w.NearestSegment(at, 4.0f) is { Breached: false } seg)
            {
                seg.Hp = MathF.Max(0, seg.Hp - d.WallDamage);
                s.From.WallDamage += d.WallDamage;
                if (seg.Breached) WallBreached(seg);
            }
        }
    }

    /// One man hit by an engine's shot: through part of his armour; a rider's horse takes half of it.
    void Strike(EngineShot s, Unit t, float dmg, float shock)
    {
        bool wasDead = t.Dead;
        if (t.Mounted) { HitHorse(t, dmg * 0.5f, s.Shooter); dmg *= 0.6f; }
        if (t.Active) Damage(s.Shooter, t, dmg, shock, s.From.Def.ArmourPierce, ranged: true);
        if (!wasDead && t.Dead && t.Team != s.From.Team) s.From.Kills++;
    }
}
