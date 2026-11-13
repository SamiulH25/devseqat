using Microsoft.Xna.Framework;

namespace Ironfield;

/// The captain's aura on the map (docs/archive/KNIGHT_ORDERS.md 5.1-5.5, K1): XP from how you fought, the wall that stops it at
/// each tier, and the trials that break the wall. The battle side is BattleAura.cs.
public sealed partial class Campaign
{
    public const int AuraTop = 5;
    /// Aura XP that fills each tier (index = the tier you stand at): to Kindled, Aura Knight, Expert, Master, Transcendent.
    public static readonly float[] AuraNeeds = { 30, 80, 180, 400, 900 };
    public static readonly string[] AuraNames = { "Unawakened", "Kindled", "Aura Knight", "Aura Expert", "Aura Master", "Transcendent" };
    public static string AuraName(int tier) => AuraNames[Math.Clamp(tier, 0, AuraTop)];
    /// What each tier gives in battle, one line (the captain sheet, the breakthrough news).
    public static readonly string[] AuraGifts =
    {
        "No aura yet. Fight well and it will stir.",
        "Breath comes back faster.",
        "Hold the aura key to sheathe your blade in aura: it cuts through armour and guards.",
        "Aura armour takes part of every blow.",
        "Your presence shakes nearby foes and steadies your men. Aura comes back as you fight.",
        "The great form at its full. There is no one above you.",
    };

    /// XP still to fill the tier you stand at (0 at the top).
    public float AuraNeed => Captain.AuraTier >= AuraTop ? 0 : AuraNeeds[Captain.AuraTier];
    /// The aura you fight with: your tier, a tier less with a cracked core (K5).
    public int AuraInBattle => Math.Max(0, Captain.AuraTier - (Captain.Wounds.Contains("core") ? 1 : 0));
    /// The news of this battle's aura for the report ("" = none).
    string auraNews = "";

    /// XP into the pool until the wall stops it; the wall is told once.
    internal void GainAura(float xp)
    {
        if (xp <= 0 || Captain.AuraTier >= AuraTop || Captain.AuraWall || S.GameOver) return;
        Captain.AuraXp = MathF.Min(AuraNeed, Captain.AuraXp + xp);
        if (Captain.AuraXp < AuraNeed) return;
        Captain.AuraWall = true;
        Log(Captain.AuraTier == 0 ? "Something stirs in you in the press of battle, and will not wake. A trial will wake it (K)." : "Your aura has reached a wall. A trial will break it (K).");
        if (auraNews == "") auraNews = "YOUR AURA HAS REACHED A WALL";
    }

    /// A trial passed: the wall breaks and you rise a tier. False if there was no wall to break.
    internal bool BreakThrough(string how)
    {
        if (!Captain.AuraWall || Captain.AuraTier >= AuraTop) return false;
        Captain.AuraTier++;
        Captain.AuraXp = 0;
        Captain.AuraWall = false;
        Captain.WallWins = 0;
        string name = AuraName(Captain.AuraTier);
        Deed(Captain.AuraTier == 1 ? $"{how} Your aura wakes: you are Kindled." : $"{how} Your aura breaks through: you are an {name}.", "aura");
        auraNews = $"BREAKTHROUGH: {name.ToUpperInvariant()}. {AuraGifts[Captain.AuraTier].ToUpperInvariant()}";
        Captain.Renown += 5 * Captain.AuraTier;
        LearnForms();
        return true;
    }

    /// The walls a trial can break (K1): a won duel to Kindled and Aura Knight; holding against two to one up to Aura Expert.
    /// Higher walls need the harder trials (K5).
    public const int DuelBreaksBelow = 2, OddsBreakBelow = 3;

    /// After a battle you fought: XP from your kills, parries and ripostes, and the trial of holding against the odds.
    internal void AuraAfterBattle(BattleResult r, bool won, float odds)
    {
        if (won) Seasoned(r.Duration > 0 ? 2 : 1);
        if (r.Duration <= 0) { GainAura(won ? 1.5f : 0.5f); return; } // auto-resolved: you did your part
        if (won && odds >= 2 && r.PlayerKills >= 3 && Captain.AuraTier < OddsBreakBelow)
            BreakThrough($"You held against odds of {odds:0.0} to one with your own blade.");
        GainAura(r.PlayerKills * 0.5f + r.Parries * 0.6f + r.Ripostes * 0.8f + (won ? 2 : 0.5f)); // after the trial: it counts towards the next tier
    }

    /// Won battles that wake the aura at its first wall (AR1), a fought one counting two: every captain who keeps fighting
    /// and winning wakes in time, even one who never seeks a trial.
    public const int SeasonWins = 12;

    /// A won battle while the first wall stands.
    void Seasoned(int n)
    {
        if (!Captain.AuraWall || Captain.AuraTier != 0) return;
        Captain.WallWins += n;
        if (Captain.WallWins >= SeasonWins) BreakThrough("Battle after battle, it wakes.");
    }

    /// After a duel: more XP than a battle, and a won duel breaks the lowest walls.
    internal void AuraAfterDuel(BattleResult r, bool won, string foe)
    {
        if (won && Captain.AuraTier < DuelBreaksBelow) BreakThrough($"You beat {foe} in single combat.");
        GainAura((won ? 6 : 2) + r.Parries * 0.6f + r.Ripostes * 0.8f);
    }

    // ---- forms (5.6, K2) ----

    /// Uses that raise a form a rank (ranks 1-3).
    public const int FormRankUses = 15;
    public static int FormRank(CaptainState c, string id) => 1 + Math.Min(2, c.FormUses.GetValueOrDefault(id) / FormRankUses);

    /// Learn the forms anyone may at your tier (Focus when kindled, Aura Slash as an expert, Aura Burst as a master).
    void LearnForms()
    {
        foreach (var f in Forms.Generic)
            if (f.Tier <= Captain.AuraTier) LearnForm(f.Id);
        LearnFlameForms();
    }

    /// Learn a form (an order's, your own); it goes to the back of the slots. False if known already.
    internal bool LearnForm(string id)
    {
        if (!Forms.Has(id) || Captain.Forms.Contains(id)) return false;
        Captain.Forms.Add(id);
        if (S.Hours > 0) Log($"You learn the form {Forms.Get(id).Name}.");
        return true;
    }

    /// Put a form you know in the first slot of its kind (a great form in the great slot); returns null or why not.
    public string? SlotForm(string id)
    {
        if (!Captain.Forms.Contains(id)) return "you do not know that form";
        Captain.Forms.Remove(id);
        Captain.Forms.Insert(0, id);
        return null;
    }

    /// The forms in battle slots now: the first three that are not great, and the first great one.
    public static List<string> SlottedForms(CaptainState c) =>
        c.Forms.Where(Forms.Has).Where(id => !Forms.Get(id).Great).Take(3).Concat(c.Forms.Where(Forms.Has).Where(id => Forms.Get(id).Great).Take(1)).ToList();

    /// After a battle: forms used rank up; the Black Flame's corruption stays in you.
    void FormsAfterBattle(BattleResult r)
    {
        foreach (var (id, n) in r.FormUses)
        {
            int before = FormRank(Captain, id);
            Captain.FormUses[id] = Captain.FormUses.GetValueOrDefault(id) + n;
            if (FormRank(Captain, id) > before && Forms.Has(id)) Log($"Your {Forms.Get(id).Name} grows stronger (rank {FormRank(Captain, id)}).");
        }
        if (r.Corruption > 0) Corrupt(r.Corruption);
        if (r.MadSpells > 0) Log(r.MadSpells == 1 ? "In the battle the black flame took you, and you do not know whose blood is on your blade." : $"In the battle the black flame took you {r.MadSpells} times.");
    }

    // ---- the higher trials and the cracked core (5.5, K5) ----

    /// The trials that can break the wall at this tier, for the captain sheet.
    public static string TrialsFor(int tier) => string.Join(", ", new[]
    {
        tier == 0 ? $"WIN {SeasonWins} BATTLES" : "",
        tier < DuelBreaksBelow ? "WIN A DUEL OR THE LISTS" : "BEAT A CHAMPION ABOVE YOU IN A DUEL",
        tier < OddsBreakBelow ? "HOLD AGAINST TWO TO ONE" : "",
        tier < VigilBreaksBelow ? "KEEP A VIGIL AT A SHRINE, STONES, OLD BATTLEFIELD OR HERMIT" : "",
        "GRIEF",
    }.Where(s => s != ""));

    /// The trial card (AR1): each trial that breaks the wall you stand at, with where to find it now. Empty below a wall.
    public List<string> NearestTrials()
    {
        var lines = new List<string>();
        var cap = Captain;
        if (!cap.AuraWall || cap.AuraTier >= AuraTop) return lines;
        int tier = cap.AuraTier;
        var at = Player.Pos;
        string Where(Vector2 p)
        {
            float days = MathF.Max(0.5f, MathF.Round(Vector2.Distance(at, p) / MathF.Max(0.1f, Speed(Player)) / 24 * 2) / 2);
            return $"{(days <= 0.5f ? "HALF A DAY" : days == 1 ? "A DAY" : $"{days:0.#} DAYS")} {Bearing(at, p).ToUpperInvariant()}";
        }
        if (tier == 0) lines.Add($"BATTLE: WIN {Math.Max(1, SeasonWins - cap.WallWins)} MORE (A BATTLE YOU FIGHT COUNTS TWICE).");
        if (tier < DuelBreaksBelow)
        {
            var lord = S.Parties.Where(p => p.Kind == PartyKind.Lord && !p.Down && Hostile(Player, p) && InSight(p.Pos) && CannotChallenge(p) == null)
                .OrderBy(p => Vector2.Distance(p.Pos, at)).FirstOrDefault();
            lines.Add(lord != null && CharacterOf(lord) is Character l
                ? $"DUEL: {l.Name.ToUpperInvariant()} WOULD MEET YOU, {Where(lord.Pos)}."
                : "DUEL: CHALLENGE A LORD YOU MEET IN THE FIELD (NOT A CAUTIOUS ONE).");
            lines.Add("THE LISTS: WIN A TOURNAMENT WHEN A TOWN HOLDS ONE.");
        }
        else if (tier < AuraTop) lines.Add("DUEL: BEAT A CHAMPION ABOVE YOU (AN ORDER KNIGHT, OR A LORD FAR ABOVE YOUR AURA).");
        if (tier < OddsBreakBelow) lines.Add("ODDS: WIN A BATTLE YOU FIGHT AT TWO TO ONE, WITH THREE KILLS OF YOUR OWN.");
        if (tier < VigilBreaksBelow && Sites.Where(x => VigilSites.Contains(x.Kind) && SiteReady(x)).OrderBy(x => Vector2.Distance(x.Pos, at)).FirstOrDefault() is Site site)
            lines.Add($"VIGIL: THE {site.Name.ToUpperInvariant()}, {Where(site.Pos)}. IF IT FAILS, YOUR CORE CRACKS.");
        lines.Add("GRIEF: THE LOSS OF A COMPANION.");
        return lines;
    }

    /// A vigil breaks the walls below Aura Master; grief and a duel with a champion above you break any wall.
    public const int VigilBreaksBelow = 4;
    /// Days a cracked core takes to mend of itself (resting in a town counts three times).
    public const float CoreDays = 30;
    internal static readonly string[] VigilSites = { "shrine", "stones", "battlefield", "hermit" };

    /// Will a night at this site be a trial for your aura?
    bool VigilAt(Site site) => Captain.AuraWall && Captain.AuraTier < VigilBreaksBelow && VigilSites.Contains(site.Kind) && S.Event == null && Events.Any(e => e.Id == "aura_vigil");

    /// The night's vigil: your aura breaks through, or the strain cracks your core. Arms and an order's teaching help.
    string KeepVigil(Site? site)
    {
        if (!Captain.AuraWall) return "The night passes quietly.";
        float odds = 0.5f + Skill("Arms") / 400f + (Captain.Order != "" ? 0.1f : 0) - (Captain.Wounds.Contains("core") ? 0.2f : 0);
        if (rng.Chance(odds)) { BreakThrough($"You keep the vigil at the {site?.Name ?? "stones"}."); return "At dawn something in you gives way, and the world is sharper."; }
        CrackCore();
        return "Near dawn the strain is too much. Something in you cracks.";
    }

    /// A trial failed: the core cracks (a lasting wound that holds the aura a tier down) until it mends.
    internal void CrackCore()
    {
        if (!Captain.Wounds.Contains("core")) Captain.Wounds.Add("core");
        Captain.CoreUntil = (float)S.Hours + CoreDays * 24;
        Log("Your core is cracked: your aura fights a tier lower until it mends (a month, rest in a town, a priest, a spring).");
        TemptFlame(); // a cracked core is where the Hollow Crown finds you (K7)
    }

    /// Mend a cracked core (time, a rite, a spring).
    internal bool MendCore(string how)
    {
        if (!Captain.Wounds.Remove("core")) return false;
        Captain.CoreUntil = 0;
        Log($"{how} Your core is whole again.");
        return true;
    }

    /// Each day: a cracked core mends with time, faster while you rest in a town.
    void CoreDaily()
    {
        if (!Captain.Wounds.Contains("core")) return;
        if (Player.Resting && SettlementAt(Player.Pos) is { Kind: not SettlementKind.Village }) Captain.CoreUntil -= 48;
        if (S.Hours >= Captain.CoreUntil) MendCore("Time and rest do their work.");
    }

    /// A companion falls while your aura stands at a wall: grief is a trial (rage now, at a cost; or mourning, later).
    void GriefTrial(Character k)
    {
        if (!Captain.AuraWall || S.Event != null || !Events.Any(e => e.Id == "aura_grief")) return;
        Raise(new PendingEvent { Id = "aura_grief", Lord = k.Id });
    }

    /// A duel won against a champion above you breaks any wall: an order knight of your aura or more, or a lord whose
    /// army stands three tiers above your aura.
    bool ChampionAbove(Party e) =>
        CharacterOf(e) is Character ch && (ch.Kind == "knight" && ch.Aura >= Captain.AuraTier || e.Tier >= Captain.AuraTier + 3);

    /// The aura news for this report, cleared once read.
    string TakeAuraNews() { var s = auraNews; auraNews = ""; return s; }
}
