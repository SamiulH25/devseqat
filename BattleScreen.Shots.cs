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
                b.Player.Pos = new(17, -37); b.Player.Heading = 0.35f; yaw = 0.35f; pitch = 0.12f; // beside our own lancers
                pendingShot = "field-horses"; shotIndex++; break;
            case 1 when b.Time > 2f:
                b.Player.Pos = new(0, -34); yaw = MathHelper.PiOver2; pitch = 0.2f; aimDir = AttackDir.Right; // looking across the field
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
            case 6 when b.Time > 21f:
                helpOpen = true;
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
                Gallery(new[] { "goblin", "goblin_archer", "hobgoblin", "goblin_boss", "wolf", "dire_wolf", "shieldmen" }, 3); // creatures (docs/CREATURES.md), a man for scale
                shotFrames = 0; shotIndex++; break;
            case 21 when shotFrames > 20:
                zoom = 11f; camPitch = 0.1f; camFocus = new Vector2(-9.8f, 0.6f);
                pendingShot = "gallery-goblins"; shotIndex++; break;
            case 22 when pendingShot == null:
                foreach (var u in b.Units.Where(u => !u.IsLeader)) u.Facing = u.Heading = u.ViewFacing = u.ViewHeading = 0;
                shotFrames = 0; shotIndex++; break;
            case 23 when shotFrames > 20:
                pendingShot = "gallery-creatures-side"; shotIndex++; break;
            case 24 when pendingShot == null: Exit(); break;
        }
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
