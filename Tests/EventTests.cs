namespace Ironfield;

/// The events (docs/pre-demo/EVENTS.md): the data, the director's pacing, checks, chains, needs and costs.
public static partial class SelfTest
{
    /// EV0: the storylets load and are valid, every verb is used, every moment has events, conditions parse.
    static void EventsValidate()
    {
        var problems = Campaign.ValidateEvents().ToList();
        Check(problems.Count == 0, "the events are valid: " + string.Join("; ", problems.Take(5)));
        Check(Campaign.Events.Count >= 80, $"about eighty events at the start ({Campaign.Events.Count})");
        foreach (var m in Campaign.Moments)
            Check(Campaign.Events.Count(e => e.Moments.Contains(m)) >= (m == "site" ? 9 : 3), $"the {m} moment has events");
        Check(Campaign.Events.Count(e => e.Choices.Any(c => c.Check != null)) >= 25, "many events ask a check");
        Check(Campaign.Events.Select(e => e.Theme).Distinct().Count() >= 10, "the events come in themed files");
        var c = Campaign.New(5);
        Check(c.Holds("day<3") && !c.Holds("day>=3") && c.Holds("gold>0") && c.Holds("!pact"), "conditions read the campaign");
    }

    /// EV1: over 200 days the director keeps its quiet and its weekly cap, drains its queue, and draws a varied spread.
    static void EventPacing()
    {
        var c = Campaign.New(919);
        new Pilot(c).Run(200 * 24);
        var drawn = c.S.EventHistory.Where(h => Campaign.Drawn(h.Id)).ToList();
        Console.WriteLine($"events in 200 days: {c.S.EventHistory.Count} answered, {drawn.Count} drawn, {c.S.EventQueue.Count} queued at the end");
        Console.WriteLine("  raised by the world: " + string.Join(", ", c.S.EventHistory.Except(drawn).GroupBy(h => h.Id).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}")));
        Check(drawn.Count >= 200 / 7 && drawn.Count <= 200 / 7 * 4, $"about two drawn events a week ({drawn.Count} in 200 days)");
        for (int i = 0; i < drawn.Count; i++)
        {
            float at = drawn[i].At;
            int week = drawn.Count(h => h.At > at - 7 * 24 && h.At <= at);
            Check(week <= Balance.Get().EventWeekCap, $"the weekly cap holds (at hour {at:0}: {week})");
        }
        for (int i = 1; i < drawn.Count; i++)
            Check(drawn[i].At - drawn[i - 1].At >= Balance.Get().EventQuietMin * 24 - 1, $"drawn events keep their quiet ({drawn[i - 1].Id} then {drawn[i].Id})");
        Check(drawn.Select(h => h.Id).Distinct().Count() >= 15, "a long run draws many kinds of event");
        Check(c.S.EventQueue.All(q => q.Until <= 0 || q.Until >= c.S.Hours), "nothing lapsed stays in the queue");
    }

    /// EV2: the odds shown are the odds rolled.
    static void EventOddsMatch()
    {
        var c = Campaign.New(31);
        var e = Campaign.EventOf("kob_tracks")!;
        float odds = c.Odds(e.Choices[0].Check!, new PendingEvent { Id = e.Id });
        int pass = 0, n = 4000;
        for (int i = 0; i < n; i++)
        {
            c.S.Event = new PendingEvent { Id = e.Id };
            Check(c.Choose(0) == null, "the check can be taken");
            if (c.EventOutcome!.Result is "pass" or "great") pass++;
            Check(c.EventOutcome.Odds == odds, "the page carries the odds it rolled");
        }
        float rate = pass / (float)n;
        Console.WriteLine($"kob_tracks: odds {odds:0.00}, passed {rate:0.00} of {n}");
        Check(MathF.Abs(rate - odds) < 0.03f, $"the shown odds match the rolled rate ({odds:0.00} vs {rate:0.00})");
        Check(Campaign.OddsWord(0.9f) == "NEAR SURE" && Campaign.OddsWord(0.5f) == "EVEN" && Campaign.OddsWord(0.1f) == "DESPERATE", "the odds have their words");
        // Skill moves the odds; a floor keeps the worst from being hopeless.
        var k = new CheckDef { Skill = "Tactics", Against = 500 };
        Check(c.Odds(k, new PendingEvent()) >= Balance.Get().EventCheckFloor - 1e-4f, "even a desperate check has a chance");
    }

    /// EV2: a chain: one answer queues the next step, which comes days later about the same cast; a lapsed one is dropped.
    static void EventChains()
    {
        var c = Campaign.New(77);
        c.S.Event = new PendingEvent { Id = "dying_stranger" };
        c.Choose(0);
        Check(c.EventOutcome!.More && c.S.EventQueue.Any(q => q.Event.Id == "map_found"), "keeping the map queues what follows");
        var q = c.S.EventQueue.First(q => q.Event.Id == "map_found");
        Check(q.At >= c.S.Hours + 7 * 24 - 1 && q.At <= c.S.Hours + 10 * 24 + 1, "it comes in its day range");
        // The rival's chain carries the same cast through three steps.
        c.S.Event = new PendingEvent { Id = "rival_lists", Lord = 5 };
        c.Choose(1);
        Check(c.S.EventQueue.Any(x => x.Event.Id == "rival_end" && x.Event.Lord == 5), "a follow-up keeps its cast");
        // A follow-up whose time ran out is dropped, not raised.
        c.S.EventQueue.Clear();
        c.S.Hours += 72;
        c.S.EventQueue.Add(new QueuedEvent { Event = new PendingEvent { Id = "map_found" }, Priority = 1, At = (float)c.S.Hours - 48, Until = (float)c.S.Hours - 24 });
        c.S.Event = null;
        Days(c, 1);
        Check(!c.S.EventQueue.Any(x => x.Event.Id == "map_found") && c.S.Event is not { Id: "map_found" }, "a lapsed follow-up is dropped");
    }

    /// EV2: costs are paid and bar a choice you cannot afford; needs bar with their reason; the outcome page lists what changed.
    static void EventNeedsAndCosts()
    {
        var c = Campaign.New(12);
        var town = c.S.Settlements.First();
        c.S.Gold = 50;
        c.S.Event = new PendingEvent { Id = "famine", Settlement = town.Id };
        var e = Campaign.EventOf("famine")!;
        Check(c.ChoiceBlocked(e.Choices[0]) != "" && c.Choose(0) != null && c.S.Event != null, "a cost you cannot pay bars the choice");
        c.S.Gold = 500;
        Check(c.Choose(0) == null && c.S.Gold == 300, "a cost is paid");
        Check(c.EventOutcome!.Lines.Any(l => l.Icon == "coin" && l.Sign < 0), "the page shows what it cost");
        Check(c.S.EventHistory[^1].Id == "famine" && c.S.EventHistory[^1].Choice == 0, "the answer is remembered");
        Check(c.Holds("chose:famine.1") && !c.Holds("chose:famine.2"), "later events can ask what you chose");
        // A need: dice only with crowns in the purse.
        c.S.Gold = 0;
        var dice = Campaign.EventOf("men_dice")!;
        Check(c.ChoiceBlocked(dice.Choices[0], new PendingEvent { Id = "men_dice" }) != "", "a need you do not meet bars the choice");
    }
}
