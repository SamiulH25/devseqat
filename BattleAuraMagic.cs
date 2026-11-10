using Microsoft.Xna.Framework;

namespace Ironfield;

/// Battle: aura and magic together (docs/archive/MAGIC_AURA.md 5, 3.4, MA5). Your own blessings feed your aura; the foe's curses reach
/// you (aura armour halves them, tier 5 turns them); the lit blade or a thrown form breaks an enemy caster's working; the
/// relic raised once a battle; craft marks that make your blade leave fever.
public partial class Battle
{
    /// Aura a Blessing or War song gives you standing in it; how long an Evil eye quenches the blade; breath under Fever.
    public const float FeedAura = 10, QuenchTime = 6, FeverBreath = 0.6f;
    /// The relic raised: aura it gives (over the top into the surge) and the reach of its blessing.
    public const float RelicAura = 30, RelicRadius = 12;
    /// Until when the blade will not light (an Evil eye), and until when Fever takes your breath.
    public float QuenchedUntil = -1, FeverUntil = -1;
    /// You raised the relic this battle (the campaign spends a charge for it).
    public bool RelicRaised;
    public bool HasRelic => Options.Standing.Contains("relic");
    /// Craft marks (a craft rite): the lit blade leaves fever on the company of a man it strikes.
    public bool Marks => Options.Standing.Contains("craft_marks");
    /// The share of a curse that reaches you: all of it, half behind aura armour (tier 3+), none at tier 5.
    float CurseShare => AuraTier >= 5 ? 0 : AuraTier >= 3 ? 0.5f : 1;
    public bool Fevered => Time < FeverUntil;
    public bool Quenched => Time < QuenchedUntil;

    /// A buff of your side lands: a Blessing or War song over you feeds your aura.
    void FeedOnBuff(AbilityDef a, Vector2 at, int team)
    {
        if (AuraTier <= 0 || !Player.Active || team != Player.Team || a.Effect is not ("blessed" or "war_song")) return;
        if (Vector2.Distance(Player.Pos, at) > a.Radius + 2) return;
        Aura = MathF.Min(AuraMax, Aura + FeedAura);
        Flash("soak", Player.Pos, 1.5f, Player.Team, a.Tradition);
    }

    /// An enemy curse lands: if you stand in it, an Evil eye quenches your blade and Fever takes your breath.
    void CurseCaptain(AbilityDef a, Vector2 at, int team)
    {
        if (!Player.Active || team == Player.Team || a.Effect is not ("evil_eye" or "fever")) return;
        if (Vector2.Distance(Player.Pos, at) > a.Radius + 1) return;
        float k = CurseShare;
        if (k <= 0) { PlayerHit = ("YOUR AURA TURNS THE CURSE", Time); Flash("soak", Player.Pos, 2, Player.Team); return; }
        if (a.Effect == "evil_eye")
        {
            QuenchedUntil = MathF.Max(QuenchedUntil, Time + QuenchTime * k);
            AuraBlade = false;
            PlayerHit = (k < 1 ? "EVIL EYE: YOUR AURA HOLDS HALF OF IT" : "EVIL EYE: YOUR BLADE IS QUENCHED", Time);
        }
        else
        {
            FeverUntil = MathF.Max(FeverUntil, Time + a.Duration * k);
            PlayerHit = ("FEVER: YOUR BREATH FAILS", Time);
        }
    }

    /// Your lit blade or a thrown form strikes an enemy caster while he works a spell: the working breaks (his Will is spent).
    void BreakCast(Unit t)
    {
        if (t.Squad == null || t.Team == Player.Team) return;
        int n = Casts.RemoveAll(c => c.By == t.Squad && c.FireAt > Time && (c.Hero ? t.Hero : IsCaster(t)));
        if (n == 0) return;
        Flash("broken", t.Pos, 2.5f, t.Team);
        PlayerHit = ("WORKING BROKEN", Time);
        Log("You break the enemy's working!");
    }

    /// Your blow has landed with the blade lit: it breaks a working, and with craft marks leaves fever.
    void BladeMarks(Unit t)
    {
        BreakCast(t);
        if (Marks && t.Squad != null && t.Active && Abilities.Has("fever")) AddTimed(t.Squad, Abilities.Get("fever"), 0.6f);
    }

    /// Raise the relic (once a battle): a Blessing round you, and aura (over the top into the surge).
    internal string? CannotRaise(Unit p) => !HasRelic ? "YOU CARRY NO RELIC" : RelicRaised ? "THE RELIC IS RAISED ALREADY" : !p.Active ? "" : null;

    void RaiseRelic(Unit p)
    {
        if (CannotRaise(p) is string why) { if (why != "") PlayerHit = (why, Time); return; }
        RelicRaised = true;
        if (Abilities.Has("blessing"))
            foreach (var s in Squads)
                if (s.Team == p.Team && s.Alive > 0 && Vector2.Distance(s.Center, p.Pos) < RelicRadius + s.Radius) AddTimed(s, Abilities.Get("blessing"), 1);
        if (AuraTier > 0)
        {
            float over = MathF.Max(0, Aura + RelicAura - AuraMax);
            Aura = MathF.Min(AuraMax, Aura + RelicAura);
            Surge = MathF.Min(SurgeMax, Surge + over);
        }
        Flash("spell:buff", p.Pos, RelicRadius, p.Team, "faith");
        Sound(Cue.Holy, p.Pos);
        PlayerHit = ("THE RELIC IS RAISED", Time);
        Log("You raise the relic. The men around you take heart.");
    }
}
