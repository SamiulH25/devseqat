using System.Text.Json;

namespace Ironfield;

public sealed class DialogueInterject
{
    public string Who = "", Text = "";
}

public sealed class DialogueChoice
{
    public string Stance = "", Label = "", Next = "", Action = "";
    public int OpinionDelta, GoldCost;
    public bool RequiresDuel;
}

public sealed class DialogueNode
{
    public string Mood = "even", Text = "";
    public DialogueInterject? Interject;
    public List<DialogueChoice> Choices = new();
}

public sealed class DialogueScene
{
    public string Id = "", Context = "lord", Entry = "open";
    public bool RequiresHostile, RequiresGrudge;
    public Dictionary<string, DialogueNode> Nodes = new();
}

/// Active state of a multi-turn conversation or pre-battle parley.
public sealed class DialogueRun
{
    public DialogueScene Scene = new();
    public string NodeId = "open";
    public Character Speaker = new();
    public DialogueNode? Current => Scene.Nodes.TryGetValue(NodeId, out var n) ? n : null;
}

public static class DialogueLibrary
{
    sealed class FileDto { public List<DialogueScene> Scenes = new(); }
    static readonly JsonSerializerOptions Json = new() { IncludeFields = true, PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
    static List<DialogueScene>? all;

    public static IReadOnlyList<DialogueScene> All => all ??= Load();

    static List<DialogueScene> Load()
    {
        var list = new List<DialogueScene>();
        string dir = Path.Combine(AppContext.BaseDirectory, "Data", "dialogue");
        if (!Directory.Exists(dir)) dir = Path.Combine("Data", "dialogue");
        if (!Directory.Exists(dir)) return list;
        foreach (var file in Directory.GetFiles(dir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
        {
            if (Path.GetFileName(file).Equals("barks.json", StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                var dto = JsonSerializer.Deserialize<FileDto>(File.ReadAllText(file), Json);
                if (dto?.Scenes != null) list.AddRange(dto.Scenes);
            }
            catch { }
        }
        return list;
    }
}

partial class Campaign
{
    /// Pick and start a multi-turn pre-battle parley or lord dialogue scene.
    public DialogueRun? StartDialogue(Character ch, string context, Party? encounter = null)
    {
        bool hostile = encounter != null && !Meeting(encounter);
        bool grudge = encounter != null && !string.IsNullOrEmpty(GrudgeOf(encounter));
        var fits = DialogueLibrary.All.Where(s =>
            s.Context == context
            && (!s.RequiresHostile || hostile)
            && (!s.RequiresGrudge || grudge)).ToList();
        if (fits.Count == 0) return null;
        // Prefer most specific scene (grudge > hostile > generic)
        var scene = fits.OrderByDescending(s => (s.RequiresGrudge ? 2 : 0) + (s.RequiresHostile ? 1 : 0)).First();
        return new DialogueRun { Scene = scene, NodeId = scene.Entry, Speaker = ch };
    }

    /// Fill standard placeholders in a dialogue node's text.
    public string FormatDialogueText(string text, Character ch) => Fill(text, ch, "");

    /// Check whether a dialogue choice is currently selectable (returns null if available, else reason).
    public string? CannotChooseDialogue(DialogueChoice c, Party? encounter)
    {
        if (c.GoldCost > 0 && S.Gold < c.GoldCost) return $"NEED {c.GoldCost} CROWNS.";
        if (c.RequiresDuel)
        {
            if (encounter == null) return "NO ENEMY FORCE TO CHALLENGE.";
            return CannotChallenge(encounter);
        }
        return null;
    }

    /// Apply a player's choice in an active multi-turn dialogue; returns any terminal action ("start_duel", "resolve_peace", "to_topics", "back_to_encounter", or "").
    public string ChooseDialogue(DialogueRun run, DialogueChoice c)
    {
        if (c.OpinionDelta != 0 && S.CaptainChar >= 0)
            run.Speaker.Opinion[S.CaptainChar] = Math.Clamp(run.Speaker.Opinion.GetValueOrDefault(S.CaptainChar) + c.OpinionDelta, -100, 100);
        if (c.GoldCost > 0 && c.Action == "resolve_peace")
            S.Gold = Math.Max(0, S.Gold - c.GoldCost);
        if (!string.IsNullOrEmpty(c.Next) && run.Scene.Nodes.ContainsKey(c.Next))
        {
            run.NodeId = c.Next;
            return "";
        }
        return c.Action;
    }

    /// Resolve a character's culture for acoustic voice selection.
    public string CultureOfCharacter(Character ch)
    {
        if (!string.IsNullOrWhiteSpace(ch.Culture) && ch.Culture != "common") return ch.Culture;
        if (ch.Faction >= 0 && ch.Faction < S.Factions.Count && !string.IsNullOrWhiteSpace(S.Factions[ch.Faction].Culture))
            return S.Factions[ch.Faction].Culture;
        return "azure";
    }

    /// Map a character's mood/hint to an acoustic VoiceMood.
    public VoiceMood VoiceMoodOf(Character ch, string? moodHint = null) => (moodHint ?? "").ToLowerInvariant() switch
    {
        "grudge"  => VoiceMood.Grudge,
        "hostile" => VoiceMood.Hostile,
        "command" => VoiceMood.Command,
        "warm"    => VoiceMood.Warm,
        "weary"   => VoiceMood.Weary,
        _ => OpinionOfYou(ch) switch
        {
            >= 25 => VoiceMood.Warm,
            <= -20 => VoiceMood.Hostile,
            _ => VoiceMood.Even,
        },
    };
}
