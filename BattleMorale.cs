using Microsoft.Xna.Framework;

namespace Ironfield;

/// Battle: squad morale, wavering and routing.
public partial class Battle
{
    /// How much sooner a craven creature breaks than a man (it routs below RoutAt + this).
    public const float CravenRout = 14;

    void UpdateMorale(float dt)
    {
        var lost = new float[2];
        for (int t = 0; t < 2; t++)
        {
            int all = 0, gone = 0;
            foreach (var u in Units) if (u.Team == t) { all++; if (!u.Active) gone++; }
            lost[t] = all == 0 ? 0 : gone / (float)all;
        }
        // Each man's morale is worked out every 2nd tick (half the men each tick) over twice the time step.
        float step = dt * 2;
        foreach (var u in Units)
        {
            if (!u.Active || u.IsLeader || u.Type.Has("fearless")) continue; // leaders (and trolls) never break
            if ((tick + u.Index) % 2 != 0) continue;
            var lead = u.Team == 0 ? Player : EnemyCaptain;
            // The enemy captain stands just behind his line, so his aura covers it all battle. Yours only reaches
            // men around your body, and not at all from command view: a side whose general is alive but looking
            // at a map would otherwise be permanently out-inspired.
            bool inspired = lead.Active && !(lead.IsPlayer && Commanding) && (u.Team == 1 || BannerUp)
                && Vector2.DistanceSquared(lead.Pos, u.Pos) < Options.AuraRadius * Options.AuraRadius;
            bool led = !lead.Dead; // a living general heartens his whole army, wherever he stands
            float m = 0;
            if (inspired) m += u.Routing ? 10 : 4;
            else if (led) m += u.Routing ? 3 : 1.5f; // a living general heartens the whole army, wherever he stands
            // A division's sergeant steadies the men around him.
            if (!inspired && u.Squad?.Division?.Leader is { Active: true } sgt && sgt != u && Vector2.DistanceSquared(sgt.Pos, u.Pos) < 100) m += u.Routing ? 4 : 1.5f;
            if (u.Routing)
            {
                // A broken man is running, not watching his company die: once he is clear of the enemy his nerve
                // returns, so a rout can end short of the map's edge. A man who breaks twice has had enough.
                float foe = Vector2.Distance(u.Pos, centroid[1 - u.Team]);
                // Goblins scatter at the first scare but come back again and again, as long as their boss lives.
                if (u.Type.Has("craven")) m += BossDown[u.Team] ? -2f : foe > 18 ? 3.5f : -0.5f;
                else m += u.Routs > 1 ? -2f : foe > 26 ? 2f : -0.8f;
            }
            else
            {
                if (u.Squad!.Alive < u.Squad.StartCount * 0.4f && u.Mods.Fury <= 0) m -= 1.5f; // battle fury: losses only anger them
                if (u.EnemyDist > 12 && u.Morale < 60) m += 1.5f;
                // A company that still has most of its men steadies itself.
                if (u.Morale < 45 && u.Squad.Alive > u.Squad.StartCount * 0.6f) m += 1.5f;
            }
            float shock = MathF.Max(0, 1 + u.Mods.Shock); // unshakeable men feel it less (BattleEffects.cs)
            m += u.Mods.MoralePerSec;
            m -= MathF.Min(3, u.Squad!.Pressure * 0.6f) * shock; // attacked in the flank or rear
            if (lost[u.Team] > 0.5f) m -= 0.5f;          // the army is losing
            // Neighbours running away, and horsemen thundering at you, are frightening.
            int routing = 0;
            bool charged = false, feared = false;
            foreach (var o in Near(u.Pos, 8, near))
            {
                if (!o.Active || o == u) continue;
                float d2 = Vector2.DistanceSquared(o.Pos, u.Pos);
                if (o.Team == u.Team) { if (o.Routing && d2 < 49) routing++; }
                else if (o.Type.Has("fear") && d2 < 64 && u.Type.VsMonster <= 1) feared = true; // hunters know what they face
                else if (!u.Mounted && o.Mounted && o.Speed > 7 && d2 < 64 && !Braced(u)
                    && Vector2.Dot(o.HeadingDir, Vector2.Normalize(u.Pos - o.Pos)) > 0.85f) charged = true;
            }
            m -= MathF.Min(2, routing * 0.3f) * shock;
            if (charged) m -= 2.5f * shock;
            if (feared) m -= 2.5f * shock; // a troll looming over you
            u.Morale = Math.Clamp(u.Morale + m * step, 0, 100);
            // Iron Will: near you, a company that still has a third of its men will not break.
            if (Options.IronWill && u.Team == 0 && inspired && u.Squad.Alive >= u.Squad.StartCount * 0.3f) u.Morale = MathF.Max(u.Morale, 16);
            if (u.Mods.Holdfast > 0 && !u.Routing && lost[u.Team] < 0.5f) u.Morale = MathF.Max(u.Morale, 16); // paid in full: they stand while the day is not lost
            if (u.Team == 0 && inspired && Player.Active && Perk("Warlord")) u.Morale = MathF.Max(u.Morale, 16); // Warlord: none break while you stand
            if (u.Squad.Heartened && !u.Routing) u.Morale = MathF.Max(u.Morale, 12); // they brought the captain down (C12)
            float routAt = (u.Squad.Company?.Has("Stubborn") == true || (Wall != null && u.Team == 1 && !Wall.GateOpen) ? RoutAt - 4 : RoutAt) - (u.Squad.Company?.RoutBonus ?? 0) // officer trait; walls; honours
                + (u.Type.Has("craven") ? CravenRout : 0);
            if (!u.Routing && u.Morale < routAt) { u.Routing = true; u.Routs++; u.Action = UnitAction.None; u.Couched = false; }
            else if (u.Routing && u.Morale > RallyAt && (u.Type.Has("craven") ? !BossDown[u.Team] : u.Routs <= 1 && !(u.Type.Bound && BossDown[u.Team])))
            {
                u.Routing = false;
                u.Morale = MathF.Max(u.Morale, Battle.RallyAt + 2); // they come back shaken but steady
            }
        }
    }
}
