using Microsoft.Xna.Framework;

namespace Ironfield;

/// Roadmap C3 (captain combat: timed parries, chambering, shield bash, a second weapon, calling your horse),
/// C7 (fatigue, ammunition wagons) and parts of C2 (a reserve, focused archers, a general who knows when to quit).
public sealed partial class Battle
{
    public const float ParryWindow = 0.35f;
    /// Battle time until which a swing you start is a riposte (after a parry).
    public float RiposteUntil = -1;
    public bool Riposting => Time < RiposteUntil;
    /// Swings started hard on the heels of the last (within ChainGap of its recovery): each costs more stamina, and the
    /// enemy, reading the rhythm, blocks them more often. A pause breaks the chain.
    public int Chain;
    public const float ChainGap = 0.4f, RiposteTime = 0.45f;
    float lastRecovered = -9;
    bool riposteSwing;
    /// How far your Arms skill has come past a new captain's 15, 0-1.
    public float ArmsEdge => Math.Clamp((Options.Arms - 15) / 85f, 0, 1);
    /// Riding and Bowcraft past a new captain's 5, 0-1 (P3).
    public float RidingEdge => Math.Clamp((Options.Riding - 5) / 95f, 0, 1);
    public float BowEdge => Math.Clamp((Options.Bowcraft - 5) / 95f, 0, 1);
    bool Perk(string id) => Options.Perks.Contains(id);
    bool secondWind;
    float ParryTime => ParryWindow + 0.1f * ArmsEdge - (Options.Wounds.Contains("eye") ? 0.05f : 0) + (Countering ? 0.25f : 0); // Measured Blade (K2)

    /// You release a swing (P2): its stamina cost, power and windup from the chain, a riposte, tiredness and Arms.
    void ReleaseSwing(Unit p)
    {
        Chain = Time - lastRecovered < ChainGap ? Chain + 1 : 0;
        riposteSwing = Riposting;
        RiposteUntil = -1;
        float cost = (8 + 5 * Chain) * (1 - 0.25f * ArmsEdge) * (riposteSwing ? 0.5f : 1);
        bool tired = p.Stamina < cost;
        p.Stamina = MathF.Max(0, p.Stamina - cost);
        p.Power = (0.6f + p.Charge) * p.Swing switch { AttackDir.Overhead => 1.2f, AttackDir.Thrust => 1f, _ => 0.95f }
            * (tired ? 0.6f : 1) * (riposteSwing ? 1.25f : 1) * FocusEdge * SpentEdge; // Focus (K2); spent, weaker (MA3)
        p.Charge = 0;
        p.Action = UnitAction.Windup;
        p.ActionTimer = (p.Swing == AttackDir.Overhead ? 0.2f : 0.15f) * p.Type.Windup / 0.4f // your weapon sets your pace (P1)
            * (tired ? 1.4f : 1) * (riposteSwing ? 0.5f : 1) * (1 - 0.15f * ArmsEdge);
        if (tired) PlayerHit = ("TOO TIRED: A WEAK BLOW", Time);
        var grip = Look.GripOf(p.Type.Look.Weapon);
        Sound(riposteSwing ? Cue.RiposteRing : grip == Grip.Two ? Cue.SwingTwo : grip is Grip.Pole or Grip.Lance ? Cue.SwingPole : Cue.SwingOne, p.Pos, priority: true);
    }
    /// The supply wagon behind each side's lines, and the arrows left in it.
    public readonly Vector2[] Wagons = { new(-8, -52), new(8, 52) };
    public readonly int[] WagonStock = { 300, 300 };
    LooseHorse? called;
    UnitType? primary, secondary;

    /// Your hands are both on the weapon (a greatsword, an axe or a bow): no shield, so only parries stop a blow cleanly.
    public bool TwoHanded => !Player.Type.HasShield;
    /// Arrows left for the weapon not in hand (each weapon keeps its own).
    int sideAmmo;

    void SetupCaptain()
    {
        primary = Player.Type;
        if (Options.PlayerSide != null) secondary = Options.PlayerSide;
        else if (Options.PlayerType == null)
        {
            // A quick battle's captain carries the old pair: sword and shield, and a greatsword.
            secondary = primary.Clone();
            secondary.Look = primary.Look with { Weapon = "greatsword", Shield = "none" };
            secondary.Damage *= 1.35f;
            secondary.Reach += 0.5f;
        }
        sideAmmo = secondary?.Ammo ?? 0;
        if (!Terrain.IsFlat) Terrain.AddProp(new Foliage(FoliageKind.Wagon, Wagons[0], 1, 1.4f, 1));
        if (!Terrain.IsFlat) Terrain.AddProp(new Foliage(FoliageKind.Wagon, Wagons[1], 1, 1.4f, 2));
    }

    void SwapWeapon(Unit p)
    {
        if (secondary == null) { Log("You carry no second weapon."); return; }
        var to = p.Type == secondary ? primary! : secondary;
        if (p.Mounted && !to.HasShield && to.Ammo == 0) { Log("Two hands for that: dismount first."); return; }
        if (p.Action != UnitAction.None || p.Charging) return;
        p.Type = to;
        (p.Ammo, sideAmmo) = (sideAmmo, p.Ammo);
        p.Action = UnitAction.Stagger; // a moment to change grip
        p.ActionTimer = 0.4f;
        Sound(Cue.WeaponSwap, p.Pos, priority: true);
        Log(to.Ammo > 0 ? $"You take up the {WeaponName(to)}. {p.Ammo} shots left." : $"You take up the {WeaponName(to)}.");
    }

    /// The weapon you hold now and whether a second one waits (for the HUD).
    public string PlayerWeapon => WeaponName(Player.Type);
    public bool HasSecondWeapon => secondary != null;

    static string WeaponName(UnitType t) => t.Look.Weapon switch
    {
        "sword" => t.HasShield ? "sword and shield" : "sword", "daneaxe" => "dane axe", "club" => "mace",
        var w => t.HasShield ? w + " and shield" : w,
    };

    /// Your bow (P1): hold attack to draw (the aim steadies), release to loose at the foe nearest your facing.
    void PlayerBow(Unit p, PlayerInput inp, float dt)
    {
        float draw = p.Type.Windup * 1.75f / p.Type.Reload // a crossbow is spanned already: it aims quickly, then reloads slowly
            * (1 - 0.25f * BowEdge);
        if (inp.Attack)
        {
            if (p.Ammo <= 0) { if (!p.Charging) PlayerHit = ("NO ARROWS: SWAP WEAPON OR GO TO THE WAGON", Time); p.Charging = true; return; }
            if (p.Cooldown > 0) return;
            p.Charging = true;
            p.Charge = MathF.Min(1, p.Charge + dt / draw);
            return;
        }
        if (!p.Charging) return;
        p.Charging = false;
        float charge = p.Charge;
        p.Charge = 0;
        if (p.Ammo <= 0 || charge < 0.3f) return; // let down without loosing
        var t = AimTarget(p);
        var aim = t?.Pos ?? p.Pos + p.Dir * p.Type.Range * 0.8f;
        Loose(p, aim, t, p.Type.Damage * (0.5f + 0.5f * charge), (1.6f - 1.1f * charge) * (1 - 0.3f * BowEdge) * (Perk("SteadyAim") ? 0.7f : 1) * (Perk("Deadeye") && charge >= 0.95f ? 0.15f : 1));
        p.Cooldown = 0.3f + (p.Type.Reload - 1) * 1.2f;
    }

    /// The enemy you are aiming at: in range and within a narrow cone of your facing, the closest to its centre line.
    Unit? AimTarget(Unit p)
    {
        Unit? best = null;
        float bs = float.MaxValue, range = MathF.Min(Weather.Vision, p.Type.Range);
        foreach (var t in Units)
        {
            if (t.Team == p.Team || !t.Active) continue;
            var d = t.Pos - p.Pos;
            float l = d.Length();
            if (l < 2 || l > range) continue;
            float off = MathF.Acos(Math.Clamp(Vector2.Dot(d / l, p.Dir), -1, 1));
            if (off > 0.2f) continue;
            float score = off * l + l * 0.02f;
            if (score < bs) { bs = score; best = t; }
        }
        return best;
    }

    /// Shield bash (or a kick, with two hands): staggers a man in front of you, through his guard.
    void Bash(Unit p)
    {
        if (p.Action != UnitAction.None || p.Charging || p.Mounted || p.Stamina < 15) return;
        p.Stamina -= 15;
        p.Action = UnitAction.Recover;
        p.ActionTimer = 0.35f;
        Unit? best = null;
        float bd = 1.9f;
        foreach (var t in Units)
        {
            if (t.Team == p.Team || !t.Active || t.Mounted) continue;
            var d = t.Pos - p.Pos;
            float l = d.Length();
            if (l < bd && (l < 0.01f || Vector2.Dot(d / l, p.Dir) > 0.5f)) { bd = l; best = t; }
        }
        if (best == null) { PlayerHit = ("BASH: NOTHING THERE", Time); return; }
        Stagger(best, 1.0f);
        best.Pos += Vector2.Normalize(best.Pos - p.Pos + new Vector2(1e-4f, 0)) * 1.2f;
        best.Morale -= 3;
        Damage(p, best, 6, 2);
        Sound(Cue.BashThud, best.Pos, priority: true);
        PlayerHit = (TwoHanded ? "KICK!" : "SHIELD BASH!", Time);
    }

    /// Whistle for a riderless horse of your side: it trots to you.
    bool CallHorse(Unit p)
    {
        called = LooseHorses.Where(h => h.Team == p.Team && Vector2.Distance(h.Pos, p.Pos) < 80).OrderBy(h => Vector2.Distance(h.Pos, p.Pos)).FirstOrDefault();
        if (called == null) return false;
        Sound(Cue.HorseWhistle, p.Pos, priority: true);
        Log("You whistle for a horse.");
        return true;
    }

    void UpdateCalledHorse(float dt)
    {
        if (called == null) return;
        if (!LooseHorses.Contains(called) || Player.Mounted || !Player.Active) { called = null; return; }
        var d = Player.Pos - called.Pos;
        float l = d.Length();
        if (l < 1.8f) { called = null; return; }
        called.Pos += d / l * MathF.Min(l, 7 * dt);
        called.Heading = MathF.Atan2(d.Y, d.X);
    }

    /// Men tire running and fighting (slower, weaker) and recover standing still. Horses and you are exempt (you have stamina).
    /// Pace and blow strength from stamina (and, for a troll, the sun).
    float Fatigue(Unit u) => u.IsPlayer ? 1 : (0.7f + 0.3f * u.Stamina / 100) * (u.Type.Has("daylight") && Sunlit ? 0.65f : 1);

    void Tire(Unit u, float dt)
    {
        if (u.IsPlayer || u.Mounted) return;
        // Effort, not ground covered: a man slogging through mud at a run is running.
        float run = u.Vel.Length() / MathF.Max(0.1f, u.Type.Speed * u.GroundPace);
        float rate = run > 0.7f ? -2.5f * run : u.Action != UnitAction.None ? -1f : run < 0.2f ? 4f : 1f;
        float extra = run < 0.2f ? 0 : Strain(u);
        rate = rate < 0 ? rate * (1 + extra) * MathF.Max(0, 1 + u.Mods.Tire) : rate - 2.5f * run * extra; // hard ground and climbs tire a man on the move; tireless men less
        u.Stamina = Math.Clamp(u.Stamina + rate * dt, 0, 100);
    }

    /// How much more than usual moving here tires a man: heavy ground (Data/ground.json `fatigue`) and climbing,
    /// both worse in heavy armour. 0 on level grass.
    float Strain(Unit u)
    {
        float weight = 1 + MathF.Max(0, u.Type.Armor - 8) / 20;
        return MathF.Max(0, Terrain.GroundDefAt(u.Pos).Fatigue - 1 + MathF.Max(0, u.Slope) * 5) * weight;
    }

    /// Archers standing at their own wagon draw arrows from it.
    void Resupply(float dt)
    {
        if ((resupplyTimer -= dt) > 0) return;
        resupplyTimer = 1.5f;
        foreach (var u in Units)
        {
            if (!u.Active || u.Type.Ammo == 0 || u.Ammo >= u.Type.Ammo || WagonStock[u.Team] <= 0) continue;
            if (Vector2.Distance(u.Pos, Wagons[u.Team]) > 7) continue;
            int n = Math.Min(4, Math.Min(u.Type.Ammo - u.Ammo, WagonStock[u.Team]));
            u.Ammo += n;
            WagonStock[u.Team] -= n;
        }
    }
    float resupplyTimer;

    /// The AI's archers: out of arrows, go to the wagon if it has any; full again, come back. Returns true if it took charge of s.
    bool ArcherSupply(Squad s, int team)
    {
        bool empty = s.Units.Where(u => u.Active).All(u => u.Ammo == 0);
        if (!empty || WagonStock[team] <= 0) return false;
        s.Order = Order.Hold;
        s.Anchor = Wagons[team];
        return true;
    }

    /// The softest enemy company in reach of these archers: least armour, then nearest.
    Squad? SoftTarget(Squad archers, int foe)
    {
        var at = archers.Centroid();
        float range = archers.Units.FirstOrDefault()?.Type.Range ?? 30;
        return SquadsOf(foe).Where(s => s.Alive > 0 && Visible(s, archers.Team) && Vector2.Distance(s.Centroid(), at) < range * 1.1f)
            .OrderBy(s => Focus(archers, s)).ThenBy(s => s.Units.First().Type.Armor + (s.Cavalry ? 3 : 0)).ThenBy(s => Vector2.Distance(s.Centroid(), at)).FirstOrDefault();
    }

    /// A beaten side sounds the retreat instead of dying to the last man (either side, so it is not an escape only the AI has).
    bool SoundRetreat(int team)
    {
        if (Time < 45 || retreated[team]) return false;
        float Str(int t) => Units.Where(u => u.Team == t && u.Active && !u.Routing).Sum(u => u.Hp + 5 * u.Type.Armor);
        float mine = Str(team), theirs = Str(1 - team);
        if (mine > theirs * 0.18f) return false;
        retreated[team] = true;
        foreach (var u in Units) if (u.Team == team && u.Active && !u.IsLeader && !u.Type.Has("fearless")) u.Routing = true; // the undead and trolls do not heed a horn
        Log(team == 1 ? "The enemy sounds the retreat!" : "Your men sound the retreat!");
        Sound(Cue.HornRetreat, centroid[team]);
        return true;
    }
    readonly bool[] retreated = new bool[2];
}
