using Microsoft.Xna.Framework;

namespace Ironfield;

/// Assaults on walled towns and castles (roadmap B1). Team 1 holds the wall (Terrain.Wall); team 0 climbs the ladders
/// or breaks the gate. Men only cross the wall's outer face at a ladder (attackers on foot) or through a broken gate.
public sealed partial class Battle
{
    /// Set when the gate breaks during the last Update (the screen rebuilds the wall's meshes).
    public bool GateBroke;
    readonly Dictionary<Squad, Vector2> posts = new(); // where each defending company stands guard
    float ramSound;
    bool ramPhase;

    public SiegeWall? Wall => Terrain.Wall;

    /// Defenders: archers in one rank along the walkway, either side of the gate; foot at the gate and the ladder tops;
    /// horse in the square behind the gate.
    void DeployDefenders(List<Company> onField)
    {
        var w = Wall!;
        var guardPosts = new List<Vector2> { new(SiegeWall.GateX, SiegeWall.Outer + 7) };
        guardPosts.AddRange(w.Ladders.Select(x => new Vector2(x, SiegeWall.Inner + 1.5f)));
        float left = SiegeWall.GateX - SiegeWall.GateHalf - 1, right = SiegeWall.GateX + SiegeWall.GateHalf + 1;
        int melee = 0;
        foreach (var c in onField)
        {
            var t = Troops.Get(c.Troop);
            string role = Ironfield.Armies.RoleOf(c);
            Vector2 at;
            int cols;
            if (role == "archers")
            {
                float width = c.Count * 1.3f;
                bool goLeft = SquadsOf(1).Count(s => s.Company != null && posts.ContainsKey(s) && posts[s].X < 0) <= SquadsOf(1).Count(s => s.Company != null && posts.ContainsKey(s) && posts[s].X > 0);
                at = goLeft ? new Vector2(left - width / 2, (SiegeWall.Top0 + SiegeWall.Top1) / 2) : new Vector2(right + width / 2, (SiegeWall.Top0 + SiegeWall.Top1) / 2);
                if (goLeft) left -= width + 2; else right += width + 2;
                cols = c.Count;
            }
            else { at = guardPosts[melee++ % guardPosts.Count]; cols = Math.Min(c.Count, 8); }
            var s = AddSquad(1, t, c.Count, at, cols, -Vector2.UnitY, c);
            Role(1, role).Add(s);
            lateral[s] = 0;
            posts[s] = at;
        }
    }

    /// The way across the wall for u toward dest: dest itself when no wall lies between, else the near side of the best passage.
    Vector2 Via(Unit u, Vector2 dest)
    {
        var w = Wall;
        if (w == null) return PathVia(u, dest); // a field: the company's route round water, hedges and houses (BattlePath.cs)
        bool inU = SiegeWall.Inside(u.Pos), inD = SiegeWall.Inside(dest);
        if (inU == inD) return dest;
        float best = float.MaxValue;
        Vector2 pick = new(dest.X, inU ? SiegeWall.Outer + 3 : SiegeWall.Outer - 3); // no way through: wait at the wall
        void Try(float x, float halfWidth, float outY, float inY, bool open)
        {
            var near = new Vector2(x, inU ? inY : outY);
            var far = new Vector2(x, inU ? outY : inY);
            float cost = Vector2.Distance(u.Pos, near) + Vector2.Distance(near, dest) + (open ? 0 : 25);
            if (cost >= best) return;
            best = cost;
            // At the foot of the passage: go through (a closed gate keeps you at it, battering).
            bool there = MathF.Abs(u.Pos.X - x) < halfWidth * 0.8f && MathF.Abs(u.Pos.Y - SiegeWall.Outer) < 3.5f;
            pick = there ? (open ? far : near) : near;
        }
        bool attackerOnFoot = u.Team == 0 && !u.Mounted;
        if (w.GateOpen || attackerOnFoot) Try(SiegeWall.GateX, SiegeWall.GateHalf, SiegeWall.Outer - 2, SiegeWall.Outer + 3, w.GateOpen);
        if (attackerOnFoot)
            foreach (float x in w.Ladders) Try(x, SiegeWall.LadderHalf, SiegeWall.Outer - 2.4f, SiegeWall.Top0 + 0.8f, true);
        return pick;
    }

    /// May a unit stepping from `from` to u.Pos cross the wall's face here?
    bool MayCross(Unit u, Vector2 from)
    {
        var w = Wall!;
        if (SiegeWall.Inside(from) == SiegeWall.Inside(u.Pos)) return true;
        return (w.GateOpen && w.AtGate(u.Pos.X)) || (u.Team == 0 && !u.Mounted && w.AtLadder(u.Pos.X));
    }

    void EnforceWall(Vector2[] before)
    {
        if (Wall == null) return;
        ladderSound = MathF.Max(0, ladderSound - 1 / 60f);
        for (int i = 0; i < Units.Count; i++)
        {
            var u = Units[i];
            if (!u.Active) continue;
            if (MayCross(u, before[i]))
            {
                if (SiegeWall.Inside(before[i]) != SiegeWall.Inside(u.Pos) && !Wall.AtGate(u.Pos.X) && ladderSound <= 0)
                { ladderSound = 1.6f; Sound(Cue.LadderHook, u.Pos); }
                continue;
            }
            u.Pos.Y = SiegeWall.Inside(before[i]) ? SiegeWall.Outer + 0.02f : SiegeWall.Outer - 0.02f;
            if (u.Mounted) u.Speed *= 0.5f;
        }
    }
    float ladderSound;

    /// Attackers on foot standing at a closed gate batter it down.
    void Batter(float dt)
    {
        var w = Wall;
        GateBroke = false;
        if (w == null || w.GateOpen) return;
        int rams = 0;
        foreach (var u in Units)
            if (u.Team == 0 && u.Active && !u.Routing && !u.Mounted && !u.IsPlayer && w.AtGate(u.Pos.X) && u.Pos.Y > SiegeWall.Outer - 3 && u.Pos.Y < SiegeWall.Outer)
            {
                rams++;
                u.Face(Vector2.UnitY);
            }
        if (Player.Active && !Player.Mounted && w.AtGate(Player.Pos.X) && Player.Pos.Y > SiegeWall.Outer - 2 && Player.Pos.Y < SiegeWall.Outer && Player.Action == UnitAction.Recover) rams += 2;
        if (rams == 0) return;
        w.GateHp -= MathF.Min(12, rams) * 5 * dt;
        if ((ramSound -= dt) <= 0)
        {
            ramSound = 0.65f;
            ramPhase = !ramPhase;
            Sound(ramPhase ? Cue.Ram : Cue.RamSwing, new Vector2(SiegeWall.GateX, SiegeWall.Outer));
        }
        if (w.GateOpen)
        {
            GateBroke = true;
            Log("The gate gives way!");
            Sound(Cue.Crash, new Vector2(SiegeWall.GateX, SiegeWall.Outer));
            Sound(Cue.Horn, new Vector2(SiegeWall.GateX, SiegeWall.Outer));
            foreach (var u in Units) if (u.Team == 1 && u.Active) u.Morale -= 10;
        }
    }

    /// The defenders' general: hold the wall; foot go for attackers who get inside near their post; horse sally once the gate is down.
    void SiegeDefend()
    {
        var w = Wall!;
        var inside = Units.Where(u => u.Team == 0 && u.Active && !u.Routing && SiegeWall.Inside(u.Pos)).ToList();
        foreach (var s in SquadsOf(1))
        {
            if (s == eGuard || s.Alive == 0) continue;
            var post = posts.TryGetValue(s, out var p) ? p : new Vector2(SiegeWall.GateX, SiegeWall.Outer + 7);
            bool archers = s.Units.Any(u => u.Active && u.Ammo > 0);
            if (archers) { s.Order = Order.Hold; s.Front = -Vector2.UnitY; s.Anchor = post; continue; }
            bool horse = s.Cavalry;
            var threat = inside.Where(u => Vector2.Distance(u.Pos, post) < (horse ? 60 : 28)).OrderBy(u => Vector2.Distance(u.Pos, post)).FirstOrDefault();
            if (horse && w.GateOpen) threat ??= Units.Where(u => u.Team == 0 && u.Active && Vector2.Distance(u.Pos, post) < 50).OrderBy(u => Vector2.Distance(u.Pos, post)).FirstOrDefault();
            if (threat?.Squad != null) { s.Order = Order.Charge; s.Target = threat.Squad; }
            else { s.Order = Order.Hold; s.Target = null; s.Front = -Vector2.UnitY; s.Anchor = post; }
        }
        eGuard.Front = -Vector2.UnitY;
        eGuard.Anchor = new Vector2(SiegeWall.GateX, 34);
    }

    /// The attackers' general (headless runs): archers stand off and shoot at the wall, foot storm the ladders and the gate,
    /// horse waits for the gate. Bowmen only stand off while there is something to shoot at: once the wall's garrison is
    /// all behind the wall (or dead) and the gate still holds, nothing can be hit from the field, so they storm with the rest.
    void SiegeAttack()
    {
        var w = Wall!;
        bool foesInTheOpen = Units.Any(u => u.Team == 1 && u.Active && !u.Routing && !SiegeWall.Inside(u.Pos));
        foreach (var s in SquadsOf(0))
        {
            if (s.Company == null || s.Alive == 0 || !(Options.AutoPlayer || s.AiControlled)) continue;
            bool archers = s.Units.Any(u => u.Active && u.Ammo > 0);
            if (archers && foesInTheOpen)
            {
                s.Order = Order.Hold;
                s.Front = Vector2.UnitY;
                s.Anchor = new Vector2(s.Anchor.X, SiegeWall.Outer - s.Units[0].Type.Range * 0.6f);
            }
            else if (s.Cavalry && !w.GateOpen) { s.Order = Order.Hold; s.Front = Vector2.UnitY; s.Anchor = new Vector2(SiegeWall.GateX + 25, -8); }
            else if (Time > 3) s.Order = Order.Charge;
        }
    }
}
