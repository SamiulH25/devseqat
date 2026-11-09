using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Ironfield;

/// BattleScreen: the staged screenshot script (IRONFIELD_SHOTS).
partial class BattleScreen
{
    void ShotScript()
    {
        var b = battle;
        var cav = b.SquadsOf(1).FirstOrDefault(s => s.Cavalry) ?? b.SquadsOf(1).First();
        switch (shotIndex)
        {
            case 0 when b.Time > 0.5f:
                b.Player.Pos = new(17, -37 - Battle.Stand); b.Player.Heading = 0.35f; yaw = 0.35f; pitch = 0.12f; // beside our own lancers
                pendingShot = "field-horses"; shotIndex++; break;
            case 1 when b.Time > 2f:
                b.Player.Pos = new(0, -34 - Battle.Stand); yaw = MathHelper.PiOver2; pitch = 0.2f; aimDir = AttackDir.Right; // looking across the field
                pendingShot = "field-view"; shotIndex++; break;
            case 2 when b.Time > 3f:
                shotWheel = wheelOpen = true; wheelVec = new Vector2(60, -60);
                pendingShot = "order-wheel"; shotIndex++; break;
            case 3 when b.Time > 13f:
                shotWheel = wheelOpen = false;
                SetCommand(true); blend = 1; camFocus = cav.Centroid(); camYaw = 2.4f; camPitch = 0.55f; zoom = 26;
                selected.Add(b.Commanded.First());
                pendingShot = "command-cav-charge"; shotIndex++; break;
            case 4 when b.Time > 20f:
                camFocus = b.Centroid(0); camYaw = 1.9f; camPitch = 0.9f; zoom = 60;
                pendingShot = "command-overview"; shotIndex++; break;
            case 5 when b.Time > 20.5f:
                camFocus = new Vector2(0, -60); camYaw = MathHelper.PiOver2; camPitch = 0.32f; zoom = 90;
                pendingShot = "landscape"; shotIndex++; break;
            case 6 when b.Time > 21f && groundView != true:
                groundView = true; camFocus = b.Centroid(0) + new Vector2(0, 25); zoom = 50; camPitch = 0.7f;
                pendingShot = "ground-map"; break; // the ground map (docs/TERRAIN.md T6)
            case 6 when groundView == true && pendingShot == null:
                groundView = false; helpOpen = true;
                pendingShot = "controls"; shotIndex++; break;
            case 7 when pendingShot == null:
                helpOpen = false; b.Winner = 0; // the after-action overlay, staged as a win
                shotFrames = 0; shotIndex++; break;
            case 8 when shotFrames > 3:
                pendingShot = "battle-result"; shotIndex++; break;
            case 9 when pendingShot == null:
                Gallery();
                shotFrames = 0; shotIndex++; break;
            case 10 when shotFrames > 20:
                pendingShot = "gallery-front"; shotIndex++; break;
            case >= 11 and <= 13 when pendingShot == null:
                // Close-ups: foot, foot, horse.
                zoom = 7.5f; camPitch = 0.08f;
                camFocus = new Vector2(new[] { -5.6f, -16.8f, -31f }[shotIndex - 11], 0.6f);
                pendingShot = $"gallery-close{shotIndex - 10}"; shotIndex++; break;
            case 14 when pendingShot == null:
                foreach (var u in b.Units.Where(u => !u.IsLeader)) { u.Team = 1; u.Facing = u.Heading = 0; }
                zoom = 21; camPitch = 0.1f; camFocus = new Vector2(-19, 0);
                shotFrames = 0; shotIndex++; break;
            case 15 when shotFrames > 20:
                pendingShot = "gallery-side"; shotIndex++; break;
            case 16 when pendingShot == null:
                zoom = 7.5f; camPitch = 0.08f; camFocus = new Vector2(-31f, 0.6f);
                pendingShot = "gallery-side-close"; shotIndex++; break;
            case 17 when pendingShot == null:
                Gallery(new[] { "clansmen", "axemen", "huscarls", "greatswords", "crossbowmen", "horse_archers" }, 4);
                shotFrames = 0; shotIndex++; break;
            case 18 when shotFrames > 20:
                pendingShot = "gallery-north"; shotIndex++; break;
            case 19 when pendingShot == null:
                zoom = 8.5f; camPitch = 0.08f; camFocus = new Vector2(-9.8f, 0.6f);
                pendingShot = "gallery-north-close"; shotIndex++; break;
            case 20 when pendingShot == null:
                Gallery(new[] { "goblin", "goblin_archer", "hobgoblin", "goblin_boss", "goblin_wolf_rider", "wolf", "dire_wolf", "troll", "war_hounds", "ogre", "beast_hunters", "shieldmen" }, 3); // creatures (docs/CREATURES.md), a man for scale
                shotFrames = 0; shotIndex++; break;
            case 21 when shotFrames > 20:
                zoom = 11f; camPitch = 0.1f; camFocus = new Vector2(-9.8f, 0.6f);
                pendingShot = "gallery-goblins"; shotIndex++; break;
            case 22 when pendingShot == null:
                foreach (var u in b.Units.Where(u => !u.IsLeader)) u.Facing = u.Heading = u.ViewFacing = u.ViewHeading = 0;
                shotFrames = 0; shotIndex++; break;
            case 23 when shotFrames > 20:
                pendingShot = "gallery-creatures-side"; shotIndex++; break;
            case 24 when pendingShot == null:
                foreach (var u in b.Units.Where(u => !u.IsLeader)) u.Facing = u.Heading = u.ViewFacing = u.ViewHeading = -MathHelper.PiOver2;
                zoom = 12f; camPitch = 0.12f; camFocus = b.Units.First(u => u.Type.Id == "troll").Pos + new Vector2(-4f, 0.6f);
                shotFrames = 0; shotIndex++; break;
            case 25 when shotFrames > 20:
                pendingShot = "gallery-troll"; shotIndex++; break;
            case 26 when pendingShot == null:
                Gallery(new[] { "skeleton", "ghoul", "necromancer", "skeleton", "ghoul", "shieldmen" }, 3); // the undead (phase 5), a man for scale
                zoom = 9f; camPitch = 0.1f;
                shotFrames = 0; shotIndex++; break;
            case 27 when shotFrames > 20:
                pendingShot = "gallery-undead"; shotIndex++; break;
            case 28 when pendingShot == null:
                foreach (var u in b.Units.Where(u => !u.IsLeader)) u.Facing = u.Heading = u.ViewFacing = u.ViewHeading = 0;
                shotFrames = 0; shotIndex++; break;
            case 29 when shotFrames > 20:
                pendingShot = "gallery-undead-side"; shotIndex++; break;
            case >= 30 and <= 37 when pendingShot == null && shotIndex % 2 == 0:
                // The four realms of docs/WORLD.md W3-W4, each in its own colours.
                Gallery(NewRealms[(shotIndex - 30) / 2].Troops, NewRealms[(shotIndex - 30) / 2].Palette);
                zoom = 19; camYaw = MathHelper.PiOver2 + 0.5f; camPitch = 0.14f;
                foreach (var u in battle.Units.Where(u => !u.IsLeader)) u.Facing = u.Heading = u.ViewFacing = u.ViewHeading = -0.9f; // three-quarter view
                shotFrames = 0; shotIndex++; break;
            case >= 31 and <= 37 when shotFrames > 20 && shotIndex % 2 == 1:
                pendingShot = $"gallery-{NewRealms[(shotIndex - 31) / 2].Name}"; shotIndex++; break;
            case 38 or 40 or 42 when pendingShot == null:
                PoseSheet((shotIndex - 38) / 2); shotFrames = 0; shotIndex++; break;
            case 39 or 41 or 43 when shotFrames > 3:
                pendingShot = new[] { "pose-sheet", "pose-sheet2", "pose-horses" }[(shotIndex - 39) / 2]; shotIndex++; break;
            case 44 when pendingShot == null: Exit(); break;
        }
    }

    static readonly (string Name, int Palette, string[] Troops)[] NewRealms =
    {
        ("sunward", 5, new[] { "sun_javelins", "ghazis", "jandars", "lions_of_the_sun", "sand_archers", "sultans_bows", "desert_riders", "mamluks", "sultans_guard" }),
        ("khaganate", 6, new[] { "yurt_guards", "torguud", "khagans_axes", "grass_spears", "eternal_spears", "steppe_archers", "steppe_bows", "kheshig", "khagans_own" }),
        ("saltmarsh", 7, new[] { "canal_guards", "brigandiers", "lion_guard", "league_pikes", "league_crossbows", "pavisiers", "lion_crossbows", "stradiots", "lion_knights" }),
        ("order", 8, new[] { "order_sergeants", "ashen_brothers", "ashen_saints", "order_spears", "dawn_pikes", "order_crossbows", "dawn_watch", "order_riders", "grand_masters" }),
    };

    /// The pose sheet (docs/VISUALS.md V1): one man frozen at each moment of each action along a row, so every pose can be
    /// checked in one still. Sheet 0: the melee and the march; 1: the bow, the fall and the cheer; 2: riders at a stand, walk, trot, canter and gallop, the last with his lance couched, then a horse rearing, one falling and one dead.
    void PoseSheet(int sheet)
    {
        battle = Battle.Empty(1);
        SetWorld();
        battle.Time = 20;
        var b = battle;
        float x = 0;
        Unit Add(string troop, int team = 0)
        {
            var t = Troops.Get(troop);
            x += t.Mount != null ? 2.4f : 1.25f;
            var u = b.AddSquad(team, t, 1, new Vector2(-x, 0), 1, -Vector2.UnitY).Units[0];
            u.ViewFacing = u.ViewHeading = u.Facing = u.Heading = -MathHelper.PiOver2 - 0.6f; // three-quarter view
            u.Palette = 0;
            x += t.Mount != null ? 2.4f : 1.25f;
            return u;
        }
        if (sheet == 2)
        {
            foreach (var (speed, anim) in new[] { (0f, 0f), (1.8f, 0.3f), (4f, 0.3f), (7.5f, 0.3f), (11f, 0.3f), (11f, 0.8f) })
            {
                var u = Add("knights");
                u.Speed = speed; u.Anim = anim; u.Vel = u.HeadingDir * speed;
                if (speed > 8) u.Couched = anim > 0.5f;
            }
            frozenRear.Add(Add("knights")); // a horse rearing at the top
            foreach (var k in new[] { 0.8f, 2f }) // a horse going down, and one lying dead
            {
                x += 2.4f;
                b.DeadHorses.Add(new DeadHorse(new Vector2(-x, 0), -MathHelper.PiOver2 - 0.6f, 0, 1, "", b.Time - k * 0.7f));
                x += 2.4f;
            }
        }
        else if (sheet == 0)
        {
            const string foot = "men_at_arms";
            Add(foot); // idle
            var w = Add(foot); w.Action = UnitAction.Windup; w.Swing = AttackDir.Right; w.ActionTimer = w.Type.Windup * 0.5f;
            w = Add(foot); w.Action = UnitAction.Windup; w.Swing = AttackDir.Overhead; w.ActionTimer = w.Type.Windup * 0.05f;
            w = Add(foot); w.Action = UnitAction.Recover; w.Swing = AttackDir.Overhead; w.ActionTimer = w.Type.Recover * 0.88f; // the strike
            w = Add(foot); w.Action = UnitAction.Recover; w.Swing = AttackDir.Right; w.ActionTimer = w.Type.Recover * 0.4f;
            w = Add(foot); w.Action = UnitAction.Block;
            w = Add(foot); w.Action = UnitAction.Stagger; w.HitFlash = 0.1f;
            w = Add(foot); w.Vel = w.Dir * 1.4f; w.Anim = 0.5f; // walking
            w = Add(foot); w.Vel = w.Dir * 4.5f; w.Anim = 0.5f; // running
            w = Add(foot); w.Routing = true; w.Vel = w.Dir * 4f; w.Anim = 1.5f;
        }
        else
        {
            const string foot = "men_at_arms";
            var a = Add("longbowmen"); a.Ammo = 10; a.EnemyDist = 20; a.Cooldown = 1.0f; // nocking
            a = Add("longbowmen"); a.Ammo = 10; a.EnemyDist = 20; a.Cooldown = 0.05f; // full draw
            a = Add("longbowmen"); a.Action = UnitAction.Recover; a.Ammo = 10; // the release
            foreach (var k in new[] { 0.35f, 0.7f })
            {
                var d = Add(foot); d.Dead = true; d.DiedAt = b.Time - k * 0.6f; d.DiedFrom = d.Pos + d.Dir * 2; // falling back
            }
            var dead = Add(foot); dead.Dead = true; dead.DiedAt = 0; dead.DiedFrom = dead.Pos + dead.Dir * 2;
            var cheer = Add(foot, 1); cheer.Palette = 0; b.Winner = 1; // the battle won: he cheers
        }
        paused = hudHidden = true;
        SetCommand(true);
        blend = 1;
        camFocus = new Vector2(-x / 2, 0.6f);
        camYaw = MathHelper.PiOver2;
        camPitch = 0.1f;
        zoom = x * 0.8f;
    }

    /// Debug lineup of every troop, left to right along each upgrade line, facing the camera.
    void Gallery(string[]? only = null, int palette = 0)
    {
        battle = Battle.Empty(1);
        SetWorld();
        var order = only ?? new[] { "levies", "shieldmen", "men_at_arms", "ironguard", "spearrunners", "pikemen", "bowmen", "longbowmen",
            "outriders", "lancers", "knights", "captain" };
        float x = 0;
        foreach (var id in order)
        {
            var t = Troops.Get(id);
            x += t.Mount != null ? 2.2f : 1.4f;
            var u = battle.AddSquad(0, t, 1, new Vector2(-x, 0), 1, -Vector2.UnitY).Units[0];
            u.ViewFacing = u.ViewHeading = u.Facing;
            u.Palette = palette;
            x += t.Mount != null ? 2.2f : 1.4f;
        }
        paused = hudHidden = true;
        SetCommand(true);
        blend = 1;
        camFocus = new Vector2(-x / 2, 0);
        camYaw = MathHelper.PiOver2;
        camPitch = 0.1f;
        zoom = 21;
    }
}
