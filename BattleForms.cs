using Microsoft.Xna.Framework;

namespace Ironfield;

/// A thrown form on its way (an aura slash, a rain of light): it lands at `At` if you still stand. The screen draws it.
public sealed record PendingForm(FormDef F, float Started, float At, Vector2 From, Vector2 Dir, float Power, bool Foe = false);

/// Battle: the captain's forms (docs/KNIGHT_ORDERS.md 5.6, K2). Three slots and a great form, pressed by the player,
/// paid in aura. Each kind is a small rule here; buffs and fields go through the spell engine (BattleAbilities.cs).
public partial class Battle
{
    /// Your form slots: 0-2 the three forms (U, I, O), 3 the great form (L). Null = empty.
    public readonly FormDef?[] FormSlot = new FormDef?[4];
    readonly float[] formReady = new float[4];
    /// Each form's uses this battle (they rank up on the map) and the Black Flame's corruption raised (K7).
    public readonly Dictionary<string, int> FormUses = new();
    public float CorruptionRaised;
    public readonly List<PendingForm> PendingForms = new();
    int greatUses;
    /// The great form once a battle; twice for a Transcendent (aura tier 5, K5).
    bool greatUsed => greatUses >= (AuraTier >= 5 ? 2 : 1);
    float focusUntil, focusK, guardUntil, counterUntil, counterK, edgeK;
    int edgeLeft;

    /// Seconds until slot i is ready (0 = ready).
    public float FormWait(int i) => MathF.Max(0, formReady[i] - Time);
    public bool GreatUsed => greatUsed;
    public bool Guarding => Time < guardUntil;
    public bool Countering => Time < counterUntil;
    public bool Focused => Time < focusUntil;
    public int EdgeLeft => edgeLeft;

    void SetupForms()
    {
        int n = 0;
        foreach (var id in Options.Forms)
        {
            var f = id == Forms.OwnId && Options.OwnForm != null ? Options.OwnForm : Forms.Has(id) ? Forms.Get(id) : null;
            if (f == null) continue;
            if (Options.FormAspects.TryGetValue(id, out var asp) && Forms.Aspect(asp) is { } a && a.Kind == f.Kind) f = a.On(f); // its aspect (MA6)
            if (f.Great) FormSlot[3] ??= f;
            else if (n < 3) FormSlot[n++] = f;
        }
    }

    /// A form's strength by its rank (1-3 from use on the map): +15% a rank.
    float FormPower(FormDef f) => 1 + 0.15f * (Math.Clamp(Options.FormRanks.GetValueOrDefault(f.Id, 1), 1, 3) - 1);

    /// Why slot i cannot be used now, or null.
    public string? CannotForm(int i)
    {
        var f = FormSlot[i];
        var p = Player;
        if (f == null) return "NO FORM IN THAT SLOT";
        if (!p.Active) return "YOU ARE DOWN";
        if (AuraTier < f.Tier) return $"NEEDS AURA TIER {f.Tier}";
        if (f.Great && greatUsed) return "THE GREAT FORM IS SPENT";
        if (Time < formReady[i]) return $"{f.Name.ToUpperInvariant()} IN {formReady[i] - Time:0}";
        if (Aura + Surge < f.Aura) return "NOT ENOUGH AURA";
        if (p.Action is UnitAction.Windup or UnitAction.Recover or UnitAction.Stagger) return "BUSY";
        if (f.Kind == "shot" && (p.Type.Ammo == 0 || p.Ammo <= 0)) return "TAKE UP A BOW";
        if (f.Kind == "leap" && p.Mounted && f.Id != Forms.OwnId) return "NOT FROM THE SADDLE"; // your own Charge leaps from the saddle (MA6)
        return null;
    }

    void UpdateForms(Unit p, PlayerInput inp)
    {
        if (inp.Form1) TryForm(p, 0);
        if (inp.Form2) TryForm(p, 1);
        if (inp.Form3) TryForm(p, 2);
        if (inp.Great) TryForm(p, 3);
        if (inp.Relic) RaiseRelic(p);
        for (int i = PendingForms.Count - 1; i >= 0; i--)
            if (Time >= PendingForms[i].At && !PendingForms[i].Foe) { var pf = PendingForms[i]; PendingForms.RemoveAt(i); if (p.Active) Land(p, pf); }
    }

    void TryForm(Unit p, int i)
    {
        if (CannotForm(i) is string why) { PlayerHit = (why, Time); return; }
        var f = FormSlot[i]!;
        float fromSurge = MathF.Min(Surge, f.Aura); // the surge goes first (MA3)
        Surge -= fromSurge;
        Aura -= f.Aura - fromSurge;
        formReady[i] = Time + f.Cooldown;
        if (f.Great) greatUses++;
        FormUses[f.Id] = FormUses.GetValueOrDefault(f.Id) + 1;
        CorruptionRaised += f.Corruption;
        PlayerHit = (f.Name.ToUpperInvariant() + "!", Time);
        Sound(Cue.Shout, p.Pos);
        if (f.Great) Log($"{f.Name}!");
        Use(p, f, FormPower(f));
    }

    void Use(Unit p, FormDef f, float k)
    {
        Flash("form:" + f.Kind, p.Pos, f.Kind is "burst" or "shout" or "buff" or "drain" ? f.Radius : 1, p.Team, f.Order, p.Dir);
        if (f.Great || f.Kind is "burst" or "shout" or "buff") Sound(Cue.Aura, p.Pos);
        switch (f.Kind)
        {
            case "focus": p.Stamina = 100; focusUntil = Time + f.Duration; focusK = f.Damage * k; break;
            case "guard": guardUntil = Time + f.Duration * k; break;
            case "edge": edgeLeft = f.Count; edgeK = f.Damage * k; break;
            case "counter": counterUntil = Time + f.Duration; counterK = f.Damage * k; break;
            case "slash": case "volley": PendingForms.Add(new PendingForm(f, Time, Time + f.Warn, p.Pos, p.Dir, k)); break;
            case "shout": Shout(p, f, k); break;
            case "leap": Leap(p, f, k); break;
            case "shot": AuraShot(p, f, k); break;
            case "burst": foreach (var t in FoesNear(p.Pos, f.Radius, f.MaxHit, p.Team)) FormBlow(p, t, f, k); Sound(Cue.Impact, p.Pos); break;
            case "drain": Drain(p, f, k); break;
            case "buff":
                foreach (var s in Squads)
                    if (s.Team == p.Team && s.Alive > 0 && Vector2.Distance(s.Center, p.Pos) < f.Radius + s.Radius) AddTimed(s, AsAbility(f), k);
                foreach (var u in Units)
                    if (u.Active && u.Team == p.Team && u.Routing && u.Routs <= 1 && Vector2.DistanceSquared(u.Pos, p.Pos) < f.Radius * f.Radius)
                    { u.Routing = false; u.Morale = MathF.Max(u.Morale, RallyAt + 10); }
                break;
            case "burn": AddField(AsAbility(f), f.Effect, p.Team, p, p.Pos + p.Dir * f.Range, f.Radius, f.Duration, k, f.Dps); break;
        }
    }

    /// A form as a spell, for the spell engine's timed effects and fields.
    static AbilityDef AsAbility(FormDef f) => new() { Id = f.Id, Name = f.Name, Icon = f.Icon, Line = f.Line, Kind = f.Kind, Effect = f.Effect, Duration = f.Duration, Radius = f.Radius, Tradition = f.Order == "hollow" ? "necromancy" : "arms" };

    /// The foes within r of a spot, nearest first, at most n.
    List<Unit> FoesNear(Vector2 at, float r, int n, int team) =>
        Near(at, r + 0.5f, nearKill).Where(t => t.Active && t.Team != team && Vector2.DistanceSquared(t.Pos, at) <= r * r)
            .OrderBy(t => Vector2.DistanceSquared(t.Pos, at)).ThenBy(t => t.Index).Take(n).ToList();

    /// A form's blow: aura goes through most of a man's armour and no guard holds it.
    void FormBlow(Unit p, Unit t, FormDef f, float k, float scale = 1)
    {
        if (p == Player && f.Kind is "slash" or "leap") BreakCast(t); // a thrown form breaks a working (MA5)
        Damage(p, t, f.Damage * k * scale, f.Shock, 0.5f);
        if (t.Active) Stagger(t, 0.4f);
    }

    /// A thrown form lands: a slash down a narrow cone, a volley on a spot ahead.
    void Land(Unit p, PendingForm pf)
    {
        var f = pf.F;
        if (f.Kind == "shot") return; // already struck; the streak was for the screen
        if (f.Kind == "volley")
        {
            var at = pf.From + pf.Dir * f.Range;
            foreach (var t in FoesNear(at, f.Radius, f.MaxHit, p.Team)) FormBlow(p, t, f, pf.Power);
            Sound(Cue.Impact, at);
            Flash("land:volley", at, f.Radius, p.Team, f.Order);
            return;
        }
        var hits = Near(pf.From + pf.Dir * (f.Range / 2), f.Range / 2 + 1, nearKill)
            .Where(t => t.Active && t.Team != p.Team && InCone(pf.From, pf.Dir, t.Pos, f.Range, f.Radius))
            .OrderBy(t => Vector2.DistanceSquared(t.Pos, pf.From)).ThenBy(t => t.Index).Take(f.MaxHit).ToList();
        foreach (var t in hits) FormBlow(p, t, f, pf.Power);
        Sound(Cue.Clash, pf.From + pf.Dir * 2);
        Flash("land:slash", pf.From, f.Range, p.Team, f.Order, pf.Dir);
    }

    /// Within a cone from `from` along `dir`: ahead, out to `range`, and no further aside than `width` plus a quarter of the way out.
    static bool InCone(Vector2 from, Vector2 dir, Vector2 at, float range, float width)
    {
        var d = at - from;
        float along = Vector2.Dot(d, dir);
        if (along < 0.2f || along > range) return false;
        float aside = MathF.Abs(d.X * dir.Y - d.Y * dir.X);
        return aside <= width + along * 0.25f;
    }

    /// Within a straight band of half-width `width` from `from` along `dir`, out to `range`.
    static bool InLine(Vector2 from, Vector2 dir, Vector2 at, float range, float width)
    {
        var d = at - from;
        float along = Vector2.Dot(d, dir);
        return along >= 0.5f && along <= range && MathF.Abs(d.X * dir.Y - d.Y * dir.X) <= width;
    }

    void Shout(Unit p, FormDef f, float k)
    {
        foreach (var t in FoesNear(p.Pos, f.Radius, 60, p.Team))
        {
            t.Morale -= f.Shock * k;
            if (Vector2.DistanceSquared(t.Pos, p.Pos) < 16) Stagger(t, 0.5f);
        }
        if (f.Effect != "")
            foreach (var s in Squads)
                if (s.Team != p.Team && s.Alive > 0 && Vector2.Distance(s.Center, p.Pos) < f.Radius + s.Radius) AddTimed(s, AsAbility(f), k);
    }

    /// Spring onto the foe nearest your facing within range; a heavy blow on landing, those near him reel.
    void Leap(Unit p, FormDef f, float k)
    {
        Unit? best = null;
        float bs = float.MaxValue;
        foreach (var t in Near(p.Pos, f.Range + 1, nearKill))
        {
            if (!t.Active || t.Team == p.Team) continue;
            var d = t.Pos - p.Pos;
            float l = d.Length();
            if (l < 1 || l > f.Range || Vector2.Dot(d / l, p.Dir) < 0.75f) continue;
            if (Wall != null && SiegeWall.Inside(t.Pos) != SiegeWall.Inside(p.Pos)) continue; // no leaping the wall
            float s = l * (2 - Vector2.Dot(d / l, p.Dir));
            if (s < bs) { bs = s; best = t; }
        }
        if (best == null)
        {
            if (p != Player) { FoeAura += f.Aura; return; }
            Aura += f.Aura; formReady[Array.IndexOf(FormSlot, f)] = Time; PlayerHit = ("NO FOE TO LEAP AT", Time); return; // nothing spent
        }
        var dir = Vector2.Normalize(best.Pos - p.Pos);
        var from = p.Pos;
        p.Pos = best.Pos - dir * 1.1f;
        Flash("land:leap", p.Pos, Vector2.Distance(from, p.Pos), p.Team, f.Order, dir);
        p.Facing = MathF.Atan2(dir.Y, dir.X);
        FormBlow(p, best, f, k);
        foreach (var t in FoesNear(p.Pos, f.Radius, 8, p.Team)) if (t != best) Stagger(t, 0.6f);
        Sound(Cue.Impact, p.Pos);
    }

    /// An arrow of aura down your facing: through up to MaxHit men within a narrow line.
    void AuraShot(Unit p, FormDef f, float k)
    {
        p.Ammo--;
        var hits = Near(p.Pos + p.Dir * (f.Range / 2), f.Range / 2 + 1, nearKill)
            .Where(t => t.Active && t.Team != p.Team && InLine(p.Pos, p.Dir, t.Pos, MathF.Min(f.Range, Weather.Vision), f.Radius))
            .OrderBy(t => Vector2.DistanceSquared(t.Pos, p.Pos)).ThenBy(t => t.Index).Take(f.MaxHit).ToList();
        foreach (var t in hits) FormBlow(p, t, f, k);
        PendingForms.Add(new PendingForm(f, Time, Time + 0.25f, p.Pos, p.Dir, 0)); // the streak, for the screen only
    }

    /// Tear the life out of the men around you: half of it comes back to you as health and aura.
    void Drain(Unit p, FormDef f, float k)
    {
        float took = 0;
        foreach (var t in FoesNear(p.Pos, f.Radius, f.MaxHit, p.Team)) took += Damage(p, t, f.Damage * k, f.Shock, 0.8f);
        p.Hp = MathF.Min(p.Type.MaxHp, p.Hp + took * 0.5f);
        Aura = MathF.Min(AuraMax, Aura + took * 0.5f);
    }

    /// Your swing's power with Focus.
    float FocusEdge => Time < focusUntil ? 1 + focusK : 1;

    /// A parry under Measured Blade strikes back.
    void CounterStrike(Unit p, Unit a)
    {
        if (Time < counterUntil && a.Active) Damage(p, a, p.Type.Damage * counterK, 3, 0.3f);
    }
}
