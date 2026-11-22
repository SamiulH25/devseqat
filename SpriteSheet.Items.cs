using Microsoft.Xna.Framework;

namespace Ironfield;

/// Neutral pixel-art sprites for looted goods and company baggage stores (painted into Data/sprites/neutral/).
public sealed partial class SpriteSheet
{
    void ItemSprites()
    {
        var steelM = new Mat(Style.Steel);
        var plateM = new Mat(Color.Lerp(Style.Steel, Style.Iron, 0.45f), Pattern.Plate);
        var mailM = new Mat(Color.Lerp(Style.Steel, Style.Iron, 0.35f), Pattern.Mail);
        var ironM = new Mat(Style.Iron);
        var woodM = new Mat(Style.Wood, Pattern.Wood);
        var leatherM = new Mat(Style.Leather);
        var leatherDk = new Mat(Color.Lerp(Style.Leather, Color.Black, 0.32f));
        var brassM = new Mat(Style.Brass);
        var silverM = new Mat(new Color(218, 224, 232));
        var goldM = new Mat(new Color(236, 198, 78));
        var rubyM = new Mat(new Color(196, 44, 52));
        var furM = new Mat(new Color(132, 98, 66), Pattern.Hair);
        var furLt = new Mat(new Color(172, 138, 102), Pattern.Hair);
        var linenM = new Mat(Style.Linen, Pattern.Cloth);
        var grainM = new Mat(new Color(214, 182, 96), Pattern.Cloth);
        var breadM = new Mat(new Color(188, 128, 64));
        var cheeseM = new Mat(new Color(232, 198, 86));
        var bayM = new Mat(new Color(132, 86, 54), Pattern.Hair);
        var maneM = new Mat(new Color(52, 36, 26));
        var oakM = new Mat(new Color(112, 74, 44), Pattern.Wood);
        var inkM = Mat.Flat(new Color(32, 24, 30));

        // goods.arms: crossed steel blade and spearhead bound with a leather strap
        {
            var c = new Canvas(18, 18);
            c.Line(3, 14, 13, 4, woodM, 0.5f, 2);        // spear shaft
            c.Line(12, 5, 15, 2, steelM, 0.78f, 2);      // spearhead
            c.Set(16, 1, steelM, 0.9f);
            c.Line(14, 14, 4, 4, steelM, 0.72f, 2);      // sword blade
            c.Set(3, 3, steelM, 0.88f);
            c.Line(11, 13, 15, 9, brassM, 0.65f, 1.5f);  // crossguard
            c.Line(13, 13, 15, 15, leatherM, 0.45f, 2);  // grip
            c.Set(16, 16, brassM, 0.75f);                // pommel
            c.Rect(7, 7, 10, 10, leatherDk, 0.1f);       // leather binding
            c.Set(8, 8, brassM, 0.7f);
            Add("goods.arms/n", c);
        }

        // goods.armour: stacked steel cuirass over mail skirt
        {
            var c = new Canvas(18, 18);
            c.Rect(4, 11, 14, 16, mailM, -0.05f);        // mail skirt
            c.Body(3, 12, y => y < 6 ? (5 - (y - 3) * 0.4f, 13 + (y - 3) * 0.4f) : (4 + (y - 6) * 0.35f, 14 - (y - 6) * 0.35f), plateM, 0.08f);
            c.Rect(7, 2, 11, 4, inkM, 0.2f);             // neck opening
            c.Line(5, 3, 7, 3, brassM, 0.7f, 1);         // left pauldron trim
            c.Line(11, 3, 13, 3, brassM, 0.7f, 1);       // right pauldron trim
            c.Rect(4, 11, 14, 12, leatherM, 0.1f);       // waist belt
            c.Set(9, 11, brassM, 0.8f);                  // buckle
            Add("goods.armour/n", c);
        }

        // goods.harness: leather saddle & coiled reins with brass stirrup
        {
            var c = new Canvas(18, 18);
            c.Ellipse(9, 8.5f, 6.5f, 4.2f, leatherM, 0.05f); // saddle body
            c.Ellipse(5, 6.5f, 2.2f, 2.5f, leatherDk, 0.05f); // pommel
            c.Ellipse(13, 6.5f, 2.4f, 2.6f, leatherDk, 0.05f); // cantle
            c.Rect(7, 6, 11, 9, leatherDk, -0.1f);       // seat hollow
            c.Line(9, 11, 9, 14, leatherM, 0.45f, 1.5f); // stirrup leather
            c.Rect(7, 14, 11, 16, brassM, 0.2f);         // brass stirrup
            c.Set(9, 15, inkM, 0.2f);
            c.Line(4, 11, 14, 11, brassM, 0.65f, 1);     // trim studs
            Add("goods.harness/n", c);
        }

        // goods.silver: gleaming silver platter and chalice
        {
            var c = new Canvas(18, 18);
            c.Ellipse(8, 10, 6.5f, 6.5f, silverM, 0.12f); // embossed platter
            c.Ellipse(8, 10, 4.2f, 4.2f, plateM, 0.05f);
            c.Body(3, 9, y => (10.5f + (y - 3) * 0.25f, 15.5f - (y - 3) * 0.25f), silverM, 0.22f); // goblet cup
            c.Rect(12, 9, 14, 14, silverM, 0.1f);        // stem
            c.Rect(10, 14, 16, 16, silverM, 0.18f);      // foot
            c.Line(11, 4, 15, 4, goldM, 0.8f, 1);        // gilt rim
            Add("goods.silver/n", c);
        }

        // goods.trinkets: leather pouch spilling coins and a ruby ring
        {
            var c = new Canvas(18, 18);
            c.Ellipse(8, 11, 5.2f, 5f, leatherM, 0.05f); // pouch body
            c.Rect(6, 4, 10, 7, leatherDk, 0.05f);       // gathered neck
            c.Line(6, 6, 10, 6, goldM, 0.75f, 1);        // drawstring
            c.Ellipse(13, 14, 2.3f, 1.8f, goldM, 0.25f); // gold coins
            c.Ellipse(10, 15, 2f, 1.5f, goldM, 0.15f);
            c.Ellipse(14, 11, 1.8f, 1.8f, goldM, 0.2f);  // ring
            c.Set(14, 10, rubyM, 0.85f);                 // ruby
            Add("goods.trinkets/n", c);
        }

        // goods.furs: bundled rolled pelts tied with leather cord
        {
            var c = new Canvas(18, 18);
            c.Ellipse(9, 12, 6.8f, 3.8f, furM, -0.05f);  // lower pelt roll
            c.Ellipse(9, 7.5f, 6.2f, 3.6f, furLt, 0.08f); // upper pelt roll
            c.Ellipse(3.5f, 7.5f, 1.6f, 2.4f, leatherDk, 0.05f); // spiral end
            c.Ellipse(3.5f, 12, 1.6f, 2.4f, leatherDk, 0.05f);
            c.Rect(8, 4, 10, 16, leatherM, 0.1f);        // tie strap
            c.Set(9, 9, brassM, 0.75f);
            Add("goods.furs/n", c);
        }

        // store.food: grain sack, bread loaf and wheel of cheese
        {
            var c = new Canvas(18, 18);
            c.Ellipse(7, 11, 5.2f, 5.4f, linenM, 0.02f); // grain sack
            c.Rect(5, 3, 9, 6, linenM, -0.05f);          // sack top
            c.Line(5, 6, 9, 6, leatherM, 0.5f, 1);       // rope tie
            c.Ellipse(13, 13.5f, 3.6f, 2.6f, breadM, 0.12f); // bread loaf
            c.Ellipse(12.5f, 9.5f, 2.8f, 2.2f, cheeseM, 0.2f); // cheese wedge
            c.Rect(6, 2, 8, 4, grainM, 0.15f);           // grain peeking out
            Add("store.food/n", c);
        }

        // store.horse: pack horse head with leather bridle
        {
            var c = new Canvas(18, 18);
            c.Body(4, 15, y => (4 + (y - 4) * 0.25f, 12 - (y - 4) * 0.15f), bayM, 0.05f); // neck
            c.Columns(7, 16, x => (4 + (x - 7) * 0.35f, 11 + (x - 7) * 0.15f), bayM, 0.1f); // muzzle
            c.Line(4, 3, 6, 13, maneM, 0.35f, 2);        // mane
            c.Set(7, 2, bayM, 0.5f); c.Set(8, 3, bayM, 0.5f); // ear
            c.Set(11, 6, inkM, 0.2f);                    // eye
            c.Line(9, 4, 15, 9, leatherM, 0.5f, 1);      // bridle
            c.Set(14, 9, brassM, 0.8f);                  // bit ring
            Add("store.horse/n", c);
        }

        // store.wagon: covered wooden supply wagon
        {
            var c = new Canvas(18, 18);
            c.Ellipse(9, 7.5f, 6.8f, 4.8f, linenM, 0.1f); // canvas tilt
            c.Line(6, 3, 6, 10, grainM, 0.35f, 1);       // hoop ribs
            c.Line(12, 3, 12, 10, grainM, 0.35f, 1);
            c.Rect(2, 10, 16, 13, oakM, 0.05f);          // wagon bed
            c.Ellipse(5, 14, 2.4f, 2.4f, woodM, 0.05f);  // rear wheel
            c.Ellipse(13, 14, 2.4f, 2.4f, woodM, 0.05f); // front wheel
            c.Set(5, 14, ironM, 0.6f); c.Set(13, 14, ironM, 0.6f); // hubs
            Add("store.wagon/n", c);
        }

        // store.vault: iron-bound oak strongbox
        {
            var c = new Canvas(18, 18);
            c.Body(4, 8, y => (3, 15), oakM, 0.15f);     // arched lid
            c.Rect(3, 8, 15, 15, oakM, 0.02f);           // chest body
            c.Rect(5, 4, 6, 15, ironM, 0.15f);           // left iron band
            c.Rect(12, 4, 13, 15, ironM, 0.15f);         // right iron band
            c.Rect(3, 8, 15, 9, ironM, 0.1f);            // lid seam
            c.Rect(8, 8, 10, 11, brassM, 0.3f);          // lockplate
            c.Set(9, 9, inkM, 0.2f);                     // keyhole
            Add("store.vault/n", c);
        }
    }
}
