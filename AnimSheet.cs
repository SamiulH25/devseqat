using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// `IRONFIELD_ANIM=/dir`: draws soldier, mount, and beast animations as strips of frames on a plain board and quits:
/// one row per clip, eight frames a row, rendered through Puppet3D + Unit3D + PixelPost at 2x pixel scale. Written twice,
/// from three-quarter front (`anim-front.png`) and from the side (`anim-side.png`), for judging motion without playing.
/// Drawing only: the units here are never in a battle.
sealed class AnimSheet
{
    const int Cols = 8, PixelScale = 2, HalfW = 84, HalfH = 104, CellW = HalfW * PixelScale, CellH = HalfH * PixelScale;

    readonly GraphicsDevice gd;
    readonly Shading? sh;
    readonly Puppet puppet;
    readonly Puppet3D? puppet3D;
    readonly Verts<VertexPositionColorTexture> sprites = new();
    readonly Verts<VertexPNCT> units3D = new();
    readonly AlphaTestEffect fx;
    readonly BasicEffect flat;

    AnimSheet(GraphicsDevice gd, Art art, Shading? sh)
    {
        this.gd = gd;
        this.sh = sh;
        puppet = new Puppet(art, sprites);
        var kit = sh != null ? MeshKit3D.TryLoad() : null;
        if (kit != null) puppet3D = new Puppet3D(kit, units3D);
        fx = new AlphaTestEffect(gd) { Texture = art.Atlas, VertexColorEnabled = true, AlphaFunction = CompareFunction.Greater, ReferenceAlpha = 128 };
        flat = new BasicEffect(gd) { VertexColorEnabled = true };
    }

    /// One clip: its name and how to set the unit (and the clock) for frame k of n.
    record Clip(string Name, string Troop, Action<Unit, float> Set, string? Horse = null, float Span = 0);

    static Clip Swing(string name, string troop, AttackDir dir) => new(name, troop, (u, k) =>
    {
        // The first three frames wind up, the rest are the blow and the recovery.
        u.Swing = dir;
        if (k < 3 / 8f) { u.Action = UnitAction.Windup; u.ActionTimer = u.Type.Windup * (1 - (k + 1 / 8f) / (3 / 8f)); }
        else { u.Action = UnitAction.Recover; u.ActionTimer = u.Type.Recover * (1 - (k - 3 / 8f) / (5 / 8f)); }
    });

    static readonly Clip[] Clips =
    {
        new("walk", "men_at_arms", (u, k) => { u.Vel = u.Dir * 1.6f; u.Anim = k * 2; }),
        new("run", "men_at_arms", (u, k) => { u.Vel = u.Dir * 4.6f; u.Anim = k * 2; }),
        new("idle", "men_at_arms", (u, k) => { }, Span: 6),
        Swing("sword right", "men_at_arms", AttackDir.Right),
        Swing("sword overhead", "men_at_arms", AttackDir.Overhead),
        Swing("sword thrust", "men_at_arms", AttackDir.Thrust),
        Swing("great right", "greatswords", AttackDir.Right),
        Swing("great overhead", "greatswords", AttackDir.Overhead),
        Swing("pike thrust", "pikemen", AttackDir.Thrust),
        Swing("spear+shield", "spearrunners", AttackDir.Thrust),
        new("block", "men_at_arms", (u, k) => { u.Action = k < 0.5f ? UnitAction.Block : UnitAction.Stagger; u.HitFlash = k >= 0.5f ? 0.15f * (1 - (k - 0.5f) * 2) : 0; }),
        new("bow", "bowmen", (u, k) => { u.Ammo = 10; u.EnemyDist = 20; if (k > 0.75f) { u.Action = UnitAction.Recover; u.ActionTimer = 0.25f * (1 - (k - 0.75f) * 4); } else u.Cooldown = 1.1f * (1 - k / 0.75f); }),
        new("crossbow", "crossbowmen", (u, k) => { u.Ammo = 10; u.EnemyDist = 20;
            if (k < 0.5f) u.Cooldown = 2.0f - k * 1.5f;
            else if (k < 0.75f) u.Cooldown = 1.0f - (k - 0.5f) / 0.25f;
            else { u.Action = UnitAction.Recover; u.ActionTimer = 0.25f * (1 - (k - 0.75f) * 4); } }, Span: 2.0f),
        new("fall", "men_at_arms", (u, k) => { u.Dead = true; u.DiedAt = 0; u.DiedFall = Fall.Cut; u.DiedFrom = u.Pos + u.Dir; }, Span: 0.72f),
        new("rider gallop", "paladin_knights", (u, k) => { u.Vel = u.Dir * 8.6f; u.Anim = k; u.Couched = true; }, Horse: "warhorse", Span: 1.0f),
        new("rider cut", "reaver_horsemen", (u, k) => { u.Vel = u.Dir * 6.2f; u.Anim = k; u.Swing = AttackDir.Right; u.Action = k < 3 / 8f ? UnitAction.Windup : UnitAction.Recover;
            u.ActionTimer = k < 3 / 8f ? u.Type.Windup * (1 - (k + 1 / 8f) / (3 / 8f)) : u.Type.Recover * (1 - (k - 3 / 8f) / (5 / 8f)); }, Horse: "warhorse", Span: 1.0f),
        new("wolf", "wolf", (u, k) => { u.Vel = u.Dir * 6.5f; u.Anim = k; if (k >= 0.5f && k < 0.75f) { u.Action = UnitAction.Windup; u.ActionTimer = u.Type.Windup * (1 - (k - 0.5f) * 4); }
            else if (k >= 0.75f) { u.Action = UnitAction.Recover; u.ActionTimer = u.Type.Recover * (1 - (k - 0.75f) * 4); } }, Span: 1.0f),
        new("bear", "bear", (u, k) => { u.Vel = u.Dir * (k < 0.375f ? 3.5f : 0.4f); u.Anim = k; if (k >= 0.375f && k < 0.625f) { u.Action = UnitAction.Windup; u.ActionTimer = u.Type.Windup * (1 - (k - 0.375f) * 4); }
            else if (k >= 0.625f) { u.Action = UnitAction.Recover; u.ActionTimer = u.Type.Recover * (1 - (k - 0.625f) / 0.375f); } }, Span: 1.2f),
        new("spider", "spider", (u, k) => { u.Vel = u.Dir * 4.5f; u.Anim = k; if (k >= 0.5f && k < 0.75f) { u.Action = UnitAction.Windup; u.ActionTimer = u.Type.Windup * (1 - (k - 0.5f) * 4); }
            else if (k >= 0.75f) { u.Action = UnitAction.Recover; u.ActionTimer = u.Type.Recover * (1 - (k - 0.75f) * 4); } }, Span: 1.0f),
        new("wyvern", "wyvern", (u, k) => { u.Altitude = 1.05f; u.Vel = u.Dir * 7f; u.Anim = k; if (k >= 0.5f && k < 0.75f) { u.Action = UnitAction.Windup; u.ActionTimer = u.Type.Windup * (1 - (k - 0.5f) * 4); }
            else if (k >= 0.75f) { u.Action = UnitAction.Recover; u.ActionTimer = u.Type.Recover * (1 - (k - 0.75f) * 4); } }, Span: 1.2f),
    };

    record ShowcaseItem(string Label, string Body, string Legs, string Head, int Palette, bool Tabard = false, string Face = "man", string Shield = "none", string Weapon = "none", bool Plume = false, bool Cape = false);

    static readonly ShowcaseItem[] Armors =
    {
        new("Tunic", "tunic", "cloth", "cap", 0),
        new("Gambeson", "gambeson", "cloth", "kettle", 0),
        new("Gambeson + Fur", "gambeson_fur", "cloth", "furcap", 4),
        new("Leather Jerkin", "leather", "cloth", "brigand_hat", 3),
        new("Jupon", "jupon", "mail", "sallet", 1),
        new("Mail + Surcoat", "mail", "mail", "nasal", 0, Tabard: true),
        new("Mail Hauberk", "mail_hauberk", "mail", "coif", 0),
        new("Mail + Fur", "mail_fur", "mail", "spangen", 4),
        new("Coat of Plates", "coatofplates", "mail", "kettle", 1),
        new("Brigandine", "brigandine", "mail", "sallet", 1),
        new("Gilded Brigandine", "brigandine_gilded", "plate", "sallet_crest", 1),
        new("Half-Plate", "breastplate", "plate", "morion", 7),
        new("Mirror Armor", "mirror", "mail", "turban", 5),
        new("Gothic Plate", "plate", "plate", "sallet_crest", 0),
        new("Fluted Plate", "plate_fluted", "plate", "armet", 0),
        new("Dark Plate", "plate_dark", "plate", "greathelm_horns", 1),
        new("Jousting Plate", "plate_lance", "plate", "bascinet", 0),
        new("Gilded Plate", "plate_gilded", "plate", "greathelm_sun", 2, Tabard: true),
        new("Plate + Surcoat", "plate_surcoat", "plate", "greathelm", 8, Tabard: true),
        new("Plate + Fur", "plate_fur", "plate", "spangen_horns", 4),
        new("Lamellar", "lamellar", "cloth", "turban", 5),
        new("Gilded Lamellar", "lamellar_gilded", "mail", "tug", 6),
        new("Dark Lamellar", "lamellar_dark", "mail", "spike_turban", 5),
        new("Scale Hauberk", "scale", "mail", "spangen", 6),
        new("Iron Scale", "scale_iron", "plate", "bascinet", 7),
        new("Northern Fur", "fur", "cloth", "bearhead", 4),
        new("Steppe Deel", "deel", "cloth", "steppe", 6),
        new("Deel + Fur", "deel_fur", "cloth", "furcap", 6),
        new("Desert Kaftan", "kaftan", "cloth", "spike_turban", 5),
        new("Caster Robe", "robe", "cloth", "hood", 0),
    };

    static readonly ShowcaseItem[] PlateShowcase =
    {
        new("Half-Plate", "breastplate", "plate", "morion", 7),
        new("Gothic Plate", "plate", "plate", "sallet_crest", 0),
        new("Fluted Maximilian", "plate_fluted", "plate", "armet", 0),
        new("Jousting Lance-Plate", "plate_lance", "plate", "bascinet", 0),
        new("Dark Iron Plate", "plate_dark", "plate", "greathelm_horns", 1),
        new("Gilded Royal Plate", "plate_gilded", "plate", "greathelm_sun", 2),
        new("Plate + Surcoat", "plate_surcoat", "plate", "greathelm", 8, Tabard: true),
        new("Nordic Plate + Fur", "plate_fur", "plate", "spangen_horns", 4),
        new("Gilded Brigandine", "brigandine_gilded", "plate", "barbute", 1),
        new("Iron Scale Plate", "scale_iron", "plate", "sallet", 7),
    };

    static readonly ShowcaseItem[] Helms =
    {
        new("Cap", "gambeson", "cloth", "cap", 0),
        new("Hood", "robe", "cloth", "hood", 0),
        new("Fur Cap", "deel", "cloth", "furcap", 6),
        new("Brigand Hat", "gambeson", "cloth", "brigand_hat", 3),
        new("Mail Coif", "mail", "mail", "coif", 0),
        new("Kettle", "mail", "mail", "kettle", 0),
        new("Nasal", "mail", "mail", "nasal", 0),
        new("Spike", "mail", "mail", "spike", 1),
        new("Spangen", "fur", "mail", "spangen", 4),
        new("Spangen Horns", "fur", "mail", "spangen_horns", 4),
        new("Bearhead", "fur", "cloth", "bearhead", 4),
        new("Sallet", "brigandine", "plate", "sallet", 1),
        new("Sallet Crest", "plate", "plate", "sallet_crest", 1),
        new("Barbute", "plate", "plate", "barbute", 7),
        new("Morion", "breastplate", "plate", "morion", 7),
        new("Bascinet", "plate", "plate", "bascinet", 0),
        new("Armet", "plate", "plate", "armet", 0),
        new("Greathelm", "plate", "plate", "greathelm", 8),
        new("Winged Greathelm", "plate_gilded", "plate", "greathelm_wings", 0),
        new("Sun Greathelm", "plate_gilded", "plate", "greathelm_sun", 8),
        new("Horned Greathelm", "plate", "plate", "greathelm_horns", 1),
        new("Dragon Helm", "plate_gilded", "plate", "dragon", 1),
        new("Steppe Helm", "deel", "cloth", "steppe", 6),
        new("Khan Tug", "lamellar", "mail", "tug", 6),
        new("Turban Helm", "lamellar", "cloth", "turban", 5),
        new("Spike Turban", "lamellar", "mail", "spike_turban", 5),
    };

    public static void WriteArmorShowcase(GraphicsDevice gd, Art art, Shading? sh, string dir)
    {
        Directory.CreateDirectory(dir);
        Sky.Set(new Weather(13, 0, 0));
        var sheet = new AnimSheet(gd, art, sh);
        sheet.RenderGrid(PlateShowcase, cols: 5, halfW: 112, halfH: 132, camYaw: 0.24f, dist: 3.55f, targetY: 0.95f, eyeLift: 0.42f, Path.Combine(dir, "plate_front_raw.png"));
        sheet.RenderGrid(PlateShowcase, cols: 5, halfW: 112, halfH: 132, camYaw: 0.85f, dist: 3.55f, targetY: 0.95f, eyeLift: 0.42f, Path.Combine(dir, "plate_3q_raw.png"));
        sheet.RenderGrid(Armors, cols: 6, halfW: 100, halfH: 122, camYaw: 0.18f, dist: 3.85f, targetY: 0.96f, eyeLift: 0.45f, Path.Combine(dir, "body_armor_front_raw.png"));
        sheet.RenderGrid(Armors, cols: 6, halfW: 100, halfH: 122, camYaw: 0.78f, dist: 3.85f, targetY: 0.96f, eyeLift: 0.45f, Path.Combine(dir, "body_armor_3q_raw.png"));
        sheet.RenderGrid(Helms, cols: 7, halfW: 88, halfH: 96, camYaw: 0.32f, dist: 2.35f, targetY: 1.54f, eyeLift: 0.22f, Path.Combine(dir, "helmets_raw.png"));
        var factionItems = new List<ShowcaseItem>();
        // 5 rows (armor tiers) x 9 columns (all 9 factions: Azure..Ashen Sun)!
        for (int p = 0; p < Style.Sides.Length; p++) factionItems.Add(new(Style.Sides[p].Name, "gambeson", "cloth", "kettle", p, Shield: "round", Weapon: "spear"));
        for (int p = 0; p < Style.Sides.Length; p++) factionItems.Add(new(Style.Sides[p].Name, "mail", "mail", "nasal", p, Tabard: true, Shield: "heater", Weapon: "sword"));
        for (int p = 0; p < Style.Sides.Length; p++) factionItems.Add(new(Style.Sides[p].Name, "brigandine", "mail", "sallet", p, Weapon: "halberd"));
        for (int p = 0; p < Style.Sides.Length; p++) factionItems.Add(new(Style.Sides[p].Name, "plate", "plate", "greathelm", p, Tabard: true, Shield: "heater", Weapon: "sword", Plume: true, Cape: true));
        for (int p = 0; p < Style.Sides.Length; p++) factionItems.Add(new(Style.Sides[p].Name, "plate_fluted", "plate", "armet", p, Weapon: "greatsword"));
        sheet.RenderGrid(factionItems.ToArray(), cols: Style.Sides.Length, halfW: 86, halfH: 108, camYaw: 0.45f, dist: 4.15f, targetY: 1.00f, eyeLift: 0.55f, Path.Combine(dir, "factions_raw.png"));
        Console.WriteLine($"Armor ({Armors.Length}), Helmet ({Helms.Length}) & Factions ({Style.Sides.Length}x5) showcase -> {dir}");
    }

    void RenderGrid(ShowcaseItem[] items, int cols, int halfW, int halfH, float camYaw, float dist, float targetY, float eyeLift, string path)
    {
        if (sh == null || puppet3D == null) return;
        int rows = (items.Length + cols - 1) / cols;
        int hw = cols * halfW, hh = rows * halfH;
        int w = hw * PixelScale, h = hh * PixelScale;
        using var rtCol = new RenderTarget2D(gd, hw, hh, false, SurfaceFormat.Color, DepthFormat.Depth24, 0, RenderTargetUsage.PreserveContents);
        using var rtNd = new RenderTarget2D(gd, hw, hh, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
        using var rtOut = new RenderTarget2D(gd, w, h, false, SurfaceFormat.Color, DepthFormat.None);

        gd.SetRenderTarget(rtNd);
        gd.Clear(Color.Transparent);
        gd.SetRenderTarget(rtCol);
        gd.Clear(new Color(122, 134, 118));

        const float Facing = MathHelper.PiOver2;
        for (int i = 0; i < items.Length; i++)
        {
            int row = i / cols, col = i % cols;
            var it = items[i];
            var look = new Look(it.Head, it.Body, it.Legs, "boots", "gloves", it.Shield, it.Weapon, it.Plume, it.Cape, it.Tabard, it.Face, 1);
            var ut = new UnitType { Id = "showcase", Class = UnitClass.Infantry, Size = 1f, Look = look };
            var u = new Unit { Index = 11, Palette = it.Palette, Coat = 1, Type = ut, Facing = Facing, ViewFacing = Facing, Heading = Facing, ViewHeading = Facing, EnemyDist = 20 };

            float a = Facing + camYaw;
            var target = new Vector3(0, targetY, 0);
            var eye = target + new Vector3(MathF.Cos(a) * dist, eyeLift, MathF.Sin(a) * dist);
            var view = Matrix.CreateLookAt(eye, target, Vector3.Up);
            var proj = Matrix.CreatePerspectiveFieldOfView(0.55f, halfW / (float)halfH, 0.1f, 100);
            var vp = new Viewport(col * halfW, row * halfH, halfW, halfH);

            gd.SetRenderTarget(rtCol);
            gd.Viewport = vp;
            gd.Clear(ClearOptions.DepthBuffer, Color.Black, 1, 0);
            Ground(view, proj);

            units3D.N = 0;
            puppet3D.Now = 1f;
            puppet3D.Detail = 0;
            puppet3D.ExactLook = true;
            puppet3D.BeginCamera(view);
            puppet3D.TryDraw(u, 0);
            if (units3D.N == 0) continue;

            gd.SetRenderTargets(rtCol, rtNd);
            gd.Viewport = vp;
            gd.BlendState = BlendState.Opaque;
            gd.DepthStencilState = DepthStencilState.Default;
            gd.RasterizerState = RasterizerState.CullNone;
            sh.Setup(sh.Unit3D, view * proj, eye, 1f, false, 0.45f);
            sh.Unit3D.Parameters["View"].SetValue(view);
            sh.Unit3D.CurrentTechnique = sh.Unit3D.Techniques["Unit3D"];
            sh.Unit3D.CurrentTechnique.Passes[0].Apply();
            units3D.Draw(gd, PrimitiveType.TriangleList, units3D.N / 3);
        }

        sh.PixelPostTo(rtOut, rtCol, rtNd, PixelScale, Vector3.One);
        gd.SetRenderTarget(null);
        var px = new Color[w * h];
        rtOut.GetData(px);
        File.WriteAllBytes(path, SpriteDump.Png(px, w, h));
    }

    public static void Write(GraphicsDevice gd, Art art, Shading? sh, string dir, string filter)
    {
        Directory.CreateDirectory(dir);
        Sky.Set(new Weather(13, 0, 0));
        var sheet = new AnimSheet(gd, art, sh);
        var clips = Clips.Where(c => filter == "" || c.Name.Contains(filter)).ToArray();
        sheet.Render(clips, 0.6f, Path.Combine(dir, "anim-front.png"));
        sheet.Render(clips, 1.45f, Path.Combine(dir, "anim-side.png"));
        Console.WriteLine($"{clips.Length} clips -> {dir} ({(sheet.puppet3D != null ? "3D 2x" : "2D")}): " + string.Join(", ", clips.Select(c => c.Name)));
    }

    void Render(Clip[] clips, float camYaw, string path)
    {
        int w = Cols * CellW, h = clips.Length * CellH;
        Color[] px;
        if (sh != null && puppet3D != null)
        {
            int hw = Cols * HalfW, hh = clips.Length * HalfH;
            using var rtCol = new RenderTarget2D(gd, hw, hh, false, SurfaceFormat.Color, DepthFormat.Depth24, 0, RenderTargetUsage.PreserveContents);
            using var rtNd = new RenderTarget2D(gd, hw, hh, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            using var rtOut = new RenderTarget2D(gd, w, h, false, SurfaceFormat.Color, DepthFormat.None);

            gd.SetRenderTarget(rtNd);
            gd.Clear(Color.Transparent);
            gd.SetRenderTarget(rtCol);
            gd.Clear(new Color(128, 140, 122));

            for (int row = 0; row < clips.Length; row++)
                for (int col = 0; col < Cols; col++)
                    Cell3D(rtCol, rtNd, row, col, clips[row], col / (float)Cols, camYaw);

            sh.PixelPostTo(rtOut, rtCol, rtNd, PixelScale, Vector3.One);
            gd.SetRenderTarget(null);
            px = new Color[w * h];
            rtOut.GetData(px);
        }
        else
        {
            using var rt = new RenderTarget2D(gd, w, h, false, SurfaceFormat.Color, DepthFormat.Depth24);
            gd.SetRenderTarget(rt);
            gd.Clear(new Color(118, 128, 112));
            for (int row = 0; row < clips.Length; row++)
                for (int col = 0; col < Cols; col++)
                {
                    gd.Viewport = new Viewport(col * CellW, row * CellH, CellW, CellH);
                    gd.Clear(ClearOptions.DepthBuffer, Color.Black, 1, 0);
                    Cell2D(clips[row], col / (float)Cols, camYaw);
                }
            gd.SetRenderTarget(null);
            px = new Color[w * h];
            rt.GetData(px);
        }

        for (int row = 0; row < clips.Length; row++) // a dark line between the rows, so each strip reads on its own
            for (int x = 0; x < w; x++) px[row * CellH * w + x] = new Color(64, 70, 60);
        File.WriteAllBytes(path, SpriteDump.Png(px, w, h));
    }

    static Unit MakeUnit(Clip c, float k, out float now)
    {
        const float Facing = MathHelper.PiOver2;
        var u = new Unit { Index = 3, Palette = 0, Coat = 1, Type = Troops.Get(c.Troop), Facing = Facing, ViewFacing = Facing, Heading = Facing, ViewHeading = Facing, EnemyDist = 2 };
        if (c.Horse != null) u.Horse = Troops.Mount(c.Horse);
        c.Set(u, k);
        u.Speed = u.Vel.Length();
        now = c.Span > 0 ? 0.001f + k * c.Span : 1f;
        return u;
    }

    static (Matrix view, Matrix proj, Vector3 eye) CellCamera(Unit u, float camYaw, float aspect)
    {
        const float Facing = MathHelper.PiOver2;
        bool wyvern = u.Type.Look.Face == "wyvern";
        bool beast = u.Type.Look.Beast;
        bool rider = u.Mounted;
        float dist = wyvern ? 11.0f : rider ? 7.6f : beast ? 6.4f : 4.7f;
        float y = wyvern ? 2.15f : rider ? 1.50f : beast ? 0.90f : 1.02f;
        float eyeLift = wyvern ? 1.3f : 0.9f;
        float a = Facing + camYaw;
        var target = new Vector3(0, y, 0);
        var eye = target + new Vector3(MathF.Cos(a) * dist, eyeLift, MathF.Sin(a) * dist);
        return (Matrix.CreateLookAt(eye, target, Vector3.Up), Matrix.CreatePerspectiveFieldOfView(0.55f, aspect, 0.1f, 100), eye);
    }

    void Cell3D(RenderTarget2D rtCol, RenderTarget2D rtNd, int row, int col, Clip c, float k, float camYaw)
    {
        var u = MakeUnit(c, k, out float now);
        var (view, proj, eye) = CellCamera(u, camYaw, HalfW / (float)HalfH);
        var vp = new Viewport(col * HalfW, row * HalfH, HalfW, HalfH);

        gd.SetRenderTarget(rtCol);
        gd.Viewport = vp;
        gd.Clear(ClearOptions.DepthBuffer, Color.Black, 1, 0);
        Ground(view, proj);

        units3D.N = 0;
        puppet3D!.Now = now;
        puppet3D.Detail = 0;
        puppet3D.BeginCamera(view);
        if (Environment.GetEnvironmentVariable("IRONFIELD_ANIM_DEBUG") == c.Name) Console.WriteLine($"{k:0.00} {Puppet.PoseOf(u, now, Ease.Hash(u.Index))}");
        puppet3D.TryDraw(u, 0);
        if (units3D.N == 0) return;

        gd.SetRenderTargets(rtCol, rtNd);
        gd.Viewport = vp;
        gd.BlendState = BlendState.Opaque;
        gd.DepthStencilState = DepthStencilState.Default;
        gd.RasterizerState = RasterizerState.CullNone;
        sh!.Setup(sh.Unit3D, view * proj, eye, now, false, 0.45f);
        sh.Unit3D.Parameters["View"].SetValue(view);
        sh.Unit3D.CurrentTechnique = sh.Unit3D.Techniques["Unit3D"];
        sh.Unit3D.CurrentTechnique.Passes[0].Apply();
        units3D.Draw(gd, PrimitiveType.TriangleList, units3D.N / 3);
    }

    void Cell2D(Clip c, float k, float camYaw)
    {
        var u = MakeUnit(c, k, out float now);
        var (view, proj, _) = CellCamera(u, camYaw, CellW / (float)CellH);
        Ground(view, proj);
        sprites.N = 0;
        puppet.Now = now;
        puppet.Begin(view);
        if (Environment.GetEnvironmentVariable("IRONFIELD_ANIM_DEBUG") == c.Name) Console.WriteLine($"{k:0.00} {Puppet.PoseOf(u, now, Ease.Hash(u.Index))}");
        puppet.Draw(u, 0);
        if (sprites.N == 0) return;
        gd.BlendState = BlendState.Opaque;
        gd.DepthStencilState = DepthStencilState.Default;
        gd.RasterizerState = RasterizerState.CullNone;
        gd.SamplerStates[0] = SamplerState.PointClamp;
        fx.World = Matrix.Identity; fx.View = view; fx.Projection = proj;
        fx.CurrentTechnique.Passes[0].Apply();
        sprites.Draw(gd, PrimitiveType.TriangleList, sprites.N / 3);
    }

    /// A flat square of turf under the unit with a cross at the origin, so drift and sliding feet show.
    void Ground(Matrix view, Matrix proj)
    {
        var g = new Color(96, 114, 80);
        var m = new Color(64, 74, 54);
        var v = new List<VertexPositionColor>();
        void Q(float x0, float z0, float x1, float z1, Color c)
        {
            v.Add(new(new(x0, 0, z0), c)); v.Add(new(new(x1, 0, z0), c)); v.Add(new(new(x0, 0, z1), c));
            v.Add(new(new(x1, 0, z0), c)); v.Add(new(new(x1, 0, z1), c)); v.Add(new(new(x0, 0, z1), c));
        }
        Q(-3, -3, 3, 3, g);
        for (int i = -3; i <= 3; i++) { Q(i - 0.015f, -3, i + 0.015f, 3, m); Q(-3, i - 0.015f, 3, i + 0.015f, m); }
        gd.DepthStencilState = DepthStencilState.None;
        gd.RasterizerState = RasterizerState.CullNone;
        flat.World = Matrix.Identity; flat.View = view; flat.Projection = proj;
        flat.CurrentTechnique.Passes[0].Apply();
        gd.DrawUserPrimitives(PrimitiveType.TriangleList, v.ToArray(), 0, v.Count / 3);
    }
}
