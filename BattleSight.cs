using Microsoft.Xna.Framework;

namespace Ironfield;

/// Battle: sight (docs/archive/TERRAIN.md T3). Every half second each company is checked against the other side's companies:
/// a crest or a deep wood between them hides it, and men inside a wood are seen only close up or when they shoot.
/// What a side has not seen it cannot shoot at, and a company that strikes from hiding is an ambush.
public partial class Battle
{
    float sightTimer;
    readonly float[] ambushLogged = { -99, -99 };
    bool sightStarted;
    /// How long a sighting lasts, and how near you must be to see men in a wood (or anyone, when fighting them).
    const float SightMemory = 4, WoodSight = 30, CloseSight = 30;
    /// How far a company inside a bank of called mist is seen (MA8).
    public const float MistSight = 25;
    bool InMist(Vector2 p) => Fields.Any(f => f.Effect?.Id == "mist" && Vector2.DistanceSquared(f.Pos, p) < f.Radius * f.Radius);

    /// Sight is on in field battles; duels, tournaments and test fields see everything.
    bool SightOn => !scripted && !Options.Duel && !Options.Tournament;

    /// Has `team` seen this company lately? Your own side always knows where it is.
    public bool Visible(Squad? s, int team) => s == null || s.Team == team || !SightOn || Time - s.SeenAt[team] < SightMemory;
    bool Visible(Unit u, int team) => u.IsLeader || Visible(u.Squad, team);
    /// Off: every enemy company is drawn on the field, flags and map, seen or not. On: unseen ones are hidden from you.
    public static bool HideUnseen = false;
    /// Is this company drawn for you? Sight still decides what your side can shoot at and when an ambush strikes.
    public bool Shown(Squad? s) => !HideUnseen || Visible(s, 0);

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
                bool keen = foe == 0 && Perk("Tactician"); // an eye for ground: you see further, and into the edge of a wood
                if (!eyes.Any(e => Sees(e, s, keen))) continue;
                bool fresh = Time - s.SeenAt[foe] > 6;
                s.SeenAt[foe] = Time;
                s.LastSeen = s.Center;
                if (fresh && sightStarted) Ambush(s);
            }
        }
        sightStarted = true;
    }

    /// Can a man standing at `eye` see company s? A keen eye (the Tactician perk) sees a quarter further.
    bool Sees(Vector2 eye, Squad s, bool keen = false)
    {
        float d = MathF.Max(0, Vector2.Distance(eye, s.Center) - s.Radius), far = keen ? 1.25f : 1;
        if (d > MistSight && InMist(s.Center)) return false; // a company in the mist is seen only close (docs/archive/MAGIC_AURA.md 3.5)
        if (d < CloseSight) return true;
        if (d > Weather.Vision * far) return false;
        if (Time - s.LooseAt < 2) return true; // a volley gives you away
        if (Terrain.CoverAt(s.Center) >= 0.5f && d > WoodSight * far) return false;
        float through = keen ? 19 : 15;
        if (ClearLine(eye, s.Center, through)) return true;
        // A wide company is seen if either end of its line is in sight, not only its middle.
        var side = new Vector2(-s.Front.Y, s.Front.X) * s.Radius * 0.8f;
        return ClearLine(eye, s.Center + side, through) || ClearLine(eye, s.Center - side, through);
    }

    /// Nothing between: no crest above the line of sight, and not too much wood or village along it.
    internal bool ClearLine(Vector2 a, Vector2 b, float through = 15)
    {
        float len = Vector2.Distance(a, b);
        if (len < 1) return true;
        var dir = (b - a) / len;
        float h0 = Terrain.Height(a) + 2, h1 = Terrain.Height(b) + 1.8f, cover = 0; // eyes over heads and spear points
        bool inside = Terrain.CoverAt(a) > 0.3f; // a man in a wood looks out past the trees round him
        for (float s = 4; s < len - 8; s += 5)
        {
            var p = a + dir * s;
            if (Terrain.Height(p) > MathHelper.Lerp(h0, h1, s / len) + 0.5f) return false; // a crest in the way (a hump of half a metre does not hide men)
            float c = Terrain.CoverAt(p);
            if (inside) { inside = c > 0.3f; continue; }
            if ((cover += c * 5) > through) return false;                            // about 27 m of wood
        }
        return true;
    }

    /// A company that was hidden and is found within a few strides of the enemy: the men it falls on are shaken.
    void Ambush(Squad s)
    {
        int foe = 1 - s.Team, shaken = 0;
        float shock = s.Team == 0 && Perk("Ambusher") ? 25 : 15; // your ambushes strike harder with the Ambusher perk
        foreach (var u in Near(s.Center, s.Radius + 15, nearBrace))
            if (u.Team == foe && u.Active && !u.IsLeader && !u.Routing) { u.Morale -= shock; shaken++; }
        if (shaken < 3 || Time - ambushLogged[s.Team] < 5) return; // one cry for companies breaking cover together
        ambushLogged[s.Team] = Time;
        Log(s.Team == 0 ? "Your men burst from cover!" : "Ambush! The enemy bursts from cover!");
        Sound(Cue.Shout, s.Center);
    }

    // ---- the spells left out (docs/archive/MAGIC_AURA.md 3.5, MA8) ----

    /// A mirage stands this far to the side of the company it shields, across the line to the enemy.
    public const float MirageOffset = 15;

    /// Where a mirage shimmers: beside the spot, square to the way the enemy lies.
    Vector2 MirageSpot(Vector2 at, int team)
    {
        var toFoe = centroid[1 - team] - at;
        var side = toFoe.LengthSquared() > 0.01f ? new Vector2(-toFoe.Y, toFoe.X) / toFoe.Length() : Vector2.UnitX;
        return at + side * MirageOffset;
    }

    /// The enemy mirage a bowman of this side would loose at instead: the nearest within his reach, if any.
    Field? MirageFor(Unit u) => Fields.Where(f => f.Effect?.Id == "mirage" && f.Team != u.Team && Vector2.Distance(f.Pos, u.Pos) < u.Type.Range)
        .OrderBy(f => Vector2.DistanceSquared(f.Pos, u.Pos)).FirstOrDefault();

    /// A working cast on this spot by `team` breaks on an enemy Ward over it (faith answers craft; the Will is spent).
    bool Warded(Vector2 at, int team) => Fields.Any(f => f.Effect?.Id == "warded" && f.Team != team && Vector2.DistanceSquared(f.Pos, at) < f.Radius * f.Radius);
}
