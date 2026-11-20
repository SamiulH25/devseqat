using Microsoft.Xna.Framework;

namespace Ironfield;

/// Officers at posts (docs/archive/OFFICERS.md).
public static partial class SelfTest
{
    /// A line of `n` companies under one officer of this skill, 6 m apart, facing an enemy company far off.
    static (Battle B, Division D, List<Squad> Line) OfficerLine(int seed, int skill, Goal goal, int n = 4, string[]? traits = null)
    {
        var b = Battle.Empty(seed);
        var line = new List<Squad>();
        for (int i = 0; i < n; i++) line.Add(b.AddSquad(0, Troops.Get("shieldmen"), 16, new((i - (n - 1) / 2f) * 22, 0), 8, Vector2.UnitY));
        var far = b.AddSquad(1, Troops.Get("shieldmen"), 20, new(0, 110), 10, -Vector2.UnitY);
        far.Order = Order.Hold; far.Anchor = new(0, 110);
        var d = b.AddDivision(0, "Sgt", line, Temper.Steady, skill, goal);
        if (traits != null) d.Traits = traits;
        return (b, d, line);
    }

    /// OF2: skill decides how well, never what. A sharp officer looks again sooner and sees threats further out; a good one
    /// sends his companies in together, keeps his line still and closes a gap; a poor one sends them in one by one and
    /// lets them wander; a good one rests a spent company; natures change how well (Hothead, Drillmaster).
    static void OfficerSkill()
    {
        var (b0, poor, _) = OfficerLine(31, 1, new Goal(Post.Front, Stance.Hold));
        var (_, good, _) = OfficerLine(31, 9, new Goal(Post.Front, Stance.Hold));
        Check(MathF.Abs(Battle.Reaction(poor) - 1.84f) < 0.01f && MathF.Abs(Battle.Reaction(good) - 0.56f) < 0.01f, "a sharp officer looks again sooner");
        Check(Battle.Notice(good) > Battle.Notice(poor) + 25, "and sees threats further out");

        // Together or piecemeal on Charge.
        float Spread(int skill, string[]? traits = null)
        {
            var (b, d, line) = OfficerLine(32, skill, new Goal(Post.Front, Stance.Hold), traits: traits);
            Run(b, 1);
            b.IssueGoal(d, new Goal(Post.Front, Stance.Charge));
            var went = new Dictionary<Squad, float>();
            for (int i = 0; i < 60 * 30 && went.Count < line.Count; i++)
            {
                b.Update(1 / 60f);
                foreach (var s in line) if (s.Order == Order.Charge && !went.ContainsKey(s)) went[s] = b.Time;
            }
            return went.Count < line.Count ? 99 : went.Values.Max() - went.Values.Min();
        }
        float poorSpread = Spread(1), goodSpread = Spread(9), hotSpread = Spread(1, new[] { "Hothead" });
        Console.WriteLine($"officers: charge goes in over {poorSpread:0.0} s (skill 1), {hotSpread:0.0} s (hothead), {goodSpread:0.0} s (skill 9)");
        Check(goodSpread < 2 && poorSpread > 10, "a good officer sends his companies in together, a poor one one after another");
        Check(hotSpread < poorSpread * 0.7f, "a Hothead sends them in quicker");

        // The line: a poor officer's companies wander from their places; a good one's stay put and close a gap.
        float Wander(int skill, string[]? traits = null)
        {
            var (b, d, line) = OfficerLine(33, skill, new Goal(Post.Front, Stance.Hold), traits: traits);
            Run(b, 11); // a good officer has closed his line up by now
            var at = line.ToDictionary(s => s, s => s.Anchor);
            Run(b, 30);
            return line.Max(s => Vector2.Distance(s.Anchor, at[s]));
        }
        float poorWander = Wander(1), goodWander = Wander(9), drillWander = Wander(1, new[] { "Drillmaster" });
        Console.WriteLine($"officers: line wanders {poorWander:0.0} m (skill 1), {drillWander:0.0} m (drillmaster), {goodWander:0.0} m (skill 9)");
        Check(poorWander > 2 && goodWander < 0.5f, "a poor officer's line wanders; a good one's stays put");
        Check(drillWander < poorWander, "a Drillmaster keeps it tighter");

        float GapAfterLoss(int skill)
        {
            var (b, d, line) = OfficerLine(34, skill, new Goal(Post.Front, Stance.Hold), n: 3);
            Run(b, 1);
            foreach (var u in line[1].Units) u.Dead = true; // the middle company is gone
            Run(b, 20);
            return Vector2.Distance(line[0].Anchor, line[2].Anchor);
        }
        float poorGap = GapAfterLoss(1), goodGap = GapAfterLoss(9);
        Console.WriteLine($"officers: after the middle company falls the ends stand {poorGap:0} m apart (skill 1), {goodGap:0} m (skill 9)");
        Check(goodGap < poorGap - 10, "a good officer closes the gap a fallen company leaves");

        // Notice: a wing on Guard meets a foe rounding the line's end further out under a sharp officer.
        bool Meets(int skill)
        {
            var b = Battle.Empty(35);
            b.AddSquad(0, Troops.Get("shieldmen"), 30, new(0, 0), 15, Vector2.UnitY);
            var wing = b.AddSquad(0, Troops.Get("men_at_arms"), 12, new(45, 0), 6, Vector2.UnitY);
            var d = b.AddDivision(0, "Sgt", new[] { wing }, Temper.Steady, skill, new Goal(Post.RightWing, Stance.Guard));
            Run(b, 15);
            var foe = b.AddSquad(1, Troops.Get("levies"), 10, b.SpotOf(d, d.Goal)!.Value + new Vector2(18, 16), 5, -Vector2.UnitY);
            foe.Order = Order.Hold; foe.Anchor = foe.Center;
            Run(b, 3);
            Console.WriteLine($"officers: guard skill {skill}: foe gap {Vector2.Distance(wing.Center, foe.Center) - foe.Radius:0} m, notice {Battle.Notice(d) * 0.7f:0} m, order {wing.Order}");
            return wing.Order == Order.Charge;
        }
        Check(Meets(9) && !Meets(1), "a sharp officer on Guard sees a foe coming round the end further out");

        // Rest: a good officer pulls a spent company back and puts the fresh one behind it into its place.
        var (br, dr, lr) = OfficerLine(36, 8, new Goal(Post.Front, Stance.Hold), n: 1);
        var second = br.AddSquad(0, Troops.Get("shieldmen"), 16, new(0, -14), 8, Vector2.UnitY);
        dr.Squads.Add(second); second.Division = dr;
        var press = br.AddSquad(1, Troops.Get("men_at_arms"), 16, new(0, 8), 8, -Vector2.UnitY);
        press.Order = Order.Charge; press.Target = lr[0];
        bool rested = false;
        for (int i = 0; i < 60 * 20 && !rested; i++)
        {
            br.Update(1 / 60f);
            if (lr[0].InContact) foreach (var u in lr[0].Units) if (u.Active) u.Morale = MathF.Min(u.Morale, 30);
            rested = dr.Resting.Contains(lr[0]);
        }
        Run(br, 1);
        Check(rested && second.Anchor.Y > lr[0].Anchor.Y, "a good officer pulls a spent company back and sends the fresh one into its place");
    }

    /// OF3: Divide lays a big army out on posts (horse on the wings, foot on the front line, bows skirmishing, spare foot in
    /// reserve) under officers whose skill follows the army's quality; the general picks stances by post and the state of
    /// the fight, only ever stances of the division's own post.
    static void AiOnPosts()
    {
        var levies = Armies.Random(new Random(40), 1, 0);
        var elite = Armies.Random(new Random(40), 7, 0);
        Check(Armies.OfficerSkill(levies) <= 2 && Armies.OfficerSkill(elite) >= 8, $"officers' skill follows the army's quality ({Armies.OfficerSkill(levies)}, {Armies.OfficerSkill(elite)})");

        var army = Armies.Divide(Armies.Large(new Random(41), 3, 1, 2400));
        var posts = army.Divisions!.Select(x => Goal.FromName(x.Goal)).ToList();
        Console.WriteLine($"officers: a big army divides into {string.Join(", ", army.Divisions!.Select(x => $"{x.Name} {x.Goal} skill {x.Skill}"))}");
        Check(posts.Any(g => g.Post == Post.Front) && posts.Any(g => g.Post is Post.LeftWing or Post.RightWing), "a big army stands on the front line and the wings");
        Check(army.Divisions!.All(x => x.Skill == Armies.OfficerSkill(army)), "its officers have the skill its quality gives");

        var b = new Battle(41, Armies.Divide(Armies.Large(new Random(42), 3, 0, 1500)), Armies.Divide(Armies.Large(new Random(43), 3, 1, 1500)),
            new BattleOptions { Cap = 4000, AutoPlayer = true, PlayerWounded = true });
        var seen = new HashSet<(Post, Stance)>();
        bool fits = true;
        for (int i = 0; i < 60 * 120 && b.Winner < 0; i++)
        {
            b.Update(1 / 60f);
            if (i % 30 != 0) continue;
            foreach (var d in b.Divisions) { seen.Add((d.Post, d.Stance)); fits &= Goal.Fits(d.Post, d.Stance); }
        }
        Console.WriteLine($"officers: stances seen {string.Join(", ", seen.Select(x => $"{x.Item1}:{x.Item2}"))}");
        Check(fits, "the general only ever gives a division a stance of its own post");
        Check(seen.Contains((Post.Front, Stance.Charge)), "front lines go in once the fight is on");
        Check(seen.Any(x => x.Item1 is Post.LeftWing or Post.RightWing && x.Item2 == Stance.Swing), "wings swing round once the line is at blows");
        Check(!b.Divisions.Any(d => d.Stance == Stance.Wait), "a reserve the general leads waits to plug the line");
    }

    /// OF4: AUTO-FILL sends the companies you lead to posts that suit them (horse to the wings, bows to the skirmishers,
    /// foot to the front) and leaves you at least three; an officer's card line names the nearest enemy and how far.
    static void PostsInDeployment()
    {
        var b = Battle.Empty(51);
        Squad Co(string troop, float x, int id) { var s = b.AddSquad(0, Troops.Get(troop), 12, new(x, -20), 6, Vector2.UnitY); s.Company = new Company(troop, 12, "", id); return s; }
        var mine = new[] { Co("lancers", -40, 1), Co("lancers", -30, 2), Co("bowmen", -20, 3), Co("shieldmen", -10, 4), Co("shieldmen", 0, 5), Co("shieldmen", 10, 6), Co("shieldmen", 20, 7) };
        var wing = b.AddDivision(0, "Wing", Array.Empty<Squad>(), Temper.Steady, 5, new Goal(Post.RightWing, Stance.Guard));
        var skirm = b.AddDivision(0, "Bows", Array.Empty<Squad>(), Temper.Steady, 5, new Goal(Post.Skirmish, Stance.Loose));
        var front = b.AddDivision(0, "Front", Array.Empty<Squad>(), Temper.Steady, 5, new Goal(Post.Front, Stance.Hold));
        int moved = b.AutoFill(0);
        Console.WriteLine($"officers: auto-fill moved {moved}: wing {wing.Squads.Count}, bows {skirm.Squads.Count}, front {front.Squads.Count}");
        Check(wing.Squads.Count == 2 && wing.Squads.All(s => s.Cavalry) && skirm.Squads.Count == 1, "AUTO-FILL sends horse to the wing and bows to the skirmishers");
        Check(front.Squads.All(s => !s.Cavalry) && mine.Count(s => s.Division == null) >= 3, "and foot to the front, leaving you at least three");
        var foe = b.AddSquad(1, Troops.Get("lancers"), 10, new(60, 40), 5, -Vector2.UnitY);
        Check(b.Situation(wing).StartsWith("HORSE ") && b.Situation(wing).EndsWith(" M"), $"the card names the nearest enemy and how far ({b.Situation(wing)})");
    }

    /// OF5: every company has a sergeant who keeps its order. He rallies wavering men near him; when he falls the company
    /// drops about a grade until another man steps up, and then has it back.
    static void CompanySergeants()
    {
        var b = Battle.Empty(61);
        var s = b.AddSquad(0, Troops.Get("men_at_arms"), 20, new(0, 0), 10, Vector2.UnitY);
        Run(b, 1.1f);
        Check(s.Sgt is { Active: true } && s.SgtDoing != SgtTask.Down, "a company has a sergeant from the start");
        var grade = s.Grade;
        float drill = s.Drill;

        // Rallying: shaken men near him take heart; with no sergeant they do not.
        float Rally(bool withSgt)
        {
            var bb = Battle.Empty(62);
            var q = bb.AddSquad(0, Troops.Get("men_at_arms"), 20, new(0, 0), 10, Vector2.UnitY);
            Run(bb, 1.1f);
            if (!withSgt) { q.Sgt = null; q.SgtDownAt = float.MaxValue; } // no sergeant, and no drill lost (a fair comparison)
            foreach (var u in q.Units) u.Morale = 30;
            Run(bb, 2);
            return q.Units.Where(u => u.Active).Average(u => u.Morale);
        }
        float with = Rally(true), without = Rally(false);
        Console.WriteLine($"officers: shaken company's morale after 2 s: {with:0} with its sergeant, {without:0} without");
        Check(with > without + 2, "a sergeant rallies wavering men");

        // He falls: a grade worse, then another man steps up and it is as before.
        s.Sgt!.Dead = true;
        Run(b, 1.1f);
        Console.WriteLine($"officers: sergeant down: drill {drill:0.00} -> {s.Drill:0.00} ({grade} -> {s.Grade}), step up in {Battle.StepUpTime(s):0} s");
        Check(s.SgtDoing == SgtTask.Down && s.Grade < grade, "with its sergeant down the company fights a grade worse");
        Run(b, Battle.StepUpTime(s) + 1.5f);
        Check(s.Sgt is { Active: true } && MathF.Abs(s.Drill - drill) < 0.001f, "another man steps up and the company is itself again");
    }
}
