using Microsoft.Xna.Framework;

namespace Ironfield;

public static partial class SelfTest
{
    /// Legends system (docs/pre-demo/LEGENDS.md LG1-LG4): Verse recording, Epithet & Rank progression, early Retire & Seal,
    /// persistent legends.json round-trip, and permanent world impact (Monuments, Named Relics & Folk Memory) in a new campaign.
    static void LegendsSystem()
    {
        string? prevDir = Environment.GetEnvironmentVariable("IRONFIELD_SAVES");
        string tmp = Path.Combine(Path.GetTempPath(), "ironfield-legends-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tmp);
        try
        {
            Environment.SetEnvironmentVariable("IRONFIELD_SAVES", tmp);
            LegendArchive.Forget();

            var c = Campaign.New(42);
            Check(c.S.Verses.Count == 0 && c.LegendRank == "NAMELESS", "new campaign starts with 0 verses and NAMELESS rank");
            Check(c.CannotSealLegend() != null, "cannot seal legend before 2 verses");
            Check(c.UnwrittenVerses(3).Count == 3, "unwritten verses prompt untouched pillars");

            // Earn 3 verses across distinct pillars
            Check(c.RecordVerse("first_blood", "Ashby Ford", c.Player.Pos, "Won a notable victory at Ashby Ford."), "records first_blood");
            Check(!c.RecordVerse("first_blood", "Ashby Ford", c.Player.Pos), "duplicate verse id is ignored");
            Check(c.CannotSealLegend() != null, "1 verse is still below MinSealVerses");

            Check(c.RecordVerse("apex_slayer", "The Highlands", c.Player.Pos + new Vector2(12, 4), "Slew the legendary Ash-Wing in the Highlands."), "records apex_slayer");
            Check(c.RecordVerse("wall_breaker", "Lowmoor", c.Player.Pos + new Vector2(-10, 8), "Stormed the walls of Lowmoor."), "records wall_breaker");
            Check(c.LegendWeight == 1 + 4 + 3, $"legend weight sums verse weights ({c.LegendWeight})");
            Check(c.LegendEpithet == "Wyrmslayer", $"epithet comes from highest-weight verse ({c.LegendEpithet})");
            Check(c.LegendRank == "SUNG IN TAVERNS", $"legend rank updates from weight ({c.LegendRank})");
            Check(c.CannotSealLegend() == null, "can retire & seal once >= 2 verses earned");

            // Retire & Seal into legends.json
            string? err = c.SealAndRetire();
            Check(err == null && c.S.Retired && c.S.GameOver && c.S.LegendSealed, $"SealAndRetire succeeds ({err})");

            // Verify persistence across cache reset
            LegendArchive.Forget();
            var loaded = LegendArchive.Load();
            Check(loaded.Count == 1, $"legends.json holds 1 sealed legend ({loaded.Count})");
            var leg = loaded[0];
            Check(leg.Verses.Count == 3 && leg.Epithet == "Wyrmslayer" && leg.Relic != null, "sealed legend keeps verses, epithet and signature relic");

            // Start a brand-new campaign and verify Permanent World Impact (LG4)
            var next = Campaign.New(99);
            Check(next.S.WorldLegends.Count == 1, "new campaign snapshots active WorldLegends");
            var mon = next.Sites.FirstOrDefault(s => s.Kind == "monument" && s.LegendId == leg.Id);
            Check(mon != null, "past legend spawns a permanent Monument site on the new map");

            // Visit the Monument and claim the past Legend's Named Relic
            next.ArriveAtSite(mon!);
            Check(next.ActiveMonument != null, "arriving at monument opens monument storylet");
            int bagBefore = next.Captain.Baggage.Count;
            string outcome = next.ResolveMonumentChoice(2);
            Check(outcome.Contains(leg.Relic!.Name), $"claiming relic reports heirloom name ({outcome})");
            Check(next.Captain.Baggage.Count == bagBefore + 1, "relic added to captain baggage");
            Check(next.Captain.Baggage[^1].Quality == ItemQuality.Named, "claimed relic has Named quality");

            // Folk memory in tavern
            string bard = next.TavernLegendLine(next.S.Settlements[0]);
            Check(bard.Contains(leg.CaptainName.ToUpperInvariant()), $"tavern bard sings of past legend ({bard})");

            // Archiving (ActiveInWorld = false) excludes it from future worlds
            LegendArchive.SetActive(leg.Id, false);
            var quiet = Campaign.New(101);
            Check(quiet.S.WorldLegends.Count == 0, "inactive legend is excluded from new campaigns");
        }
        finally
        {
            Environment.SetEnvironmentVariable("IRONFIELD_SAVES", prevDir);
            LegendArchive.Forget();
            try { Directory.Delete(tmp, true); } catch (Exception) { }
        }
    }
}
