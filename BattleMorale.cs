using Microsoft.Xna.Framework;

namespace Ironfield;

/// Battle: squad morale, wavering and routing.
public partial class Battle
{
    /// How much sooner a craven creature breaks than a man (it routs below RoutAt + this).
    public const float CravenRout = 14;

    void UpdateMorale(float dt)
    {
        int all0 = 0, gone0 = 0, all1 = 0, gone1 = 0;
        bool anyRouting = false, anyFear = false, anyFastHorse = false, anyTier6 = false;
        var units = Units;
        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            if (u.Team == 0) { all0++; if (!u.Active) gone0++; }
            else { all1++; if (!u.Active) gone1++; }
            if (!u.Active) continue;
            if (u.Routing) anyRouting = true;
            else if (u.Type.Tier >= 6 && !u.Type.Wild) anyTier6 = true;
            if (u.Mounted && u.Speed > 7) anyFastHorse = true;
            if (u.Type.Has("fear")) anyFear = true;
        }
        float lost0 = all0 == 0 ? 0 : gone0 / (float)all0;
        float lost1 = all1 == 0 ? 0 : gone1 / (float)all1;
        // Each man's morale is worked out every 2nd tick (half the men each tick) over twice the time step.
        float step = dt * 2;
        float auraR2 = Options.AuraRadius * Options.AuraRadius;
        for (int ui = 0; ui < units.Count; ui++)
        {
            var u = units[ui];
            if (!u.Active || u.IsLeader || u.Type.Has("fearless")) continue; // leaders (and trolls) never break
            if ((tick + u.Index) % 2 != 0) continue;
            var lead = u.Team == 0 ? Player : EnemyCaptain;
            // The enemy captain stands just behind his line, so his aura covers it all battle. Yours only reaches
            // men around your body, and not at all from command view: a side whose general is alive but looking
            // at a map would otherwise be permanently out-inspired.
            bool inspired = lead.Active && !(lead.IsPlayer && Commanding) && (u.Team == 1 || BannerUp)
                && Vector2.DistanceSquared(lead.Pos, u.Pos) < auraR2;
            bool led = !lead.Dead; // a living general heartens his whole army, wherever he stands
            float m = 0;
            if (inspired) m += u.Routing ? 10 : 4;
            else if (led) m += u.Routing ? 3 : 1.5f; // a living general heartens the whole army, wherever he stands
            // A division's sergeant steadies the men around him.
            if (!inspired && u.Squad?.Division?.Leader is { Active: true } sgt && sgt != u && Vector2.DistanceSquared(sgt.Pos, u.Pos) < 100) m += u.Routing ? 4 : 1.5f;
            bool craven = u.Type.Has("craven");
            if (u.Routing)
            {
                // A broken man is running, not watching his company die: once he is clear of the enemy his nerve
                // returns, so a rout can end short of the map's edge. A man who breaks twice has had enough.
                float foe = Vector2.Distance(u.Pos, centroid[1 - u.Team]);
                // Goblins scatter at the first scare but come back again and again, as long as their boss lives.
                if (craven) m += BossDown[u.Team] ? -2f : foe > 18 ? 3.5f : -0.5f;
                else m += u.Routs > RalliesOf(u.Type) ? -2f : foe > 26 ? 2f : -0.8f;
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
            float teamLost = u.Team == 0 ? lost0 : lost1;
            if (teamLost > 0.5f) m -= 0.5f;          // the army is losing
            // Neighbours running away, and horsemen thundering at you, are frightening.
            int routing = 0;
            bool charged = false, feared = false, steadied = false;
            bool canFear = anyFear && u.Type.VsMonster <= 1 && (u.Type.Tier < 5 || u.Type.Wild);
            bool canCharge = anyFastHorse && !u.Mounted && !Braced(u);
            bool canSteady = anyTier6 && u.Type.Tier < 6;
            if (anyRouting || canFear || canCharge || canSteady)
            {
                var up = u.Pos;
                int uTeam = u.Team;
                int x0 = CellOf(up.X - 8), x1 = CellOf(up.X + 8), z0 = CellOf(up.Y - 8), z1 = CellOf(up.Y + 8);
                bool done = false;
                for (int z = z0; z <= z1 && !done; z++)
                {
                    int row = z * GridN;
                    for (int x = x0; x <= x1 && !done; x++)
                    {
                        var cell = cells[row + x];
                        int cnt = cell.Count;
                        for (int i = 0; i < cnt; i++)
                        {
                            var o = cell[i];
                            if (!o.Active || o == u) continue;
                            float d2 = Vector2.DistanceSquared(o.Pos, up);
                            if (d2 >= 64) continue;
                            if (o.Team == uTeam)
                            {
                                if (o.Routing && d2 < 49) routing++;
                                else if (canSteady && !steadied && !o.Routing && o.Type.Tier >= 6 && !o.Type.Wild) steadied = true; // an elite man near steadies (TI1)
                            }
                            else if (canFear && !feared && o.Type.Has("fear")) feared = true; // tier V and up have seen worse (TI1) // hunters know what they face
                            else if (canCharge && !charged && o.Mounted && o.Speed > 7
                                && Vector2.Dot(o.HeadingDir, Vector2.Normalize(up - o.Pos)) > 0.85f) charged = true;
                            if (routing >= 7 && (!canFear || feared) && (!canCharge || charged) && (!canSteady || steadied)) { done = true; break; }
                        }
                    }
                }
            }
            m -= MathF.Min(2, routing * 0.3f) * shock;
            if (charged) m -= 2.5f * shock;
            if (feared) m -= 2.5f * shock; // a troll looming over you
            if (steadied && u.Type.Tier < 6) m += 0.4f;
            u.Morale = Math.Clamp(u.Morale + m * step, 0, 100);
            // Iron Will: near you, a company that still has a third of its men will not break.
            if (Options.IronWill && u.Team == 0 && inspired && u.Squad.Alive >= u.Squad.StartCount * 0.3f) u.Morale = MathF.Max(u.Morale, 16);
            if (u.Mods.Holdfast > 0 && !u.Routing && teamLost < 0.5f) u.Morale = MathF.Max(u.Morale, 16); // paid in full: they stand while the day is not lost
            if (u.Team == 0 && inspired && Player.Active && Perk("Warlord")) u.Morale = MathF.Max(u.Morale, 16); // Warlord: none break while you stand
            if (u.Squad.Heartened && !u.Routing) u.Morale = MathF.Max(u.Morale, 12); // they brought the captain down (C12)
            float routAt = (u.Squad.Company?.Has("Stubborn") == true || (Wall != null && u.Team == 1 && !Wall.AnyGateOpen) ? RoutAt - 4 : RoutAt) - (u.Squad.Company?.RoutBonus ?? 0) // officer trait; walls; honours
                + (craven ? CravenRout : 0);
            if (!u.Routing && u.Morale < routAt) { u.Routing = true; anyRouting = true; u.Routs++; u.Action = UnitAction.None; u.Couched = false; }
            else if (u.Routing && u.Morale > RallyAt && (craven ? !BossDown[u.Team] : u.Routs <= RalliesOf(u.Type) && !(u.Type.Bound && BossDown[u.Team])))
            {
                u.Routing = false;
                u.Morale = MathF.Max(u.Morale, Battle.RallyAt + 2); // they come back shaken but steady
            }
        }
    }
}
