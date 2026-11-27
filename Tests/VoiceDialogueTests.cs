namespace Ironfield;

/// Self-tests for the Acoustic Instrument Speech engine, new instruments.bank voices,
/// battle bark loading, and multi-turn diplomacy/parley scenes.
public static partial class SelfTest
{
    static void VoiceBankAndSpeech()
    {
        var bank = AudioBank.Shared;
        string[] expected = { "bassoon", "english_horn", "cello_solo", "trombone_muted", "clarinet", "voice_ooh", "steppe_reed" };
        foreach (var id in expected)
            Check(bank.HasVoice(id), $"instruments.bank has new voice '{id}'");

        string[] cultures = { "northern", "azure", "crimson", "southern", "steppe", "league", "order" };
        foreach (var c in cultures)
        {
            var prof = VoiceProfile.ForRegion(c);
            var pcm1 = VoiceSpeech.Render(prof, "Shields tight! Hold the ridge, captain?", VoiceMood.Command, 10);
            var pcm2 = VoiceSpeech.Render(prof, "Shields tight! Hold the ridge, captain?", VoiceMood.Command, 10);
            var (peak, rms, finite) = Level(pcm1);
            Check(finite && pcm1.Length > 2000 && peak > 0.05f && peak <= 1.0f && rms > 0.01f, $"speech renders cleanly for {c}");
            Check(pcm1.Length == pcm2.Length && pcm1[500] == pcm2[500], $"speech render is deterministic for {c}");
        }
    }

    static void BattleBarksAndParley()
    {
        Check(BattleBarks.All.Count >= 70, "barks.json loaded expanded battle vocal stims");
        string[] topics =
        {
            "order_hold", "order_charge", "order_reckless", "order_follow", "order_advance", "order_fallback",
            "order_move", "order_form_here", "order_fire", "order_hold_fire", "order_cast_hold", "order_cast_free",
            "order_relieve", "order_merge", "order_split", "order_rebound_echo",
            "form_wall", "form_square", "form_wedge", "form_loose", "form_line",
            "brace_call", "brace_hit", "rally_answer", "flank_warn", "waver_hold",
            "arrows_out", "volley_call", "clash_cry", "victory_cheer", "foe_brace", "foe_charge",
            "foe_grudge_taunt",
        };
        foreach (var t in topics)
            Check(BattleBarks.Pick(t, "Steady", 7).Length > 0, $"barks.json topic '{t}' resolves");

        // Origin-styled vocal stims: each people speaks in its own voice, and subcultures map cleanly
        string[] origins = { "azure", "crimson", "northern", "southern", "steppe", "league", "order", "bandit" };
        var braceByOrigin = origins.Select(c => BattleBarks.Pick("brace_call", "", 3, c)).ToList();
        Check(braceByOrigin.Distinct().Count() == origins.Length, "every troop origin has its own distinct brace call");
        var holdByOrigin = origins.Select(c => BattleBarks.Pick("order_hold", "", 3, c)).ToList();
        Check(holdByOrigin.Distinct().Count() == origins.Length, "every troop origin has its own distinct hold rebound");
        Check(BattleBarks.Pick("brace_call", "", 3, "northern_fjord") == BattleBarks.Pick("brace_call", "", 3, "northern"), "subculture maps to parent origin voice");
        Check(BattleBarks.Pick("order_hold", "", 3, "common") == BattleBarks.Pick("order_hold", "", 3, ""), "common falls back to generic lines");

        // Rebound commands (primary + second-company relay echo) and brace calls are read-only and replay-safe
        var b = new Battle(11,
            new ArmySpec("mine", 0, new List<Company> { new("manor_foot", 16, "infantry", 1), new("brevon_fyrd", 16, "infantry", 2) }),
            new ArmySpec("theirs", 0, new List<Company> { new("march_scouts", 10, "cavalry", 3) }),
            new BattleOptions());
        for (int i = 0; i < 400; i++) b.Update(1 / 60f); // advance past t=6s
        int before = b.Spoken.Count;
        var mine = b.SquadsOf(0).ToList();
        b.SpeakSquadOrder(mine, "form_wall");
        Check(b.Spoken.Count == before, "rebound command waits briefly after the horn");
        for (int i = 0; i < 20; i++) b.Update(1 / 60f); // +0.33s: primary rebound fires
        Check(b.Spoken.Count == before + 1 && b.Spoken[^1].Key, "primary company rebounds the command");
        for (int i = 0; i < 30; i++) b.Update(1 / 60f); // +0.50s: second-company relay echo fires
        Check(b.Spoken.Count == before + 2 && !b.Spoken[^1].Key, "second company echoes the command down the line");
        int beforeBrace = b.Spoken.Count;
        b.SpeakBrace(mine[0]);
        Check(b.Spoken.Count == beforeBrace + 1 && b.Spoken[^1].Key, "call to brace fires cleanly");
        b.SpeakBrace(mine[0]);
        Check(b.Spoken.Count == beforeBrace + 1, "call to brace respects cooldown");

        Check(DialogueLibrary.All.Count >= 3, "parley.json loaded multi-turn scenes");
        var camp = Campaign.New(seed: 19, layout: 5);
        var lord = camp.S.Characters.First(c => c.Kind == "lord" && !c.Dead);
        var run = camp.StartDialogue(lord, "lord");
        Check(run != null && run.Current != null && run.Current.Choices.Count >= 2, "lord multi-turn dialogue starts cleanly");
        float beforeOp = camp.OpinionOfYou(lord);
        string act = camp.ChooseDialogue(run!, run!.Current!.Choices[0]);
        Check(act == "" && run.NodeId != run.Scene.Entry, "choosing a stance advances to the next node");
        Check(camp.OpinionOfYou(lord) >= beforeOp, "honourable stance improves or holds lord opinion");
    }
}
