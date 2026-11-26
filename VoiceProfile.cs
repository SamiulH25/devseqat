namespace Ironfield;

/// Delivery mood for acoustic instrument speech (docs/VOICE_DIALOGUE.md).
public enum VoiceMood { Even, Warm, Hostile, Command, Weary, Grudge }

/// Deterministic acoustic speech identity for a character, soldier or sergeant.
/// Pure data + math (no MonoGame graphics), so headless tests and --sounds use it directly.
public readonly record struct VoiceProfile(string Culture, string Instrument, int SemitoneOffset, float TempoMul, int Seed)
{
    public static string NormalizeCulture(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture)) return "azure";
        string c = culture.ToLowerInvariant();
        if (c.StartsWith("northern") || c.Contains("skar") || c.Contains("fjord") || c.Contains("holt")) return "northern";
        if (c.StartsWith("crimson") || c.Contains("vespra") || c.Contains("crag")) return "crimson";
        if (c.StartsWith("southern") || c.Contains("qadir") || c.Contains("rimal") || c.Contains("maren")) return "southern";
        if (c.StartsWith("steppe") || c.Contains("naryn") || c.Contains("horde")) return "steppe";
        if (c.StartsWith("league") || c.Contains("brine")) return "league";
        if (c.StartsWith("order") || c.Contains("tarn")) return "order";
        return "azure";
    }

    public static (int RootMidi, int[] Scale) RegionScale(string culture) => NormalizeCulture(culture) switch
    {
        "northern" => (50, new[] { 0, 2, 3, 5, 7, 9, 10, 12 }),   // D Dorian
        "crimson"  => (57, new[] { 0, 2, 3, 5, 7, 8, 11, 12 }),   // A Harmonic Minor
        "southern" => (53, new[] { 0, 1, 4, 5, 7, 8, 10, 12 }),   // F Hijaz
        "steppe"   => (50, new[] { 0, 2, 5, 7, 9, 12, 14 }),      // D Pentatonic
        "league"   => (55, new[] { 0, 2, 4, 7, 9, 11, 12 }),      // G Folk
        "order"    => (48, new[] { 0, 1, 3, 5, 7, 8, 10, 12 }),   // C Phrygian
        _          => (52, new[] { 0, 2, 4, 5, 7, 9, 10, 12 }),   // E Mixolydian (Brevon / common)
    };

    /// Pick an acoustic instrument by region, role/kind and temper/nature.
    public static string PickInstrument(string culture, string kind, string nature, bool shout = false)
    {
        string c = NormalizeCulture(culture);
        if (shout)
        {
            return c switch
            {
                "northern" => "war_horn",
                "steppe"   => "morin_khuur",
                "southern" => "english_horn",
                "order"    => "low_brass",
                _          => "french_horn",
            };
        }

        // Nature / archetype overrides keep characters distinct inside the same realm
        if (nature is "Grim" or "Cruel" or "Hard")
            return c is "northern" or "steppe" ? "trombone_muted" : "bassoon";
        if (nature is "Sly" or "Cunning" or "Merry")
            return c == "southern" ? "oud" : "clarinet";
        if (nature is "Pious" or "Devout")
            return "voice_ooh";
        if (kind == "sergeant")
            return c is "northern" ? "trombone_muted" : "bassoon";
        if (kind == "knight")
            return "cello_solo";

        return c switch
        {
            "northern" => kind == "lord" ? "nyckelharpa" : "trombone_muted",
            "crimson"  => kind == "lord" ? "cello_solo" : "viols",
            "southern" => kind == "lord" ? "english_horn" : "oud",
            "steppe"   => kind == "lord" ? "morin_khuur" : "steppe_reed",
            "league"   => kind == "lord" ? "clarinet" : "cittern",
            "order"    => kind == "lord" ? "choir_aah" : "voice_ooh",
            _          => kind == "lord" ? "cello_solo" : "lute",
        };
    }

    public static VoiceProfile ForCharacter(Character ch, string? fallbackCulture = null)
    {
        string c = NormalizeCulture(string.IsNullOrWhiteSpace(ch.Culture) || ch.Culture == "common" ? fallbackCulture : ch.Culture);
        string trait = ch.Traits.Count > 0 ? ch.Traits[0] : "";
        string inst = PickInstrument(c, ch.Kind, trait);
        int h = Rng.Mix(ch.Id, Rng.Of(ch.Name), 71);
        int ageShift = ch.Age >= 52 ? -2 : ch.Age <= 23 ? 2 : 0;
        int semi = ((h & 7) % 7) - 3 + ageShift;
        float tempo = 0.92f + ((h >> 4) & 15) * (0.16f / 15f);
        if (ch.Age >= 55) tempo *= 0.92f;
        return new VoiceProfile(c, inst, Math.Clamp(semi, -5, 5), tempo, h);
    }

    public static VoiceProfile ForSoldier(int soldierId, string culture, string nature, bool sergeant = false, bool shout = false)
    {
        string c = NormalizeCulture(culture);
        string inst = PickInstrument(c, sergeant ? "sergeant" : "soldier", nature, shout);
        int h = Rng.Mix(soldierId, Rng.Of(nature), sergeant ? 131 : 47);
        int semi = ((h & 7) % 7) - 3;
        float tempo = 0.95f + ((h >> 4) & 15) * (0.14f / 15f);
        return new VoiceProfile(c, inst, semi, tempo, h);
    }

    public static VoiceProfile ForRegion(string culture, string? instrumentOverride = null, int seed = 1)
    {
        string c = NormalizeCulture(culture);
        string inst = string.IsNullOrEmpty(instrumentOverride) ? PickInstrument(c, "lord", "") : instrumentOverride;
        return new VoiceProfile(c, inst, 0, 1f, seed);
    }
}
