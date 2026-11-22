using System.Globalization;
using Microsoft.Xna.Framework;

namespace Ironfield;

/// The events' effects (docs/pre-demo/EVENTS.md): "verb", "verb:n", "verb:arg" or "verb:arg:n". Each verb is one entry in a
/// table (its shape, its icon and word for the page) and one case in `ApplyEffect`. Most act on the event's cast by role:
/// `opinion` on its lord, `unrest` on its settlement, `man_hurt` on its man.
public sealed partial class Campaign
{
    /// A verb: its shape ("" none, "n" a number, "a" a word, "an" a word and a number), the icon and words on the page,
    /// whether a rise is bad (unrest), whether it never shows (a flag), and the role it needs in the cast.
    sealed record VerbDef(string Shape, string Icon, string Word, bool LowGood = false, bool Hidden = false, string Needs = "");

    static readonly Dictionary<string, VerbDef> Verbs = new()
    {
        ["gold"] = new("n", "coin", "CROWNS"),
        ["renown"] = new("n", "star", "RENOWN"),
        ["food"] = new("n", "bread", "DAYS OF FOOD"),
        ["horses"] = new("n", "horse", "HORSES"),
        ["cohesion"] = new("n", "flag", "COHESION"),
        ["xp"] = new("n", "up", "DRILL"),
        ["opinion"] = new("n", "heart", "", Needs: "lord"),
        ["opinion_companion"] = new("n", "heart", "", Needs: "companion"),
        ["opinion_liege"] = new("n", "crown", "YOUR LIEGE"),
        ["opinion_realm"] = new("n", "crown", "THE REALM'S LORDS"),
        ["unrest"] = new("n", "fire", "UNREST", LowGood: true, Needs: "settlement"),
        ["prosperity"] = new("n", "up", "PROSPERITY", Needs: "settlement"),
        ["exhaustion"] = new("n", "skull", "WAR WEARINESS", LowGood: true),
        ["calm"] = new("n", "sun", "OMEN", LowGood: true),
        ["dread"] = new("n", "skull", "OMEN", LowGood: true),
        ["corrupt"] = new("n", "fire", "THE BLACK FLAME", LowGood: true),
        ["sick"] = new("n", "skull", "FALL SICK"),
        ["desert"] = new("n", "skull", "DESERT"),
        ["heal"] = new("n", "heart", "OF THE WOUNDED HEAL"),
        ["find"] = new("n", "coin", "CROWNS, MORE OR LESS"),
        ["men"] = new("n", "flag", "MEN JOIN YOU"),
        ["man"] = new("", "flag", "A MAN JOINS YOU"),
        ["learn"] = new("n", "up", "A LESSON"),
        ["skill"] = new("an", "up", ""),
        ["buff"] = new("an", "sun", ""),
        ["item"] = new("a", "sword", ""),
        ["farsight"] = new("n", "scroll", "SEE FAR"),
        ["gamble"] = new("n", "star", "A PRIZE, OR NOTHING"),
        ["reveal"] = new("", "scroll", "A PLACE ON THE MAP"),
        ["spent"] = new("n", "", "", Hidden: true, Needs: "site"),
        ["flag"] = new("a", "", "", Hidden: true),
        ["unflag"] = new("a", "", "", Hidden: true),
        ["quest"] = new("", "scroll", "A QUEST", Needs: "lord"),
        ["bounty"] = new("", "coin", "A BOUNTY"),
        ["peace"] = new("", "flag", "PEACE", Needs: "lord"),
        ["lists"] = new("", "sword", "FIGHT IN THE LISTS", Needs: "settlement"),
        ["disband"] = new("", "skull", "THE RISING DISBANDS", Needs: "settlement"),
        ["crown"] = new("", "crown", "A CROWN", Needs: "settlement"),
        ["prisoners_free"] = new("", "chain", "PRISONERS FREED"),
        ["prisoners_recruit"] = new("", "chain", "PRISONERS TAKE SERVICE"),
        ["bury"] = new("", "heart", "THE DEAD BURIED"),
        ["road_sword"] = new("", "sword", "SWORD AND SHIELD"),
        ["road_lance"] = new("", "horse", "LANCE AND SADDLE"),
        ["road_bow"] = new("", "arrow", "THE BOW"),
        ["search"] = new("", "star", "TREASURE, OR WORSE", Needs: "site"),
        ["omen"] = new("", "star", "A SIGN, GOOD OR ILL", Needs: "site"),
        ["spy"] = new("", "scroll", "SCOUT THE COUNTRY"),
        ["mend"] = new("", "heart", "AN OLD WOUND MENDS"),
        ["vigil"] = new("", "sun", "A TRIAL OF YOUR AURA", Needs: "site"),
        ["breakthrough"] = new("", "sun", "YOUR AURA BREAKS THROUGH"),
        ["rival"] = new("n", "heart", "YOUR RIVAL"),
        ["rival_bout"] = new("", "sword", "A BOUT IN THE LISTS"),
        ["rival_fate"] = new("", "scroll", "YOUR RIVAL'S FATE"),
        ["pact"] = new("", "fire", "THE PACT", LowGood: true),
        ["hollow_crown"] = new("", "crown", "THE HOLLOW CROWN"),
        ["burn_out"] = new("", "fire", "BURN OUT"),
        ["man_deed"] = new("", "scroll", "ON HIS RECORD", Needs: "man"),
        ["man_hurt"] = new("", "skull", "HE IS HURT", Needs: "man"),
        ["man_leaves"] = new("", "skull", "HE LEAVES", Needs: "man"),
        ["tie"] = new("", "heart", "BROTHERS NOW", Needs: "other"),
        ["mention"] = new("", "star", "MENTIONED", Needs: "man"),
    };

    /// An effect read: its verb, word, number, and whether it has the shape its verb wants.
    static (string Verb, string Arg, float N, bool Ok) ParseEffect(string fx)
    {
        var p = fx.Split(':');
        string verb = p[0];
        if (!Verbs.TryGetValue(verb, out var v)) return (verb, "", 0, false);
        static bool Num(string s, out float n) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out n);
        float n = 0;
        return v.Shape switch
        {
            "" => (verb, "", 0, p.Length == 1),
            "n" => (verb, "", Num(p.ElementAtOrDefault(1) ?? "", out n) ? n : 0, p.Length == 2 && Num(p[1], out _)),
            "a" => (verb, p.ElementAtOrDefault(1) ?? "", 0, p.Length == 2 && p[1] != ""),
            _ => (verb, p.ElementAtOrDefault(1) ?? "", p.Length == 3 && Num(p[2], out n) ? n : 0, p.Length == 3 && p[1] != "" && Num(p[2], out _)),
        };
    }

    /// What is wrong with an effect in this event, or null.
    static string? VerbProblem(string fx, EventDef e)
    {
        var (verb, arg, _, ok) = ParseEffect(fx);
        if (!Verbs.TryGetValue(verb, out var v)) return $"unknown effect {fx}";
        if (!ok) return $"effect {fx} wants {(v.Shape switch { "n" => "verb:number", "a" => "verb:word", "an" => "verb:word:number", _ => "no number" })}";
        bool atSite = e.Moments.Contains("site") || e.Cast.ContainsKey("site");
        if (v.Needs != "" && !(v.Needs == "site" ? atSite : e.Cast.ContainsKey(v.Needs))) return $"{verb} needs a {v.Needs} in the cast";
        if (verb == "bounty" && !e.Cast.ContainsKey("band") && !e.Cast.ContainsKey("settlement")) return "bounty needs a band or a settlement in the cast";
        if (verb == "skill" && Skills.All.All(s => s.Id != arg)) return $"unknown skill {arg}";
        if (verb == "buff" && !Effects.Has(arg)) return $"unknown effect {arg}";
        if (verb == "item" && Gear.Find(arg) == null) return $"unknown gear {arg}";
        return null;
    }

    /// An effect as a line on the page: icon, words, good or bad. Empty words for the hidden ones.
    public EventLine Describe(string fx, PendingEvent pe)
    {
        var (verb, arg, n, _) = ParseEffect(fx);
        if (!Verbs.TryGetValue(verb, out var v) || v.Hidden) return new("", "", 0);
        int sign = v.Shape is "n" or "an" ? (n == 0 ? 0 : (n > 0) != v.LowGood ? 1 : -1) : verb is "man_hurt" or "man_leaves" or "pact" or "desert" or "sick" or "disband" ? -1 : 1;
        string num = n.ToString(n % 1 == 0 ? "0" : "0.#", CultureInfo.InvariantCulture), signed = (n > 0 ? "+" : "") + num;
        string text = verb switch
        {
            "opinion" => $"{(CharacterById(pe.Lord)?.Name ?? "THE LORD").ToUpperInvariant()} {signed}",
            "opinion_companion" => $"{(CharacterById(pe.Companion)?.Name ?? "YOUR COMPANION").ToUpperInvariant()} {signed}",
            "sick" or "desert" => $"{num}% {v.Word}",
            "heal" => $"{num}% {v.Word}",
            "find" => $"ABOUT {num} CROWNS",
            "men" => $"{Scaled((int)n)} {v.Word}",
            "farsight" => $"SEE FAR FOR {num} DAYS",
            "gamble" => $"STAKE {num} CROWNS",
            "learn" => v.Word,
            "skill" => $"{(Skills.All.FirstOrDefault(s => s.Id == arg)?.Name ?? arg).ToUpperInvariant()} +{num}",
            "buff" => $"{(Effects.Has(arg) ? Effects.Get(arg).Name : arg).ToUpperInvariant()}{(n >= LastingDays / 2 ? "" : $", {num} DAYS")}",
            "item" => (Gear.Find(arg)?.Name ?? arg).ToUpperInvariant(),
            "rival" => $"{v.Word} {signed}",
            _ when v.Shape == "n" => $"{v.Word} {signed}",
            _ => v.Word,
        };
        if (verb == "buff" && Effects.Has(arg)) sign = Effects.Get(arg).Harmful ? -1 : 1;
        if (verb is "man_hurt" or "man_leaves" or "pact" or "desert" or "sick" or "disband" or "dread" or "corrupt") sign = -1;
        return new(v.Icon, text, sign);
    }

    /// Apply one effect; returns a short note for the page and the log when there is more to say than its line.
    string? ApplyEffect(string fx, PendingEvent pe)
    {
        var (verb, arg, n, _) = ParseEffect(fx);
        var s = pe.Settlement >= 0 ? SettlementById(pe.Settlement) : null;
        var lord = CharacterById(pe.Lord);
        var co = Player.Companies.FirstOrDefault(x => x.Id == pe.Company);
        string? said = SiteEffect(verb, n, pe, out bool siteVerb);
        if (siteVerb) return said;
        if (MenEffect(verb, pe) is string camp) return camp == "" ? null : camp;
        switch (verb)
        {
            case "gold": S.Gold = MathF.Max(0, S.Gold + n); break;
            case "renown": Captain.Renown = MathF.Max(0, Captain.Renown + n); break;
            case "food": Player.Food = MathF.Max(0, Player.Food + n); break;
            case "horses": Player.Horses = Math.Max(0, Player.Horses + (int)n); break;
            case "cohesion": foreach (var x in co != null ? new List<CompanyRecord> { co } : Player.Companies) x.Cohesion = Math.Clamp(x.Cohesion + n, 0, 30); break;
            case "xp": foreach (var x in co != null ? new List<CompanyRecord> { co } : Player.Companies) foreach (var st in x.Stacks) st.AddXp(st.Men * n); break;
            case "opinion": if (lord != null) Remember(lord, Me, n >= 0 ? "you did right by me" : "you crossed me", n); break;
            case "opinion_companion": if (CharacterById(pe.Companion) is Character k) Remember(k, Me, n >= 0 ? "you heard me out" : "you would not listen", n); break;
            case "opinion_liege": if (RulerOf(Captain.Contract) is Character r) Remember(r, Me, n >= 0 ? "you served me well" : "you failed me", n); break;
            case "opinion_realm":
                foreach (var l in S.Characters.Where(x => x.Kind == "lord" && !x.Dead && x.Faction == Captain.Contract))
                    l.Opinion[S.CaptainChar] = Math.Clamp(l.Opinion.GetValueOrDefault(S.CaptainChar) + n, -100, 100);
                break;
            case "unrest": if (s != null) s.Unrest = Math.Clamp(s.Unrest + n, 0, 100); break;
            case "prosperity": if (s != null) s.Prosperity = Math.Clamp(s.Prosperity + n, 0, 100); break;
            case "exhaustion":
                foreach (var w in S.Wars.Where(w => w.Active && (w.A == Captain.Contract || w.B == Captain.Contract))) AddExhaustion(w, Captain.Contract, n);
                break;
            case "calm": S.Omen = MathF.Max(0, S.Omen - n); break;
            case "dread": S.Omen += n; break;
            case "corrupt": Corrupt(n); break;
            case "flag": S.Flags[arg] = (float)S.Hours; break;
            case "unflag": S.Flags.Remove(arg); break;
            case "farsight": S.FarSightUntil = (float)S.Hours + n * 24; break;
            case "skill": Practise(arg, n); break;
            case "buff": AddBuff(arg, n); break;
            case "item": return Own(arg, from: EventOf(pe.Id) is EventDef ie ? Fill(ie.Title, pe) : "") != null ? null : "You have one already.";
            case "peace": return lord != null ? AcceptPeace(lord.Faction) : null;
            case "man": return GiftMen(1, SettlementNear(Player.Pos)) > 0 ? null : "You have no room for him.";
            case "sick" or "desert":
                int hit = 0;
                foreach (var st in Player.Companies.SelectMany(x => x.Stacks))
                {
                    int cut = Math.Min(st.Men - (st.Men > 1 ? 1 : 0), (int)MathF.Round(st.Men * n / 100));
                    if (cut <= 0) continue;
                    if (verb == "sick") { st.Men -= cut; st.Wounded += cut; } else st.Take(cut);
                    hit += cut;
                }
                return hit > 0 ? verb == "sick" ? $"{hit} men fall sick." : $"{hit} men slip away in the night." : "";
            case "reveal":
                var site = SiteById(pe.Site) is Site named && !SiteSeen(named) ? named
                    : Sites.Where(x => !SiteSeen(x)).OrderBy(x => Vector2.Distance(x.Pos, Player.Pos)).FirstOrDefault();
                if (site == null) return "";
                S.SitesSeen.Add(site.Id);
                return $"You learn the way to the {site.Name}, to the {Bearing(Player.Pos, site.Pos)}.";
            case "lists": if (s != null) { Lists = s; return "The heralds cry your name. You ride into the lists."; } break;
            case "gamble":
                // A stake of n crowns at dice: the charming read the table better.
                if (EventDice.Chance(0.4f + Skill("Charm") / 500f)) { S.Gold += n; return $"The dice love you tonight: you win {n:0} crowns."; }
                S.Gold = MathF.Max(0, S.Gold - n);
                return $"The dice go against you. {n:0} crowns gone.";
            case "quest": return lord != null ? GiveQuest(lord) : null;
            case "bounty": return pe.Party >= 0 ? Bounty(PartyById(pe.Party)) : s != null ? Bounty(Rebels(s)) : null;
            case "prisoners_free": FreePrisoners(); break;
            case "prisoners_recruit": foreach (var g in Player.Prisoners.ToList()) RecruitPrisoners(g); break;
            case "road_sword" or "road_lance" or "road_bow": return ChooseRoad(verb[5..]);
            case "crown": return s != null ? ClaimCrown(s) ?? $"{s.Name} is yours, and you are its king." : null;
            case "bury": Bury(pe.Grave); CompanionsReact("bury"); break;
            case "disband":
                if (s != null && Rebels(s) is Party reb) { reb.Down = true; S.Parties.Remove(reb); return "The pretender takes your gold and is gone."; }
                break;
            case "breakthrough": BreakThrough(pe.Id == "aura_grief" ? "In your rage the wall gives way." : "Your mourning is done, and you rise from it."); break;
            case "rival": if (CharacterById(Captain.Rival) is Character rv) Remember(rv, Me, n >= 0 ? "you were a brother to me" : "you looked down on me", n); break;
            case "rival_bout": return RivalBout();
            case "rival_fate": return RivalFate();
            case "pact": return TakePact();
            case "hollow_crown": return EndFlame(true);
            case "burn_out": return EndFlame(false);
        }
        return null;
    }
}
