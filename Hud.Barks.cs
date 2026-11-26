using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// Restrained world-space battle flavor bubbles & acoustic voice bridge (docs/VOICE_DIALOGUE.md):
/// - Hard screen cap: max 1 world bubble in Fight View, max 2 in Command View.
/// - Center-screen reticle safe zone in Fight View (never blocks directional combat or telegraphs).
/// - Short lifetime (2.6s), crisp single-line flavor pills; Key callouts stay on the left edge.
partial class Hud
{
    int spokenAudioIdx;
    Battle? spokenAudioBattle;

    /// Play acoustic voice for newly arrived `B.Spoken` lines (strictly presentation-side, replay-safe).
    void PumpSpokenAudio()
    {
        if (spokenAudioBattle != B) { spokenAudioBattle = B; spokenAudioIdx = B.Spoken.Count; return; }
        int mode = Settings.Current.BattleBarks;
        while (spokenAudioIdx < B.Spoken.Count)
        {
            var said = B.Spoken[spokenAudioIdx++];
            if (mode == 0 || (mode == 1 && !said.Key)) continue;
            if (B.Time - said.Time > 1.5f) continue;

            float pan = 0f;
            var u = SpeakerUnit(said);
            if (u != null && B.Player.Active)
                pan = Math.Clamp((u.Pos.X - B.Player.Pos.X) / 35f, -0.75f, 0.75f);

            var profile = VoiceProfile.ForSoldier(
                said.Soldier != 0 ? said.Soldier : Rng.Of(said.Name),
                said.Culture, said.Nature, said.Sergeant, shout: said.Key);
            AudioEngine.Current?.Speak(profile, said.Text, said.Mood, pan, volume: said.Key ? 0.92f : 0.72f, priority: said.Key, maxWords: 7);
        }
    }

    Unit? SpeakerUnit(Said said)
    {
        if (said.Squad == null) return null;
        if (said.Soldier != 0)
            return said.Squad.Units.FirstOrDefault(u => u.Active && u.SoldierId == said.Soldier);
        return said.Squad.Units.FirstOrDefault(u => u.Active && (u.IsLeader || u.Hero || u.Sergeant))
            ?? said.Squad.Units.FirstOrDefault(u => u.Active);
    }

    /// Draw restrained floating flavor bubbles above speaking units (never takes over the screen).
    void DrawWorldBarks()
    {
        PumpSpokenAudio();
        if (ShowResult || Deploying || Settings.Current.BattleBarks < 2) return;

        int maxWorld = Command ? 2 : 1;
        var vp = new Viewport(0, 0, W, H);
        // Center-screen safe zone in Fight View so combat reticle & weapon arcs stay completely unobstructed
        var safe = new Rectangle((int)(W * 0.36f), (int)(H * 0.31f), (int)(W * 0.28f), (int)(H * 0.38f));

        int drawn = 0;
        foreach (var said in B.Spoken.AsEnumerable().Reverse())
        {
            if (drawn >= maxWorld) break;
            if (said.Key) continue; // Key callouts render cleanly on the left edge instead of floating over melee
            float age = B.Time - said.Time;
            if (age < 0 || age > 2.6f) continue;

            var u = SpeakerUnit(said);
            if (u == null) continue;

            float lift = u.Mounted ? 3.9f : 2.9f;
            var sp = vp.Project(new Vector3(u.Pos.X, B.Terrain.Height(u.Pos) + lift, u.Pos.Y), Proj, View, Matrix.Identity);
            if (sp.Z < 0f || sp.Z > 1f) continue;

            float alpha = age < 0.15f ? age / 0.15f : age > 2.15f ? Math.Clamp((2.6f - age) / 0.45f, 0f, 1f) : 1f;
            string line = said.Text.Length > 52 ? said.Text[..49] + "..." : said.Text;
            string label = $"{said.Name.ToUpperInvariant()}: \"{line.ToUpperInvariant()}\"";

            int padX = U(8), ph = U(18);
            int pw = Math.Min(U(320), TW(label, 1) + padX * 2);
            label = kit.Fit(label, pw - padX * 2);
            int bx = Math.Clamp((int)sp.X - pw / 2, U(14), W - pw - U(14));
            int by = Math.Max(U(14), (int)sp.Y - ph - U(8));

            var rect = new Rectangle(bx, by, pw, ph);
            if (!Command && rect.Intersects(safe))
                rect.Y = Math.Max(U(14), safe.Y - ph - U(10));

            Color accent = said.Team == 1 ? Foe : Gold;
            Rect(rect, Color.Black * (0.76f * alpha));
            Color edge = accent * (0.65f * alpha);
            Rect(new Rectangle(rect.X, rect.Y, rect.Width, 1), edge);
            Rect(new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), edge);
            Rect(new Rectangle(rect.X, rect.Y, 1, rect.Height), edge);
            Rect(new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), edge);
            Text(label, rect.X + padX, rect.Y + U(5), Color.Lerp(accent, Color.White, 0.72f) * alpha, 1);
            drawn++;
        }
    }
}
