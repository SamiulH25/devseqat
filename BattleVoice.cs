using System.Text.Json;
using Microsoft.Xna.Framework;

namespace Ironfield;

/// Battle bark lines loaded from `Data/dialogue/barks.json` (docs/VOICE_DIALOGUE.md).
public sealed class BattleBarkLine
{
    public string Topic = "", Nature = "", Text = "";
}

public static class BattleBarks
{
    sealed class FileDto { public List<BattleBarkLine> Barks = new(); }
    static readonly JsonSerializerOptions Json = new() { IncludeFields = true, PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
    static List<BattleBarkLine>? all;

    public static IReadOnlyList<BattleBarkLine> All => all ??= Load();

    static List<BattleBarkLine> Load()
    {
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Data", "dialogue", "barks.json");
            if (!File.Exists(path)) path = Path.Combine("Data", "dialogue", "barks.json");
            if (File.Exists(path))
                return JsonSerializer.Deserialize<FileDto>(File.ReadAllText(path), Json)?.Barks ?? new();
        }
        catch { }
        return new();
    }

    public static string Pick(string topic, string nature, int seed)
    {
        var fits = All.Where(l => l.Topic == topic && (l.Nature == "" || l.Nature == nature)).ToList();
        if (fits.Any(l => l.Nature != "")) fits = fits.Where(l => l.Nature != "").ToList();
        if (fits.Count == 0) return "";
        int idx = Math.Abs(seed) % fits.Count;
        return fits[idx].Text;
    }
}

/// Read-only battle bark director: sergeant order echoes, enemy champion/grudge taunts,
/// and 2-line brother call-and-response. Never touches sim RNG or combat state (replays stay exact).
public sealed partial class Battle
{
    float lastOrderBarkAt = -10, lastFlavorBarkAt = -10;
    bool foeTauntSpoken;
    (int Soldier, string Name, string Nature, Squad? Squad, string Other, float DueAt)? pendingReply;

    /// Sergeant echoes a division goal you just issued (Key callout, priority acoustic voice).
    internal void SpeakDivisionOrder(Division d, Goal g)
    {
        if (d.Team != 0 || d.Leader is not { Active: true } sgt || Time - lastOrderBarkAt < 2.8f) return;
        string topic = g.Stance switch
        {
            Stance.Charge or Stance.Advance => "order_charge",
            Stance.Harass or Stance.Loose or Stance.Screen => "order_skirmish",
            Stance.Swing => "order_flank",
            Stance.Wait or Stance.Plug or Stance.Follow => "order_reserve",
            _ => "order_hold",
        };
        string nature = NamedOf(sgt)?.Nature ?? "";
        string text = BattleBarks.Pick(topic, nature, Rng.Mix(d.Id, (int)(Time * 10), Spoken.Count));
        if (text == "") return;
        lastOrderBarkAt = Time;
        string name = d.Name != "" ? d.Name : "Sergeant";
        Spoken.Add(new Said(sgt.SoldierId, name, text, Time, sgt.Squad, Key: true, Team: 0, Mood: VoiceMood.Command, Culture: sgt.Type.Culture, Nature: nature, Sergeant: true));
    }

    /// Periodic read-only check for enemy champion/grudge taunts, player-nearby rallies, and brother replies.
    void CheckBattleBarks()
    {
        // 1. Delayed 2-line brother reply
        if (pendingReply is { } pr && Time >= pr.DueAt)
        {
            pendingReply = null;
            var u = pr.Squad?.Units.FirstOrDefault(x => x.Active && x.SoldierId == pr.Soldier);
            if (u != null)
            {
                string text = BattleBarks.Pick("man_brother_reply", pr.Nature, Rng.Mix(pr.Soldier, Spoken.Count, 19))
                    .Replace("{other}", pr.Other);
                if (text != "")
                    Spoken.Add(new Said(pr.Soldier, pr.Name, text, Time, pr.Squad, Key: false, Team: 0, Mood: VoiceMood.Warm, Culture: u.Type.Culture, Nature: pr.Nature));
            }
        }

        // 2. Enemy champion / grudge rival taunt once when closing on the captain
        if (!foeTauntSpoken && Player.Active && Time > 2.5f)
        {
            foreach (var u in Units)
            {
                if (u.Team != 1 || !u.Active || (!u.Hero && !u.IsLeader)) continue;
                if (Vector2.DistanceSquared(u.Pos, Player.Pos) > 38f * 38f) continue;
                foeTauntSpoken = true;
                bool hasGrudge = !string.IsNullOrEmpty(Options.Grudge);
                string topic = hasGrudge ? "foe_grudge_taunt" : "foe_taunt";
                string text = BattleBarks.Pick(topic, "", Rng.Mix(u.SoldierId, 97, Spoken.Count));
                if (text != "")
                {
                    string name = u.Squad?.Name is { Length: > 0 } sn ? sn : "Enemy Captain";
                    Spoken.Add(new Said(0, name, text, Time, u.Squad, Key: true, Team: 1, Mood: hasGrudge ? VoiceMood.Grudge : VoiceMood.Hostile, Culture: u.Type.Culture));
                }
                break;
            }
        }

        // 3. Nearby named man rallies when fighting beside the captain (strict flavor cooldown)
        if (anyNamed && Player.Active && Time - lastFlavorBarkAt >= 18f && Time > 6f)
        {
            foreach (var u in Units)
            {
                if (u.Team != 0 || !u.Active || u.SoldierId == 0 || u.IsPlayer) continue;
                if (u.Target is not { Active: true } foe || foe.Team == 0) continue;
                if (Vector2.DistanceSquared(u.Pos, Player.Pos) > 14f * 14f) continue;
                if (NamedOf(u) is not NamedSpec m) continue;
                string text = BattleBarks.Pick("man_rally", m.Nature, Rng.Mix(m.Id, (int)Time, 41));
                if (text != "")
                {
                    lastFlavorBarkAt = Time;
                    Spoken.Add(new Said(m.Id, m.Name, text, Time, u.Squad, Key: false, Team: 0, Mood: VoiceMood.Command, Culture: u.Type.Culture, Nature: m.Nature));
                }
                break;
            }
        }
    }

    /// Queue a 2-line reply from a living brother in the same company when `speaker` drops a combat bark.
    void QueueBrotherReply(Unit speaker, NamedSpec m)
    {
        if (pendingReply != null || m.Brothers == null || m.Brothers.Length == 0 || speaker.Squad == null) return;
        foreach (var u in speaker.Squad.Units)
        {
            if (u == speaker || !u.Active || u.SoldierId == 0 || !m.Brothers.Contains(u.SoldierId)) continue;
            if (NamedOf(u) is not NamedSpec bro) continue;
            pendingReply = (bro.Id, bro.Name, bro.Nature, u.Squad, m.Name, Time + 1.45f);
            return;
        }
    }
}
