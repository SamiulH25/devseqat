using Microsoft.Xna.Framework;

namespace Ironfield;

/// Live, incremental AI-vs-AI battles on the campaign map: parties lock in place, trade casualties over campaign hours
/// rather than vanishing in a single tick, and can be reinforced by nearby lords or joined mid-fight by the player.
public partial class Campaign
{
    public MapSkirmish? SkirmishOf(Party p) => p.BattleId < 0 ? null : S.Skirmishes.FirstOrDefault(s => s.Id == p.BattleId);

    void StartSkirmish(Party a, Party b)
    {
        var sideA = Comrades(a).Where(p => p.BattleId < 0 && !p.Down).ToList();
        var sideB = Comrades(b).Where(p => p.BattleId < 0 && !p.Down && !sideA.Contains(p)).ToList();
        if (sideA.Count == 0 || sideB.Count == 0) return;

        var sk = new MapSkirmish
        {
            Id = S.NextId++,
            Pos = (a.Pos + b.Pos) * 0.5f,
            Seed = Rng.Mix(S.Seed, a.Id, b.Id, (int)(S.Hours * 4)),
            EdgeA = 1 + B.AuraWeight * ChampionAura(a),
            EdgeB = Edge(b),
        };
        foreach (var p in sideA) AddToSkirmish(sk, p, 0);
        foreach (var p in sideB) AddToSkirmish(sk, p, 1);
        S.Skirmishes.Add(sk);
    }

    void AddToSkirmish(MapSkirmish sk, Party p, int team)
    {
        if (p.BattleId >= 0 || p.Down) return;
        p.BattleId = sk.Id;
        p.Moving = false;
        p.ChaseId = -1;
        p.Goal = sk.Pos;
        p.Pos = Vector2.Lerp(p.Pos, sk.Pos, 0.35f);
        if (team == 0) { sk.SideA.Add(p.Id); sk.StartMenA += p.Men; }
        else { sk.SideB.Add(p.Id); sk.StartMenB += p.Men; }
        foreach (var c in p.Companies)
            foreach (var st in c.Stacks)
                if (st.Men > 0)
                    sk.Stacks[$"{c.Id}:{st.Troop}"] = new SkirmishStack { Start = st.Men };
    }

    void JoinSkirmish(MapSkirmish sk, Party p, int team)
    {
        foreach (var m in Comrades(p))
            if (m.BattleId < 0 && !m.Down && !sk.SideA.Contains(m.Id) && !sk.SideB.Contains(m.Id))
                AddToSkirmish(sk, m, team);
    }

    void StepSkirmishes(float h)
    {
        if (S.Skirmishes.Count == 0) return;
        // Nearby free AI parties can ride into an ongoing skirmish to reinforce their side.
        float joinR2 = B.JoinRadius * Map.Spread * B.JoinRadius * Map.Spread;
        foreach (var sk in S.Skirmishes)
        {
            var a0 = sk.SideA.Select(PartyById).FirstOrDefault(p => p != null && !p.Down);
            var b0 = sk.SideB.Select(PartyById).FirstOrDefault(p => p != null && !p.Down);
            if (a0 == null || b0 == null) continue;
            foreach (var p in S.Parties)
            {
                if (p.Kind == PartyKind.Player || p.Down || p.BattleId >= 0 || p.Cooldown > 0 || p.Lair || p.Voyage.Count > 0) continue;
                if (Vector2.DistanceSquared(p.Pos, sk.Pos) > joinR2) continue;
                if (OnDuty(p) && (Rabble(a0) || Rabble(b0))) continue;
                if (Hostile(p, b0) && !Hostile(p, a0)) JoinSkirmish(sk, p, 0);
                else if (Hostile(p, a0) && !Hostile(p, b0)) JoinSkirmish(sk, p, 1);
            }
        }

        foreach (var sk in S.Skirmishes.ToList())
            StepOneSkirmish(sk, h);
    }

    void StepOneSkirmish(MapSkirmish sk, float h)
    {
        var sideA = sk.SideA.Select(PartyById).Where(p => p != null && !p.Down).Cast<Party>().ToList();
        var sideB = sk.SideB.Select(PartyById).Where(p => p != null && !p.Down).Cast<Party>().ToList();
        if (sideA.Count == 0 || sideB.Count == 0) { CancelSkirmish(sk); return; }

        sk.Hours += h;
        sk.Step++;
        var (w0, w1) = Rules.AutoWeights(sideA, sideB, sk.EdgeA, sk.EdgeB);
        float r0 = ((Rng.Mix(sk.Seed, sk.Step, 1) & 0xFFFF) / 65535f) * 0.24f + 0.88f;
        float r1 = ((Rng.Mix(sk.Seed, sk.Step, 2) & 0xFFFF) / 65535f) * 0.24f + 0.88f;
        float p0 = w0 * r0, p1 = w1 * r1, sum = MathF.Max(1f, p0 + p1);

        // Paced so small clashes finish in ~2-4h, blowouts/caravan ambushes quickly, and large lord battles in ~6-12h.
        bool blowout = MathF.Max(p0, p1) / sum > 0.75f || sideA.Any(p => p.Kind == PartyKind.Caravan) || sideB.Any(p => p.Kind == PartyKind.Caravan);
        float scale = (blowout ? 0.65f : 0.24f) * h;
        InflictSkirmishStep(sk, sideA, scale * (p1 / sum));
        InflictSkirmishStep(sk, sideB, scale * (p0 / sum));

        int menA = SkirmishRemaining(sk, sideA), menB = SkirmishRemaining(sk, sideB);
        bool brokeA = menA == 0 || (sk.Hours >= 1f && menA <= sk.StartMenA * 0.35f);
        bool brokeB = menB == 0 || (sk.Hours >= 1f && menB <= sk.StartMenB * 0.35f);
        if (brokeA || brokeB || sk.Hours >= 24f)
        {
            int winner = brokeA && !brokeB ? 1 : brokeB && !brokeA ? 0 : p0 >= p1 ? 0 : 1;
            FinishSkirmish(sk, sideA, sideB, winner);
        }
    }

    static int SkirmishRemaining(MapSkirmish sk, List<Party> side)
    {
        int rem = 0;
        foreach (var p in side)
            foreach (var c in p.Companies)
                foreach (var st in c.Stacks)
                    rem += sk.Stacks.TryGetValue($"{c.Id}:{st.Troop}", out var rec) ? Math.Max(0, rec.Start - rec.Fallen) : st.Men;
        return rem;
    }

    void InflictSkirmishStep(MapSkirmish sk, List<Party> side, float lossFrac)
    {
        var stacks = side.SelectMany(p => p.Companies).Where(c => c.Men > 0)
            .SelectMany(c => c.Stacks.Where(st => st.Men > 0).Select(st => (c, st))).ToList();
        if (stacks.Count == 0) return;
        float weight = MathF.Max(1f, stacks.Sum(x => x.st.Men / (float)x.st.Type.Tier));
        int totalMen = stacks.Sum(x => x.st.Men);
        float want = totalMen * Math.Clamp(lossFrac, 0.01f, 0.35f);
        foreach (var (c, st) in stacks)
        {
            float share = want * (st.Men / (float)st.Type.Tier) / weight;
            int hit = Math.Min(st.Men, (int)MathF.Floor(share) + (((Rng.Mix(sk.Seed, sk.Step, c.Id, st.Men) & 0xFFFF) / 65535f) < (share - MathF.Floor(share)) ? 1 : 0));
            if (hit <= 0) continue;
            if (sk.Stacks.TryGetValue($"{c.Id}:{st.Troop}", out var rec)) rec.Fallen = Math.Min(rec.Start, rec.Fallen + hit);
            // Keep at least 1 man in the company's live stack until FinishSkirmish/CancelSkirmish resolves so rosters never hold empty companies mid-fight.
            int keep = c.Men + c.Wounded - hit <= 0 ? 1 : 0;
            st.Men = Math.Max(keep, st.Men - hit);
            if (st.Xp > st.PoolCap) st.Xp = st.PoolCap;
        }
    }

    void FinishSkirmish(MapSkirmish sk, List<Party> sideA, List<Party> sideB, int winner)
    {
        var results = new List<CompanyResult>();
        var kills = new float[2];
        for (int team = 0; team < 2; team++)
        {
            var side = team == 0 ? sideA : sideB;
            foreach (var p in side)
            {
                p.BattleId = -1;
                foreach (var c in p.Companies)
                {
                    var parts = new List<StackResult>();
                    foreach (var st in c.Stacks)
                    {
                        if (!sk.Stacks.TryGetValue($"{c.Id}:{st.Troop}", out var rec) || rec.Start <= 0) continue;
                        st.Men = rec.Start; // restore pre-skirmish headcount so Apply() handles wounds, captives, XP & loot once
                        int fallen = Math.Min(rec.Start, rec.Fallen);
                        int fled = team == winner ? 0 : (int)((rec.Start - fallen) * 0.15f);
                        parts.Add(new StackResult(st.Troop, rec.Start, rec.Start - fallen - fled, fled, fallen));
                    }
                    if (parts.Count == 0) continue;
                    int start = parts.Sum(x => x.Start), fallenAll = parts.Sum(x => x.Fallen), fledAll = parts.Sum(x => x.Fled);
                    kills[1 - team] += fallenAll;
                    results.Add(new CompanyResult(team, c.Id, start, start - fallenAll - fledAll, fledAll, fallenAll, 0, new int[8], true, parts,
                        Routed: team != winner && fledAll + fallenAll > start / 2));
                }
            }
        }
        foreach (var cr in results)
        {
            var opp = cr.Team == 0 ? sideB : sideA;
            int totalOpp = Math.Max(1, opp.Sum(p => p.Men));
            cr.KillsByTier[1] = (int)MathF.Round(kills[cr.Team] * cr.Start / MathF.Max(1, (cr.Team == 0 ? sideA : sideB).Sum(p => p.Men)));
        }
        S.Skirmishes.Remove(sk);
        var r = new BattleResult(winner, 0, results, LeaderFate.Unhurt, LeaderFate.Unhurt, 0);
        AiBattles++;
        Apply(r, sideA, sideB, "");
        var w = winner == 0 ? sideA[0] : sideB[0];
        var l = winner == 0 ? sideB[0] : sideA[0];
        if (w.Kind == PartyKind.Lord || l.Kind == PartyKind.Lord)
            LogAll($"{w.Name} defeats {l.Name} near {Map.ProvinceAt(l.Pos).Name}.");
    }

    internal void CancelSkirmish(MapSkirmish sk)
    {
        foreach (int id in sk.SideA.Concat(sk.SideB))
            if (PartyById(id) is Party p && p.BattleId == sk.Id) p.BattleId = -1;
        S.Skirmishes.Remove(sk);
    }
}
