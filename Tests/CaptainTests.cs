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
}
