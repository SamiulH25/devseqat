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
            case 44 or 46 when pendingShot == null:
                // Variety: a row of one troop, every man with his own face, and some another helm or weapon.
                Gallery(Enumerable.Repeat(shotIndex == 44 ? "shieldmen" : "levies", 7).ToArray());
                zoom = 12f; camPitch = 0.06f;
                shotFrames = 0; shotIndex++; break;
            case 45 or 47 when shotFrames > 20:
                pendingShot = shotIndex == 45 ? "gallery-variety" : "gallery-variety2"; shotIndex++; break;
            case 48 when pendingShot == null:
                // Armour: one man in each body armour, close up, front then side.
                Gallery(new[] { "shieldmen", "men_at_arms", "ironguard", "dune_spears", "league_pikes", "steppe_bows", "clansmen", "foot_knights" });
                zoom = 9.5f; camPitch = 0.06f; camFocus = new Vector2(-5.6f, 0.6f);
                shotFrames = 0; shotIndex++; break;
            case 49 when shotFrames > 20:
                pendingShot = "gallery-armour"; shotIndex++; break;
            case 50 when pendingShot == null:
                camFocus = new Vector2(-16.8f, 0.6f);
                pendingShot = "gallery-armour2"; shotIndex++; break;
            case 51 when pendingShot == null:
                foreach (var u in battle.Units.Where(u => !u.IsLeader)) u.Facing = u.Heading = u.ViewFacing = u.ViewHeading = 0;
                camFocus = new Vector2(-5.6f, 0.6f);
                shotFrames = 0; shotIndex++; break;
            case 52 when shotFrames > 20:
                pendingShot = "gallery-armour-side"; shotIndex++; break;
            case 53 or 55 when pendingShot == null:
                // Melee (docs/MELEE.md): three companies a side, 14 s after they charged; levies, then elites.
                Clash(shotIndex == 53 ? "levies" : "immortals"); shotFrames = 0; shotIndex++; break;
            case 54 or 56 when shotFrames > 20:
                pendingShot = shotIndex == 54 ? "clash-levies" : "clash-elites"; shotIndex++; break;
            case 57 or 59 when pendingShot == null:
                MagicScene(shotIndex == 57); shotFrames = 0; shotIndex++; break;
            case 58 or 60 when shotFrames > 12:
                pendingShot = shotIndex == 58 ? "magic-spells" : "magic-aura"; shotIndex++; break;
            case 61 or 63 or 65 when pendingShot == null:
                SpectacleScene((shotIndex - 61) / 2); shotFrames = 0; shotIndex++; break;
            case 62 or 64 or 66 when shotFrames > 12:
                pendingShot = shotIndex switch { 62 => "spectacle-night", 64 => "spectacle-breakthrough", _ => "gather-great" }; shotIndex++; break;
            case >= 67 and < 83 when shotIndex % 2 == 1 && pendingShot == null:
                WorkScene((shotIndex - 67) / 2); shotFrames = 0; shotIndex++; break;
            case >= 67 and < 83 when shotIndex % 2 == 0 && shotFrames > 12:
                pendingShot = WorkShots[(shotIndex - 68) / 2]; shotIndex++; break;
            case 83 when pendingShot == null: Exit(); break;
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

    /// Three companies of `troop` a side charge each other; the sim runs 14 s, then the view is held from above one flank.
    void Clash(string troop)
    {
        battle = Battle.Empty(7);
        SetWorld();
        var b = battle;
        var t = Troops.Get(troop);
        var mine = new List<Squad>();
        var theirs = new List<Squad>();
        for (int i = -1; i <= 1; i++)
        {
            mine.Add(b.AddSquad(0, t, 24, new Vector2(i * 14, -14), 8, Vector2.UnitY));
            theirs.Add(b.AddSquad(1, t, 24, new Vector2(i * 14, 14), 8, -Vector2.UnitY));
        }
        for (int i = 0; i < 3; i++) { mine[i].Order = theirs[i].Order = Order.Charge; mine[i].Target = theirs[i]; theirs[i].Target = mine[i]; }
        for (int i = 0; i < 60 * 14; i++) b.Update(1 / 60f);
        foreach (var u in b.Units) { u.ViewFacing = u.Facing; u.ViewHeading = u.Heading; }
        paused = hudHidden = true;
        SetCommand(true);
        blend = 1;
        camFocus = Vector2.Zero;
        camYaw = MathHelper.PiOver2 + 0.35f;
        camPitch = 0.6f;
        zoom = 19;
    }

    /// Magic and aura staged for a shot (docs/MAGIC_AURA.md 6): spells (a smite's pillar, lightning, burning ground, mist, a
    /// ward, a blessing) or the captain's aura (the blade lit, a burst, a slash, presence).
    void MagicScene(bool spells)
    {
        battle = Battle.Empty(9, new BattleOptions { AuraTier = 4, Forms = new[] { "red_rose_bloom", "rose_cut", "thorn_edge" } });
        SetWorld();
        var b = battle;
        var circle = b.AddSquad(0, Troops.Get("saints_militant"), 3, new Vector2(-6, -14), 3, Vector2.UnitY);
        var mine = b.AddSquad(0, Troops.Get("men_at_arms"), 16, new Vector2(6, -10), 8, Vector2.UnitY);
        var foes = b.AddSquad(1, Troops.Get("levies"), 24, new Vector2(0, 12), 8, -Vector2.UnitY);
        var foes2 = b.AddSquad(1, Troops.Get("bowmen"), 16, new Vector2(-18, 16), 8, -Vector2.UnitY);
        circle.Order = mine.Order = foes.Order = foes2.Order = Order.Hold;
        b.Player.Pos = new Vector2(0, 2);
        b.Player.Facing = b.Player.Heading = MathHelper.PiOver2;
        b.Input = new PlayerInput { Aura = !spells, Dir = AttackDir.Right, Facing = MathHelper.PiOver2 };
        for (int i = 0; i < 30; i++) b.Update(1 / 60f);
        var by = circle.Units[0];
        if (spells)
        {
            b.AddField(Abilities.Get("wildfire"), "burning", 0, by, new Vector2(-18, 16), 6, 60, 1, 10);
            b.AddField(Abilities.Get("mist"), "mist", 0, by, new Vector2(18, 14), 9, 60, 1, 0);
            b.AddField(Abilities.Get("ward"), "warded", 0, by, new Vector2(6, -10), 8, 60, 1, 6);
            b.AddField(Abilities.Get("mirage"), "mirage", 0, by, new Vector2(20, -6), 5, 60, 1, 0);
            b.AddTimed(mine, Abilities.Get("blessing"), 1);
        }
        for (int i = 0; i < 110; i++) Effects(1 / 60f); // the steady effects (fire, mist, presence) build up first
        if (spells)
        {
            b.Flash("spell:attack", new Vector2(0, 12), 3, 0, "faith");
            b.Flash("bolt", new Vector2(8, 13), 1, 0, "craft");
            b.Flash("cast", by.Pos, 1, 0, "faith");
            b.Flash("spell:buff", mine.Center, 10, 0, "faith");
        }
        else
        {
            b.Flash("form:burst", b.Player.Pos, 5, 0, "rose");
            b.Flash("land:slash", b.Player.Pos, 8, 0, "rose", Vector2.UnitY);
        }
        for (int i = 0; i < 14; i++) { b.Update(1 / 60f); Effects(1 / 60f); SpectacleFrame(1 / 60f); } // a moment on: rings spread, sparks fly
        foreach (var u in b.Units) { u.ViewFacing = u.Facing; u.ViewHeading = u.Heading; }
        paused = hudHidden = true;
        SetCommand(true);
        blend = 1;
        camFocus = spells ? new Vector2(0, 4) : b.Player.Pos;
        camYaw = MathHelper.PiOver2 + 0.5f;
        camPitch = spells ? 0.55f : 0.45f;
        zoom = spells ? 46 : 16;
    }

    /// The spectacle pass (docs/SPECTACLE.md). 0: a smite falling at night beside a burning field, a Transcendent captain
    /// shining, to show the moving lights and the glass and scorch scars. 1: a breakthrough at its height, the colour drained
    /// from all but the captain, the beat's bars drawn, the men near him leaning back. 2: a great working (the Tempest) at
    /// 70% gathered beside a smite at half (S2): the sigils, the pull, the column and the darkened sky.
    void SpectacleScene(int which)
    {
        bool night = which == 0;
        var opt = new BattleOptions { AuraTier = night ? 5 : 3, Forms = new[] { "red_rose_bloom" }, Weather = night ? new Weather(23.5f) : Weather.Clear };
        battle = Battle.Empty(11, opt);
        SetWorld();
        var b = battle;
        var circle = b.AddSquad(0, Troops.Get("saints_militant"), 3, new Vector2(-8, -14), 3, Vector2.UnitY);
        var mine = b.AddSquad(0, Troops.Get("men_at_arms"), 20, new Vector2(4, -6), 10, Vector2.UnitY);
        var foes = b.AddSquad(1, Troops.Get("levies"), 30, new Vector2(0, 12), 10, -Vector2.UnitY);
        circle.Order = mine.Order = foes.Order = Order.Hold;
        b.Player.Pos = new Vector2(0, 0);
        b.Player.Facing = b.Player.Heading = MathHelper.PiOver2;
        b.Input = new PlayerInput { Aura = true, Dir = AttackDir.Right, Facing = MathHelper.PiOver2 };
        for (int i = 0; i < 30; i++) b.Update(1 / 60f);
        var by = circle.Units[0];
        if (night)
        {
            b.AddField(Abilities.Get("wildfire"), "burning", 0, by, new Vector2(-14, 14), 5, 60, 1, 10);
            AddScar(new Vector2(14, 6), 3, ScarKind.Glass); // an earlier smite
            AddScar(new Vector2(-4, 18), 2, ScarKind.Scorch);
        }
        for (int i = 0; i < 90; i++) { Effects(1 / 60f); SpectacleFrame(1 / 60f); }
        if (night) b.Flash("spell:attack", new Vector2(6, 13), 3, 0, "faith");
        else if (which == 2)
        {
            StageCast("tempest", circle, new Vector2(0, 14), 0.7f);
            StageCast("smite", circle, new Vector2(14, 8), 0.5f);
            for (int i = 0; i < 120; i++) { Effects(1 / 60f); SpectacleFrame(1 / 60f); } // the sky darkens, the pull builds
        }
        else
        {
            b.Flash("breakthrough", b.Player.Pos, Battle.BreakRally, 0);
            beats.Ask(BeatKind.Breakthrough, b.Player.Pos, realTime - 1f, 30); // held at its height for the shot
            breakAt = realTime - 0.3f;
        }
        for (int i = 0; i < 10; i++) { b.Update(1 / 60f); Effects(1 / 60f); SpectacleFrame(1 / 60f); }
        foreach (var u in b.Units) { u.ViewFacing = u.Facing; u.ViewHeading = u.Heading; }
        paused = hudHidden = true;
        SetCommand(true);
        blend = 1;
        camFocus = new Vector2(0, 5);
        camYaw = MathHelper.PiOver2 + 0.4f;
        camPitch = 0.42f;
        zoom = 34;
    }

    static readonly string[] WorkShots = { "smite-strike", "miracle", "tempest", "wildfire", "crows", "aura-ladder", "clash-beat", "black-flame" };

    /// The strong workings and the aura's moments, each caught a moment after it lands (docs/SPECTACLE.md S3-S5).
    void WorkScene(int which)
    {
        string shot = WorkShots[which];
        bool aura = shot is "aura-ladder" or "clash-beat" or "black-flame";
        var opt = new BattleOptions
        {
            AuraTier = aura ? (shot == "aura-ladder" ? 5 : 4) : 2, EnemyAura = shot == "clash-beat" ? 4 : 0,
            Forms = new[] { shot == "aura-ladder" ? "lily_ward" : "red_rose_bloom" },
            Weather = shot == "wildfire" ? new Weather(20.5f) : shot == "tempest" ? new Weather(15, 0.5f) : Weather.Clear,
        };
        battle = Battle.Empty(12 + which, opt);
        SetWorld();
        var b = battle;
        var circle = b.AddSquad(0, Troops.Get(shot is "miracle" or "smite-strike" ? "saints_militant" : "sky_callers"), 3, new Vector2(-10, -16), 3, Vector2.UnitY);
        var mine = b.AddSquad(0, Troops.Get("men_at_arms"), 20, new Vector2(4, -6), 10, Vector2.UnitY);
        var foes = b.AddSquad(1, Troops.Get("levies"), 30, new Vector2(0, 12), 10, -Vector2.UnitY);
        circle.Order = mine.Order = foes.Order = Order.Hold;
        foreach (var s in b.Squads) s.Will = 0; // no casts of their own in the shot
        b.Player.Pos = shot == "aura-ladder" ? new Vector2(-6, 2) : Vector2.Zero;
        b.Player.Facing = b.Player.Heading = MathHelper.PiOver2;
        b.Input = new PlayerInput { Aura = true, Dir = AttackDir.Right, Facing = MathHelper.PiOver2 };
        var by = circle.Units[0];
        var at = new Vector2(0, 13);
        float run = 0.3f;
        void Work(string id, string kind, float r)
        {
            b.Flash("spell:" + kind, at, r, 0, Abilities.Get(id).Tradition);
            b.Flash("work:" + id, at, r, 0, Abilities.Get(id).Tradition, by.Pos - at);
        }
        switch (shot)
        {
            case "smite-strike": Work("smite", "attack", 3); run = 0.1f; break;
            case "miracle": Work("miracle", "rally", 16); run = 1.4f; break;
            case "tempest": Work("tempest", "field", 14); b.AddField(Abilities.Get("tempest"), "storm", 0, by, at, 14, 30, 1, 0); run = 2.5f; break;
            case "wildfire": Work("wildfire", "field", 7); b.AddField(Abilities.Get("wildfire"), "burning", 0, by, at, 7, 30, 1, 10); run = 1.2f; break;
            case "crows": Work("crows", "debuff", 9); run = 3.2f; break;
            case "aura-ladder": run = 1.5f; break;
            case "clash-beat": b.StageClash(); beats.Ask(BeatKind.Clash, b.ClashAt, realTime - 1f, 30); run = 1f; break;
            case "black-flame": b.MadUntil = 30; run = 1f; break;
        }
        for (int i = 0; i < (int)(run * 60); i++)
        {
            if (shot == "aura-ladder") b.Player.Pos += new Vector2(0.06f, 0.03f); // walking, so he leaves his footprints
            b.Update(1 / 60f); Effects(1 / 60f); SpectacleFrame(1 / 60f);
        }
        foreach (var u in b.Units) { u.ViewFacing = u.Facing; u.ViewHeading = u.Heading; }
        paused = hudHidden = true;
        SetCommand(true);
        blend = 1;
        camFocus = shot is "tempest" or "crows" or "smite-strike" ? new Vector2(0, 9) : new Vector2(0, 4);
        camYaw = MathHelper.PiOver2 + 0.4f;
        camPitch = shot is "tempest" or "crows" or "smite-strike" or "miracle" ? 0.16f : 0.42f;
        zoom = shot is "tempest" or "crows" ? 40 : 30;
    }

    /// A cast frozen part-way through its gathering, for a shot (S1.7): `g` of its warning gone, so the frozen frame shows it.
    void StageCast(string id, Squad by, Vector2 at, float g)
    {
        var a = Abilities.Get(id);
        float warn = MathF.Max(0.5f, a.Warn);
        battle.Casts.Add(new Cast { A = a, By = by, At = at, Started = battle.Time - warn * g, FireAt = battle.Time + warn * (1 - g), Power = 1 });
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
