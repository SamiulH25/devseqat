using Microsoft.Xna.Framework;

namespace Ironfield;

/// The enemy hunts you (docs/COMPANY.md C13). When you do real harm or stand far forward, the enemy general sends his horse
/// (or his reserve) for you, once and then after a cooldown; good bows mark a captain in the open with a warning first; a
/// lord with a grudge (`BattleOptions.Grudge`) comes for you himself; his nearest men close round him when you come near;
/// and when two or more are on you, your household guard and your nearest idle company step in. All on the battle's own
/// state and order of thinking, so replays stay exact.
public sealed partial class Battle
{
    /// The company the enemy general has sent for you (null: none now), and how many he has sent this battle.
    public Squad? Hunter { get; private set; }
    public int HuntsSent { get; private set; }
    /// Volleys enemy bows have put on you this battle.
    public int VolleysOnYou { get; private set; }
    float huntAgain = 10, huntSentAt, coverTimer, standFrom = -1;
    bool grudgeSpoken;

    /// How you know what is coming: `Flashes` kind "mark" over where a volley will fall (drawn as the warning ring).
    public const float VolleyWarn = 1.6f, VolleyWindow = 0.6f, VolleyRadius = 4;
    /// Kills, or metres ahead of your own line, that make the general send for you.
    const int HarmKills = 6;
    const float ForwardBy = 30;

    /// Seconds between the general's orders against you: longer on Easy, shorter on Hard.
    float HuntCooldown => Options.EnemyDamage < 0.9f ? 90 : Options.EnemyDamage > 1.1f ? 40 : 60;

    /// Twice a second, after the enemy general's own orders: send for the captain, mark him with bows, the guard round
    /// the enemy captain, and a grudge lord's charge.
    internal void HuntGeneral()
    {
        var p = Player;
        if (Hunter != null && (!p.Active || p.Routing || Hunter.Alive < Hunter.StartCount * 0.3f || Time - huntSentAt > 45))
        {
            Hunter.HuntCaptain = false;
            Hunter = null;
        }
        if (Hunter == null && Time >= huntAgain && p.Active && !p.Routing && (PlayerKills >= HarmKills * (HuntsSent + 1) || Forward(p) > ForwardBy))
            SendHunter();
        if (Options.EnemyDamage >= 0.9f) MarkWithBows(); // Easy keeps the notice but not the volleys
        CloseTheGuard();
        Grudge();
    }

    /// How far ahead of your own line's centre you stand, toward the enemy.
    float Forward(Unit p)
    {
        var dir = centroid[1] - centroid[0];
        if (dir.LengthSquared() < 1) return 0;
        dir.Normalize();
        return Vector2.Dot(p.Pos - centroid[0], dir);
    }

    /// Send for the captain now, whatever he has done (staged shots only).
    internal void SendHunterNow() { if (Hunter == null) SendHunter(); }

    /// The general names you a target: his horse not yet at blows, else his reserve.
    void SendHunter()
    {
        var s = Squads.Where(q => q.Team == 1 && q.Cavalry && q.Alive > 0 && !q.InContact && q.Division == null && q.Units.Any(u => u.Active && !u.Routing)).OrderBy(q => Vector2.DistanceSquared(q.Center, Player.Pos)).FirstOrDefault()
            ?? (reserveSquad[1] is { Alive: > 0, InContact: false } r ? r : null);
        if (s == null) return;
        if (s == reserveSquad[1]) reserveSquad[1] = null;
        Hunter = s;
        s.HuntCaptain = true;
        s.HuntedFor = true;
        s.Order = Order.Charge;
        s.Target = null;
        HuntsSent++;
        huntSentAt = Time;
        huntAgain = Time + HuntCooldown;
        string what = s.Cavalry ? "THEIR HORSE IS SENT FOR YOU" : "THEIR RESERVE IS SENT FOR YOU";
        Alerts.Add(new Alert("hunt", what, null, s.Center, Time));
        if (Options.Goals && !Goals.Any(g => g.Id == "hunters")) Goals.Add(Goal("hunters"));
    }

    /// Elite bows (and trained bows against a lone rider) put a volley on a captain in the open: never while their own men
    /// are near him. A ring warns where it will fall.
    void MarkWithBows()
    {
        var p = Player;
        foreach (var s in Squads)
        {
            if (s.Team != 1 || s.Alive == 0 || s.Units.Count == 0 || s.Units[0].Type.Ammo == 0 || s.Units[0].Mounted) continue;
            if (s.VolleyAt > 0 && Time > s.VolleyAt + VolleyWindow) s.VolleyAt = -1;
            if (s.VolleyAt > 0 || Time < s.VolleyAgain || !p.Active || p.Routing) continue;
            var grade = s.Grade;
            bool will = grade == Grade.Elite || grade >= Grade.Trained && p.Mounted && Alone(p);
            float range = s.Units[0].Type.Range;
            if (!will || Vector2.Distance(s.Center, p.Pos) > range - 5 || OwnMenNear(p.Pos, 1, 8) || !Visible(p, 1)) continue;
            if (s.Units.Count(u => u.Active && u.Ammo > 0) * 2 < s.Alive) continue;
            s.VolleyAt = Time + VolleyWarn;
            s.VolleyAim = p.Pos;
            s.VolleyAgain = Time + 25;
            Flash("mark", p.Pos, VolleyRadius, 1);
            Alerts.Add(new Alert("volley", "ARCHERS MARK YOU", null, p.Pos, Time));
            VolleysOnYou++;
        }
    }

    /// Any active man of `team` within r of a spot.
    bool OwnMenNear(Vector2 at, int team, float r)
    {
        foreach (var o in Near(at, r, nearNotice))
            if (o.Team == team && !o.Routing && !o.IsLeader && Vector2.DistanceSquared(o.Pos, at) < r * r) return true;
        return false;
    }

    /// A man of a marking company looses at the captain in the volley's window, unless his own side has reached him.
    bool LoosesAtCaptain(Unit u, Squad s)
    {
        if (s.VolleyAt <= 0 || Time < s.VolleyAt || Time > s.VolleyAt + VolleyWindow || u.Ammo <= 0 || u.Cooldown > 0 || !Player.Active) return false;
        if (OwnMenNear(Player.Pos, u.Team, 6)) { s.VolleyAt = -1; return false; }
        u.Face(Player.Pos - u.Pos);
        Shoot(u, Player);
        return true;
    }

    /// The enemy captain's nearest six footmen close round him while you are within 20 m of him.
    void CloseTheGuard()
    {
        foreach (var u in Units) u.GuardSlot = -1;
        var c = EnemyCaptain;
        if (!c.Active || c.Routing || !Player.Active || Vector2.Distance(Player.Pos, c.Pos) > 20) return;
        int k = 0;
        foreach (var u in Units.Where(u => u.Active && !u.Routing && u.Team == c.Team && !u.IsLeader && !u.Mounted && !u.Type.Wild && u.Squad?.HuntCaptain != true
                     && Vector2.DistanceSquared(u.Pos, c.Pos) < 15 * 15).OrderBy(u => Vector2.DistanceSquared(u.Pos, c.Pos)).Take(6))
            u.GuardSlot = k++;
    }

    /// A guardsman's place: a half ring in front of his captain, facing you.
    Vector2 GuardSpot(Unit u)
    {
        var c = EnemyCaptain;
        var to = Player.Pos - c.Pos;
        to = to.LengthSquared() > 1e-3f ? Vector2.Normalize(to) : c.Dir;
        float a = (u.GuardSlot - 2.5f) * 0.45f;
        var dir = new Vector2(to.X * MathF.Cos(a) - to.Y * MathF.Sin(a), to.X * MathF.Sin(a) + to.Y * MathF.Cos(a));
        return c.Pos + dir * 1.9f;
    }

    /// A lord with a grudge comes for you from the start, and holds back when he is badly hurt.
    void Grudge()
    {
        if (Options.Grudge == "" || !EnemyCaptain.Active) return;
        if (!grudgeSpoken) { grudgeSpoken = true; Log(Options.Grudge); }
        bool hurt = EnemyCaptain.Hp < EnemyCaptain.Type.MaxHp * 0.35f;
        eGuard.Order = hurt ? Order.Hold : Order.Charge;
    }

    /// In a man's thinking: a hunter goes for the captain, a covering man for the foes on the captain, a guardsman to his
    /// place, a grudge lord for you. True when the man has been moved and his thinking is done.
    bool HuntThink(Unit u, Squad s, ref Unit? enemy, ref float d)
    {
        var p = Player;
        if (!p.Active || p.Routing) return false;
        bool atBlows = enemy != null && enemy != p && d <= ReachOf(u) + 0.5f;
        if ((s.HuntCaptain || u == EnemyCaptain && Options.Grudge != "" && eGuard.Order == Order.Charge) && u.Team != p.Team && !atBlows)
        {
            enemy = p;
            d = Vector2.Distance(u.Pos, p.Pos);
            return false;
        }
        if (s.CoverUntil > Time && u.Team == p.Team && !atBlows && OnTheCaptain(u) is (Unit foe, float fd))
        {
            (enemy, d) = (foe, fd);
            return false;
        }
        if (u.GuardSlot >= 0 && !atBlows && !u.Mounted)
        {
            var to = GuardSpot(u) - u.Pos;
            float l = to.Length();
            u.Face(p.Pos - u.Pos);
            u.Wish = l > 0.3f ? to / l * u.Type.Speed : Vector2.Zero;
            return true;
        }
        return false;
    }

    /// The nearest foe holding the captain as his mark, within 15 m of the captain.
    (Unit, float)? OnTheCaptain(Unit u)
    {
        Unit? best = null;
        float bd = float.MaxValue;
        foreach (var o in Near(Player.Pos, 15, nearNotice))
        {
            if (!o.OnCaptain && o.Squad?.HuntCaptain != true || o.Team == u.Team || o.Routing) continue;
            float dd = Vector2.DistanceSquared(o.Pos, u.Pos);
            if (dd < bd) { bd = dd; best = o; }
        }
        return best == null ? null : (best, MathF.Sqrt(bd));
    }

    /// Twice a second: when two or more are on you, your household guard and your nearest idle foot company come to cover
    /// you for ten seconds ("TO THE CAPTAIN!"); and the stand goal when four come at once.
    void UpdateHunt(float dt)
    {
        if ((coverTimer -= dt) > 0) return;
        coverTimer = 0.5f;
        var p = Player;
        if (!p.Active || p.Routing) return;
        int on = MarkedBy + (Hunter != null && Vector2.Distance(Hunter.Center, p.Pos) < 25 ? 1 : 0);
        if (on >= 4 && standFrom < 0 && Options.Goals && !Goals.Any(g => g.Id == "stand")) { standFrom = Time; Goals.Add(Goal("stand")); }
        if (on < 2) return;
        bool called = false;
        foreach (var s in Squads)
        {
            if (s.Team != p.Team || s.Alive == 0 || s.AiControlled || s.CoverUntil > Time) continue;
            if (s.Company?.Has("Household") == true && s.Order == Order.Follow) { s.CoverUntil = Time + 10; called = true; }
        }
        var near = Squads.Where(s => s.Team == p.Team && s.Alive > 0 && !s.AiControlled && !s.InContact && !s.Cavalry && s.Order == Order.Hold && s.Division == null
                && s.Units.Count > 0 && s.Units[0].Type.Ammo == 0 && s.CoverUntil <= Time && Vector2.Distance(s.Center, p.Pos) < 35 + s.Radius)
            .OrderBy(s => Vector2.DistanceSquared(s.Center, p.Pos)).FirstOrDefault();
        if (near != null) { near.CoverUntil = Time + 10; called = true; }
        if (called && Time - lastCoverCall > 15)
        {
            lastCoverCall = Time;
            Alerts.Add(new Alert("cover", "TO THE CAPTAIN!", near, p.Pos, Time));
        }
    }

    float lastCoverCall = -99;

    /// The stand goal: you still stand ten seconds after four came for you at once.
    int StandState() => standFrom < 0 ? 0 : !Player.Active ? -1 : Time - standFrom >= 10 ? 1 : Winner >= 0 ? -1 : 0;

    /// The hunters goal: every man of the company sent for you dead, none home.
    int HuntersState()
    {
        var h = Squads.FirstOrDefault(s => s.HuntedFor);
        if (h == null) return Winner >= 0 ? -1 : 0;
        if (h.Units.All(u => u.Dead)) return 1;
        return Winner >= 0 || h.Units.Any(u => u.Fled) ? -1 : 0;
    }
}
