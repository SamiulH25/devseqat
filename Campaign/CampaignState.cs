using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;

namespace Ironfield;

// Persistent campaign data: plain classes with public fields, serialised as JSON (docs/CAMPAIGN.md 10.3).
// Nothing here references MonoGame graphics: the self-test runs whole campaigns headless.

public enum SettlementKind { Village, Town, Castle }
/// Monster: creatures of the wild (docs/archive/CREATURES.md), hostile to every party of men; Party.Breed says what they are.
public enum PartyKind { Player, Lord, Bandit, Garrison, Monster, Caravan }

/// A persistent company (CAMPAIGN_OVERHAUL.md 2): a banner with a role, a history and an officer, holding troop stacks of any
/// troops that fit its role. The company's face (portrait, name in battle) is its largest stack.
public sealed class CompanyRecord
{
    public int Id, Battles, Kills;
    public string Name = "", Role = "";
    public float Cohesion = 10;
    /// The company's sergeant (B5): a name and up to three traits; "" = none yet.
    public string Officer = "";
    /// Battles the current sergeant has lived through (15 make him a companion, F3).
    public int OfficerBattles;
    /// The companion who leads it into battle as a hero unit, while fit ("" = none); kept by Campaign.RefreshHeroes.
    public string Hero = "";
    /// The division it fights in (a DivisionRecord.Id); -1 = your retinue, which takes your orders at once.
    public int Division = -1;
    public List<string> Traits = new();
    public List<TroopStack> Stacks = new();
    /// Where the company was raised (2.4): its culture and home settlement.
    public string Culture = "", Home = "";
    /// The charge on its banner (an Emblem); the field is the colour of the side it fights for.
    public int Banner;
    /// Veteran honours earned by its history (at most three), and the counters that earn them.
    public List<string> Honours = new();
    public int HorseKills, Assaults, SinceRout;
    /// Its last battles, newest last (at most LedgerSize).
    public List<LedgerEntry> Ledger = new();
    public const int LedgerSize = 8;
    /// Your companies' named men (docs/archive/LEGACY.md L1): the few who earned a name, each one of the men of his stack.
    public List<Soldier> Roll = new();
    /// A sergeant just risen (C4): the men you may choose instead (the risen man first), until the end of SergeantPickUntil.
    public List<int> SergeantPick = new();
    public int SergeantPickUntil;
    /// A circle of mages (docs/archive/MAGIC_AURA.md 3.2-3.3): spells learned beyond its line's, those you prepared for battle
    /// (empty = its line's), its master (a Soldier id on the roll, 0 = none) and until when it is leaderless (hours).
    public List<string> Spells = new(), Prepared = new();
    public int Master;
    public double LeaderlessUntil;
    /// The roll has been started (a new company names its first man once).
    public bool RollStarted;
    /// Battle honours (L5): the names of notable wins sewn on its banner, kept for ever; and those a realm took when the
    /// company broke before it, until it beats that realm again.
    public List<string> BattleHonours = new();
    public List<LostHonour> LostHonours = new();
    /// The named man who became its sergeant (L4), null for a sergeant from elsewhere.
    public Soldier? OfficerMan;

    [JsonIgnore] public int Men => Stacks.Sum(s => s.Men);
    [JsonIgnore] public int Wounded => Stacks.Sum(s => s.Wounded);
    /// The largest stack (ties go to the higher tier): the company's portrait and battle name.
    [JsonIgnore] public TroopStack? Main => Stacks.Count == 0 ? null : Stacks.OrderByDescending(s => s.Men + s.Wounded).ThenByDescending(s => s.Type.Tier).First();
    [JsonIgnore] public string Troop => Main?.Troop ?? "levies";
    [JsonIgnore] public UnitType Type => Troops.Get(Troop);
    /// Men ready for promotion in all stacks together.
    [JsonIgnore] public int Ready => Stacks.Sum(s => s.Ready);

    /// The stack of this troop, created empty if the company has none yet.
    public TroopStack Stack(string troop)
    {
        var s = Stacks.FirstOrDefault(x => x.Troop == troop);
        if (s == null) Stacks.Add(s = new TroopStack { Troop = troop });
        return s;
    }

    public void Prune() => Stacks.RemoveAll(s => s.Men + s.Wounded <= 0);

    /// Take up to n fit men out of the company: lowest tiers first (deserters, the rearguard's dead), or spread across the stacks
    /// by size. Their share of each stack's XP pool goes with them. Returns how many were taken from each troop.
    public List<(string Troop, int Men)> RemoveFit(int n, bool lowestFirst)
    {
        var taken = new List<(string, int)>();
        n = Math.Min(n, Men);
        if (n <= 0) return taken;
        if (lowestFirst)
            foreach (var s in Stacks.OrderBy(s => s.Type.Tier).ToList())
            {
                int k = Math.Min(n, s.Men);
                if (k <= 0) continue;
                s.Take(k);
                taken.Add((s.Troop, k));
                n -= k;
                if (n == 0) break;
            }
        else
        {
            int total = Men, left = n;
            foreach (var s in Stacks.OrderByDescending(s => s.Men).ToList())
            {
                int k = Math.Min(s.Men, Math.Min(left, (int)MathF.Ceiling(n * s.Men / (float)total)));
                if (k <= 0) continue;
                s.Take(k);
                taken.Add((s.Troop, k));
                left -= k;
            }
            for (; left > 0 && Men > 0; left--) // rounding: take the rest one by one from the biggest stacks
            {
                var s = Stacks.Where(x => x.Men > 0).OrderByDescending(x => x.Men).First();
                s.Take(1);
                taken.Add((s.Troop, 1));
            }
        }
        return taken;
    }
}

/// One troop type in a company (2.1): its fit men, its wounded, and the XP pool they share. The pool is a total, not a per-man
/// average: recruits add men, not XP, so refilling a veteran stack does not dilute it; a promotion spends one man's upgradeXp.
public sealed class TroopStack
{
    public string Troop = "";
    public int Men, Wounded;
    public float Xp;

    [JsonIgnore] public UnitType Type => Troops.Get(Troop);
    /// The most XP the pool may hold: what its fit men could spend.
    [JsonIgnore] public float PoolCap => Type.UpgradeXp > 0 && Type.UpgradesTo.Length > 0 ? Type.UpgradeXp * Men : 0;
    /// Men the pool could promote now (ignoring gold and place).
    [JsonIgnore] public int Ready => PoolCap <= 0 ? 0 : Math.Min(Men, (int)(Xp / Type.UpgradeXp + 1e-4f));

    public void AddXp(float xp) => Xp = Math.Clamp(Xp + xp, 0, PoolCap);

    /// Remove k fit men and their share of the pool.
    public void Take(int k)
    {
        k = Math.Min(k, Men);
        if (k <= 0) return;
        Xp -= Men > 0 ? Xp * k / Men : 0;
        Men -= k;
        Xp = Math.Clamp(Xp, 0, PoolCap);
    }
}

/// A named man of one of your companies (docs/archive/LEGACY.md L1). He is one of the men of the stack of his troop: hurt while
/// among its wounded. When he leaves the roll he goes to CampaignState.Fallen with how (Fate), where (DiedAt) and his
/// company (Company, CompanyName), so he is remembered after the company itself is gone.
/// Your own knight order (docs/archive/KNIGHT_ORDERS.md 8, K8): founded from your company. Its creed (three vows), the form it
/// teaches, the day it was founded, its knights (named men's ids) and how many grand masters it has had.
public sealed class FoundedOrder
{
    public string Name = "", Form = "", Company = "";
    public List<string> Vows = new();
    public List<int> Knights = new();
    public int Day, Masters = 1;
}

public sealed class Soldier
{
    public int Id, Battles, Kills, Wounds, Joined, DiedDay, Company = -1;
    /// His age (docs/COMPANY.md C1): 17-30 when named, a year older each year; 0 on an old save (`AgeOf` makes one up).
    public int Age;
    public string Name = "", Troop = "", Fate = "", DiedAt = "", CompanyName = "";
    /// His nature, one word (docs/COMPANY.md C2: Campaign.Natures), and the place he comes from.
    public string Nature = "", Home = "";
    /// Ties to other named men, at most two (docs/COMPANY.md C3), and whether he fights his next battle to avenge one.
    public List<SoldierTie> Ties = new();
    public bool Avenging;
    /// Your hand on the roll (C4): times you mentioned him after a win (three make him "of the banner"), and what you armed
    /// him with ("" = his own kit): a name for the deed line, and the gear id or "arms" for a lot of looted arms.
    public int Mentions;
    public string Carries = "", CarriesDef = "";
    public bool Hurt;
    /// Knighted into your own order (docs/archive/KNIGHT_ORDERS.md 8, K8).
    public bool Knight;
    /// What he is known for, newest last (at most MaxDeeds).
    public List<string> Deeds = new();
    public const int MaxDeeds = 4;

    /// His age, or one made up from his id for a man named before ages were kept.
    public static int AgeOf(Soldier m) => m.Age > 0 ? m.Age : 18 + m.Id * 37 % 12;

    public void Deed(string d) { Deeds.Add(d); if (Deeds.Count > MaxDeeds) Deeds.RemoveAt(1); } // the first, how he got his name, stays
}

/// A tie between two named men (C3): Brothers, Kin, Rivals, Master (he is the other's master) or Lad (the other is his).
public sealed class SoldierTie
{
    public int Other;
    public string Kind = "";
}

/// A battle honour a realm took from a company (L5).
public sealed class LostHonour
{
    public string Name = "";
    public int Faction = -1;
}

/// One line of a company's ledger (2.4).
public sealed class LedgerEntry
{
    public int Day, Lost, Kills;
    public string Foe = "";
    /// The battle's name (L5), "" for battles before names.
    public string Name = "";
    public bool Won;
}

/// Captured men held by a party (B3).
public sealed class PrisonerGroup
{
    public string Troop = "";
    public int Count;
}

/// A contract from a faction's steward (B4). Kind: hunt (a lord), clear (brigands), defend (a village), scout (a town), take (a castle or town).
public sealed class Contract
{
    public int Id, Target, Faction;
    /// A lord's quest (F8): the character who asked (-1 = the steward's board); he rewards you mostly with his favour.
    public int Giver = -1;
    public string Kind = "", Text = "";
    public float Deadline, Reward, Progress;
    /// A village elders' job (S3): the village that asked (-1 = none); such jobs have Faction -1 and need no contract.
    public int From = -1;
    /// The next step of a quest of more than one step, put on your list when this one is done.
    public Contract? Then;
    /// A knight order's contract (K4): the order that gave it ("" = none); done, it raises your standing with the order.
    public string Order = "";
}

/// One of your divisions: led by a companion (LeaderChar) or by a company's sergeant (LeaderCompany).
public sealed class DivisionRecord
{
    public int Id, LeaderChar = -1, LeaderCompany = -1;
    /// The goal it starts a battle with: Hold, Attack, FlankLeft, FlankRight, Reserve, Screen, Harass or Guard.
    public string Goal = "Hold";
}

/// A realm's army: lords who ride together under a leader against a seat ("siege") or to relieve one of their own ("relieve").
public sealed class LordArmy
{
    public int Id, Faction, Leader, Target = -1;
    public string Aim = "siege";
    public float Since, Until;
    public List<int> Members = new();
}

public sealed class Party
{
    public int Id, Faction = -1, HomeId = -1, ChaseId = -1, GoalSettlement = -1, PatrolProvince = -1, Horses, Tier = 1;
    /// The lord's Character (F3); -1 for parties without one.
    public int CharId = -1;
    public string Name = "", Task = "patrol";
    public PartyKind Kind;
    public Vector2 Pos, Goal;
    public bool Moving, Down, Resting;
    /// Travellers you chose to rob (S4): hostile to you whatever their realm.
    public bool Prey;
    public float Gold, Cooldown, DownUntil, TaskUntil, Facing;
    public List<CompanyRecord> Companies = new();
    public List<PrisonerGroup> Prisoners = new();
    public List<int> Captives = new();   // lords you hold for ransom
    /// A vassal's standing order from you (F6): "defend" your lands, "follow" you, or join the realm's "host".
    public string Order = "";
    public bool Captive;                 // a lord held by you
    public float Food = 10;              // days of food (yours; lords are fed by their faction)
    public int SiegeTarget = -1;         // the settlement this party is besieging
    /// Monsters (docs/archive/CREATURES.md): what they are ("goblin"); a holdout is a Lair party that never moves, its Gold the
    /// hoard; a raiding band names its holdout (LairId, a party id) and the village it is going to plunder (RaidOn).
    public string Breed = "";
    public bool Lair;
    public int LairId = -1, RaidOn = -1;
    public float NextRaid;               // a holdout's next raid (campaign hours)
    /// A holdout whose raiders plundered a realm's village is hunted by that realm's lords until HuntedUntil.
    public int HuntedBy = -1;
    public float HuntedUntil;
    /// A knight order's army (docs/archive/KNIGHT_ORDERS.md 4.3): the order's id, "" for every other party.
    public string KnightOrder = "";
    /// The army this lord rides in (CampaignState.Armies, LordArmies.cs); -1 for none.
    public int ArmyId = -1;

    public int Men => Companies.Sum(c => c.Men);
    public int Wounded => Companies.Sum(c => c.Wounded);
}

public sealed class Settlement
{
    public int Id, Province, Faction, Recruits, Horses;
    /// War hounds ready at this place's kennel (docs/archive/CREATURES.md phase 4).
    public int Hounds;
    public string Name = "";
    /// The culture its people belong to (F2): it keeps it when the settlement changes hands, so its recruits do too.
    public string Culture = "common";
    public SettlementKind Kind;
    public Vector2 Pos;
    public float ThreatUntil;
    public List<CompanyRecord> Mercenaries = new();
    public List<CompanyRecord> Garrison = new();   // towns and castles (B1)
    public List<Contract> Offers = new();          // the steward's contract board (towns), the elders' jobs (villages)
    public float OffersAt;                         // when the elders next think of work for you (villages)
    public float Supplies = 30;                    // days a garrison can hold out under siege
    public int Owner = -1;                         // party id of the holder of the fief (-1: the faction)
    public bool Besieged;
    public float SiegeSince;                       // when the siege began (ladders take 12 hours)
    /// Fiefs (F6): prosperity scales income and recruits; unrest above 70 raises rebels and at 100 the place defects.
    public float Prosperity = 50, Unrest;
    /// Buildings by id and level (towns and castles), and the one under construction until a time.
    public Dictionary<string, int> Buildings = new();
    public string Building = "";
    public float BuildUntil;
    /// Tax level: 0 low, 1 normal, 2 high (set by the holder; a seat's villages pay at its rate).
    public int Tax = 1;
}

/// Where a big battle's dead lie unburied, how many, and since when.
public sealed class Grave
{
    public int Id, Dead;
    public Vector2 Pos;
    public double Hours;
}

public sealed class Province
{
    public int Id, Faction;
    public string Name = "", Kind = "Meadow";
    /// The province as the land knows it: "the Greywater Hills", "Oakhollow Forest" (S1).
    [JsonIgnore] public string Title => Kind switch
    {
        "Hills" => $"the {Name} Hills", "Forest" => $"{Name} Forest", "Farmland" => $"the {Name} Fields",
        "Marsh" => $"the {Name} Fens", "Steppe" => $"the {Name} Steppe", "Badlands" => $"the {Name} Wastes", _ => $"the Vale of {Name}",
    };
    public Vector2 Center;
    public List<int> Neighbors = new();
    /// How wet its ground is from recent rain, 0-1 (docs/archive/TERRAIN.md T7; Campaign.RainDaily).
    public float Wet;
}

/// A knight order on the map (docs/archive/KNIGHT_ORDERS.md 4): its patron realm, chapter house, grand master and commanders,
/// its standing with you, and how it fares.
public sealed class OrderState
{
    public string Id = "";
    /// The realm it rides for (-1: none, a free order you can hire) and its chapter house (a settlement id).
    public int Patron = -1, Chapter = -1;
    /// Character ids: the grand master and the knight-commanders who lead its armies.
    public int GrandMaster = -1;
    public List<int> Commanders = new();
    /// What the order thinks of you (-100..100) and of the other orders (by order id).
    public float Standing;
    public Dictionary<string, float> Regard = new();
    /// It died out (its patron fell and nobody took it in, or the Black Flame took it).
    public bool Gone;
    /// A crusade it has called (docs/archive/KNIGHT_ORDERS.md 4.6): the party it rides against, until when.
    public int CrusadeOn = -1;
    public float CrusadeUntil;
    /// When it next raises a knight-commander's army, and when its chapter next has brothers to spare you (campaign hours).
    public float NextArmy, NextLevy;
}

public sealed class FactionState
{
    public int Id;
    public string Name = "";
    public float Treasury, WarScore;
    /// The realm's ruler and marshal (F5): character ids, -1 until the council fills them. The marshal leads its hosts.
    public int Ruler = -1, Marshal = -1;
    /// A tributary realm pays its overlord a quarter of its income and joins its wars until a time (F7); -1 = free.
    public int TributaryOf = -1;
    public float TributeUntil;
    /// The settlement the realm's hosts march on (7.1, chosen by the marshal), -1 = none.
    public int Objective = -1;
    /// When the council next sits (campaign hours), how many times it has sat, and what it decided last time.
    public float NextCouncil;
    public int Councils;
    public List<string> LastCouncil = new();
    /// Seats taken and not yet granted: the council decides who holds them.
    public List<Grant> Pending = new();
    /// A motion for war on this realm (faction id, -1 = none) and who proposed it (a character id).
    public int WarMotion = -1, WarMotionBy = -1;
    /// A realm founded in play (5.4): it has no entry in factions.json, so it carries its own culture and heraldry, and
    /// fields the troop lines of the listed realm `Template`.
    public bool Founded;
    public string Culture = "";
    public int Palette = 2, Template, Capital = -1;
}

/// A captured seat waiting for the council (5.3), and who took it (a character id, -1 = nobody in particular).
public sealed class Grant
{
    public int Seat, Captor = -1;
}

/// The state between two factions (B6): at war, or in truce until a time. Score: what each side has won from the other.
public sealed class War
{
    public int A, B;
    public bool Active = true;
    public float TruceUntil, ScoreA, ScoreB;
    /// War exhaustion of each side, 0-100 (F7): losses, lost seats and the war's length; it pushes a realm to sue for peace.
    public float ExhA, ExhB;
    /// When this war (or the last one) began, in campaign hours.
    public float Since;
}

/// An alliance between two realms against a shared enemy (F7), until a time.
public sealed class Pact
{
    public int A, B;
    public float Until;
}

/// Gold owed under peace terms (F7): paid each week until the weeks run out.
public sealed class Payment
{
    public int From, To, Weeks;
    public float PerWeek;
}

/// The marshal's call to the host (F7): where to come, by when, and who called.
public sealed class Summons
{
    public int Faction, Seat, From;
    public float Until;
}

public sealed class CaptainState
{
    public float Renown = 50, WoundedUntil;
    /// The captain is a woman (LD6: whom you may court); the creator does not ask yet, so false.
    public bool Female;
    /// Before the armoury (P1) gear was three tiers; a save that still has them is moved onto the kit when loaded (`Campaign.MigrateGear`).
    public int ArmorTier, HelmTier, HorseTier, Contract = 0; // Contract: faction you serve, -1 = none
    /// What the captain wears and carries (docs/archive/PROGRESSION.md P1).
    public CaptainKit Kit = new();
    /// Every piece the captain owns as a thing (docs/COMPANY.md C9): make, marks, name, story. Free plain pieces need none.
    public List<GearItem> Baggage = new();
    /// Career (F5): you swore fealty to the realm you serve (a vassal, not a hired sword); when this contract began;
    /// and the highest rank reached so far (an int of Rank), so each career beat is shown once.
    public bool Fealty;
    public float ContractSince;
    public int BestRank;
    /// Skills 0-100 (step F4) by id, and the perks taken from their branches (Skills.cs).
    public Dictionary<string, float> Skills = new();
    public List<string> Perks = new();
    /// Perks forgotten for gold so far; each costs more than the last (P3).
    public int Retrained;
    /// Lasting wounds the captain carries (P4: "scar", "knee", "hand", "eye"; `Campaign.LastingWounds`).
    public List<string> Wounds = new();
    /// Perk choices given by rank, free of any branch's 25-point marks (P5: one at Sworn Sword), and the perks taken with them.
    public int FreePerks;
    public List<string> BonusPerks = new();
    /// The aura (docs/archive/KNIGHT_ORDERS.md 5): tier 0-5, XP towards the next, and the wall (XP full, a trial needed).
    public int AuraTier;
    public float AuraXp;
    public bool AuraWall;
    /// Battles won while the first wall stands (docs/ARCANA.md AR1): a fought one counts two; at `Campaign.SeasonWins` it wakes.
    public int WallWins;
    /// When a cracked core (the `core` wound, K5) mends of itself.
    public float CoreUntil;
    /// The forms you know, in slot order (5.6), and how often each was used (its rank); the Black Flame in you (7).
    public List<string> Forms = new();
    public Dictionary<string, int> FormUses = new();
    public float Corruption;
    /// The Black Flame (docs/archive/KNIGHT_ORDERS.md 7, K7): the Hollow Crown's pact taken; the highest threshold (25 each) whose
    /// price you have paid; purged once (no second bargain); how it ended ("" = it has not; "crown" or "burnt").
    public bool Pact, Purged;
    public int FlameSeen;
    public string FlameEnd = "";
    /// Your knight order (docs/archive/KNIGHT_ORDERS.md 4.5, K4): its id ("" = unsworn), your rank in it (Campaign.OrderRanks, granted
    /// and so stored), the order deeds you have done, a vow broken and not yet atoned for, and the orders you left or were cast
    /// out of.
    public string Order = "";
    public int OrderRank, OrderDeeds;
    public bool Penance;
    public List<string> Forsworn = new();
    /// Your rival in the order (a knight character id, -1 = none; docs/archive/KNIGHT_ORDERS.md 6.2).
    public int Rival = -1;
    /// Your own form (docs/archive/MAGIC_AURA.md 4.4, MA6): how you fight, battle by battle (style -> points), the two styles offered
    /// once you may make one, and the form you made (registered as Forms.OwnId). The aspect each form took at rank 3 (4.5).
    public Dictionary<string, float> StyleTally = new();
    public List<string> OwnFormOffer = new();
    public FormDef? OwnForm;
    public Dictionary<string, string> FormAspects = new();
    /// Who the captain was before the company (the character creator, Data/origins.json): birth and past ids ("" = a save
    /// from before origins, or a campaign started without the creator), and the face chosen (a Variety visage, -1 = the plain head).
    public string Birth = "", Origin = "";
    public int Visage = -1;
    /// The painted face chosen in the creator (null: derived from the captain's id) and the arms on your first banners (-1: none chosen).
    public CaptainLook? Likeness;
    public int Arms = -1;
}

/// The captain's painted face as chosen in the creator (PortraitPainter numbers: cut 0 short .. 4 bald, beard 0 none, 1 full,
/// 2 moustache, 3 forked; eye, brow, nose, jaw and scar as `Face`).
public sealed class CaptainLook
{
    public int Cut, Beard = 1, Eye, Brow = 1, Nose = 1, Jaw = 1, Scar;
}

/// The captain's kit: gear ids from Data/gear.json worn in each slot, the second weapon ("" = none), and the gear bought
/// (free gear is always yours and is not listed).
public sealed class CaptainKit
{
    public string Weapon = "arming_sword", Side = "greatsword", Shield = "heater_shield", Body = "mail", Helm = "sallet", Horse = "courser";
    /// Old saves only: the gear ids owned before pieces were things; `Campaign.MigrateGear` moves them into `CaptainState.Baggage`.
    public List<string> Owned = new();
    public IEnumerable<string> Worn => new[] { Weapon, Side, Shield, Body, Helm, Horse }.Where(id => id != "");
}

public sealed class LogEntry
{
    public double At;
    public string Text = "";
}

/// A campaign buff: an effect id (Data/effects.json) and the hour it ends.
public sealed class Buff
{
    public string Effect = "";
    public double Until;
}

public sealed class CampaignState
{
    /// Version 4 is the campaign overhaul (troop stacks and everything after): older saves are refused, not migrated.
    public const int CurrentVersion = 4;
    public const string TooOld = "this save is from before the campaign overhaul and cannot be loaded";
    public int Version = CurrentVersion, Seed, NextId = 1, UnpaidWeeks;
    /// Ids of named men (L1), counted apart from NextId so naming men moves no other id.
    public int NextSoldier = 1;
    /// Every named man your companies have lost (L3), never trimmed.
    public List<Soldier> Fallen = new();
    /// The named men you may mention after the last won battle (C4); emptied by a mention or the next battle.
    public List<int> MentionPool = new();
    /// Villages a pensioned man went home to (C4): their recruits come a little readier.
    public List<string> Pensioned = new();
    /// You retired on the company's legend (L7), and how many captains have led it since you began (handing over).
    public bool Retired;
    public int Generation;
    /// Your last fought battle's deployment, by company (docs/archive/COMBAT_QOL.md Q6), whether to start the next one from it, and
    /// your control groups in battle (digit -> company ids).
    public List<DeploySpot> LastDeploy = new();
    public bool DeployAsLast = true;
    public Dictionary<int, List<int>> ControlGroups = new();
    /// How far the provinces of this save's map reach (WorldMap.Extent for new campaigns; older saves have their own).
    public float MapExtent = WorldMap.PreviousExtent;
    /// How the map lays out its realms (WorldMap.Layout): saves from before docs/archive/WORLD.md W1 have none and keep the old bands.
    public int Layout = WorldMap.LegacyLayout;
    /// How many realms Data/factions.json held when the campaign began (older saves: three). Realms past it are founded in play.
    public int DataRealms = 3;
    public double Hours;
    public ulong Rng;
    /// The roll's own stream (L1), so naming men never moves the campaign's main draws.
    public ulong RollRng;
    /// The knight orders' own random stream (docs/archive/KNIGHT_ORDERS.md), so they move nothing else's draws.
    public ulong OrderRng;
    /// The houses' stream (families, births, deaths, marriages; Campaign/Houses.cs).
    public ulong KinRng;
    /// The noble houses (Campaign/Houses.cs).
    public List<House> Houses = new();
    /// The knight orders (docs/archive/KNIGHT_ORDERS.md 4), made on load for saves from before them.
    public List<OrderState> Orders = new();
    /// The order you founded (K8); null until then.
    public FoundedOrder? OwnOrder;
    public float Gold, LastWeekBalance;
    /// Goods from the field you carry to sell (kind id -> lots; Campaign/Goods.cs).
    public SortedDictionary<string, int> Goods = new();
    public bool Ironman, GameOver;
    /// Why the campaign ended ("" = the Ironman captain was not ransomed), and whether it was won (5.5; play may go on).
    public string EndReason = "";
    public bool Won;
    public List<War> Wars = new();
    /// Lords' armies in the field (LordArmies.cs).
    public List<LordArmy> Armies = new();
    public CaptainState Captain = new();
    public List<Party> Parties = new();
    /// Sites (docs/archive/SIDE_CONTENT.md S2): when each can be used again (campaign hours; absent = now), the ones you have
    /// been to, and the site you are riding to (-1 = none).
    public Dictionary<int, float> SiteUntil = new();
    public List<int> SitesSeen = new();
    public int GoalSite = -1;
    public List<Settlement> Settlements = new();
    public List<Province> Provinces = new();
    public List<FactionState> Factions = new();
    public List<LogEntry> Log = new();
    public List<Contract> Contracts = new();   // the contracts you have accepted
    /// Diplomacy (F7): alliances, gold owed under terms, the record of treaties (newest last), and the marshal's summons to you.
    public List<Pact> Pacts = new();
    public List<Payment> Payments = new();
    public List<string> Treaties = new();
    public Summons? Summons;
    /// Events (F8): the one waiting for your answer, when the next may come, and when each last fired.
    public PendingEvent? Event;
    public float NextEvent = 72;
    public Dictionary<string, float> EventsFired = new();
    /// Grave fields of big battles whose dead were not buried (docs/archive/CREATURES.md phase 5): one may rise as an undead host.
    public List<Grave> Graves = new();
    public float LastSummons = -1000;
    public int LastBattleFoe = -1;             // the last party you defeated (for hunt contracts)
    public float LastBattleAt = -9999;         // when you last fought (the after_battle events, S6)
    /// Follow-up events waiting (S6): a "flag:name" effect sets name to the hour; a "flag:name" trigger fires a week later.
    public Dictionary<string, float> Flags = new();
    /// Your deeds for the chronicle and the end summary (S7), what you did counted by kind, and a watchtower's long view.
    public List<LogEntry> Deeds = new();
    public List<int> SitesVisited = new();
    /// Effects on your company for a while (docs/archive/COMPANIONS_MAGIC.md A5): a shrine's blessing, a feast, hunger, a rite.
    /// Carried into every battle until they end (Campaign/Buffs.cs).
    public List<Buff> Buffs = new();
    /// Omen (C2): craft draws it; it fades a point a day and, past thresholds, draws ill events.
    public float Omen;
    /// The day after your last camp rite (one a day), and the relic you carry with its charges (Campaign/Rites.cs).
    public int RiteDay, RelicCharges;
    public bool Relic;
    public Dictionary<string, int> Tally = new();
    public float FarSightUntil;
    /// Lords, companions, wanderers and you (F3); CaptainChar is your own record.
    public List<Character> Characters = new();
    public int CaptainChar = -1;
    /// Your divisions (docs/archive/COMBAT_OVERHAUL.md 1.5): who leads each and the goal it starts a battle with.
    public List<DivisionRecord> Divisions = new();

    static readonly JsonSerializerOptions Json = new() { IncludeFields = true, WriteIndented = true };

    public string Serialize() => JsonSerializer.Serialize(this, Json);

    public static CampaignState Deserialize(string json)
    {
        // Read the version on its own first: an old save's shape no longer matches these classes.
        using (var doc = JsonDocument.Parse(json))
        {
            int v = doc.RootElement.TryGetProperty("Version", out var ve) && ve.TryGetInt32(out int vi) ? vi : 0;
            if (v < CurrentVersion) throw new InvalidDataException(TooOld);
            if (v > CurrentVersion) throw new InvalidDataException($"save version {v} is newer than this build reads ({CurrentVersion})");
        }
        return JsonSerializer.Deserialize<CampaignState>(json, Json) ?? throw new InvalidDataException("empty save");
    }

    public static string SaveDir => Environment.GetEnvironmentVariable("IRONFIELD_SAVES") is { Length: > 0 } d ? d
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ironfield", "saves");

    /// Write to a temporary file first and keep the previous save as .bak, so a crash mid-save never loses a campaign.
    public void Save(string slot)
    {
        Directory.CreateDirectory(SaveDir);
        string p = Path.Combine(SaveDir, slot + ".json"), tmp = p + ".tmp";
        File.WriteAllText(tmp, Serialize());
        if (File.Exists(p)) File.Copy(p, p + ".bak", true);
        File.Move(tmp, p, true);
    }

    /// True after Load had to fall back to the .bak file.
    public static bool RestoredFromBackup;

    /// Why the last Load returned null for a save that exists (an old or unreadable file), for the screens to show.
    public static string? LoadError;

    public static CampaignState? Load(string slot)
    {
        var p = Path.Combine(SaveDir, slot + ".json");
        RestoredFromBackup = false;
        LoadError = null;
        if (!File.Exists(p)) return null;
        try { return Deserialize(File.ReadAllText(p)); }
        catch (Exception e)
        {
            // A save from before the overhaul is refused outright (no migration, by design); a damaged one falls back to its backup.
            if (e.Message != TooOld && File.Exists(p + ".bak"))
            {
                Console.WriteLine($"save {slot} unreadable ({e.Message}); restoring the backup");
                try { var back = Deserialize(File.ReadAllText(p + ".bak")); RestoredFromBackup = true; return back; }
                catch (Exception e2) { e = e2; }
            }
            LoadError = e.Message;
            Console.WriteLine($"save {slot} not loaded: {e.Message}");
            return null;
        }
    }

    public static bool Exists(string slot) => File.Exists(Path.Combine(SaveDir, slot + ".json"));
}

/// Deterministic random numbers whose state lives in the save (System.Random cannot be saved). xorshift64*.
public sealed class Rng
{
    readonly CampaignState s;
    readonly bool roll, orders, kin;
    public Rng(CampaignState state) { s = state; if (s.Rng == 0) s.Rng = 0x9E3779B97F4A7C15UL ^ (ulong)(uint)s.Seed; }
    /// The roll's stream (CampaignState.RollRng), seeded apart from the main one.
    public Rng(CampaignState state, bool roll) : this(state) { this.roll = roll; if (roll && s.RollRng == 0) s.RollRng = 0xD1B54A32D192ED03UL ^ (ulong)(uint)s.Seed; }
    /// The knight orders' stream (CampaignState.OrderRng).
    public static Rng Orders(CampaignState state)
    {
        var r = new Rng(state, false, true);
        if (state.OrderRng == 0) state.OrderRng = 0xA0761D6478BD642FUL ^ (ulong)(uint)state.Seed;
        return r;
    }
    Rng(CampaignState state, bool roll, bool orders) : this(state) { this.roll = roll; this.orders = orders; }
    /// The houses' stream (CampaignState.KinRng).
    public static Rng Kin(CampaignState state)
    {
        var r = new Rng(state, false, false) { kinStream = true };
        if (state.KinRng == 0) state.KinRng = 0xE7037ED1A0B428DBUL ^ (ulong)(uint)state.Seed;
        return r;
    }
    bool kinStream { init => kin = value; }

    public ulong NextU()
    {
        ulong x = kin ? s.KinRng : orders ? s.OrderRng : roll ? s.RollRng : s.Rng;
        x ^= x >> 12; x ^= x << 25; x ^= x >> 27;
        if (kin) s.KinRng = x; else if (orders) s.OrderRng = x; else if (roll) s.RollRng = x; else s.Rng = x;
        return x * 0x2545F4914F6CDD1DUL;
    }

    public float Float() => (NextU() >> 40) / (float)(1UL << 24);
    public int Next(int max) => max <= 0 ? 0 : (int)(NextU() % (ulong)max);
    public int Next(int min, int max) => min + Next(max - min);
    public float Range(float a, float b) => a + (b - a) * Float();
    public bool Chance(float p) => Float() < p;
    public T Pick<T>(IReadOnlyList<T> list) => list[Next(list.Count)];
}

/// Campaign balance numbers (Data/campaign.json overrides these defaults).
public sealed class Balance
{
    /// Fief buildings (F6); the built-in defaults when empty.
    public List<BuildingDef> Buildings = new();
    /// Veteran honours (F1); the built-in defaults when empty.
    public List<HonourDef> Honours = new();
    /// The captain's skill tree (F4); the built-in defaults when empty.
    public List<SkillDef> Skills = new();
    public float HourSeconds = 0.5f, FootSpeed = 5.5f, HorseSpeed = 9, RoadBonus = 1.3f, Vision = 55, EncounterRadius = 3.5f, JoinRadius = 10;
    /// How much closer two AI parties must be before they fight (the player's own encounters use EncounterRadius).
    public float AiClashRadius = 5.5f;
    public float StartGold = 300, StartRenown = 50;
    public float UpkeepRate = 0.1f, ContractPay = 0.85f, LootRate = 0.25f, LootCoinShare = 0.3f, RansomRate = 1, MercMarkup = 1.5f;
    public float WoundBase = 0.3f, WoundArmor = 40, WoundMin = 0.25f, WoundMax = 0.7f, CaptureChance = 0.5f, FledReturnOnDefeat = 0.7f;
    public float XpParticipation = 10, XpVictory = 1.5f, XpPerKillTier = 6;
    public float RenownPerVictory = 5, RenownStrengthDivisor = 50;
    /// What the captain's baggage carries (docs/COMPANY.md C9): a base, and more for each spare horse (to six).
    public float BaggageBase = 20, BaggagePerHorse = 5;
    /// Your hand on the roll (C4): the purse for a man you mention, and a pension for a man you send home.
    public int MentionPurse = 20, PensionCost = 40;
    /// What a company's senior named man's nature adds to its might in auto-resolve (docs/COMPANY.md C2).
    public float NatureWeight = 0.01f;
    public float CompanySlotsBase = 10, MenBase = 40, MenRenownDivisor = 5;
    /// Men per company, party, band and garrison are this many times the base numbers below; per-man prices are divided by it.
    public float ArmyScale = 3;
    /// How much drill adds to a company's strength in auto-resolve (Rules.Might): x(1 + this x drill). Fought battles
    /// reward drill (docs/archive/MELEE.md D8); --agree measures it (5: equal-might fights one to four tiers apart near a coin toss).
    public float DrillWeight = 5;
    /// How much a tier V-VII troop's attack style and passive traits add to its strength in auto-resolve, per style rank
    /// (1-3 at tiers V-VII): x(1 + this x rank). --agree measures it (docs/archive/COMPANIONS_MAGIC.md D5).
    public float TraitWeight = 0.1f;
    /// What an aura tier adds to the side its holder leads in auto-resolve (you, or an order knight leading his army): x(1 +
    /// this x tier). Worth a company, never an army (docs/archive/KNIGHT_ORDERS.md 5.7, K6).
    public float AuraWeight = 0.03f;
    /// A feast in town, crowns a man (docs/archive/COMPANIONS_MAGIC.md A5).
    public float FeastPerMan = 6;
    /// Named men a company keeps at most (L1), and how much more often a named man lives through what kills his stack (L2).
    public int RollSize = 6;
    public float VeteranSurvival = 0.6f;
    /// Cohesion a company loses with each named man killed or lost (L3).
    public float FallenCohesion = 1.5f;
    public int VillageRecruitsMax = 8, LordsPerFaction = 8, BanditsMax = 80;
    /// A bandit band appears this many times a day while below BanditsMax (0-1) — the roll for each attempt.
    public float BanditSpawnChance = 0.95f;
    /// How many bands the world tries to raise each day (while under BanditsMax), so a big map fills up quickly.
    /// It scales with the map's area, so a bigger world stays populated: Campaign.BanditSpawnsWanted.
    public int BanditSpawnsPerDay = 5;
    /// Lords per faction and bandit bands are quoted per the original 26-province map and multiply by how much world there
    /// is (Campaign.LordsWanted, Campaign.BanditsWanted), so a realm has as many parties as its ground needs.
    /// Bandit bands are rolled 1-3 strong, and gain a man for every this many days of campaign (they grow as you do).
    public int BanditBandMin = 10, BanditBandMax = 20;
    public float BanditGrowPerDays = 9;
    public float BanditTierPerDays = 45;
    /// Daily chance (0-1) that a lord who sees no enemy province to raid marches to a neighbouring faction's front instead:
    /// how often the AI picks a fight on its own.
    public float LordAggression = 0.35f;
    public float HealField = 0.08f, HealTown = 0.25f, DrillCamp = 0.6f, DrillTown = 1.2f;
    public float TownIncome = 150, CastleIncome = 90, VillageIncome = 40, LordWage = 1.3f;
    public float TruceDays = 30, BountyBandits = 50, BountyLord = 120;
    /// Goblin holdouts (docs/archive/CREATURES.md): how many per forest or hill province, the daily chance one is raised while
    /// under that, how far from any settlement, and (in base men, times ArmyScale) their size when raised, their cap,
    /// daily growth and when a boss appears. Raids leave every HoldoutRaidDays once the holdout has HoldoutRaidMen,
    /// with HoldoutRaidBand men, at a village within HoldoutRaidRange. Gold: hoard to start, per day and per raid.
    public float HoldoutsPerWildProvince = 0.7f, HoldoutSpawnChance = 0.45f, HoldoutClearance = 12;
    public float HoldoutStartMen = 10, HoldoutMaxMen = 45, HoldoutGrowPerDay = 0.7f, HoldoutBossMen = 35;
    public float HoldoutRaidDays = 4, HoldoutRaidMen = 18, HoldoutRaidBand = 10, HoldoutRaidRange = 45;
    /// A holdout's host counts this many times its strength in an auto-resolved fight: it fights from its holes.
    public float HoldoutDefence = 2.5f;
    /// Days a realm's lords hunt a holdout after its raiders plunder one of their villages.
    public float HoldoutHuntDays = 6;
    /// Wolf packs: how many per forest province, the daily chance one appears while under that, and their size in base
    /// men (times ArmyScale; a wolf more for every 30 days), plus the dire wolf that leads each.
    public float WolfPacksPerForest = 0.45f, WolfSpawnChance = 0.15f, WolfPackMin = 4, WolfPackMax = 9;
    public float HoldoutStartHoard = 60, HoldoutHoardPerDay = 4, HoldoutRaidLoot = 40, BountyHoldout = 80;
    // Rare monsters (docs/archive/CREATURES.md phase 3): how many trolls, how many in each, the bounty on one.
    public float RareStart = 3, RareEvery = 60, RareMax = 8, RareSpawnChance = 0.3f, TrollMen = 2, BountyTroll = 300;
    /// A troll's weight in an auto-resolve by night and by day (a battle at night or noon shows the same).
    public float TrollNight = 1.6f, TrollDay = 0.8f;
    /// Creatures for you (phase 4): hounds a kennel breeds a week, and the most it keeps.
    public float KennelPerWeek = 4, KennelMax = 12;
    /// The chance a town's mercenary board offers ogres each week, and how many.
    public float OgreOfferChance = 0.05f, OgreMen = 1;
    /// The dead rise (phase 5): fallen men that make a grave field, how many days until it may rise, its nightly chance,
    /// the days before it rests for good, the chance a realm buries a battle's dead in its lands, the most undead hosts at once.
    public float GraveMin = 40, GraveRiseDays = 3, GraveRiseChance = 0.45f, GraveDays = 15, RealmBuryChance = 0.92f, UndeadMax = 3;
    /// An undead host: the share of a field's dead that rise as skeletons, its size bounds (times ArmyScale), the share of
    /// the fallen it raises after a win, what it hunts, its auto-resolve weight by night and by day, and its bounty.
    public float UndeadShare = 0.5f, UndeadMinMen = 10, UndeadMaxMen = 50, UndeadGrowShare = 0.3f, UndeadPreyAt = 1.1f, UndeadNight = 1.3f, UndeadDay = 0.8f, BountyUndead = 250;
    /// Spider nests (docs/archive/MONSTERS.md): how many per forest or fen province, the daily chance a missing one is spun,
    /// its spiders at first and at most (base men), and how many hatch a day. They raid like holdouts, by night.
    public float NestsPerWood = 0.2f, NestSpawnChance = 0.35f, NestStartMen = 6, NestMaxMen = 28, NestGrowPerDay = 0.5f;
    /// Cave bears: how many sleuths per hill or forest province, the daily chance a missing one wakes, bears in each
    /// (base men, a bear more every 90 days), and the bounty.
    public float BearsPerWild = 0.12f, BearSpawnChance = 0.1f, BearMen = 2, BountyBear = 120;
    /// Wyverns: how many at the start, one more every WyvernEvery days up to WyvernMax, the daily chance a missing one comes
    /// to roost, wyverns in each (base men), and the bounty.
    public float WyvernStart = 1, WyvernEvery = 75, WyvernMax = 4, WyvernSpawnChance = 0.25f, WyvernMen = 1, BountyWyvern = 450;
    /// The hourly chance a troll or wyvern with no prey in sight picks a village to ravage.
    public float MonsterRavageChance = 0.12f;
    public float HorseCostRate = 0.5f;

    static Balance? loaded;
    public static Balance Get()
    {
        if (loaded != null) return loaded;
        loaded = new Balance();
        foreach (var dir in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var p = Path.Combine(dir, "Data", "campaign.json");
            if (!File.Exists(p)) continue;
            loaded = JsonSerializer.Deserialize<Balance>(File.ReadAllText(p), new JsonSerializerOptions { IncludeFields = true, ReadCommentHandling = JsonCommentHandling.Skip })!;
            break;
        }
        return loaded;
    }
}

/// One culture's name pools.
public sealed class CultureNames
{
    public List<string> Places = new(), Lords = new(), Men = new(), Ladies = new();
}

/// Name lists (Data/names.json).
public sealed class Names
{
    public List<string> Places = new(), Lords = new(), Epithets = new(), Bandits = new(), Sergeants = new(), Prefixes = new(), Suffixes = new(), Ladies = new();
    public Dictionary<string, List<string>> Nouns = new();
    /// Each culture's own places and lords (docs/archive/SIDE_CONTENT.md S1).
    public Dictionary<string, CultureNames> Cultures = new();

    public List<string> PlacesOf(string culture) => Cultures.TryGetValue(culture, out var c) && c.Places.Count > 0 ? c.Places : Places;
    public List<string> LordsOf(string culture) => Cultures.TryGetValue(culture, out var c) && c.Lords.Count > 0 ? c.Lords : Lords;
    /// Common soldiers' names of a culture (L1); the sergeants' list when it has none.
    public List<string> MenOf(string culture) => Cultures.TryGetValue(culture, out var c) && c.Men.Count > 0 ? c.Men : Sergeants;
    /// Women's names of a culture (docs/archive/LORDS.md LD0); the common list when it has none.
    public List<string> LadiesOf(string culture) => Cultures.TryGetValue(culture, out var c) && c.Ladies.Count > 0 ? c.Ladies : Ladies;

    static Names? loaded;
    public static Names Get()
    {
        if (loaded != null) return loaded;
        foreach (var dir in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var p = Path.Combine(dir, "Data", "names.json");
            if (!File.Exists(p)) continue;
            return loaded = JsonSerializer.Deserialize<Names>(File.ReadAllText(p), new JsonSerializerOptions { IncludeFields = true, PropertyNameCaseInsensitive = true })!;
        }
        throw new FileNotFoundException("Data/names.json not found");
    }
}
