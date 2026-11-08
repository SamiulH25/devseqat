using Microsoft.Xna.Framework;

namespace Ironfield;

/// Battle: sight (docs/TERRAIN.md T3). Every half second each company is checked against the other side's companies:
/// a crest or a deep wood between them hides it, and men inside a wood are seen only close up or when they shoot.
/// What a side has not seen it cannot shoot at, and a company that strikes from hiding is an ambush.
public partial class Battle
{
    float sightTimer;
    bool sightStarted;
    /// How long a sighting lasts, and how near you must be to see men in a wood (or anyone, when fighting them).
    const float SightMemory = 3, WoodSight = 25, CloseSight = 12;

    /// Sight is on in field battles; duels, tournaments and test fields see everything.
    bool SightOn => !scripted && !Options.Duel && !Options.Tournament;

    /// Has `team` seen this company lately? Your own side always knows where it is.
    public bool Visible(Squad? s, int team) => s == null || s.Team == team || !SightOn || Time - s.SeenAt[team] < SightMemory;
    bool Visible(Unit u, int team) => u.IsLeader || Visible(u.Squad, team);

    void UpdateSight(float dt)
    {
        if (!SightOn || (sightTimer -= dt) > 0) return;
        sightTimer = 0.5f;
        for (int team = 0; team < 2; team++)
        {
            int foe = 1 - team;
            var eyes = Squads.Where(w => w.Team == foe && w.Alive > 0).Select(w => w.Center).ToList();
            var leader = foe == 0 ? Player : EnemyCaptain;
            if (leader.Active) eyes.Add(leader.Pos);
            foreach (var s in Squads)
            {
                if (s.Team != team || s.Alive == 0) continue;
                if (!eyes.Any(e => Sees(e, s))) continue;
                bool fresh = Time - s.SeenAt[foe] > 6;
                s.SeenAt[foe] = Time;
                s.LastSeen = s.Center;
                if (fresh && sightStarted) Ambush(s);
            }
        }
        sightStarted = true;
    }

    /// Can a man standing at `eye` see company s?
    bool Sees(Vector2 eye, Squad s)
    {
        float d = MathF.Max(0, Vector2.Distance(eye, s.Center) - s.Radius);
        if (d < CloseSight) return true;
        if (d > Weather.Vision) return false;
        if (Time - s.LooseAt < 2) return true; // a volley gives you away
        if (Terrain.CoverAt(s.Center) >= 0.5f && d > WoodSight) return false;
        return ClearLine(eye, s.Center);
    }

    /// Nothing between: no crest above the line of sight, and not too much wood or village along it.
    internal bool ClearLine(Vector2 a, Vector2 b)
    {
        float len = Vector2.Distance(a, b);
        if (len < 1) return true;
        var dir = (b - a) / len;
        float h0 = Terrain.Height(a) + 2, h1 = Terrain.Height(b) + 1.5f, cover = 0;
        bool inside = Terrain.CoverAt(a) > 0.3f; // a man in a wood looks out past the trees round him
        for (float s = 4; s < len - 8; s += 5)
        {
            var p = a + dir * s;
            if (Terrain.Height(p) > MathHelper.Lerp(h0, h1, s / len)) return false; // a crest in the way
            float c = Terrain.CoverAt(p);
            if (inside) { inside = c > 0.3f; continue; }
            if ((cover += c * 5) > 15) return false;                                 // about 27 m of wood
        }
        return true;
    }

    /// A company that was hidden and is found within a few strides of the enemy: the men it falls on are shaken.
    void Ambush(Squad s)
    {
        int foe = 1 - s.Team, shaken = 0;
        foreach (var u in Near(s.Center, s.Radius + 15, nearBrace))
            if (u.Team == foe && u.Active && !u.IsLeader && !u.Routing) { u.Morale -= 15; shaken++; }
        if (shaken < 3) return;
        Log(s.Team == 0 ? "Your men burst from cover!" : "Ambush! The enemy bursts from cover!");
        Sound(Cue.Shout, s.Center);
    }
}
