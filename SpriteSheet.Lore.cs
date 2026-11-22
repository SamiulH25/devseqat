using Microsoft.Xna.Framework;

namespace Ironfield;

/// Lore-grounded Roven equipment (docs/LORE.md): signature elite & Knight Order armour/helms for the seven realms + the
/// Hollow Crown, three mid-tier regional bodies (`jubbah`, `tarn_habit`, `buffcoat`), and three iconic shields.
public sealed partial class SpriteSheet
{
    static readonly Mat AshIron = new(new Color(102, 106, 112), Pattern.Plate);
    static readonly Mat AshCloth = new(new Color(98, 100, 104), Pattern.Cloth);
    static readonly Mat CharSteel = new(new Color(44, 42, 48), Pattern.Plate);
    static readonly Mat Ember = Mat.Flat(new Color(224, 96, 36));
    static readonly Mat WolfPelt = new(new Color(118, 120, 126), Pattern.Hair);
    static readonly Mat SunBronze = new(new Color(196, 148, 72), Pattern.Plate);
    static readonly Mat BuffLeather = new(new Color(148, 104, 62), Pattern.Cloth);
    static readonly Mat SilverBright = new(new Color(204, 212, 224), Pattern.Plate);

    /// Lore-grounded elite & order helms, or null.
    Canvas? LoreHelm(string kind, string v)
    {
        bool side = v == "s", back = v == "b";
        switch (kind)
        {
            case "lily_armet":
            {
                // Brevon / Silver Lily: polished silvered armet with raised fleur/lily brow crest.
                var c = new Canvas(19, 19);
                c.Body(5, 17, y => (2.5f + MathF.Abs(y - 10) * 0.12f, 16.5f - MathF.Abs(y - 10) * 0.12f), SilverBright, 0.12f);
                c.Ellipse(9.5f, 7.5f, 6.5f, 4.5f, SilverBright, 0.18f);
                // Raised silver fleur/lily crest along the crown
                if (!side)
                {
                    c.Body(1, 6, y => (8.2f - (6 - y) * 0.25f, 10.8f + (6 - y) * 0.25f), SilverBright, 0.28f);
                    c.Set(9, 1, trim, 0.85f); c.Set(7, 3, SilverBright, 0.75f); c.Set(11, 3, SilverBright, 0.75f);
                }
                else
                {
                    c.Body(1, 6, y => (6f + (y - 1) * 0.4f, 13.5f - (y - 1) * 0.2f), SilverBright, 0.26f);
                    c.Line(6, 2, 13, 3, trim, 0.8f, 1);
                }
                if (!back)
                {
                    if (side) { c.Rect(11, 9, 16.5f, 10.6f, ink, 0.2f); c.Line(11, 13, 16, 12, SilverBright, 0.8f, 1); }
                    else { c.Rect(5, 9.2f, 14, 10.8f, ink, 0.2f); c.Set(9, 10, SilverBright, 0.7f); c.Line(6, 13, 13, 13, trim, 0.75f, 1); }
                }
                else c.Rect(7, 13, 12, 17, cloth, 0.05f); // mantling tail
                return c;
            }
            case "rose_sallet":
            {
                // Vespra / Red Rose: darkened gothic sallet with pierced visor and sweeping feather crest.
                var c = new Canvas(19, 20);
                // Sweeping team-coloured feather crest
                if (side) c.Body(1, 8, y => (2 + y * 0.5f, 10 + y * 0.35f), plumeCloth, 0.15f);
                else c.Body(1, 7, y => (7.5f - (7 - y) * 0.35f, 11.5f + (7 - y) * 0.35f), plumeCloth, 0.15f);
                c.Ellipse(9.5f, 10f, 7f, 5.2f, plate, -0.08f);
                // Flared sallet tail & bevor
                if (side)
                {
                    c.Body(11, 17, y => (1.5f, 16.2f), plate, -0.05f);
                    c.Rect(11, 11.5f, 16.5f, 13f, ink, 0.2f);
                    c.Line(9, 10, 16, 10, brass, 0.7f, 1);
                }
                else
                {
                    c.Body(11, 17, y => (2.5f, 16.5f), plate, -0.05f);
                    if (!back)
                    {
                        c.Rect(4.5f, 11.5f, 14.5f, 13f, ink, 0.2f);
                        c.Line(5, 10, 14, 10, brass, 0.7f, 1);
                        c.Set(9, 15, brass, 0.75f); // rose stud on bevor
                    }
                }
                return c;
            }
            case "tarn_cowl":
            {
                // Tarn / Knights of Tarn: austere ash-grey monastic cowl over a stark unpainted highland-iron mask.
                var c = new Canvas(19, 17);
                c.Ellipse(9.5f, 8.5f, 7.6f, 7.2f, AshCloth, -0.06f);
                c.Body(9, 16.5f, y => (2f, 17f), AshCloth, -0.1f);
                if (!back)
                {
                    // Unpainted hammered ash-iron face mask inside the cowl
                    if (side)
                    {
                        c.Body(5, 14.5f, y => (10.5f, 16.2f), AshIron, 0.05f);
                        c.Rect(12.5f, 8.5f, 16.2f, 9.8f, ink, 0.2f);
                    }
                    else
                    {
                        c.Body(5, 14.5f, y => (5.2f, 13.8f), AshIron, 0.05f);
                        c.Rect(6.2f, 8.5f, 8.6f, 9.8f, ink, 0.2f);
                        c.Rect(10.4f, 8.5f, 12.8f, 9.8f, ink, 0.2f);
                        c.Line(9.5f, 6, 9.5f, 13.5f, AshIron, 0.75f, 1);
                    }
                }
                else c.Body(4, 15, y => (5f, 14f), AshCloth, -0.18f);
                return c;
            }
            case "hollow_crown":
            {
                // The Hollow Crown: gaunt blackened-iron greathelm ringed with a jagged blackened iron circlet.
                var c = new Canvas(19, 21);
                c.Body(7, 20, y => (3f, 16f), CharSteel, -0.08f);
                c.Ellipse(9.5f, 8f, 6.5f, 3.2f, CharSteel, -0.05f);
                // Jagged blackened iron crown spikes
                foreach (int sx in new[] { 4, 7, 9, 12, 15 })
                {
                    int h = sx == 9 ? 1 : sx is 7 or 12 ? 2 : 3;
                    c.Line(sx, 7, sx, h, CharSteel, 0.35f, 1.5f);
                    c.Set(sx, h, AshIron, 0.65f);
                }
                c.Rect(3, 6.2f, 16, 7.6f, AshIron, -0.15f);
                if (!back)
                {
                    if (side)
                    {
                        c.Rect(10.5f, 11.5f, 16f, 13.2f, ink, 0.2f);
                        c.Set(13, 12, Ember, 0.9f);
                    }
                    else
                    {
                        c.Rect(4.5f, 11.5f, 14.5f, 13.2f, ink, 0.2f);
                        c.Set(7, 12, Ember, 0.9f); c.Set(12, 12, Ember, 0.9f); // faint ash-ember glow in visor
                        c.Line(9.5f, 11, 9.5f, 19, CharSteel, 0.3f, 1);
                    }
                }
                return c;
            }
            case "wolfsworn":
            {
                // Skar / The Wolfsworn: riveted spectacle helm topped with a sculpted iron wolf-muzzle crest and fur aventail.
                var c = new Canvas(19, 20);
                c.Body(12, 19, y => (2f, 17f), WolfPelt, -0.05f); // wolf-fur aventail
                c.Ellipse(9.5f, 9.5f, 6.8f, 5.5f, AshIron, 0.06f);
                // Iron wolf crest along the top
                if (side)
                {
                    c.Body(2, 7, y => (4 + (y - 2) * 0.4f, 15.5f), WolfPelt, 0.08f);
                    c.Line(12, 3, 16, 5, AshIron, 0.7f, 2); // iron wolf muzzle
                    c.Set(6, 2, WolfPelt, 0.6f); // ears
                }
                else
                {
                    c.Ellipse(9.5f, 4.5f, 4.5f, 3.2f, WolfPelt, 0.08f);
                    c.Set(6, 1, WolfPelt, 0.65f); c.Set(13, 1, WolfPelt, 0.65f); // wolf ears
                    c.Rect(7.5f, 4, 11.5f, 6.5f, AshIron, 0.2f); // iron snout plate
                }
                if (!back)
                {
                    if (side) c.Rect(11, 10, 15.5f, 12.5f, AshIron, 0.15f);
                    else
                    {
                        c.Line(5, 10, 14, 10, SilverBright, 0.65f, 1); // spectacle brow
                        c.Set(7, 11, ink, 0.2f); c.Set(12, 11, ink, 0.2f);
                        c.Line(9.5f, 9.5f, 9.5f, 13.5f, SilverBright, 0.7f, 1);
                    }
                }
                return c;
            }
            case "falcon_kulah":
            {
                // Qadir / Burning Falcon: tall fluted bronze-and-steel conical helm with hawk-beak nasal and silk turban wrap.
                var c = new Canvas(19, 21);
                c.Body(13, 20, y => (3f, 16f), mail, -0.05f); // mail aventail
                c.Body(2, 11, y => (9.5f - (y - 2) * 0.72f, 9.5f + (y - 2) * 0.72f), SunBronze, 0.15f); // fluted bronze cone
                c.Set(9, 1, brass, 0.9f);
                c.Body(9.5f, 14, y => (2f, 17f), cloth, 0.08f); // wrapped silk turban band
                c.Line(3, 11.5f, 16, 11.5f, brass, 0.75f, 1);
                if (!back)
                {
                    if (side) c.Line(15, 11, 17, 15, SunBronze, 0.8f, 1.5f); // hawk-beak nasal
                    else c.Line(9.5f, 10, 9.5f, 16, SunBronze, 0.85f, 1.5f);
                }
                return c;
            }
            case "sky_mask":
            {
                // Naryn / Eternal Sky: domed steppe helm with hammered-steel war-mask and streaming horsehair plume.
                var c = new Canvas(19, 22);
                // Streaming horsehair tug plume at top
                c.Body(1, 7, y => (8f - (y - 1) * 0.35f, 11f + (y - 1) * 0.35f), plumeCloth, 0.12f);
                c.Ellipse(9.5f, 10.5f, 6.8f, 5f, plate, 0.05f);
                c.Body(12, 20, y => (2.5f, 16.5f), Lacquer, -0.05f); // lamellar cheek/neck guards
                if (!back)
                {
                    if (side)
                    {
                        c.Body(10, 18, y => (11f, 16.5f), SilverBright, 0.1f);
                        c.Set(14, 13, ink, 0.2f);
                    }
                    else
                    {
                        c.Body(10, 18.5f, y => (5.5f, 13.5f), SilverBright, 0.1f); // hammered war-mask
                        c.Rect(6.5f, 12.5f, 8.5f, 13.5f, ink, 0.2f);
                        c.Rect(10.5f, 12.5f, 12.5f, 13.5f, ink, 0.2f);
                        c.Line(7, 16, 12, 16, brass, 0.65f, 1); // engraved moustache ridge
                    }
                }
                return c;
            }
            case "lantern_burgonet":
            {
                // Brinemere / Lantern Company: swept combed arsenal burgonet with brass-trimmed cheek-pieces.
                var c = new Canvas(19, 20);
                // High swept arsenal comb
                if (side) c.Body(1, 7, y => (3.5f + (y - 1) * 0.35f, 14.5f - (y - 1) * 0.2f), plate, 0.2f);
                else c.Body(1, 7, y => (8f, 11f), plate, 0.22f);
                c.Line(9.5f, 1, 9.5f, 7, brass, 0.8f, 1);
                c.Ellipse(9.5f, 10.5f, 7.2f, 5.2f, plate, 0.08f);
                c.Body(12, 18, y => (2.5f, 16.5f), plate, 0.02f); // hinged cheek-pieces
                c.Line(2, 12, 17, 12, brass, 0.75f, 1); // brass brow peak
                if (!back && !side) c.Body(12.5f, 17.5f, y => (6.5f, 12.5f), skin, 0); // open burgonet face
                return c;
            }
            default: return null;
        }
    }

    /// Lore-grounded elite/order and mid-tier torsos, or null.
    Canvas? LoreTorso(string body, string v)
    {
        bool side = v == "s";
        var c = new Canvas(side ? 13 : 20, 23);
        Func<float, (float, float)> span = side ? SideSpan : TorsoSpan;
        switch (body)
        {
            case "plate_lily":
            {
                // Brevon / Silver Lily: silvered fluted plate with crisp white-and-team quartered surcoat & silver clasp.
                PlateTorso(c, v);
                c.Body(3, 22, span, linen, 0.08f);
                if (!side)
                {
                    for (int y = 4; y < 22; y++) for (int x = 2; x < 18; x++)
                        if (c.Has(x, y) && ((x < 10) == (y < 13))) c.Set(x, y, cloth, 0.52f);
                    c.Rect(3, 13.5f, 17, 15.2f, leather, 0.05f);
                    c.Set(10, 14, SilverBright, 0.88f); // silver clasp
                    c.Set(10, 6, SilverBright, 0.88f);  // silver lily badge
                    c.Set(9, 7, SilverBright, 0.78f); c.Set(11, 7, SilverBright, 0.78f);
                }
                else
                {
                    c.Body(13, 15.2f, span, leather);
                    c.Body(4, 13, span, cloth, 0.05f);
                }
                c.Body(0, 3.6f, span, SilverBright, 0.2f); // silvered pauldrons/gorget
                return c;
            }
            case "plate_vespran":
            {
                // Vespra / Red Rose: darkened Milanese fluted plate with team sash and brass rivets.
                PlateTorso(c, v);
                for (int y = 0; y < 23; y++) for (int x = 0; x < c.W; x++) c.Shade(x, y, -0.14f);
                if (!side)
                {
                    c.Line(4, 3, 15, 14, cloth, 0.62f, 2.5f); // duellist silk sash
                    c.Line(4, 3, 15, 14, brass, 0.75f, 0.8f);
                    c.Rect(3, 14, 17, 15.6f, leather);
                    foreach (int rx in new[] { 5, 10, 15 }) { c.Set(rx, 15, brass, 0.8f); c.Set(rx, 19, brass, 0.75f); }
                }
                else
                {
                    c.Body(13.5f, 15.5f, span, cloth, 0.1f);
                    c.Set(6, 14, brass, 0.8f);
                }
                return c;
            }
            case "plate_tarn":
            {
                // Tarn / Knights of Tarn: matte grey highland-iron plate, zero gold, rough ash-grey monastic scapular.
                c.Body(0, 22.5f, span, AshIron, -0.06f);
                c.Body(13.5f, 15.5f, span, leather, -0.1f);
                if (!side)
                {
                    c.Body(2, 22.5f, _ => (6.5f, 13.5f), AshCloth, -0.05f); // central monastic scapular
                    c.Line(6.5f, 2, 6.5f, 22, ink, 0.25f, 1);
                    c.Line(13.5f, 2, 13.5f, 22, ink, 0.25f, 1);
                    c.Rect(4, 14, 16, 15.4f, leather, -0.1f);
                }
                else
                {
                    c.Body(2, 22.5f, _ => (4f, 9f), AshCloth, -0.05f);
                }
                return c;
            }
            case "plate_hollow":
            {
                // The Hollow Crown: soot-blackened scorched steel with jagged shoulders and faint ember seams.
                c.Body(0, 22.5f, span, CharSteel, -0.12f);
                c.Body(0, 3.5f, span, CharSteel, 0.04f); // jagged pauldrons
                c.Body(13.5f, 15.2f, span, AshIron, -0.2f);
                if (!side)
                {
                    c.Line(7, 3, 13, 3, Ember, 0.85f, 1);     // ember glow at gorget seam
                    c.Line(6, 14, 14, 14, Ember, 0.75f, 1);   // ember seam at waist
                    c.Line(10, 4, 10, 13, AshIron, 0.45f, 1); // spine ridge
                }
                else
                {
                    c.Set(8, 3, Ember, 0.85f);
                    c.Line(4, 14, 9, 14, Ember, 0.7f, 1);
                }
                return c;
            }
            case "jarl_scale":
            {
                // Skar / The Wolfsworn: heavy iron scale over mail with thick grey wolf-pelt mantle and silver brooches.
                c.Body(0, 22.5f, span, mail, -0.06f);
                c.Body(4, 19, span, AshIron, 0.04f);
                for (int y = 5; y < 19; y += 3) for (int x = 2; x < c.W - 2; x += 2) c.Shade(x + (y % 2), y, -0.35f); // scales
                c.Body(0, 6.2f, span, WolfPelt, 0.06f); // heavy wolf-pelt mantle
                c.Body(13.5f, 15.5f, span, leather);
                if (!side)
                {
                    c.Set(6, 5, SilverBright, 0.85f); c.Set(14, 5, SilverBright, 0.85f); // twin hacksilver brooches
                    c.Set(10, 14, SilverBright, 0.8f);
                }
                return c;
            }
            case "mirror_qadir":
            {
                // Qadir / Burning Falcon: polished bronze-and-steel char-aina mirror discs over dyed silk & mail.
                c.Body(0, 22.5f, span, cloth, 0.04f);
                c.Body(15, 22.5f, span, mail, -0.04f);
                c.Body(13.2f, 15.2f, span, brass, 0.15f);
                if (!side)
                {
                    c.Ellipse(10, 8.2f, 4.6f, 4.4f, SunBronze, 0.22f); // central sun-mirror disc
                    c.Ellipse(10, 8.2f, 2.5f, 2.4f, SilverBright, 0.28f);
                    c.Line(4, 3, 16, 3, brass, 0.78f, 1);
                }
                else
                {
                    c.Ellipse(6.5f, 8.2f, 3.6f, 4.2f, SunBronze, 0.2f);
                }
                return c;
            }
            case "lamellar_khan":
            {
                // Naryn / Eternal Sky: lacquered-iron lamellar over fur-trimmed coat with silver sky-disc & horsehair tassel.
                c.Body(0, 22.5f, span, clothDk, -0.04f);
                c.Body(2, 19, span, Lacquer, 0.06f);
                for (int y = 3; y < 19; y += 3) for (int x = 2; x < c.W - 2; x++) c.Shade(x, y, -0.34f); // lamellar rows
                c.Body(19, 22.5f, span, WolfPelt, 0.05f); // fur hem
                c.Body(13.5f, 15.4f, span, brass, 0.12f);
                if (!side)
                {
                    c.Ellipse(10, 8, 2.8f, 2.8f, SilverBright, 0.25f); // silver sky-disc
                    c.Line(4, 2, 4, 8, plumeCloth, 0.7f, 1.5f);        // horsehair tassel
                }
                return c;
            }
            case "plate_lantern":
            {
                // Brinemere / Lantern Company: burnished three-quarter cuirass over sea-dyed buff coat with brass lantern badge.
                c.Body(0, 22.5f, span, BuffLeather, 0.02f);
                c.Body(1.5f, 14.5f, span, plate, 0.12f);
                c.Body(14.5f, 19.5f, span, plate, 0.02f); // wide laminated tassets
                for (int y = 16; y < 20; y += 2) for (int x = 2; x < c.W - 2; x++) c.Shade(x, y, -0.35f);
                c.Body(13.5f, 15.2f, span, leather);
                if (!side)
                {
                    c.Rect(8.5f, 5.5f, 11.5f, 9.5f, brass, 0.28f); // brass lantern badge
                    c.Set(10, 7, Ember, 0.85f);
                    c.Line(4, 2, 16, 2, brass, 0.75f, 1);
                }
                return c;
            }
            // ---------- General mid-tier regional bodies ----------
            case "jubbah":
            {
                // Qadir general T2-T3: lightweight quilted desert coat with linen trim and diagonal brass-buttoned sash.
                c.Body(0, 22.5f, span, cloth, 0.06f);
                for (int y = 3; y < 22; y += 4) for (int x = 2; x < c.W - 2; x++) c.Shade(x, y, -0.2f);
                c.Body(13.5f, 15.6f, span, linen, 0.1f); // wrapped cloth waist sash
                if (!side)
                {
                    c.Line(6, 2, 12, 13, brass, 0.72f, 1); // diagonal buttoned closure
                    c.Body(20.5f, 22.5f, span, linen, 0.08f);
                }
                return c;
            }
            case "tarn_habit":
            {
                // Tarn general T2-T3: plain highland mail under a rough ash-grey monastic tabard (no gold).
                c.Body(0, 22.5f, span, mail, -0.04f);
                c.Body(1, 21.5f, y => side ? (3.5f, 9.5f) : (5f, 15f), AshCloth, -0.05f);
                c.Body(13.5f, 15.4f, span, leather, -0.1f);
                return c;
            }
            case "buffcoat":
            {
                // Brinemere general T2-T3: oiled leather buff-coat with steel gorget, brass buttons, and team sash.
                c.Body(0, 22.5f, span, BuffLeather, 0.04f);
                c.Body(0.5f, 3.8f, span, plate, 0.08f); // steel gorget
                c.Body(13.5f, 15.5f, span, cloth, 0.06f); // team-colour sash
                if (!side)
                {
                    for (int y = 5; y < 13; y += 3) c.Set(10, y, brass, 0.78f);
                }
                return c;
            }
            default: return null;
        }
    }

    /// Matching sleeves for lore-grounded bodies, or null.
    Canvas? LoreSleeve(string body)
    {
        var c = new Canvas(8, 20);
        switch (body)
        {
            case "plate_lily":
                c.Rect(1.5f, 0, 6.5f, 20, SilverBright, 0.12f);
                c.Rect(1, 0, 7, 3, cloth, 0.08f);
                c.Ellipse(4, 10, 3.4f, 2.4f, SilverBright, 0.2f);
                return c;
            case "plate_vespran":
                c.Rect(1.5f, 0, 6.5f, 20, cloth, 0.05f); // slashed silk sleeve
                c.Rect(1, 0, 7, 7, plate, -0.1f);
                c.Ellipse(4, 10, 3.4f, 2.4f, plate, -0.06f);
                c.Set(4, 10, brass, 0.75f);
                return c;
            case "plate_tarn":
                c.Rect(1.5f, 0, 6.5f, 20, AshIron, -0.06f);
                c.Rect(1, 0, 7, 4, AshCloth, -0.06f);
                c.Ellipse(4, 10, 3.2f, 2.2f, AshIron, 0);
                return c;
            case "plate_hollow":
                c.Rect(1.5f, 0, 6.5f, 20, CharSteel, -0.1f);
                c.Ellipse(4, 10, 3.4f, 2.4f, CharSteel, 0);
                c.Set(4, 10, Ember, 0.8f);
                return c;
            case "jarl_scale":
                c.Rect(1.5f, 0, 6.5f, 20, mail, -0.04f);
                c.Rect(1, 0, 7, 5, WolfPelt, 0.06f);
                return c;
            case "mirror_qadir":
                c.Rect(1.5f, 0, 6.5f, 20, cloth, 0.05f);
                c.Rect(1.2f, 11, 6.8f, 19, SunBronze, 0.14f); // bazuband bracer
                return c;
            case "lamellar_khan":
                c.Rect(1.5f, 0, 6.5f, 20, clothDk);
                c.Rect(1, 0, 7, 6, Lacquer, 0.08f);
                c.Rect(1.5f, 16, 6.5f, 20, WolfPelt, 0.05f);
                return c;
            case "plate_lantern":
                c.Rect(1.5f, 0, 6.5f, 20, BuffLeather, 0.04f);
                c.Rect(1, 0, 7, 6, plate, 0.1f);
                c.Line(1, 6, 7, 6, brass, 0.75f, 1);
                return c;
            case "jubbah":
                c.Rect(1.2f, 0, 6.8f, 20, cloth, 0.06f);
                c.Rect(1.2f, 16.5f, 6.8f, 20, linen, 0.08f);
                return c;
            case "tarn_habit":
                c.Rect(1.5f, 0, 6.5f, 20, mail, -0.04f);
                c.Rect(1, 0, 7, 4, AshCloth, -0.06f);
                return c;
            case "buffcoat":
                c.Rect(1.2f, 0, 6.8f, 20, BuffLeather, 0.04f);
                c.Rect(1.2f, 16.5f, 6.8f, 20, leather, -0.06f);
                return c;
            default: return null;
        }
    }

    /// Iconic lore-grounded shields, or null.
    Canvas? LoreShield(string kind)
    {
        switch (kind)
        {
            case "lantern_pavise":
            {
                // Brinemere: tall guild pavise with brass rim and lantern emblem.
                var pv = new Canvas(20, 34);
                pv.Fill((x, y) => y > 0.3f && x > 0.5f && x < 19.5f && (y > 2 || MathF.Abs(x - 10) < 7 + y), (x, y) => new Vector3((x - 10) / 18, 0, 1), cloth);
                pv.Rect(8.8f, 1, 11.2f, 33.7f, BuffLeather, 0.05f);
                pv.Border(brass);
                pv.Rect(7, 9, 13, 17, brass, 0.25f);
                pv.Rect(8.5f, 11, 11.5f, 15, Ember, 0.85f);
                return pv;
            }
            case "tarn_kite":
            {
                // Tarn: austere hammered ash-iron kite shield, riveted, zero gilding.
                var c = new Canvas(20, 27);
                c.Fill((x, y) => y > 0.5f && y < 26.5f && MathF.Abs(x - 10) < (y < 9 ? 9.2f : 9.2f * (1 - (y - 9) / 18f)), (x, y) => new Vector3((x - 10) / 14, (y - 9) / 20, 1), AshIron);
                c.Border(CharSteel);
                c.Line(10, 2, 10, 24, AshCloth, 0.45f, 2);
                c.Line(3, 9, 17, 9, AshCloth, 0.45f, 2);
                return c;
            }
            case "sun_kalkan":
            {
                // Qadir: bossed bronze-and-steel sun-mirror kalkan with radiating brass ribs.
                var c = new Canvas(20, 25);
                c.Fill((x, y) => InEl(x, y, 10, 12.5f, 9.6f, 9.6f), (x, y) => new Vector3((x - 10) / 14, (y - 12.5f) / 14, 1), cloth);
                c.Border(SunBronze);
                c.Line(3, 12.5f, 17, 12.5f, brass, 0.72f, 1);
                c.Line(10, 5, 10, 20, brass, 0.72f, 1);
                c.Line(5, 7.5f, 15, 17.5f, brass, 0.65f, 1);
                c.Line(15, 7.5f, 5, 17.5f, brass, 0.65f, 1);
                c.Ellipse(10, 12.5f, 3.6f, 3.6f, SunBronze, 0.25f);
                c.Ellipse(10, 12.5f, 1.8f, 1.8f, SilverBright, 0.3f);
                return c;
            }
            default: return null;
        }
    }
}
