using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Ironfield;

/// TitleScreen: the loading screen. The title's dusk carries on while a campaign is made (the same company still on the
/// march, the eye drifting along with it): the name, the sword under it as the measure of the work (the blade takes the
/// light from the hilt out, sparks where the light ends), what is being made, and a scroll with a word of counsel.
sealed partial class TitleScreen
{
    /// One a load. Rules of the game as the tips tell them; no keys (bindings change).
    static readonly string[] Counsel =
    {
        "MEN FIGHTING DOWNHILL HIT HARDER, AND A CREST HIDES WHAT STANDS BEHIND IT.",
        "WOUNDED MEN HEAL OVER THE DAYS THAT FOLLOW A BATTLE, FASTER WHILE THE COMPANY RESTS IN A TOWN.",
        "UPKEEP IS PAID EACH WEEK. UNPAID MEN DESERT.",
        "A WOOD SLOWS HORSE, BREAKS FORMATIONS AND STOPS ARROWS.",
        "TIRED MEN FIGHT BADLY. RELIEVE THEM BEFORE THEY BREAK.",
        "DEEP WATER IS SLOW, TIRING AND BREAKS YOUR RANKS. CROSS BY THE FORD OR THE BRIDGE.",
        "STAKES STOP HORSE. YOUR MEN ON FOOT SLIP BETWEEN THEM.",
        "EACH VICTORY ADDS TO YOUR RENOWN, AND RENOWN RAISES THE NUMBER OF MEN YOU MAY LEAD.",
    };
    static readonly string[] Dots = { "", ".", "..", "..." };

    string loadTitle = "";
    float loadBar, loadAt;
    int counsel;

    /// A load begins: `what` is its name ("A NEW CAMPAIGN").
    public void BeginLoading(string what)
    {
        loadTitle = what;
        loadBar = 0;
        loadAt = time;
        counsel = titleShots != null ? 0 : Random.Shared.Next(Counsel.Length);
        AudioEngine.Current?.Play(Cue.ScrollUnroll, 0.65f);
    }

    /// A step of the loading screen: the picture lives on and the light on the blade creeps toward `goal`, the share of the
    /// work the host knows is behind it (1 once it is done: the light then runs out to the point). True when the blade is lit.
    public bool Loading(float dt, float goal)
    {
        time += dt;
        ms = Mouse.GetState();
        loadBar += (goal - loadBar) * (1 - MathF.Exp(-(goal >= 1 ? 14 : 0.5f) * dt));
        return loadBar > 0.99f;
    }

    public void DrawLoading(string stage)
    {
        BeginPicture();
        float t = time - loadAt;
        Scene(Q(6) * MathF.Min(t, 20));
        int x = U(64), ty = (int)(ui.H * 0.115f), k = Math.Max(U(9), ui.H / 60), sy = ty + 7 * k + U(14);
        Logo(x, ty, k);
        Sword(x, sy, 53 * k, loadBar);
        Spaced(loadTitle, x + U(40), sy + U(20), Kit.Parchment, 2, U(5));
        if (stage != "")
        {
            ui.Text(stage, x + U(40), sy + U(42), Kit.Parchment * 0.7f, 1);
            ui.Text(Dots[Ui.Instant ? 3 : (int)(t * 2.5f) % 4], x + U(40) + ui.TW(stage, 1), sy + U(42), Kit.Parchment * 0.7f, 1);
        }
        var parch = new Rectangle(x, (int)(ui.H * 0.43f), U(250), 0);
        var lines = ui.Wrap(Counsel[counsel], parch.Width - U(34));
        int lh = U(12);
        parch.Height = (lines.Count + 1) * lh + U(54);
        int shown = ui.Scroll(parch, Ui.Instant ? 1 : Ease.Out(t / 0.7f)), ly = parch.Y + U(26);
        if (ly + lh - U(4) <= shown) ui.Ink("A WORD OF COUNSEL", parch.Center.X, ly, Kit.Ink, 1, cx: true);
        ly += lh + U(6);
        foreach (var line in lines) { if (ly + lh - U(4) <= shown) ui.Ink(line, parch.X + U(17), ly, Kit.InkDeep, 1); ly += lh; }
        ui.End();
    }

    /// Where the light on the blade ends: a white-hot point in its glow, sparks leaping off it and falling.
    void Flare(int x, int y)
    {
        float pulse = Ui.Instant ? 1 : 0.8f + 0.2f * MathF.Sin(time * 9) * MathF.Sin(time * 5.3f);
        var at = new Vector2(x, y);
        ui.Disc(at, U(22) * pulse, Kit.Ember * 0.07f); ui.Disc(at, U(12) * pulse, Kit.Ember * 0.16f); ui.Disc(at, U(6) * pulse, Flame * 0.4f);
        ui.Disc(at, U(2.5f), new Color(255, 250, 230));
        for (int i = 0; i < 9; i++)
        {
            float life = 0.6f + 0.5f * Ease.Hash(i * 7 + 1), t = (time + i * 0.37f) % life / life;
            float vx = (Ease.Hash(i * 13 + 5) - 0.35f) * U(46), vy = -(0.3f + Ease.Hash(i * 29 + 2)) * U(30);
            R(x + vx * t, y + vy * t + U(60) * t * t, u, u, Color.Lerp(Flame, Kit.Ember, t) * (1 - t));
        }
    }
}
