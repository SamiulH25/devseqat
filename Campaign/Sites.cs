using Microsoft.Xna.Framework;

namespace Ironfield;

/// A place of interest between the settlements (docs/SIDE_CONTENT.md S2). Sites are made with the map from its seed, so a save
/// holds only what you did at them (CampaignState.SiteUntil, SitesSeen).
public sealed class Site
{
    public int Id, Province;
    /// "ruin", "shrine", "stones", "battlefield", "mine", "hermit", "watchtower", "spring" or "mill".
    public string Kind = "", Name = "";
    public Vector2 Pos;

    static readonly string[] Saints = { "Aldhelm", "Brigid", "Cuthbert", "Dunstan", "Edith", "Frideswide", "Guthlac", "Hilda", "Ia", "Kenelm",
        "Melor", "Nectan", "Oswald", "Petroc", "Sativola", "Werburgh", "Winifred", "Wystan" };

    /// A site's name: ruins and old fields take a forgotten place's name; the rest are named for their province.
    public static string NameFor(string kind, string province, PlaceNamer namer, Random rng) => kind switch
    {
        "ruin" => $"Ruins of {namer.Next("")}",
        "battlefield" => $"Field of {namer.Next("")}",
        "shrine" => $"St {Saints[rng.Next(Saints.Length)]}'s Shrine",
        "stones" => $"{province} Stones",
        "mine" => $"{province} Mine",
        "hermit" => $"Hermit of {province}",
        "watchtower" => $"{province} Watch",
        "spring" => $"{province} Spring",
        _ => $"{province} Mill",
    };

    /// One line on what the place is, for the map tooltip.
    public string What => Kind switch
    {
        "ruin" => "AN OLD RUIN. SEARCH IT, IF YOU DARE",
        "shrine" => "A WAYSIDE SHRINE. PRAYER LIFTS THE MEN",
        "stones" => "A RING OF STANDING STONES. OMENS, GOOD OR ILL",
        "battlefield" => "AN OLD BATTLEFIELD. GEAR LIES IN THE GRASS",
        "mine" => "A SILVER MINE. TOLLS AND STRONG DIGGERS",
        "hermit" => "A HERMIT'S HUT. HE KNOWS THINGS",
        "watchtower" => "AN OLD WATCHTOWER. SEE FAR FROM THE TOP",
        "spring" => "A HEALING SPRING. REST THE WOUNDED",
        _ => "A WATERMILL. FLOUR FOR SALE",
    };
}

/// Sites: going there, the event a visit raises, and the effects only sites use.
public sealed partial class Campaign
{
    public static readonly string[] SiteKinds = { "ruin", "shrine", "stones", "battlefield", "mine", "hermit", "watchtower", "spring", "mill" };

    public IReadOnlyList<Site> Sites => Map.Sites;
    public Site? SiteById(int id) => id >= 0 && id < Map.Sites.Count ? Map.Sites[id] : null;
    public Site? SiteAt(Vector2 p, float r = 4.5f) => Map.Sites.Where(s => Vector2.Distance(s.Pos, p) < r).OrderBy(s => Vector2.Distance(s.Pos, p)).FirstOrDefault();
    public bool SiteReady(Site s) => S.SiteUntil.GetValueOrDefault(s.Id) <= S.Hours;
    public bool SiteSeen(Site s) => S.SitesSeen.Contains(s.Id);

    /// How the site stands for you: "" when it can be used now, else when (or that it is spent for good).
    public string SiteWait(Site s)
    {
        float until = S.SiteUntil.GetValueOrDefault(s.Id);
        if (until <= S.Hours) return "";
        double days = (until - S.Hours) / 24;
        return days > 999 ? "NOTHING MORE TO FIND HERE" : $"AGAIN IN {Math.Ceiling(days):0} DAYS";
    }

    public void GoTo(Site s) { Player.Goal = s.Pos; Player.Moving = true; Player.ChaseId = -1; Player.GoalSettlement = -1; S.GoalSite = s.Id; Player.Resting = false; }

    /// You reach a site: the first visit is remembered, and a ready site raises its event (one of the events whose trigger
    /// is "site_" + its kind).
    void ArriveAtSite(Site site)
    {
        if (!S.SitesSeen.Contains(site.Id)) S.SitesSeen.Add(site.Id);
        if (!SiteReady(site)) { Log($"{site.Name}: {SiteWait(site).ToLowerInvariant()}."); return; }
        var fits = Events.Where(e => e.Trigger == "site_" + site.Kind).ToList();
        if (fits.Count == 0 || S.Event != null) return;
        var pe = new PendingEvent { Site = site.Id, Settlement = SettlementNear(site.Pos)?.Id ?? -1 };
        pe.Id = fits[rng.Next(fits.Count)].Id;
        Raise(pe);
    }

    Settlement? SettlementNear(Vector2 p) => S.Settlements.OrderBy(s => Vector2.Distance(s.Pos, p)).FirstOrDefault();

    /// The effects only sites use; null when the verb is not one of them.
    string? SiteEffect(string verb, float n, PendingEvent pe, out bool handled)
    {
        handled = true;
        var site = SiteById(pe.Site);
        switch (verb)
        {
            case "spent": if (site != null) S.SiteUntil[site.Id] = (float)S.Hours + n * 24; return null;
            case "heal":
                int healed = 0;
                foreach (var st in Player.Companies.SelectMany(c => c.Stacks))
                {
                    int k = (int)MathF.Round(st.Wounded * n / 100);
                    st.Wounded -= k; st.Men += k; healed += k;
                }
                return healed > 0 ? $"{healed} wounded are back on their feet." : null;
            case "find":
                int found = (int)(n * rng.Range(0.6f, 1.4f));
                S.Gold += found;
                return $"You find {found} crowns.";
            case "learn":
                var low = Skills.All.OrderBy(d => Skill(d.Id)).First();
                Practise(low.Id, n);
                return $"Your {low.Name.ToLowerInvariant()} grows.";
            case "men":
                int got = GiftMen(Scaled((int)n), SettlementNear(site?.Pos ?? Player.Pos));
                return got > 0 ? $"{got} men join you." : "You have no room for them.";
            case "search": return Search(site);
            case "omen": return Omen();
            case "spy": return Spy(site?.Pos ?? Player.Pos);
        }
        handled = false;
        return null;
    }

    /// A ruin searched: coin, a relic, an old banner, nothing, or goblins who have made it their den.
    string Search(Site? site)
    {
        float roll = rng.Float();
        if (roll < 0.35f) { int g = (int)rng.Range(120, 260); S.Gold += g; return $"Under the rubble: a rotted chest and {g} crowns."; }
        if (roll < 0.55f) { S.Gold += 100; Captain.Renown += 8; return "You find a saint's relic in a fallen chapel. The tale of it spreads."; }
        if (roll < 0.70f) { foreach (var c in Player.Companies) c.Cohesion = Math.Clamp(c.Cohesion + 4, 0, 30); return "An old war banner, still bright under the dust. The men cheer it."; }
        if (roll < 0.85f || site == null || !Lurkers(site)) return "Only bones and nettles.";
        return "Goblins have made the ruin their den, and they do not want visitors!";
    }

    /// Goblins in the ruin: a small holdout right where you stand (so they fall on you at once).
    bool Lurkers(Site site)
    {
        if (Holdouts.Any(h => Vector2.Distance(h.Pos, site.Pos) < 12)) return false;
        var lair = new Party
        {
            Id = S.NextId++, Kind = PartyKind.Monster, Breed = "goblin", Lair = true, Name = $"Goblins of the {site.Name}",
            Pos = site.Pos, Goal = site.Pos, Task = "hold", Gold = B.HoldoutStartHoard * 1.5f, NextRaid = (float)S.Hours + B.HoldoutRaidDays * 24,
        };
        lair.Companies.Add(NewCompany("goblin", "infantry", Scaled(B.HoldoutStartMen), 0));
        S.Parties.Add(lair);
        return true;
    }

    /// A night among the stones: a good or a bad sign (worse after dark, and when the dead are restless).
    string Omen()
    {
        bool night = S.Hours % 24 is >= 20 or < 5;
        float bad = (night ? 0.45f : 0.3f) + (S.Graves.Count > 0 ? 0.1f : 0);
        if (rng.Chance(bad))
        {
            foreach (var c in Player.Companies) c.Cohesion = Math.Clamp(c.Cohesion - 3, 0, 30);
            return "A raven lands on the tallest stone and will not leave. The men whisper all night.";
        }
        foreach (var c in Player.Companies) c.Cohesion = Math.Clamp(c.Cohesion + 3, 0, 30);
        Captain.Renown += 2;
        return "Dawn comes gold through the stones. The men take it for a blessing.";
    }

    /// From a watchtower: what moves within a long ride, nearest first (a few lines for the log).
    string Spy(Vector2 from)
    {
        var seen = S.Parties.Where(p => !p.Down && p != Player && (Hostile(Player, p) || p.Kind == PartyKind.Monster) && Vector2.Distance(p.Pos, from) < 110 * Map.Spread)
            .OrderBy(p => Vector2.Distance(p.Pos, from)).Take(4).ToList();
        if (seen.Count == 0) return "From the top you see empty country for miles.";
        return "From the top you see " + string.Join("; ", seen.Select(p => $"{p.Name} ({p.Men}) to the {Bearing(from, p.Pos)}")) + ".";
    }

    /// "north-east" and the like, from a to b (the map's north is low Y).
    public static string Bearing(Vector2 a, Vector2 b)
    {
        var d = b - a;
        string[] names = { "east", "south-east", "south", "south-west", "west", "north-west", "north", "north-east" };
        int i = (int)MathF.Round(MathF.Atan2(d.Y, d.X) / (MathF.PI / 4));
        return names[((i % 8) + 8) % 8];
    }

    /// Men who join for nothing (miners, the hermit's lads): local levies, into companies with room, else a new company.
    int GiftMen(int n, Settlement? from)
    {
        n = Math.Min(n, RoomForMen);
        if (n <= 0 || from == null) return 0;
        string levy = RecruitTroopFor(from);
        int left = n;
        foreach (var co in Player.Companies.Where(co => Rules.Fits(co, levy)).ToList())
        {
            int k = Math.Min(left, Rules.Room(co, levy));
            if (k <= 0) continue;
            Enlist(co, levy, k, from);
            left -= k;
            if (left == 0) break;
        }
        if (left > 0 && Player.Companies.Count < Slots)
        {
            var co = NewCompany(levy, "infantry", 0, 0, from);
            Player.Companies.Add(co);
            int k = Math.Min(left, Rules.Room(co, levy));
            Enlist(co, levy, k, from);
            left -= k;
        }
        return n - left;
    }
}
