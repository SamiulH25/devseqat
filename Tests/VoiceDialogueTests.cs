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
        Check(BattleBarks.All.Count >= 20, "barks.json loaded battle barks");
        Check(BattleBarks.Pick("order_hold", "Steady", 7).Length > 0, "order_hold picks a line");
        Check(BattleBarks.Pick("foe_grudge_taunt", "", 3).Length > 0, "foe_grudge_taunt picks a line");

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
