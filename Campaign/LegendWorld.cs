using Microsoft.Xna.Framework;

namespace Ironfield;

/// Permanent world impact of past sealed Legends (docs/pre-demo/LEGENDS.md LG4): map monuments, discoverable Named Relics,
/// and folk memory in taverns and lord greetings. Snapshotted into `CampaignState.WorldLegends` at `Campaign.New` so saves
/// and replays remain 100% deterministic even if `legends.json` changes later.
public sealed partial class Campaign
{
    /// When the player arrives at a past Legend's monument, this holds the active visit for the Monument Storylet panel.
    public (Site Site, SealedLegend Legend)? ActiveMonument;
    /// Result text after choosing an action at a Legend Monument.
    public string MonumentOutcome = "";

    public static string MonumentName(SealedLegend leg) => leg.Fate switch
    {
        "Fallen" => $"Cairn of {leg.CaptainName}",
        "Crowned" => $"Pillar of {leg.CaptainName}",
        _ => $"{leg.CaptainName}'s Stand",
    };

    /// Called at the end of `Campaign.New`: snapshot up to 4 active past Legends into this campaign and place their monuments.
    internal void SeedWorldLegends()
    {
        var active = LegendArchive.Active().Take(4).ToList();
        if (active.Count == 0) return;
        S.WorldLegends = active.Select(l => new SealedLegend
        {
            Id = l.Id, CaptainName = l.CaptainName, Epithet = l.Epithet, CompanyName = l.CompanyName,
            Homeland = l.Homeland, Fate = l.Fate, Rank = l.Rank, Palette = l.Palette, Arms = l.Arms,
            Days = l.Days, Battles = l.Battles, Kills = l.Kills, AnchorPlace = l.AnchorPlace, AnchorPos = l.AnchorPos,
            Portrait = l.Portrait,
            Relic = l.Relic == null ? null : new LegendGear { Def = l.Relic.Def, Name = l.Relic.Name, From = l.Relic.From, Quality = l.Relic.Quality, Marks = new(l.Relic.Marks) },
            Verses = l.Verses.Select(v => new LegendVerse
            {
                Id = v.Id, Pillar = v.Pillar, Title = v.Title, Line = v.Line, Place = v.Place, Icon = v.Icon,
                Epithet = v.Epithet, WitnessName = v.WitnessName, Day = v.Day, Weight = v.Weight, WitnessSeed = v.WitnessSeed, Pos = v.Pos
            }).ToList(),
            ActiveInWorld = true, SealedAt = l.SealedAt,
        }).ToList();
        RestoreLegendMonuments();
    }

    /// Ensure every snapshotted `WorldLegends` entry has its permanent Monument site in `Map.Sites` (called on New and Load).
    internal void RestoreLegendMonuments()
    {
        if (S.WorldLegends.Count == 0) return;
        var existing = Map.Sites.Where(s => s.Kind == "monument").Select(s => s.LegendId).ToHashSet();
        foreach (var leg in S.WorldLegends)
        {
            if (existing.Contains(leg.Id)) continue;
            var pos = ClampMonumentPos(leg.AnchorPos, leg.Id);
            var prov = Map.ProvinceAt(pos);
            Map.Sites.Add(new Site
            {
                Id = Map.Sites.Count,
                Province = prov.Id,
                Kind = "monument",
                Name = MonumentName(leg),
                Pos = pos,
                LegendId = leg.Id,
            });
        }
    }

    Vector2 ClampMonumentPos(Vector2 wanted, string salt)
    {
        if (wanted != Vector2.Zero && Map.Landness(wanted) > 0.5f) return wanted;
        // Fallback: pick a deterministic non-water spot near a settlement so the monument is always reachable.
        if (S.Settlements.Count == 0) return Vector2.Zero;
        int idx = Math.Abs(Rng.Mix(S.Seed, salt.Length * 97 + (salt.Length > 0 ? salt[0] : 1))) % S.Settlements.Count;
        var s = S.Settlements[idx];
        var p = s.Pos + new Vector2(8, 6);
        return Map.Landness(p) <= 0.5f ? s.Pos + new Vector2(-8, -6) : p;
    }

    /// Resolve one of the 3 choices at a visited Legend Monument (0 = Pay respects, 1 = Swear by deed, 2 = Claim relic).
    public string ResolveMonumentChoice(int choice)
    {
        if (ActiveMonument is not var (site, leg)) return "";
        S.LegendMonumentsVisited.Add(leg.Id);
        S.SiteUntil[site.Id] = (float)S.Hours + 30 * 24;
        string msg;
        if (choice == 2 && leg.Relic != null && !S.LegendRelicsClaimed.Contains(leg.Id))
        {
            S.LegendRelicsClaimed.Add(leg.Id);
            var item = new GearItem
            {
                Id = S.NextItemId++,
                Def = leg.Relic.Def,
                Name = leg.Relic.Name,
                From = leg.Relic.From,
                Quality = ItemQuality.Named,
                Marks = new(leg.Relic.Marks),
            };
            Captain.Baggage.Add(item);
            msg = $"You uncover {item.Name} beneath the cairn stones and take it into your baggage.";
            LogYours($"Claimed the heirloom of {leg.FullName}: {item.Name}.");
            Deed($"Claimed {item.Name} at {site.Name}.", "legend");
        }
        else if (choice == 1)
        {
            Captain.Renown += 20;
            int got = GiftMen(3, SettlementNear(site.Pos));
            msg = got > 0
                ? $"You swear by the deeds of {leg.FullName}. (+20 renown, {got} local men take up arms with you.)"
                : $"You swear by the deeds of {leg.FullName}. Your men cheer and your renown rises (+20 renown).";
            LogYours($"Swore by the legend of {leg.FullName} at {site.Name}.");
        }
        else
        {
            AddBuff("shrine_blessing", 3);
            foreach (var c in Player.Companies) c.Cohesion = MathF.Min(30, c.Cohesion + 4);
            Captain.Renown += 10;
            msg = $"Your company stands in silence before {site.Name}. (+4 cohesion, blessed for 3 days, +10 renown.)";
            LogYours($"Paid respects at {site.Name}.");
        }
        MonumentOutcome = msg;
        return msg;
    }

    /// A bard's line for a town tavern (LG4): celebrates a past World Legend or your own Legend once Sung in Taverns+.
    public string TavernLegendLine(Settlement s)
    {
        if (S.WorldLegends.Count > 0)
        {
            var leg = S.WorldLegends[(s.Id + Day / 7) % S.WorldLegends.Count];
            var v = leg.Verses.OrderByDescending(x => x.Weight).FirstOrDefault();
            string deed = v != null ? v.Line : $"led {leg.CompanyName} for {leg.Days} days.";
            return $"A BARD SINGS OF {leg.FullName.ToUpperInvariant()}: \"{deed.ToUpperInvariant()}\"";
        }
        if (LegendWeight >= 5 && S.Verses.Count > 0)
        {
            var v = S.Verses[^1];
            return $"THE HEARTH SINGS YOUR OWN VERSE ({v.Title}): \"{v.Line.ToUpperInvariant()}\"";
        }
        return "";
    }

    /// Folk memory reaction for lord greetings (LG4): references your Legend Rank or an active past Legend.
    internal string LegendGreeting(Character ch)
    {
        if (ch.Kind != "lord") return "";
        if (LegendWeight >= 9 && LegendEpithet != "")
            return $"Your name runs ahead of your banner, {CaptainFullName}.";
        if (S.WorldLegends.Count > 0)
        {
            string facName = ch.Faction >= 0 && ch.Faction < FactionNames.Length ? FactionNames[ch.Faction] : "";
            var leg = S.WorldLegends.FirstOrDefault(l => l.Homeland.Equals(facName, StringComparison.OrdinalIgnoreCase)) ?? S.WorldLegends[0];
            if (Rng.Mix(ch.Id, Day / 14) % 3 == 0)
                return $"Men in these halls still speak of {leg.FullName}.";
        }
        return "";
    }
}
