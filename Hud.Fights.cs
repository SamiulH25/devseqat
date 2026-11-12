using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// Hud: who your companies are fighting and how it goes, so a melee reads as fights, not one blot. Each company's fight is
/// read from its men: the enemy company most of them are striking (or shooting), and where they meet. How it goes is the
/// men each side lost in the last ten seconds, tipped by morale: WINNING, EVEN or LOSING. Drawn as a crossed-swords mark
/// at the meeting place with the word (and in command view a line from the company to it and on to the foe), a chip over
/// the card and a line in the tooltip. Drawing only.
sealed partial class Hud
{
    /// How far back "how it goes" looks, in battle seconds.
    const float FightWindow = 10;

    readonly Dictionary<Squad, Fight> fights = new();
    readonly Dictionary<Squad, List<(float At, int Alive)>> alive = new();
    Battle? fightsOf;
    float fightsAt = -99, fightSampledAt = -99;

    /// Once a frame: each of your companies' fight, and every half second each company's men standing (for the losses).
    void SampleFights()
    {
        if (fightsOf != B || B.Time < fightsAt) { alive.Clear(); fightsOf = B; fightsAt = -99; fightSampledAt = -99; }
        if (B.Time - fightsAt >= 0.5f)
        {
            fightsAt = B.Time;
            foreach (var s in B.Squads)
            {
                if (!alive.TryGetValue(s, out var h)) alive[s] = h = new();
                h.Add((B.Time, s.Alive));
                h.RemoveAll(x => x.At < B.Time - FightWindow - 1);
            }
        }
        if (B.Time - fightSampledAt >= 0.2f || fightSampledAt < 0)
        {
            fightSampledAt = B.Time;
            fights.Clear();
            foreach (var s in B.Squads)
                if (s.Team == 0 && s.Alive > 0 && B.FightOf(s) is Fight f) fights[s] = f;
        }
    }

    /// Men this company lost (killed, fled or broken) in the last ten seconds.
    int Lost(Squad s)
    {
        if (!alive.TryGetValue(s, out var h) || h.Count == 0) return 0;
        return Math.Max(0, h.FirstOrDefault(x => x.At >= B.Time - FightWindow, h[^1]).Alive - s.Alive);
    }

    /// How the fight goes, as a word and its colour.
    (string Word, Color Col) Standing(Squad s, Fight f)
    {
        if (f.Shooting) return ("SHOOTING", Gold);
        int ours = Lost(s), theirs = Lost(f.Foe);
        float score = (theirs - ours) / MathF.Max(4, ours + theirs) + (AvgMorale(s.Units) - AvgMorale(f.Foe.Units)) / 200f;
        return score > 0.2f ? ("WINNING", Ok) : score < -0.2f ? ("LOSING", Bad) : ("EVEN", Warn);
    }

    /// The tooltip's line: who, how it goes, and the losses behind the word.
    string FightLine(Squad s, Fight f)
    {
        string foe = f.Foe.Name.ToUpperInvariant();
        return f.Shooting ? $"SHOOTING AT {foe}" : $"FIGHTING {foe}: {Standing(s, f).Word}   LOST {Lost(s)}, THEY LOST {Lost(f.Foe)} (10 S)";
    }

    /// Crossed swords where each of your companies meets its foe, with how it goes. In command view a line runs from the company
    /// to the fight and on to the foe (dashed for arrows); in the field only fights within 90 m show, fading with distance.
    void FightMarks()
    {
        var vp = new Viewport(0, 0, W, H);
        Vector2? At(Vector2 p, float lift)
        {
            var v = vp.Project(new Vector3(p.X, B.Terrain.Height(p) + lift, p.Y), Proj, View, Matrix.Identity);
            return v.Z is > 0 and < 1 ? new Vector2(v.X, v.Y) : null;
        }
        foreach (var (s, f) in fights)
        {
            float k = 1;
            if (!Command)
            {
                float d = Vector2.Distance(f.Front, B.Player.Pos);
                if (d > 90 || d < 4) continue; // too far to matter, or your own fight (the foe's bar is over him)
                k = d < 50 ? 1 : 1 - (d - 50) / 40;
            }
            var (word, col) = Standing(s, f);
            if (At(f.Front, 2.4f) is not Vector2 mark) continue;
            if (Command && At(s.Centroid(), 0.6f) is Vector2 from && At(f.Foe.Centroid(), 0.6f) is Vector2 to)
            {
                if (f.Shooting) Dashed(from, to, col * 0.8f, U(1));
                else
                {
                    Line(from, mark, Friendly * 0.85f, U(2));
                    Line(mark, to, Foe * 0.6f, U(1));
                }
            }
            Swords(mark, col * k, f.Shooting);
            if (Command && (Selected.Contains(s) || s == HoverSquad)) word += " VS " + f.Foe.Name.ToUpperInvariant(); // the chosen ones name their foe
            int bw = TW(word, 1) + U(6);
            Rect(new Rectangle((int)mark.X - bw / 2, (int)mark.Y + U(10), bw, U(10)), Color.Black * (0.55f * k));
            TextC(word, mark.X, mark.Y + U(11), col * k, 1);
        }
    }

    /// Two crossed blades (or, for arrows, a single shaft and head), with a dark edge.
    void Swords(Vector2 c, Color col, bool arrow)
    {
        float r = U(8);
        void Pair(Color cc, float w, Vector2 o)
        {
            if (arrow)
            {
                Line(c + o + new Vector2(-r, r), c + o + new Vector2(r, -r), cc, w);
                Line(c + o + new Vector2(r, -r), c + o + new Vector2(r * 0.2f, -r), cc, w);
                Line(c + o + new Vector2(r, -r), c + o + new Vector2(r, -r * 0.2f), cc, w);
                return;
            }
            Line(c + o + new Vector2(-r, -r), c + o + new Vector2(r, r), cc, w);
            Line(c + o + new Vector2(r, -r), c + o + new Vector2(-r, r), cc, w);
            Line(c + o + new Vector2(-r * 0.9f, -r * 0.3f), c + o + new Vector2(-r * 0.3f, -r * 0.9f), cc, w); // the hilts
            Line(c + o + new Vector2(r * 0.9f, -r * 0.3f), c + o + new Vector2(r * 0.3f, -r * 0.9f), cc, w);
        }
        Pair(Color.Black * (col.A / 255f * 0.8f), U(5), Vector2.Zero);
        Pair(col, U(3), Vector2.Zero);
    }

    void Dashed(Vector2 a, Vector2 b, Color c, float w)
    {
        float len = Vector2.Distance(a, b), dash = U(6);
        if (len < 1) return;
        var dir = (b - a) / len;
        for (float t = 0; t < len; t += dash * 2) Line(a + dir * t, a + dir * MathF.Min(len, t + dash), c, w);
    }
}
