using Microsoft.Xna.Framework;

namespace Ironfield;

/// A named man's words in a battle (docs/archive/COMPANY.md C6): who said it, the line, when, his company.
public sealed record Said(int Soldier, string Name, string Text, float Time, Squad? Squad, bool Key = false, int Team = 0, VoiceMood Mood = VoiceMood.Even, string Culture = "", string Nature = "", bool Sergeant = false);

/// Named men in the fight (C6): an alert when one of yours is hard pressed near you, and a line from him (by his nature, from
/// Data/talk.json) when he fells a captain or a horseman, when his brother falls, and when you pull him out. Read only: no dice,
/// nothing in the fight changes, so replays are untouched.
public sealed partial class Battle
{
    /// How often a hard-pressed alert may come, and how near you he must be.
    public const float PressedGap = 20, PressedReach = 60;
    /// What the named men have said, oldest first.
    public readonly List<Said> Spoken = new();
    float pressedAt = -PressedGap;

    /// The campaign's word on a named man in this battle, if he is one.
    public static NamedSpec? NamedOf(Unit u) => u.SoldierId == 0 ? null : u.Squad?.Company?.Named?.FirstOrDefault(m => m.Id == u.SoldierId);

    /// One of yours under half his strength with a foe on him, within reach of you: once every `PressedGap` seconds at most.
    void PressedAlerts()
    {
        CheckBattleBarks();
        if (!anyNamed || Time - pressedAt < PressedGap || !Player.Active) return;
        foreach (var u in Units)
        {
            if (u.SoldierId == 0 || u.Team != 0 || !u.Active || u.IsPlayer || u.Hp >= u.Type.MaxHp * 0.5f) continue;
            if (u.Target is not { Active: true } foe || foe.Team == u.Team || Vector2.Distance(foe.Pos, u.Pos) > 3) continue;
            if (Vector2.Distance(u.Pos, Player.Pos) > PressedReach) continue;
            pressedAt = Time;
            Alerts.Add(new Alert("pressed", $"{NameOf(u)} is hard pressed", u.Squad, u.Pos, Time));
            return;
        }
    }

    /// He says his line on `topic` (`man_felled_captain`, `man_felled_horse`, `man_brother_down`, `man_saved`): one by his nature
    /// if there is one, else a plain one; `other` fills {other}.
    internal void Speak(Unit u, string topic, string other = "")
    {
        if (u.Team != 0 || NamedOf(u) is not NamedSpec m) return;
        var fits = TalkLines.All.Where(l => l.Topic == topic && (l.Nature == "" || l.Nature == m.Nature)).ToList();
        if (fits.Any(l => l.Nature != "")) fits = fits.Where(l => l.Nature != "").ToList();
        if (fits.Count == 0) return;
        string text = fits[(m.Id * 31 + Spoken.Count * 7) % fits.Count].Text.Replace("{man}", m.Name).Replace("{other}", other == "" ? "him" : other);
        bool key = topic is "man_felled_captain" or "man_brother_down" or "man_saved";
        var mood = topic is "man_brother_down" ? VoiceMood.Grudge : topic is "man_saved" ? VoiceMood.Warm : VoiceMood.Command;
        Spoken.Add(new Said(m.Id, m.Name, text, Time, u.Squad, Key: key, Team: 0, Mood: mood, Culture: u.Type.Culture, Nature: m.Nature));
        if (topic is "man_felled_horse" or "man_felled_captain") QueueBrotherReply(u, m);
        Log($"{m.Name}: \"{text}\"");
    }

    /// From `Kill`: what the named men say when one of them fells a captain or a horseman, and when one of them falls.
    void NamedKill(Unit t, Unit killer, bool wasMounted)
    {
        if (!anyNamed) return;
        if (killer.SoldierId != 0 && killer.Team == 0 && t.Team != 0 && !killer.IsPlayer)
        {
            if (t.IsLeader || t.Hero) Speak(killer, "man_felled_captain");
            else if (wasMounted) Speak(killer, "man_felled_horse");
        }
        if (t.SoldierId == 0 || t.Team != 0) return;
        foreach (var u in Units)
            if (u != t && u.Active && u.SoldierId != 0 && u.Team == 0 && NamedOf(u)?.Brothers?.Contains(t.SoldierId) == true)
                Speak(u, "man_brother_down", NameOf(t));
    }
}
