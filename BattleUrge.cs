namespace Ironfield;

using Microsoft.Xna.Framework;

/// Battle: each man's own itch to go in (docs/archive/MELEE.md D4, reworked). A man has a temper (low drill, a hothead's company, a
/// low grit draw) and an urge that rises with a foe close by, a blow taken and neighbours lunging, and falls with drill, a
/// sergeant or your banner near, and a company in good order. At the top he steps out of the ranks alone (`Lunging`) and
/// runs at the nearest foe; a sergeant pulls such men back. When enough of a company has gone, it charges as a body
/// (`TipCompany`). The mirror: a drilled man with his nerve low holds back from the blows (`Hesitant`). Deterministic:
/// only sim state and the man's fixed `Grit`, so replays match.
public sealed partial class Battle
{
    const float LungeAtUrge = 0.75f, LungeEndUrge = 0.4f, TipShare = 0.35f, HotTipShare = 0.25f;
    readonly List<Unit> nearUrge = new(16);

    /// How readily a man goes in unordered (0-1): untrained men and hotheads high, drilled men low; his fixed grit draw spreads a company.
    float TemperOf(Unit u, Squad s)
    {
        float t = 1.1f - 1.2f * s.Drill + (0.5f - u.Grit) * 0.6f;
        if (s.Company?.Has("Hothead") == true) t += 0.25f;
        if (s.Company?.Has("Stubborn") == true) t -= 0.1f;
        return Math.Clamp(t, 0, 1);
    }

    /// May this company's men go in unordered at all: foot in the open field holding their ground (not bows, beasts, the dead, leaders).
    bool ImpulsiveCompany(Squad s)
    {
        if (s.Order != Order.Hold || s.BrokeRanks || s.BreakOffUntil > Time || s.Swarm || s.Cavalry || Wall != null || s.Alive == 0 || s.Units.Count == 0) return false;
        var t = s.Units[0].Type;
        return t.Ammo <= 0 && !t.Wild && !t.Has("raised") && !t.Leader;
    }

    /// Every 4th tick for each man: his urge, and whether he lunges or hangs back.
    void UpdateUrge(float dt)
    {
        float step = dt * 4;
        foreach (var u in Units)
        {
            if ((tick + u.Index) % 4 != 0) continue;
            var s = u.Squad;
            if (s == null || !u.Active || u.Routing || u.Mounted || u.IsPlayer || u.IsLeader || u.Hero || u.Sergeant || !ImpulsiveCompany(s))
            {
                if (u.Urge != 0 || u.Lunging || u.Hesitant) { u.Urge = 0; u.Lunging = false; u.Hesitant = false; }
                continue;
            }
            float d = u.EnemyDist, temper = TemperOf(u, s);
            bool steady = Steadied(s);
            float gain = 0;
            if (!steady)
            {
                if (d < 18) gain += 0.9f * (18 - d) / 18 * temper;
                if (d < 20 && Time - u.HurtAt < 3) gain += 0.25f * temper; // a blow taken, a foe close by: he wants to hit back
                int lunging = 0;
                foreach (var o in Near(u.Pos, 5, nearUrge)) if (o != u && o.Lunging && o.Team == u.Team && o.Squad == s) lunging++;
                gain += 0.05f * Math.Min(4, lunging) * temper; // others going in pulls him along
            }
            float loss = 0.08f + 0.4f * s.Drill + 0.2f * s.Formed * s.Drill + (steady ? 0.6f : 0) + (SergeantUp(s) ? 0.1f : 0); // his sergeant holds him back
            u.Urge = Math.Clamp(u.Urge + (gain - loss) * step, 0, 1);
            if (u.Lunging)
            {
                if (d < 3) u.Urge = MathF.Max(u.Urge, 0.6f); // at blows he stays
                if (u.Urge < LungeEndUrge || d > 30 || Time - u.LungeAt > 15) u.Lunging = false; // called back, or nothing left to run at
            }
            else if (u.Urge >= LungeAtUrge && d < 18) { u.Lunging = true; u.LungeAt = Time; }
            u.Hesitant = !u.Lunging && !steady && temper < 0.4f && u.Morale < 55 && d < 10; // a drilled man with his nerve gone
        }
    }

    /// Once a second: a company's restlessness, a warning to you, and the tipping point when enough of it has lunged.
    void TipCompany(Squad s)
    {
        if (s.BrokeRanks && !s.Broken && !s.InContact) s.BrokeRanks = false; // re-formed: back under orders
        if (!ImpulsiveCompany(s)) { s.Restless = 0; if (s.Team == 0) Raise("restless", s, false, ""); return; }
        int n = 0, lunging = 0;
        float urge = 0;
        foreach (var u in s.Units)
        {
            if (!u.Active || u.Routing) continue;
            n++; urge += u.Urge;
            if (u.Lunging) lunging++;
        }
        if (n == 0) return;
        s.Restless = urge / n;
        if (s.Team == 0 && !s.AiControlled) Raise("restless", s, s.Restless >= 0.3f, $"{s.Name} straining at the leash: steady them");
        bool hot = s.Company?.Has("Hothead") == true;
        if (lunging < Math.Min(n, 3) || lunging < n * (hot ? HotTipShare : TipShare)) return;
        var foe = NearestFoe(s);
        if (foe == null) return;
        s.Order = Order.Charge;
        s.Target = foe;
        s.Formed = MathF.Min(s.Formed, 0.3f);
        s.BrokeRanks = true;
        string name = s.Company?.Name is { Length: > 0 } nm ? nm : s.Name;
        Log(s.Team == 0 ? $"{name} break ranks and charge!" : $"The enemy {name} break ranks!");
        Sound(Cue.Shout, s.Center);
    }
}
