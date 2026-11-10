using Microsoft.Xna.Framework;

namespace Ironfield;

/// One thing on your side that needs you (docs/archive/COMBAT_QOL.md Q5): its kind, the line shown, the company and where.
public sealed record Alert(string Kind, string Text, Squad? Squad, Vector2 At, float Time);

/// Alerts for the player (docs/archive/COMBAT_QOL.md Q5): twice a second, from state the sim already keeps. Each fires once until
/// its state clears. They change nothing in the battle, so replays are untouched.
public sealed partial class Battle
{
    /// Every alert raised, oldest first (the screen shows the newest few).
    public readonly List<Alert> Alerts = new();
    readonly HashSet<(string, object?)> alertsOn = new();
    float alertTimer;

    void UpdateAlerts(float dt)
    {
        if ((alertTimer -= dt) > 0) return;
        alertTimer = 0.5f;
        foreach (var s in Squads)
        {
            if (s.Team != 0 || s.AiControlled || s.FoldedInto != null || s.Units.Count == 0) continue;
            var t = s.Units[0].Type;
            int routing = s.Units.Count(u => u.Active && u.Routing);
            bool live = s.Alive > 0 && routing <= s.Alive;
            float morale = AvgMorale(s), stamina = s.Units.Where(u => u.Active && !u.Routing).Select(u => u.Stamina).DefaultIfEmpty(100).Average();
            Raise("breaking", s, s.Alive > 0 && routing > s.Alive, $"{s.Name} are breaking!");
            Raise("wavering", s, live && morale < 40, $"{s.Name} wavering" + (s.Pressure > 1 ? " (flank)" : ""));
            Raise("flank", s, live && s.Pressure > 2 && morale >= 40, $"{s.Name} hit in the flank");
            Raise("arrows", s, live && t.Ammo > 0 && s.Units.All(u => !u.Active || u.Ammo == 0), $"{s.Name} out of arrows");
            Raise("tired", s, live && s.InContact && stamina < 25, $"{s.Name} tiring: relieve them");
            Raise("worn", s, live && s.Alive < s.StartCount * 0.35f, $"{s.Name} badly worn: fold them in");
            Raise("rally", s, live && s.Rallying, $"{s.Name} rallying to charge again");
            // Enemy horse coming at its flank or rear from close by.
            var horse = live ? Squads.FirstOrDefault(e => e.Team == 1 && e.Cavalry && e.Alive > 0 && Visible(e, 0)
                && Vector2.Distance(e.Center, s.Center) < 60 + s.Radius && Vector2.Dot(Vector2.Normalize(e.Center - s.Center + new Vector2(1e-3f, 0)), s.Front) < 0.3f) : null;
            Raise("horse", s, horse != null, $"Enemy horse at {s.Name}'s flank");
        }
        foreach (var d in Divisions)
            if (d.Team == 0) Raise("leaderless", d, d.Alive > 0 && d.Leader is { Active: false }, $"{d.Name} has lost its sergeant", d.Squads.FirstOrDefault(q => q.Alive > 0));
    }

    /// Raise an alert when its state comes on (once until it clears).
    void Raise(string kind, object key, bool on, string text, Squad? at = null)
    {
        var k = (kind, key);
        if (!on) { alertsOn.Remove(k); return; }
        if (!alertsOn.Add(k)) return;
        var s = at ?? key as Squad;
        Alerts.Add(new Alert(kind, text, s, s?.Center ?? Vector2.Zero, Time));
    }

    /// The alerts worth showing now: the newest few from the last `seconds`, whose state still holds.
    public IEnumerable<Alert> RecentAlerts(float seconds = 10, int most = 4) =>
        Alerts.AsEnumerable().Reverse().Where(a => Time - a.Time < seconds && (a.Squad == null || a.Squad.FoldedInto == null)).Take(most);
}
