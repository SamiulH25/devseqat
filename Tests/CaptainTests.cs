using Microsoft.Xna.Framework;

namespace Ironfield;

/// The enemy sees and hunts the captain (docs/COMPANY.md C12-C13).
public static partial class SelfTest
{
    /// C12: a famed captain is picked over a nearer man of his; the cap on places round him holds; a man at blows keeps his
    /// foe; rabble give ground after he kills; a company that brings him down takes heart; and it all replays exactly.
    static void EnemySeesCaptain()
    {
        // Notice: the same foe, the same two targets; only the captain's fame changes.
        bool TurnsToCaptain(float renown)
        {
            var b = Battle.Empty(5, new BattleOptions { Renown = renown });
            b.Player.Pos = new Vector2(-4, 0);
            b.AddSquad(0, Troops.Get("levies"), 1, new Vector2(3.5f, 0), 1, -Vector2.UnitX);
            var foe = b.AddSquad(1, Troops.Get("levies"), 1, Vector2.Zero, 1, Vector2.UnitX);
            foe.Drill = 0.4f; // trained: no rush, no champion
            b.Update(1 / 60f);
            return foe.Units[0].OnCaptain;
        }
        Check(!TurnsToCaptain(0), "an unknown captain is just one more man: the foe takes the nearer");
        Check(TurnsToCaptain(500), "a famed captain is picked over a nearer man");

        // The cap: eight foes round a famed captain with men of his near; never more than MarkCap mark him.
        var c = Battle.Empty(6, new BattleOptions { Renown = 500, AuraTier = 3 });
        c.Player.Pos = Vector2.Zero;
        c.AddSquad(0, Troops.Get("levies"), 6, new Vector2(0, -5), 6, Vector2.UnitY);
        var ring = c.AddSquad(1, Troops.Get("levies"), 8, new Vector2(0, 3.5f), 8, -Vector2.UnitY);
        ring.Drill = 0.4f;
        int most = 0;
        for (int i = 0; i < 120; i++) { c.Update(1 / 60f); most = Math.Max(most, c.Units.Count(u => u.OnCaptain && u.Active)); }
        Check(most > 0, "some of them go for the famed captain");
        Check(most <= c.MarkCap, $"never more than {c.MarkCap} hold the captain at once (saw {most})");

        // A man at blows keeps his man, however famed the captain beside him.
        var k = Battle.Empty(7, new BattleOptions { Renown = 500 });
        k.Player.Pos = new Vector2(-1.6f, 0);
        k.AddSquad(0, Troops.Get("levies"), 1, new Vector2(1.0f, 0), 1, -Vector2.UnitX);
        var duel = k.AddSquad(1, Troops.Get("levies"), 1, Vector2.Zero, 1, Vector2.UnitX);
        duel.Drill = 0.4f;
        k.Update(1 / 60f);
        Check(!duel.Units[0].OnCaptain, "a man at blows with someone keeps his foe");

        // Rabble give ground after the captain kills one of theirs, and his kill shakes them more than a levy's would.
        float MoraleAfterKill(bool byCaptain, out Battle battle, out Squad mob)
        {
            var r = Battle.Empty(8);
            r.Player.Pos = Vector2.Zero;
            var friend = r.AddSquad(0, Troops.Get("levies"), 1, new Vector2(0, -3), 1, Vector2.UnitY);
            mob = r.AddSquad(1, Troops.Get("levies"), 4, new Vector2(0, 2.5f), 4, -Vector2.UnitY);
            mob.Drill = 0.1f;
            r.Kill(mob.Units[0], byCaptain ? r.Player : friend.Units[0]);
            battle = r;
            return mob.Units.Where(u => u.Active).Average(u => u.Morale);
        }
        float byLevy = MoraleAfterKill(false, out _, out _), byCaptain = MoraleAfterKill(true, out var rb0, out var mob0);
        Check(byCaptain < byLevy, $"the captain's kill shakes the company he cut into ({byCaptain:0.0} against {byLevy:0.0})");
        for (int i = 0; i < 6; i++) rb0.Update(1 / 60f);
        Check(mob0.Units.Where(u => u.Active).All(u => Vector2.Dot(u.Wish, u.Pos - rb0.Player.Pos) > 0), "rabble give ground when the captain cuts one of them down");

        // The company that brings him down takes heart.
        var h = Battle.Empty(9);
        var hunters = h.AddSquad(1, Troops.Get("levies"), 3, new Vector2(0, -78), 3, -Vector2.UnitY);
        h.Kill(h.Player, hunters.Units[0]);
        Check(hunters.Heartened, "a company that brings the captain down takes heart");

        // Replays stay exact with a famed captain in the thick of it.
        var rng = new Random(12);
        var rb = new Battle(33, Armies.Random(rng, 2, 0), Armies.Random(rng, 2, 1), new BattleOptions { Renown = 400, AuraTier = 2 });
        rb.StartRecording(33);
        for (int step = 0; rb.Winner < 0 && step < 60 * 120; step++)
        {
            rb.Input = new PlayerInput { Facing = MathHelper.PiOver2, Move = new Vector2(0, 1), Attack = step % 40 < 15, Dir = (AttackDir)(step / 60 % 4) };
            rb.Update(1 / 60f);
        }
        var rec = rb.FinishRecording()!;
        var played = Replay.Deserialize(rec.Serialize());
        Check(played.Matches(played.Play()), "a battle where the enemy marks the captain replays exactly");
    }

    /// C13: the general sends for a captain who rides far forward, once, and the battle replays; bows never mark you in a
    /// melee but do in the open; a grudge lord comes for you; the guard closes round its captain; your men cover you.
    static void EnemyHuntsCaptain()
    {
        // The order: ride out ahead of your line; the general sends for you once (the cooldown holds), and it replays.
        var b = new Battle(41, Armies.Standard(0), Armies.Standard(1), new BattleOptions { Renown = 300 });
        b.StartRecording(41);
        int most = 0;
        for (int step = 0; b.Winner < 0 && step < 60 * 55; step++)
        {
            b.Input = new PlayerInput { Facing = MathHelper.PiOver2, Throttle = step < 60 * 5 ? 1 : 0, Sprint = true }; // ride out ahead of the line, then sit
            b.Update(1 / 60f);
            most = Math.Max(most, b.HuntsSent);
        }
        Console.WriteLine($"hunt: {b.HuntsSent} sent, hunter {b.Hunter?.Name ?? "none"}, {b.Alerts.Count(a => a.Kind == "hunt")} alerts");
        Check(most == 1, $"the general sends for a captain far forward, once in the cooldown (sent {most})");
        Check(b.Alerts.Any(a => a.Kind == "hunt"), "an alert tells you they are coming");
        var rec = b.FinishRecording()!;
        var back = Replay.Deserialize(rec.Serialize());
        Check(back.Matches(back.Play()), "a battle where the general sends for you replays exactly");

        // Bows: elite bows mark a captain in the open, with a warning; never while their own men are on him.
        Squad Bows(Battle f)
        {
            var s = f.AddSquad(1, Troops.Get("longbowmen"), 8, new Vector2(0, 30), 4, -Vector2.UnitY);
            s.Drill = 0.95f;
            return s;
        }
        var open = Battle.Empty(42);
        open.Player.Pos = Vector2.Zero;
        var ob = Bows(open);
        for (int i = 0; i < 40; i++) open.Update(1 / 60f);
        open.HuntGeneral();
        Check(ob.VolleyAt > open.Time, "elite bows mark a captain in the open, with a warning first");
        var melee = Battle.Empty(42);
        melee.Player.Pos = Vector2.Zero;
        var mb = Bows(melee);
        melee.AddSquad(1, Troops.Get("levies"), 2, new Vector2(0, 2.5f), 2, -Vector2.UnitY);
        for (int i = 0; i < 40; i++) melee.Update(1 / 60f);
        melee.HuntGeneral();
        Check(mb.VolleyAt <= 0, "bows never mark the captain while their own men are on him");
        var easy = Battle.Empty(42, new BattleOptions { EnemyDamage = 0.8f });
        easy.Player.Pos = Vector2.Zero;
        var eb = Bows(easy);
        for (int i = 0; i < 40; i++) easy.Update(1 / 60f);
        easy.HuntGeneral();
        Check(eb.VolleyAt <= 0, "on Easy the bows do not mark you");

        // A grudge lord goes for you past a nearer man of yours.
        float MovedToward(string grudge)
        {
            var g = Battle.Empty(43, new BattleOptions { Grudge = grudge });
            g.Player.Pos = new Vector2(0, 50);
            g.AddSquad(0, Troops.Get("levies"), 1, g.EnemyCaptain.Pos + new Vector2(9, 0), 1, Vector2.UnitY);
            var start = Vector2.Distance(g.EnemyCaptain.Pos, g.Player.Pos);
            for (int i = 0; i < 180; i++) { if (i % 30 == 0) g.HuntGeneral(); g.Update(1 / 60f); }
            return start - Vector2.Distance(g.EnemyCaptain.Pos, g.Player.Pos);
        }
        float grudged = MovedToward("Lord Test has sworn to have your head!"), plain = MovedToward("");
        Check(grudged > 3 && grudged > plain + 2, $"a lord with a grudge comes for you himself ({grudged:0.0} m against {plain:0.0} m)");

        // The guard closes round the enemy captain when you come near.
        var gd = Battle.Empty(44);
        var guard = gd.AddSquad(1, Troops.Get("levies"), 6, gd.EnemyCaptain.Pos + new Vector2(7, 2), 3, -Vector2.UnitY);
        gd.Player.Pos = gd.EnemyCaptain.Pos + new Vector2(0, -14);
        float before = guard.Units.Average(u => Vector2.Distance(u.Pos, gd.EnemyCaptain.Pos));
        for (int i = 0; i < 240; i++) { if (i % 30 == 0) gd.HuntGeneral(); gd.Player.Pos = gd.EnemyCaptain.Pos + new Vector2(0, -14); gd.Update(1 / 60f); }
        float after = guard.Units.Where(u => u.GuardSlot >= 0).Select(u => Vector2.Distance(u.Pos, gd.EnemyCaptain.Pos)).DefaultIfEmpty(99).Average();
        Check(after < 3.5f && after < before, $"his nearest men close round their captain when you come near ({before:0.0} m to {after:0.0} m)");

        // Your men answer: two or more on you and your nearest idle company comes to cover you.
        var cv = Battle.Empty(45, new BattleOptions { Renown = 500 });
        cv.Player.Pos = Vector2.Zero;
        var mine = cv.AddSquad(0, Troops.Get("levies"), 8, new Vector2(0, -16), 4, Vector2.UnitY);
        var foes = cv.AddSquad(1, Troops.Get("levies"), 4, new Vector2(0, 3), 4, -Vector2.UnitY);
        foes.Drill = 0.4f;
        float covered = -1;
        for (int i = 0; i < 120 && covered < 0; i++) { cv.Update(1 / 60f); if (mine.CoverUntil > cv.Time) covered = cv.Time; }
        Check(covered >= 0, "your nearest idle company comes to cover you when two or more are on you");
        Check(cv.Alerts.Any(a => a.Kind == "cover"), "TO THE CAPTAIN! is raised");
    }
}
