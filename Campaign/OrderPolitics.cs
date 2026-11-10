using Microsoft.Xna.Framework;

namespace Ironfield;

/// The orders' people and politics (docs/archive/KNIGHT_ORDERS.md 4.6, 6.2, K6): your rival, grand masters who die and are chosen
/// (you may be), crusades against what an order hunts, and an order that rides with you once you lead it.
public sealed partial class Campaign
{
    /// A day's chance an old grand master dies; a host this big is worth a crusade; how long one lasts (days).
    public const float GrandMasterDeath = 1 / 2000f, CrusadeDays = 20;
    public int CrusadeHost => Scaled(40);

    void OrderPoliticsDaily()
    {
        foreach (var o in S.Orders.Where(o => !o.Gone))
        {
            if (CharacterById(o.GrandMaster) is Character gm && !gm.Dead && gm.Id != S.CaptainChar && ORng.Chance(GrandMasterDeath))
            {
                gm.Dead = true;
                Log($"{gm.Name} is dead. {KnightOrders.Get(o.Id).Short} will choose another.");
            }
            if (CharacterById(o.GrandMaster) is not { Dead: false }) Elect(o);
            Crusade(o);
        }
    }

    // ---- the rival (6.2) ----

    /// A squire sworn the same season as you: a short chain of events (meeting, the lists, his fate).
    void DrawRival(OrderDef def)
    {
        if (OrderById(def.Id) is not OrderState o) return;
        var r = MakeKnight(def, o.Patron, 0);
        r.Aura = 0;
        Captain.Rival = r.Id;
        S.Flags["rival_meet"] = (float)S.Hours;
    }

    /// A bout with your rival at the lists: your arms and aura against his.
    string RivalBout()
    {
        if (CharacterById(Captain.Rival) is not Character r) return "He is gone.";
        bool won = rng.Chance(0.4f + Skill("Arms") / 300f + 0.1f * (AuraInBattle - r.Aura));
        if (won)
        {
            Captain.Renown += 5;
            if (Captain.Order != "") RaiseStanding(Captain.Order, 3);
            Remember(r, Me, "you shamed me at the lists", -10);
            return $"You put {r.Name} on his back before the whole chapter.";
        }
        Captain.WoundedUntil = MathF.Max(Captain.WoundedUntil, (float)S.Hours + 24);
        Remember(r, Me, "you fought me fairly", 5);
        return $"{r.Name} beats you, and helps you up.";
    }

    /// The end of the chain: a friend rises beside you; a rival who hates you leaves for the Black Flame (K7 hunts him).
    string RivalFate()
    {
        if (CharacterById(Captain.Rival) is not Character r) return "He is gone.";
        if (OpinionOf(r, Me) >= 10)
        {
            r.OrderRank = 1;
            r.Aura = Math.Max(r.Aura, 1);
            if (Captain.Order != "") RaiseStanding(Captain.Order, 5);
            return $"{r.Name} is knighted the same day as your name is spoken in the chapter. He calls you brother.";
        }
        r.Order = "hollow";
        r.Aura = Math.Max(2, Captain.AuraTier);
        S.Flags["rival_fallen"] = (float)S.Hours;
        return $"{r.Name} has left the order in the night. They say he went looking for a faster way up. They say a black fire.";
    }

    // ---- grand masters (4.6) ----

    /// The commanders choose a new grand master: you, if you are a Knight-Commander with the order's trust (standing 80)
    /// and an aura no commander passes; else the commander with the strongest aura.
    void Elect(OrderState o)
    {
        var def = KnightOrders.Get(o.Id);
        var best = o.Commanders.Select(CharacterById).OfType<Character>().Where(k => !k.Dead).OrderByDescending(k => k.Aura).ThenBy(k => k.Id).FirstOrDefault();
        if (Captain.Order == o.Id && Captain.OrderRank >= 3 && o.Standing >= 80 && Captain.AuraTier >= (best?.Aura ?? 0))
        {
            o.GrandMaster = S.CaptainChar;
            Captain.OrderRank = 4;
            Captain.Renown += 30;
            Deed($"The commanders of {def.Short} choose you as their Grand Master.", "oaths");
            return;
        }
        if (best == null) { o.GrandMaster = MakeKnight(def, o.Patron, 4).Id; CharacterById(o.GrandMaster)!.Name = "Grand Master " + CharacterById(o.GrandMaster)!.Name; return; }
        o.GrandMaster = best.Id;
        best.OrderRank = 4;
        best.Aura = Math.Max(best.Aura, 3);
        if (Captain.Order == o.Id) Log($"{def.Short} choose {best.Name} as Grand Master.");
    }

    /// You lead your order: its armies ride with you.
    bool LeadsOrder(Party p) => p.KnightOrder != "" && p.KnightOrder == Captain.Order && Captain.OrderRank >= 4;

    // ---- crusades (4.6) ----

    /// An order calls a crusade against a great host of what it hunts (the dead, monsters, a brigand king); its armies march,
    /// and you are summoned if sworn. It ends when the host is broken or the time runs out.
    void Crusade(OrderState o)
    {
        var def = KnightOrders.Get(o.Id);
        if (o.CrusadeOn >= 0)
        {
            if (PartyById(o.CrusadeOn) is { Down: false } && S.Hours < o.CrusadeUntil) return;
            o.CrusadeOn = -1;
            return;
        }
        var host = S.Parties.Where(p => !p.Down && Hunted(def, p) && (p.Men >= CrusadeHost || p.Kind == PartyKind.Bandit && p.CharId >= 0))
            .OrderByDescending(p => p.Men).ThenBy(p => p.Id).FirstOrDefault();
        if (host == null) return;
        o.CrusadeOn = host.Id;
        o.CrusadeUntil = (float)S.Hours + CrusadeDays * 24;
        Log(Captain.Order == o.Id ? $"{def.Name} calls a crusade against {host.Name}, and summons you." : $"{def.Name} calls a crusade against {host.Name}.");
    }

    /// The crusade's host broken with you in the field: standing with every order that called it, and a deed in yours.
    void CrusadeWon(bool won, List<Party> foes)
    {
        if (!won) return;
        foreach (var o in S.Orders.Where(o => o.CrusadeOn >= 0 && foes.Any(f => f.Id == o.CrusadeOn)))
        {
            RaiseStanding(o.Id, 10);
            if (o.Id == Captain.Order) { Captain.OrderDeeds++; Deed($"You break {PartyById(o.CrusadeOn)?.Name ?? "the host"} in {KnightOrders.Get(o.Id).Short}'s crusade.", "oaths"); }
            o.CrusadeOn = -1;
        }
    }

    /// An order army's march when its order has one for it: the crusade's host, or you, its grand master. False: think as a lord.
    bool OrderMarches(Party p)
    {
        if (p.KnightOrder == "" || OrderById(p.KnightOrder) is not OrderState o) return false;
        if (o.CrusadeOn >= 0 && PartyById(o.CrusadeOn) is { Down: false } host && Rules.Strength(host) < Rules.Strength(p) * 2.5f)
        {
            p.Task = "crusade"; p.ChaseId = host.Id; p.Goal = host.Pos; p.Moving = true; p.TaskUntil = (float)S.Hours + 4;
            return true;
        }
        if (LeadsOrder(p) && Vector2.Distance(p.Pos, Player.Pos) > 6)
        {
            p.Task = "escort"; p.ChaseId = -1; p.Goal = Player.Pos; p.Moving = true; p.TaskUntil = (float)S.Hours + 2;
            return true;
        }
        return false;
    }
}
