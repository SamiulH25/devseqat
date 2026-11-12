using Microsoft.Xna.Framework;

namespace Ironfield;

/// A company's fight right now (read by the HUD, Hud.Fights.cs): the foe, whether it is only shooting, how many men are at it
/// and where they meet.
public record struct Fight(Squad Foe, bool Shooting, int Engaged, Vector2 Front);

/// Battle: who a company is fighting, read from its men. Read only: the sim never calls it.
partial class Battle
{
    readonly Dictionary<Squad, (int N, int Melee, Vector2 Sum)> fightTally = new();

    /// The enemy company most of this company's men are at blows with (or shooting at), and where they meet.
    public Fight? FightOf(Squad s)
    {
        fightTally.Clear();
        foreach (var u in s.Units)
        {
            if (!u.Active || u.Routing || u.Target is not Unit t || !t.Active || t.Team == u.Team || t.Squad == null) continue;
            float d2 = Vector2.DistanceSquared(u.Pos, t.Pos), reach = u.Mounted || t.Mounted ? 6 : 4.5f;
            bool melee = d2 < reach * reach, shoot = !melee && u.Type.Range > 0 && d2 < u.Type.Range * u.Type.Range;
            if (!melee && !shoot) continue;
            fightTally.TryGetValue(t.Squad, out var e);
            fightTally[t.Squad] = (e.N + 1, e.Melee + (melee ? 1 : 0), e.Sum + (melee ? (u.Pos + t.Pos) / 2 : t.Pos));
        }
        if (fightTally.Count == 0) return null;
        Squad? foe = null;
        (int N, int Melee, Vector2 Sum) best = default;
        foreach (var kv in fightTally)
            if (foe == null || kv.Value.N > best.N) { foe = kv.Key; best = kv.Value; }
        if (best.N < Math.Max(1, s.Alive / 10)) return null; // a stray man or two is not the company's fight
        return new Fight(foe!, best.Melee * 2 < best.N, best.N, best.Sum / best.N);
    }
}
