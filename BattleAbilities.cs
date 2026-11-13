using Microsoft.Xna.Framework;

namespace Ironfield;

/// A spell or ability on its way (docs/archive/COMPANIONS_MAGIC.md E3): it fires at `FireAt` on the spot it glowed on, if its
/// caster still stands. The screen draws the glow (BattleScreen.Draw).
public sealed class Cast
{
    public AbilityDef A = null!;
    public Squad By = null!;
    public bool Hero;
    public Vector2 At;
    public Unit? On;
    public float Started, FireAt, Power;
}

/// A patch of ground under a spell (mist, smoke, a ward, a storm, burning ground) until `Until`.
public sealed class Field
{
    public AbilityDef A = null!;
    public EffectDef? Effect;
    public int Team;
    public Unit By = null!;
    public Vector2 Pos;
    public float Radius, Until, Power, Dps, Started;
}

/// Battle: casters and their spells (docs/archive/COMPANIONS_MAGIC.md E1-E5, B3). A company with spells (mage troops, or a
/// companion's abilities) thinks every 0.5 s: Will comes back, and the first spell that is ready, affordable and whose
/// rule finds a target starts a cast. Nobody presses a button: the player steers casters by where he puts them.
public partial class Battle
{
    public readonly List<Cast> Casts = new();
    public readonly List<Field> Fields = new();
    /// Some company on the field can cast (set in AddSquad): without one, nothing here runs.
    bool anyCaster;
    /// Omen the player's side raised with craft this battle (handed to the campaign, C2).
    public float OmenRaised;

    /// The company's spells: its casters' (the best tier's list), or its hero's abilities.
    void LearnSpells(Squad s, Company? company)
    {
        var ids = new List<(string id, bool hero)>();
        var best = s.Units.Where(u => u.Type.Spells.Length > 0).OrderByDescending(u => u.Type.Tier).FirstOrDefault();
        var book = company?.Spells is { Length: > 0 } prepared ? prepared.Where(Abilities.Has).ToArray() : best?.Type.Spells; // the spells you prepared (MA4)
        if (best != null && book != null) ids.AddRange(book.OrderByDescending(id => Abilities.Get(id).Great).Select(id => (id, false))); // a great working when it is time, before the rest
        s.CasterStart = s.Units.Count(u => u.Type.Spells.Length > 0);
        s.MasterRank = company?.MasterRank ?? 0;
        s.Will = WillCap(s);
        if (company?.Leaderless == true) s.GreatDone = true; // no great working without a master
        if (company is { HeroGifts.Length: > 0 } && s.Units.Any(u => u.Hero)) ids.AddRange(company.HeroGifts.Where(Abilities.Has).Select(id => (id, true)));
        if (ids.Count == 0) return;
        s.Spells = ids.Select(x => Abilities.Get(x.id)).ToArray();
        s.ByHero = ids.Select(x => x.hero).ToArray();
        s.SpellReady = s.Spells.Select(a => 3 + rng.NextSingle() * 4).ToArray(); // nobody casts in the first moments
        s.HeroLevel = company?.HeroLevel ?? 0;
        anyCaster = anyAura = true;
    }

    static bool IsCaster(Unit u) => u.Type.Spells.Length > 0;

    /// A circle's full Will: 100, and 10 more for each rank of its master (MA4).
    static float WillCap(Squad s) => 100 + 10 * s.MasterRank;
    /// What a spell costs this company: a master of rank 3 works the great working for 15 less.
    static float WillCost(Squad s, AbilityDef a) => a.Will - (a.Great && s.MasterRank >= 3 ? 15 : 0);
    public static bool CasterCompany(Squad s) => s.Units.Count > 0 && s.Units[0].Type.Class == UnitClass.Caster;

    /// How strong a company's spells land: a caster's tier (0.6 at II, 1 at IV, 1.4 at VI) and how many of them still stand;
    /// a hero's by his level.
    float SpellPower(Squad s, bool hero)
    {
        if (hero) return 1 + 0.05f * s.HeroLevel;
        int tier = 0, alive = 0;
        foreach (var u in s.Units) if (u.Active && !u.Routing && IsCaster(u)) { alive++; tier = Math.Max(tier, u.Type.Tier); }
        return alive == 0 ? 0 : (0.6f + 0.2f * (tier - 2)) * (0.5f + 0.5f * alive / (float)Math.Max(1, s.CasterStart));
    }

    /// The caster who speaks for the company now: its hero for his abilities, else its best standing caster.
    Unit? Speaker(Squad s, bool hero) => hero ? s.Units.FirstOrDefault(u => u.Hero && u.Active)
        : s.Units.Where(u => u.Active && !u.Routing && IsCaster(u)).OrderByDescending(u => u.Type.Tier).ThenBy(u => u.Index).FirstOrDefault();

    Cast AddCast(Cast c) { Casts.Add(c); return c; }

    /// The men who gather a cast raise their staffs for it (drawing only, Puppet.PoseOf).
    void MarkCast(Cast c)
    {
        foreach (var u in c.By.Units)
            if (u.Active && (c.Hero ? u.Hero : IsCaster(u))) { u.CastStart = c.Started; u.CastFire = c.FireAt; u.CastAim = c.At; }
    }

    void UpdateAbilities(float dt)
    {
        if (!anyCaster && Casts.Count == 0 && Fields.Count == 0) return;
        TideTick(dt);
        for (int i = 0; i < Squads.Count; i++)
            if (Squads[i].Spells.Length > 0 && (tick + i * 7) % 30 == 0) ThinkSpells(Squads[i], 0.5f);
        for (int i = Casts.Count - 1; i >= 0; i--)
            if (Time >= Casts[i].FireAt) { var c = Casts[i]; Casts.RemoveAt(i); Fire(c); }
        if (tick % 30 == 0) UpdateFields(0.5f);
    }

    /// Will comes back (twice as fast standing), then one cast if a spell is ready and its rule finds a target.
    void ThinkSpells(Squad s, float dt)
    {
        int casters = 0, tier = 0;
        bool still = true;
        foreach (var u in s.Units)
            if (u.Active && !u.Routing && IsCaster(u)) { casters++; tier = Math.Max(tier, u.Type.Tier); if (u.Vel.LengthSquared() > 1) still = false; }
        float cap = s.CasterStart == 0 ? 0 : WillCap(s) * casters / s.CasterStart; // it is the men who cast
        s.Will = MathF.Min(cap, s.Will + (0.5f + 0.5f * Math.Max(0, tier - 2)) * (still ? 2 : 1) * dt);
        if (Casts.Any(c => c.By == s)) return;
        bool engaged = casters > 0 && s.Units.Any(u => u.Active && IsCaster(u) && u.EnemyDist < 3); // a mage with a blade at him cannot work
        if (s.CastSpell >= 0 && OrderedCast(s, casters, engaged)) return;
        if (s.HoldSpells) return; // they keep their Will for your word
        for (int i = 0; i < s.Spells.Length; i++)
        {
            var a = s.Spells[i];
            bool hero = s.ByHero[i];
            if (Time < s.SpellReady[i] || (a.Great && s.GreatDone)) continue;
            if (hero ? Speaker(s, true) == null : casters == 0 || engaged || s.Will < WillCost(s, a) || s.Will < 20) continue; // under 20 Will a company is drained
            if (!hero && !TideHolds(a, WillCost(s, a)) && !(a.Great && s.Will >= WillCost(s, a) * 2)) continue; // the Tide is low: wait, but a great working may overreach (BattleTide.cs)
            if (Target(s, a, out var at, out var on) is false) continue;
            float power = SpellPower(s, hero);
            if (power <= 0) continue;
            if (!hero && !PayForCast(s, a, WillCost(s, a), a.Great)) continue;
            s.SpellReady[i] = Time + a.Cooldown;
            if (a.Great) s.GreatDone = true;
            MarkCast(AddCast(new Cast { A = a, By = s, Hero = hero, At = at, On = on, Started = Time, FireAt = Time + a.Warn, Power = power }));
            Flash("cast", Speaker(s, hero)?.Pos ?? s.Center, 1, s.Team, a.Tradition);
            if (s.Team == 0 && a.Tradition == "craft") OmenRaised += a.Omen;
            if (a.Great) Log(s.Team == 0 ? $"Your {s.Name} call down {a.Name}!" : $"The enemy's casters call down {a.Name}!");
            Sound(hero ? Cue.Shout : Cue.Spell, Speaker(s, hero)?.Pos ?? s.Center);
            return;
        }
    }

    // ---- casts you order (docs/archive/MAGIC_AURA.md 3.1) ----

    /// An ordered cast costs this much more Will (they break their rhythm for you), and the order lapses after this long.
    public const float OrderedWill = 5, OrderLasts = 30;

    /// The ordered spell i would have to overreach for (the spell fan's warning).
    public bool WouldOverreach(Squad s, int i) => i >= 0 && i < s.Spells.Length && !s.ByHero[i] && !TideHolds(s.Spells[i], WillCost(s, s.Spells[i]) + OrderedWill);

    /// Why the company cannot cast spell i now, or null (the HUD's spell fan; the spot is not known yet).
    public string? CannotCast(Squad s, int i)
    {
        if (i < 0 || i >= s.Spells.Length) return "NO SUCH SPELL";
        var a = s.Spells[i];
        bool hero = s.ByHero[i];
        if (Speaker(s, hero) == null) return hero ? "HE IS DOWN" : "THE CASTERS ARE DOWN";
        if (a.Great && s.GreatDone) return "SPENT";
        if (Time < s.SpellReady[i]) return $"IN {s.SpellReady[i] - Time:0}";
        if (!hero && (s.Will < WillCost(s, a) + OrderedWill || s.Will < 20)) return "NO WILL";
        if (!hero && !TideHolds(a, WillCost(s, a) + OrderedWill) && s.Will < (WillCost(s, a) + OrderedWill) * 2) return "NO TIDE";
        return null;
    }

    /// Cast the spell you ordered: walk into range, wait for it to be ready, then cast on your spot. True while the order
    /// stands (nothing else is cast meanwhile); false once it is given up.
    bool OrderedCast(Squad s, int casters, bool engaged)
    {
        int i = s.CastSpell;
        if (i >= s.Spells.Length || Time - s.CastSince > OrderLasts) { DropCast(s, "the working could not be done"); return false; }
        var a = s.Spells[i];
        bool hero = s.ByHero[i];
        if (a.Great && s.GreatDone) { DropCast(s, "that working is spent"); return false; }
        var me = Speaker(s, hero);
        if (me == null || Time < s.SpellReady[i]) return true;
        if (!hero && (casters == 0 || engaged || s.Will < WillCost(s, a) + OrderedWill || s.Will < 20)) return true;
        if (!hero && !TideHolds(a, WillCost(s, a) + OrderedWill) && s.Will < (WillCost(s, a) + OrderedWill) * 2) return true; // wait for the Tide, or the Will to overreach
        var at = a.Self ? s.Center : s.CastAt;
        if (!a.Self && Vector2.Distance(me.Pos, at) > a.Range)
        {
            float gap = Vector2.Distance(s.Anchor, at);
            if (gap > a.Range * 0.9f && !Engaged(s)) s.Shift(at, gap - a.Range * 0.8f); // close in once; the anchor then stays
            return true;
        }
        if (a.Needs != "" && !(a.Needs == "dry" ? Dry(at) : Raining(at))) { DropCast(s, a.Needs == "dry" ? $"{a.Name} needs dry ground" : $"{a.Name} needs rain"); return false; }
        Unit? on = null;
        if (a.When == "officer" || a.Kind == "officer")
        {
            on = Units.Where(u => u.Active && u.Team != s.Team && (u.Sergeant || u.Hero || u.IsLeader) && Vector2.DistanceSquared(u.Pos, at) < 64)
                .OrderBy(u => Vector2.DistanceSquared(u.Pos, at)).ThenBy(u => u.Index).FirstOrDefault();
            if (on == null) { DropCast(s, "no officer there"); return false; }
            at = on.Pos;
        }
        float power = SpellPower(s, hero);
        if (power <= 0) return true;
        if (!hero && !PayForCast(s, a, WillCost(s, a) + OrderedWill, true)) return true; // your word: they overreach if they must
        s.SpellReady[i] = Time + a.Cooldown;
        if (a.Great) s.GreatDone = true;
        s.CastSpell = -1;
        MarkCast(AddCast(new Cast { A = a, By = s, Hero = hero, At = at, On = on, Started = Time, FireAt = Time + a.Warn, Power = power }));
        Flash("cast", me.Pos, 1, s.Team, a.Tradition);
        if (s.Team == 0 && a.Tradition == "craft") OmenRaised += a.Omen;
        if (s.Team == 0) Log($"{s.Name}: {a.Name}!");
        Sound(hero ? Cue.Shout : Cue.Spell, me.Pos);
        return true;
    }

    void DropCast(Squad s, string why)
    {
        s.CastSpell = -1;
        if (s.Team == 0) Log($"{s.Name}: {why}.");
    }

    // ---- the rules (`when`) ----

    bool Target(Squad s, AbilityDef a, out Vector2 at, out Unit? on)
    {
        at = s.Center;
        on = null;
        var me = Speaker(s, false) ?? Speaker(s, true);
        var from = me?.Pos ?? s.Center;
        float range = a.Range;
        Squad? pick = null;
        switch (a.When)
        {
            case "self":
                if (!(Engaged(s) || s.Units.Any(u => u.Hero && u.Active && u.EnemyDist < 4))) return false;
                pick = s;
                break;
            case "closing":
                pick = Own(s, from, range, o => !Carries(o, a) && FoeGap(o) < 30);
                break;
            case "fighting":
                pick = Own(s, from, range, o => !Carries(o, a) && Engaged(o));
                break;
            case "wavering":
                pick = Own(s, from, range, o => !Carries(o, a) && AvgMorale(o) < 45);
                break;
            case "hurt":
                pick = Own(s, from, range, o => o.Units.Count(u => u.Active && u.Hp < u.Type.MaxHp * 0.6f) >= 3);
                break;
            case "archers":
                if (a.Kind == "buff") pick = Own(s, from, range, o => !Carries(o, a) && o.Units[0].Type.Ammo > 0 && o.Units.Any(u => u.Active && u.Ammo > 0) && FoeGap(o) < o.Units[0].Type.Range);
                else pick = Foe(s, from, range, e => e.Units[0].Type.Ammo > 0 && !Carries(e, a));
                break;
            case "shot":
                pick = Own(s, from, range, o => !Covered(o, a) && Squads.Any(e => e.Team != s.Team && e.Alive > 0 && e.Units[0].Type.Ammo > 0
                    && Time - e.LooseAt < 3 && Vector2.Distance(e.Center, o.Center) < e.Units[0].Type.Range + o.Radius));
                if (a.Kind == "field" && a.Tag == "undead" && pick == null)
                    pick = Own(s, from, range, o => !Covered(o, a) && Squads.Any(e => e.Team != s.Team && e.Alive > 0 && e.Units[0].Type.Breed == "undead" && Vector2.Distance(e.Center, o.Center) < 25));
                break;
            case "massed":
                pick = Foe(s, from, range, e => e.Alive >= 5 && !Carries(e, a) && !OwnNear(s.Team, e.Center, a.Radius + 3) && Ready(e, a));
                break;
            case "foes":
                pick = Foe(s, from, range, e => !Carries(e, a));
                break;
            case "horse":
                pick = Foe(s, from, range, e => e.Cavalry && e.Units.Any(u => u.Active && u.Speed > 4));
                break;
            case "undead":
                pick = Foe(s, from, range, e => e.Units[0].Type.Breed == "undead");
                break;
            case "officer":
                on = Units.Where(u => u.Active && u.Team != s.Team && (u.Sergeant || u.Hero || u.IsLeader) && Vector2.DistanceSquared(u.Pos, from) < range * range)
                    .OrderBy(u => Vector2.DistanceSquared(u.Pos, from)).ThenBy(u => u.Index).FirstOrDefault();
                if (on == null || (a.Kind == "debuff" && on.Squad != null && Carries(on.Squad, a))) return false;
                at = on.Pos;
                return true;
            case "routing":
                if (!Units.Any(u => u.Active && u.Routing && u.Team == s.Team && u.Routs <= 1 && Vector2.DistanceSquared(u.Pos, from) < a.Radius * a.Radius)) return false;
                at = from;
                return true;
            case "losing":
                if (!Losing(s.Team)) return false;
                if (a.Kind is "rally" or "buff") { at = centroid[s.Team]; return true; }
                pick = Foe(s, from, range, e => e.Alive >= 6 && !OwnNear(s.Team, e.Center, a.Radius + 3) && Ready(e, a));
                break;
        }
        if (pick == null) return false;
        at = pick.Center;
        return true;
    }

    /// The nearest company of the caster's side within range that meets the rule (the caster's own counts).
    Squad? Own(Squad s, Vector2 from, float range, Func<Squad, bool> ok)
    {
        Squad? best = null;
        float bd = range * range;
        foreach (var o in Squads)
        {
            if (o.Team != s.Team || o.Alive == 0) continue;
            float d = Vector2.DistanceSquared(o.Center, from);
            if (d <= bd && ok(o)) { bd = d; best = o; }
        }
        return best;
    }

    /// The fullest enemy company within range that meets the rule (most men under the glow).
    Squad? Foe(Squad s, Vector2 from, float range, Func<Squad, bool> ok)
    {
        Squad? best = null;
        int most = 0;
        foreach (var e in Squads)
        {
            if (e.Team == s.Team || e.Alive == 0 || Vector2.DistanceSquared(e.Center, from) > range * range || !ok(e)) continue;
            if (e.Alive > most) { most = e.Alive; best = e; }
        }
        return best;
    }

    /// Gap from a company's centre to the nearest enemy company's edge.
    float FoeGap(Squad s)
    {
        float best = float.MaxValue;
        foreach (var e in Squads)
            if (e.Team != s.Team && e.Alive > 0) best = MathF.Min(best, Vector2.Distance(e.Center, s.Center) - e.Radius - s.Radius);
        return best;
    }

    bool Engaged(Squad s) => s.Alive > 0 && FoeGap(s) < 3;
    bool Carries(Squad s, AbilityDef a) => a.Effect != "" && s.Timed.Any(t => t.Effect.Id == a.Effect && t.Until > Time + 2);
    bool Covered(Squad s, AbilityDef a) => Fields.Any(f => f.A == a && f.Team == s.Team && Vector2.Distance(f.Pos, s.Center) < f.Radius);
    bool OwnNear(int team, Vector2 p, float r) => Squads.Any(o => o.Team == team && o.Alive > 0 && Vector2.Distance(o.Center, p) < r + o.Radius);

    /// The side is losing: fewer men standing than the foe by a sixth, or two companies of it wavering.
    bool Losing(int team)
    {
        int mine = 0, theirs = 0, wavering = 0;
        foreach (var o in Squads)
        {
            if (o.Team == team) { mine += o.Alive; if (o.Alive > 0 && AvgMorale(o) < 40) wavering++; }
            else theirs += o.Alive;
        }
        return mine < theirs * 0.85f || wavering >= 2;
    }

    /// Ground and weather allow it here: wildfire needs dry burnable ground and no rain, lightning rain or a storm laid on it.
    bool Ready(Squad e, AbilityDef a) => a.Needs switch
    {
        "dry" => Dry(e.Center),
        "rain" => Raining(e.Center),
        _ => true,
    };

    internal bool Dry(Vector2 p) => Weather.Rain < 0.3f && Terrain.GroundAt(p) is GroundKind.Grass or GroundKind.Crops or GroundKind.Scrub or GroundKind.Wood or GroundKind.DenseWood or GroundKind.Stony
        && !Fields.Any(f => f.Effect?.Id == "storm" && Vector2.Distance(f.Pos, p) < f.Radius);
    bool Raining(Vector2 p) => Weather.Rain >= 0.3f || Fields.Any(f => f.Effect?.Id == "storm" && Vector2.Distance(f.Pos, p) < f.Radius);

    // ---- what a cast does when it lands ----

    void Fire(Cast c)
    {
        var a = c.A;
        var by = Speaker(c.By, c.Hero);
        if (by == null) return; // the caster fell: the working dies with him
        if (a.Needs != "" && !(a.Needs == "dry" ? Dry(c.At) : Raining(c.At))) return;
        int team = c.By.Team;
        if (Warded(c.At, team)) { Flash("broken", c.At, a.Radius, team, a.Tradition); Sound(Cue.WardShatter, c.At); return; } // a ward breaks the working (MA8)
        Flash("spell:" + a.Kind, c.At, a.Radius, team, a.Tradition);
        Flash("work:" + a.Id, c.At, a.Radius, team, a.Tradition, by.Pos - c.At); // which working, and where from: the screen's spectacle reads it
        if (a.Great) Wonder(team == 0 ? a.Name + " was worked for us." : "The foe worked " + a.Name + ".");
        if (a.Tradition == "faith" && (a.Kind is "attack" or "rally" || a.Great)) Sound(Cue.Holy, c.At);
        else if (a.Tradition == "necromancy") Sound(Cue.CastNecro, c.At);
        else if (a.Id == "crows") Sound(Cue.CrowSwarm, c.At);
        else if (a.Id is "evil_eye" or "fever" or "dread_drums") Sound(Cue.CastHex, c.At);
        else if (a.Id is "storm" or "fair_wind" or "mist" or "tempest" or "war_song" or "foresight" or "mirage") Sound(Cue.CastGaldr, c.At);
        if (a.Effect == "burning" || a.Burn > 0) Sound(Cue.Flame, c.At);
        switch (a.Kind)
        {
            case "buff":
                if (a.Self) AddTimed(c.By, a, c.Power);
                else foreach (var o in Squads) if (o.Team == team && o.Alive > 0 && Vector2.Distance(o.Center, c.At) < a.Radius + o.Radius) AddTimed(o, a, c.Power);
                FeedOnBuff(a, a.Self ? c.By.Center : c.At, team);
                break;
            case "debuff":
                foreach (var e in Squads) if (e.Team != team && e.Alive > 0 && Vector2.Distance(e.Center, c.At) < a.Radius + e.Radius && !InLight(e)) AddTimed(e, a, c.Power); // not in a Transcendent's light (MA7)
                CurseCaptain(a, c.At, team);
                break;
            case "field":
                AddField(a, a.Effect, team, by, a.Effect == "mirage" ? MirageSpot(c.At, team) : c.At, a.Radius, a.Duration, c.Power, a.Dps);
                break;
            case "attack":
                foreach (var t in Near(c.At, a.Radius + 0.5f, nearKill).Where(t => t.Active && t.Team != team && Vector2.DistanceSquared(t.Pos, c.At) < a.Radius * a.Radius)
                             .OrderBy(t => Vector2.DistanceSquared(t.Pos, c.At)).ThenBy(t => t.Index).Take(a.MaxHit).ToList())
                    Blast(by, t, a, c.Power);
                if (a.Burn > 0) AddField(a, "burning", team, by, c.At, a.Radius, a.Burn, c.Power, a.Dps);
                Sound(a.Id == "fire_pots" ? Cue.FirePotShatter : Cue.Impact, c.At);
                break;
            case "bolt":
                // Lightning finds the tallest: a rider, then an officer, then the man nearest the glow.
                var bolt = Near(c.At, a.Radius + 0.5f, nearKill).Where(t => t.Active && t.Team != team && Vector2.DistanceSquared(t.Pos, c.At) < a.Radius * a.Radius)
                    .OrderByDescending(t => t.Mounted).ThenByDescending(t => t.Sergeant || t.Hero || t.IsLeader).ThenBy(t => Vector2.DistanceSquared(t.Pos, c.At)).ThenBy(t => t.Index).FirstOrDefault();
                if (bolt != null) { Blast(by, bolt, a, c.Power); if (bolt.Mounted) HitHorse(bolt, a.Damage * c.Power * 0.5f, by); }
                Flash("bolt", bolt?.Pos ?? c.At, 1, team, a.Tradition);
                Sound(Cue.Thunder, c.At);
                break;
            case "heal":
                foreach (var u in Units)
                    if (u.Active && u.Team == team && Vector2.DistanceSquared(u.Pos, c.At) < a.Radius * a.Radius) u.Hp = MathF.Min(u.Type.MaxHp, u.Hp + a.Heal * c.Power);
                break;
            case "rally":
                foreach (var u in Units)
                    if (u.Active && u.Team == team && u.Routing && (a.Great || u.Routs <= 1) && Vector2.DistanceSquared(u.Pos, c.At) < a.Radius * a.Radius)
                    { u.Routing = false; u.Morale = MathF.Max(u.Morale, RallyAt + 12); }
                if (a.Effect != "") foreach (var o in Squads) if (o.Team == team && o.Units.Count > 0 && Vector2.Distance(o.Center, c.At) < a.Radius + o.Radius) AddTimed(o, a, c.Power);
                if (team == 0 && c.Hero) Log($"{c.By.Name}: \"Rally to me!\"");
                break;
            case "officer":
                if (c.On is not { Active: true } foe || Vector2.Distance(foe.Pos, by.Pos) > a.Range + 5) return;
                Damage(by, foe, a.Damage * c.Power, a.Shock);
                if (foe.Dead && foe.Squad != null) foreach (var u in foe.Squad.Units) if (u.Active) u.Morale -= a.Shock;
                if (team == 0 && foe.Dead) Log($"{c.By.Name}'s champion cuts down an enemy officer!");
                break;
        }
    }

    /// An attack spell's blow on one man: through his armour as a blade would, harder on its tag (the dead, for Smite).
    void Blast(Unit by, Unit t, AbilityDef a, float power)
    {
        float dmg = a.Damage * power * (a.Tag != "" && t.Type.Breed == a.Tag ? a.VsTag : 1);
        Damage(by, t, dmg, a.Shock);
    }

    /// A timed effect on a company, refreshed if it already carries it; its totals change at once.
    internal void AddTimed(Squad s, AbilityDef a, float power)
    {
        if (a.Effect == "") return;
        var e = Effects.Get(a.Effect);
        s.Timed.RemoveAll(t => t.Effect == e);
        s.Timed.Add(new TimedEffect(e, MathF.Min(power, 1.5f), Time + a.Duration, a.Id));
        anyAura = true;
        RefreshMods(s);
    }

    internal void AddField(AbilityDef a, string effect, int team, Unit by, Vector2 at, float radius, float duration, float power, float dps)
    {
        Fields.Add(new Field { A = a, Effect = effect == "" ? null : Effects.Get(effect), Team = team, By = by, Pos = at, Radius = radius, Until = Time + duration, Power = power, Dps = dps, Started = Time });
        anyAura = true;
        foreach (var s in Squads) if (Vector2.Distance(s.Center, at) < radius + s.Radius) RefreshMods(s);
    }

    /// Fields end, and burn the men in them (a tagged field burns only its tag: a ward, the dead).
    void UpdateFields(float dt)
    {
        for (int i = Fields.Count - 1; i >= 0; i--)
        {
            var f = Fields[i];
            if (Time >= f.Until) { Fields.RemoveAt(i); continue; }
            if (f.Dps <= 0) continue;
            foreach (var u in Near(f.Pos, f.Radius + 0.5f, nearKill).ToList())
            {
                if (!u.Active || Vector2.DistanceSquared(u.Pos, f.Pos) > f.Radius * f.Radius) continue;
                if (f.A.Tag != "" ? u.Type.Breed != f.A.Tag || u.Team == f.Team : f.A.On == "foes" && u.Team == f.Team) continue;
                u.Hp -= f.Dps * f.Power * dt;
                u.HurtAt = Time;
                u.Morale -= f.Dps * 0.2f * dt;
                if (u.Hp <= 0) Kill(u, f.By);
            }
        }
    }

    /// The fields over a company (its centre inside, to whom the field speaks).
    Mods FieldsOn(Squad s)
    {
        var m = new Mods();
        foreach (var f in Fields)
        {
            if (f.Effect == null || Vector2.Distance(f.Pos, s.Center) > f.Radius + s.Radius * 0.5f) continue;
            bool own = s.Team == f.Team;
            if (f.A.On == "all" || (f.A.On == "own") == own) m.Add(f.Effect, MathF.Min(f.Power, 1.5f));
        }
        return m;
    }

    Mods TimedOn(Squad s)
    {
        var m = new Mods();
        s.Timed.RemoveAll(t => t.Until <= Time);
        foreach (var t in s.Timed) m.Add(t.Effect, t.K);
        return m;
    }
}
