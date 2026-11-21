using Microsoft.Xna.Framework;

namespace Ironfield;

/// BattleScreen, the fight view's own way to command (docs/archive/PLAYTEST.md PT4, "As rebuilt"). You have no cursor there, so:
/// number keys pick the arm that listens (1 foot, 2 bows, 3 horse, 4 mages, 0 all), and orders go where you look: charge
/// sends them at the company under the reticle, the hold key held down plants their line on the ground under it.
partial class BattleScreen
{
    /// Seconds the hold key has been down in the fight view (under 0: not held); a tap shorter than HoldTap is a plain hold.
    float holdHeld = -1;
    const float HoldTap = 0.25f, LookReach = 140;
    float lastCaptainClick;

    /// The order you just gave, shown over the arms in the fight view.
    void Prompt(string what) { hud.CombatPrompt = what; hud.CombatPromptAt = Ui.Now; }

    /// The ground under the reticle, no further than LookReach from the captain; null when you look at the sky.
    Vector2? LookGround()
    {
        var vp = GraphicsDevice.Viewport;
        var near = vp.Unproject(new Vector3(vp.Width / 2, vp.Height / 2, 0), proj, view, Matrix.Identity);
        var far = vp.Unproject(new Vector3(vp.Width / 2, vp.Height / 2, 1), proj, view, Matrix.Identity);
        if (far.Y >= near.Y - 0.001f * Vector3.Distance(near, far)) return null; // level or upward: no ground there
        var g = GroundUnder(vp.Width / 2, vp.Height / 2);
        var from = battle.Player.Pos;
        var d = g - from;
        return d.Length() > LookReach ? from + Vector2.Normalize(d) * LookReach : g;
    }

    /// The enemy company you look at in the fight view: the one with a man nearest the ground under the reticle, if your
    /// side can see it. The reach grows with the distance, so a far company is as easy to mark as a near one.
    Squad? LookFoe()
    {
        if (!battle.Player.Active || LookGround() is not Vector2 at) return null;
        float reach = 3 + Vector2.Distance(at, battle.Player.Pos) * 0.06f, bd = reach * reach;
        Unit? best = null;
        foreach (var u in battle.Units)
        {
            if (u.Team != 1 || u.Squad == null || !u.Active || u.Routing) continue;
            float d = Vector2.DistanceSquared(u.Pos, at);
            if (d < bd) { bd = d; best = u; }
        }
        return best?.Squad is { } s && battle.Shown(s) ? s : null;
    }

    /// A number key in the fight view: that arm listens (with the add key, as well as the others; again, it stops). 0: all.
    void ListenArm(int digit, bool add)
    {
        selectedDivision = null;
        if (digit == 0) { selected.Clear(); Prompt("ALL COMPANIES LISTEN"); host.Audio?.Play(Cue.Order, 0.4f); return; }
        int arm = digit - 1;
        if (arm > 3) return;
        var who = battle.Commanded.Where(s => s.Alive > 0 && s.Works == null && Hud.ArmOf(s) == arm).ToList();
        if (who.Count == 0) { Prompt($"YOU LEAD NO {Hud.ArmNames[arm]}"); return; }
        bool had = who.All(selected.Contains) && selected.Count(s => s.Alive > 0) == who.Count;
        bool drop = add ? who.All(selected.Contains) : had;
        if (!add) selected.Clear();
        if (drop) foreach (var s in who) selected.Remove(s);
        else foreach (var s in who) selected.Add(s);
        Prompt(selected.Count == 0 ? "ALL COMPANIES LISTEN" : hud.ListenWord() + ": LISTEN");
        host.Audio?.Play(Cue.Order, 0.4f);
    }

    /// The hold key in the fight view. Tapped: hold where you stand. Held and let go: draw up on the ground you look at,
    /// facing the enemy (the marker and the men's places show while it is held).
    void HoldKey()
    {
        if (ctl.Pressed(Act.HoldPosition)) { holdHeld = 0; return; }
        if (holdHeld < 0) return;
        if (ctl.Down(Act.HoldPosition)) { holdHeld += frameDt; return; }
        bool tap = holdHeld < HoldTap;
        holdHeld = -1;
        if (tap || LookGround() is not Vector2 at) { Command(Cmd.Hold); return; }
        if (Targets().Count == 0) return;
        OrderArrows(at);
        LineAt(at, battle.Centroid(1));
        host.Audio?.Play(Cue.HornHold, 0.65f);
        Prompt(hud.ListenWord() + ": HOLD THAT GROUND!");
        battle.Log((selected.Count == 0 ? "All troops" : string.Join(", ", Targets().Select(s => s.Name))) + ": hold that ground!");
    }

    /// The place being picked with the hold key: gold rings on the ground, and where each man would stand.
    void HoldMarker()
    {
        if (command || holdHeld < HoldTap || LookGround() is not Vector2 at) return;
        var enemy = battle.Centroid(1);
        Ring(at, 0.9f, Gold, 14, 1.4f);
        DashedRing(at, 1.8f, Gold * 0.8f);
        if (Targets().Count == 0) return;
        var (a, b) = LineEnds(at, enemy);
        foreach (var slot in PreviewSlots(a, b, enemy)) Ring(slot, 0.32f, Color.White * 0.95f, 6, 1f); // the men's places; no line, it ran from edge to edge
    }

    /// Put the listening companies in a formation outright (the deck's formation row, or the next one on the cycle key);
    /// a company that cannot take it (horse has no shield wall) keeps a plain line.
    void SetShape(Formation shape)
    {
        var list = Targets();
        if (list.Count == 0) return;
        foreach (var s in list)
        {
            if (s.CannotForm(shape) != null && s.Shapes(s.Units[0].Type.HasShield).Contains(shape)) continue; // not learned yet (TI1): keeps its shape
            var f = s.Choices(s.Units[0].Type.HasShield).Contains(shape) ? shape : Formation.Line;
            Send(s, q => q.Formation = f);
        }
        string word = shape switch
        {
            Formation.ShieldWall => "shield wall!", Formation.Square => "form square!", Formation.Wedge => "wedge!", Formation.Loose => "open ranks!", _ => "form line!",
        };
        host.Audio?.Play(Cue.Order, 0.6f);
        if (!command) Prompt(hud.ListenWord() + ": " + word.ToUpperInvariant());
        battle.Log((selected.Count == 0 ? "All troops" : string.Join(", ", list.Select(s => s.Name))) + ": " + word);
    }
}
