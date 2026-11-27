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

/// Read-only battle bark director: rebound command echoes, calls to brace against horse,
/// rally answers, tactical warnings, enemy cries, and 2-line brother call-and-response.
/// Never touches sim RNG or combat state (replays stay exact).
public sealed partial class Battle
{
    readonly record struct PendingBark(
        float DueAt, int Soldier, string Name, string Text, Squad? Squad,
        bool Key, int Team, VoiceMood Mood, string Culture, string Nature, bool Sergeant);

    readonly List<PendingBark> pendingBarks = new();
    readonly Dictionary<Squad, float> squadBraceBarkAt = new();
    readonly HashSet<Squad> dryArrowsSpoken = new();

    float lastOrderBarkAt = -10, lastFlavorBarkAt = -10;
    float lastBraceBarkAt = -10, lastBraceHitBarkAt = -10;
    float lastTacticalBarkAt = -10, lastFlankBarkAt = -10, lastWaverBarkAt = -10, lastVolleyBarkAt = -10, lastFoeChargeBarkAt = -10;
    bool foeTauntSpoken, clashSpoken, victorySpoken;

    /// Pick the best living voice inside a squad (division sergeant, named veteran/hero, leader, or front-rank corporal).
    static (Unit? Unit, int SoldierId, string Name, string Nature, bool Sergeant) PickSpeaker(Squad s)
    {
        if (s.Division is { Leader: { Active: true } sgt })
        {
            string nat = NamedOf(sgt)?.Nature ?? "";
            string nm = s.Division.Name != "" ? s.Division.Name : s.Name;
            return (sgt, sgt.SoldierId, nm, nat, true);
        }
        Unit? best = null;
        foreach (var u in s.Units)
        {
            if (!u.Active || u.IsPlayer || u.Routing) continue;
            if (u.Sergeant || u.IsLeader || u.Hero) { best = u; break; }
            if (best == null && u.SoldierId != 0) best = u;
        }
        best ??= s.Units.FirstOrDefault(u => u.Active && !u.IsPlayer && !u.Routing)
            ?? s.Units.FirstOrDefault(u => u.Active && !u.IsPlayer);
        if (best == null) return (null, 0, "", "", false);
        var named = NamedOf(best);
        string name = named != null ? named.Name : s.Name != "" ? s.Name : "Sergeant";
        return (best, best.SoldierId, name, named?.Nature ?? "", best.Sergeant || best.IsLeader || best.Hero);
    }

    void QueueBark(PendingBark bark)
    {
        if (pendingBarks.Count >= 4) pendingBarks.RemoveAt(0);
        pendingBarks.Add(bark);
    }

    /// Deliver any due delayed command rebounds, relay echoes, rally answers, or brother replies.
    void DrainPendingBarks()
    {
        if (pendingBarks.Count == 0) return;
        for (int i = 0; i < pendingBarks.Count; i++)
        {
            var pb = pendingBarks[i];
            if (Time < pb.DueAt) continue;
            pendingBarks.RemoveAt(i--);
            if (pb.Squad != null)
            {
                bool alive = pb.Soldier != 0
                    ? pb.Squad.Units.Any(x => x.Active && x.SoldierId == pb.Soldier)
                    : pb.Squad.Units.Any(x => x.Active && !x.IsPlayer);
                if (!alive) continue;
            }
            Spoken.Add(new Said(pb.Soldier, pb.Name, pb.Text, Time, pb.Squad, pb.Key, pb.Team, pb.Mood, pb.Culture, pb.Nature, pb.Sergeant));
        }
    }

    /// Direct command / formation / fire order rebound: the primary company echoes the captain ~0.26s after the horn,
    /// and if multiple companies were ordered a second company relays the call down the line ~0.66s later.
    internal void SpeakSquadOrder(IReadOnlyList<Squad> squads, string topic)
    {
        if (Time <= 0 || squads.Count == 0 || Time - lastOrderBarkAt < 1.6f) return;
        Squad? first = null;
        (Unit? Unit, int SoldierId, string Name, string Nature, bool Sergeant) sp1 = default;
        foreach (var s in squads)
        {
            if (s.Team != 0 || s.Alive == 0) continue;
            var sp = PickSpeaker(s);
            if (sp.Unit != null) { first = s; sp1 = sp; break; }
        }
        if (first == null || sp1.Unit == null) return;
        string text = BattleBarks.Pick(topic, sp1.Nature, Rng.Mix(first.StartCount, (int)(Time * 10), Spoken.Count + pendingBarks.Count));
        if (text == "") return;
        lastOrderBarkAt = Time;
        QueueBark(new PendingBark(Time + 0.26f, sp1.SoldierId, sp1.Name, text, first,
            Key: true, Team: 0, Mood: VoiceMood.Command, Culture: sp1.Unit.Type.Culture, Nature: sp1.Nature, Sergeant: true));

        // Second-company relay echo when ordering multiple companies (rebound down the line)
        if (squads.Count >= 2)
        {
            foreach (var s in squads)
            {
                if (s == first || s.Team != 0 || s.Alive == 0) continue;
                var sp2 = PickSpeaker(s);
                if (sp2.Unit == null) continue;
                string echo = BattleBarks.Pick("order_rebound_echo", sp2.Nature, Rng.Mix(s.StartCount, (int)(Time * 13), Spoken.Count + 7));
                if (echo != "")
                    QueueBark(new PendingBark(Time + 0.66f, sp2.SoldierId, sp2.Name, echo, s,
                        Key: false, Team: 0, Mood: VoiceMood.Command, Culture: sp2.Unit.Type.Culture, Nature: sp2.Nature, Sergeant: false));
                break;
            }
        }
    }

    /// Sergeant echoes a division goal you just issued (Key callout, priority acoustic voice),
    /// plus a relay echo from a second company in the division when present.
    internal void SpeakDivisionOrder(Division d, Goal g)
    {
        if (d.Team != 0 || d.Leader is not { Active: true } sgt || Time - lastOrderBarkAt < 2.2f) return;
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

        var other = d.Squads.FirstOrDefault(q => q != sgt.Squad && q.Alive > 0);
        if (other != null)
        {
            var sp2 = PickSpeaker(other);
            if (sp2.Unit != null)
            {
                string echo = BattleBarks.Pick("order_rebound_echo", sp2.Nature, Rng.Mix(d.Id, (int)(Time * 13), Spoken.Count + 11));
                if (echo != "")
                    QueueBark(new PendingBark(Time + 0.62f, sp2.SoldierId, sp2.Name, echo, other,
                        Key: false, Team: 0, Mood: VoiceMood.Command, Culture: sp2.Unit.Type.Culture, Nature: sp2.Nature, Sergeant: false));
            }
        }
    }

    /// Spearmen/infantry call to brace against oncoming cavalry, or shout when braced points stop a charge.
    internal void SpeakBrace(Squad s, bool impact = false)
    {
        if (Time <= 2f || s.Alive == 0) return;
        if (impact)
        {
            if (s.Team != 0 || Time - lastBraceHitBarkAt < 10f) return;
            var sp = PickSpeaker(s);
            if (sp.Unit == null) return;
            string text = BattleBarks.Pick("brace_hit", sp.Nature, Rng.Mix(s.StartCount, (int)(Time * 7), Spoken.Count));
            if (text == "") return;
            lastBraceHitBarkAt = Time;
            Spoken.Add(new Said(sp.SoldierId, sp.Name, text, Time, s, Key: false, Team: 0, Mood: VoiceMood.Command, Culture: sp.Unit.Type.Culture, Nature: sp.Nature, Sergeant: sp.Sergeant));
            return;
        }
        if (Time - lastBraceBarkAt < 6.5f) return;
        if (squadBraceBarkAt.TryGetValue(s, out float prev) && Time - prev < 14f) return;
        var speaker = PickSpeaker(s);
        if (speaker.Unit == null) return;
        string topic = s.Team == 0 ? "brace_call" : "foe_brace";
        string line = BattleBarks.Pick(topic, speaker.Nature, Rng.Mix(s.StartCount, (int)(Time * 9), Spoken.Count));
        if (line == "") return;
        lastBraceBarkAt = Time;
        squadBraceBarkAt[s] = Time;
        Spoken.Add(new Said(speaker.SoldierId, speaker.Name, line, Time, s,
            Key: s.Team == 0, Team: s.Team, Mood: s.Team == 0 ? VoiceMood.Command : VoiceMood.Hostile,
            Culture: speaker.Unit.Type.Culture, Nature: speaker.Nature, Sergeant: s.Team == 0));
    }

    /// Nearby men answer when the captain blows Rally or sounds the King's Charge.
    internal void SpeakRally(Unit p, bool kingsCharge)
    {
        Unit? best = null;
        float bestD2 = RallyRadius * RallyRadius;
        foreach (var u in Units)
        {
            if (!u.Active || u.Team != p.Team || u.IsPlayer || u.Squad == null) continue;
            float d2 = Vector2.DistanceSquared(u.Pos, p.Pos);
            if (!kingsCharge && d2 >= bestD2) continue;
            if (best == null || (u.Sergeant && !best.Sergeant) || d2 < bestD2)
            {
                best = u;
                bestD2 = d2;
            }
        }
        if (best == null) return;
        var named = NamedOf(best);
        string nature = named?.Nature ?? "";
        string name = named != null ? named.Name : best.Squad?.Name is { Length: > 0 } sn ? sn : "Sergeant";
        string text = BattleBarks.Pick("rally_answer", nature, Rng.Mix(best.SoldierId, (int)(Time * 11), Spoken.Count));
        if (text == "") return;
        QueueBark(new PendingBark(Time + 0.32f, best.SoldierId, name, text, best.Squad,
            Key: true, Team: 0, Mood: VoiceMood.Command, Culture: best.Type.Culture, Nature: nature, Sergeant: true));
    }

    /// Victory cheer when the enemy army breaks and the ride-down pursuit begins.
    internal void SpeakVictory()
    {
        if (victorySpoken) return;
        victorySpoken = true;
        var sq = Squads.FirstOrDefault(s => s.Team == 0 && s.Alive > 0);
        if (sq == null) return;
        var sp = PickSpeaker(sq);
        if (sp.Unit == null) return;
        string text = BattleBarks.Pick("victory_cheer", sp.Nature, Rng.Mix(sq.StartCount, (int)(Time * 5), Spoken.Count));
        if (text == "") return;
        Spoken.Add(new Said(sp.SoldierId, sp.Name, text, Time, sq, Key: true, Team: 0, Mood: VoiceMood.Command, Culture: sp.Unit.Type.Culture, Nature: sp.Nature, Sergeant: true));
    }

    /// Periodic read-only check for battlefield vocal stims: clash cries, flank/waver warnings,
    /// archer volleys & dry quivers, enemy charges & taunts, and player-nearby rallies.
    void CheckBattleBarks()
    {
        DrainPendingBarks();

        // 1. Enemy champion / grudge rival taunt once when closing on the captain
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

        // 2. First big melee clash cry (once per battle when the lines meet)
        if (!clashSpoken && Time > 6.5f)
        {
            foreach (var s in Squads)
            {
                if (s.Team != 0 || s.Alive < 3 || !s.InContact) continue;
                int engaged = 0;
                foreach (var u in s.Units)
                    if (u.Active && !u.Routing && u.Target is { Active: true } foe && foe.Team == 1 && Vector2.DistanceSquared(u.Pos, foe.Pos) < 16f)
                        if (++engaged >= 3) break;
                if (engaged < 3) continue;
                var sp = PickSpeaker(s);
                if (sp.Unit == null) continue;
                string text = BattleBarks.Pick("clash_cry", sp.Nature, Rng.Mix(s.StartCount, (int)Time, 23));
                if (text != "")
                {
                    clashSpoken = true;
                    lastTacticalBarkAt = Time;
                    Spoken.Add(new Said(sp.SoldierId, sp.Name, text, Time, s, Key: false, Team: 0, Mood: VoiceMood.Command, Culture: sp.Unit.Type.Culture, Nature: sp.Nature, Sergeant: sp.Sergeant));
                }
                break;
            }
        }

        // 3. Situational tactical calls (paced by global tactical cooldown so voices stay crisp)
        if (Time > 6.5f && Time - lastTacticalBarkAt >= 7.5f)
        {
            // 3a. Flank warning (Key callout)
            if (Time - lastFlankBarkAt >= 14f)
            {
                foreach (var s in Squads)
                {
                    if (s.Team != 0 || s.AiControlled || s.Alive < 3 || s.Pressure <= 2f) continue;
                    var sp = PickSpeaker(s);
                    if (sp.Unit == null) continue;
                    string text = BattleBarks.Pick("flank_warn", sp.Nature, Rng.Mix(s.StartCount, (int)Time, 53));
                    if (text != "")
                    {
                        lastFlankBarkAt = lastTacticalBarkAt = Time;
                        Spoken.Add(new Said(sp.SoldierId, sp.Name, text, Time, s, Key: true, Team: 0, Mood: VoiceMood.Command, Culture: sp.Unit.Type.Culture, Nature: sp.Nature, Sergeant: true));
                        return;
                    }
                }
            }

            // 3b. Archers out of arrows (once per archer company)
            foreach (var s in Squads)
            {
                if (s.Team != 0 || s.AiControlled || s.Alive < 2 || s.Units.Count == 0 || s.Units[0].Type.Ammo <= 0) continue;
                if (dryArrowsSpoken.Contains(s)) continue;
                if (!s.Units.All(u => !u.Active || u.Ammo == 0)) continue;
                dryArrowsSpoken.Add(s);
                var sp = PickSpeaker(s);
                if (sp.Unit == null) continue;
                string text = BattleBarks.Pick("arrows_out", sp.Nature, Rng.Mix(s.StartCount, (int)Time, 61));
                if (text != "")
                {
                    lastTacticalBarkAt = Time;
                    Spoken.Add(new Said(sp.SoldierId, sp.Name, text, Time, s, Key: false, Team: 0, Mood: VoiceMood.Weary, Culture: sp.Unit.Type.Culture, Nature: sp.Nature, Sergeant: sp.Sergeant));
                    return;
                }
            }

            // 3c. Wavering / broken ranks steady-call
            if (Time > 8f && Time - lastWaverBarkAt >= 16f)
            {
                foreach (var s in Squads)
                {
                    if (s.Team != 0 || s.AiControlled || s.Alive < 3) continue;
                    if (!s.Broken && AvgMorale(s) >= 42f) continue;
                    var sp = PickSpeaker(s);
                    if (sp.Unit == null) continue;
                    string text = BattleBarks.Pick("waver_hold", sp.Nature, Rng.Mix(s.StartCount, (int)Time, 73));
                    if (text != "")
                    {
                        lastWaverBarkAt = lastTacticalBarkAt = Time;
                        Spoken.Add(new Said(sp.SoldierId, sp.Name, text, Time, s, Key: false, Team: 0, Mood: VoiceMood.Command, Culture: sp.Unit.Type.Culture, Nature: sp.Nature, Sergeant: true));
                        return;
                    }
                }
            }

            // 3d. Friendly archer volley call
            if (Time - lastVolleyBarkAt >= 16f)
            {
                foreach (var s in Squads)
                {
                    if (s.Team != 0 || s.Alive < 2 || s.Units.Count == 0 || s.Units[0].Type.Ammo <= 0) continue;
                    if (Time - s.LoosedAt > 0.7f) continue;
                    var sp = PickSpeaker(s);
                    if (sp.Unit == null) continue;
                    string text = BattleBarks.Pick("volley_call", sp.Nature, Rng.Mix(s.StartCount, (int)Time, 83));
                    if (text != "")
                    {
                        lastVolleyBarkAt = lastTacticalBarkAt = Time;
                        Spoken.Add(new Said(sp.SoldierId, sp.Name, text, Time, s, Key: false, Team: 0, Mood: VoiceMood.Command, Culture: sp.Unit.Type.Culture, Nature: sp.Nature, Sergeant: sp.Sergeant));
                        return;
                    }
                }
            }

            // 3e. Enemy charge cry when an enemy company surges close to the player
            if (Player.Active && Time > 7.5f && Time - lastFoeChargeBarkAt >= 18f)
            {
                foreach (var s in Squads)
                {
                    if (s.Team != 1 || s.Alive < 4 || s.Order != Order.Charge || s.Swarm) continue;
                    if (Vector2.DistanceSquared(s.Center, Player.Pos) > 36f * 36f) continue;
                    var sp = PickSpeaker(s);
                    if (sp.Unit == null) continue;
                    string text = BattleBarks.Pick("foe_charge", sp.Nature, Rng.Mix(s.StartCount, (int)Time, 91));
                    if (text != "")
                    {
                        lastFoeChargeBarkAt = lastTacticalBarkAt = Time;
                        Spoken.Add(new Said(0, sp.Name, text, Time, s, Key: false, Team: 1, Mood: VoiceMood.Hostile, Culture: sp.Unit.Type.Culture, Nature: sp.Nature, Sergeant: true));
                        return;
                    }
                }
            }
        }

        // 4. Nearby named man rallies when fighting beside the captain
        if (anyNamed && Player.Active && Time - lastFlavorBarkAt >= 14f && Time > 6f)
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
        if (m.Brothers == null || m.Brothers.Length == 0 || speaker.Squad == null) return;
        foreach (var u in speaker.Squad.Units)
        {
            if (u == speaker || !u.Active || u.SoldierId == 0 || !m.Brothers.Contains(u.SoldierId)) continue;
            if (NamedOf(u) is not NamedSpec bro) continue;
            string text = BattleBarks.Pick("man_brother_reply", bro.Nature, Rng.Mix(bro.Id, Spoken.Count, 19))
                .Replace("{other}", m.Name);
            if (text != "")
                QueueBark(new PendingBark(Time + 1.45f, bro.Id, bro.Name, text, u.Squad,
                    Key: false, Team: 0, Mood: VoiceMood.Warm, Culture: u.Type.Culture, Nature: bro.Nature, Sergeant: false));
            return;
        }
    }
}
