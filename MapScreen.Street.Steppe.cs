using Microsoft.Xna.Framework;

namespace Ironfield;

/// MapScreen: the steppe people's pieces of the street (felt and timber, a camp that can move): the feast tent (their tavern),
/// the khan's ordu, a great ger on its platform inside a lattice fence under horse-tail standards (their keep), the war camp
/// with its lookout (their barracks), the elders' ger, the ovoo on the rise (their landmark), the well sweep; and the parts
/// they share: gers, horse-tail standards, the hooked ornament band. Drawing only.
partial class MapScreen
{
    static Color Felt(TownLook l, TownColours c) => HeroWall(l, c, new Color(226, 214, 188));
    static Color SteppeWood(TownLook l) => l.Night ? new Color(48, 30, 30) : new Color(140, 78, 44);
    /// White horse hair by the hour, and how bright gilt shows (night greys both down).
    static Color WhiteHair(TownLook l) => l.Night ? new Color(98, 96, 116) : l.Dusk ? new Color(206, 182, 170) : new Color(236, 232, 222);
    static float Glow(TownLook l) => l.Night ? 0.5f : l.Dusk ? 0.8f : 1f;
    static Color SteppeRed(TownLook l) => l.Night ? new Color(96, 40, 36) : l.Dusk ? new Color(150, 60, 48) : new Color(186, 70, 50);

    /// The hooked ornament the steppe sews on felt and paints on doors: a running band of S-hooks, three rows high.
    static readonly string[] HookMap = { "#.##", "#..#", "##.#" };

    void Ornament(int x0, int x1, int y, Color col, int a)
    {
        for (int row = 0; row < 3; row++)
            for (int x = x0, i = 0; x + a <= x1; x += a, i++)
                if (HookMap[row][i % 4] == '#') ui.Rect(new Rectangle(x, y + row * a, a, a), col);
    }

    /// A horse-tail standard: a tall pole with a gilt trident head, a ring under it and a bell of horse hair that sways; `hair`
    /// white or black, a pennant of the holder's cloth below it if `cloth` is not transparent.
    void Tug(int x, int ground, int h, Color pole, Color hair, Color cloth, float t, float glow = 1)
    {
        int s = Math.Max(1, ui.U(1)), top = ground - h;
        int sway = (int)MathF.Round(MathF.Sin(t * 2.4f + x * 0.05f) * s);
        ui.Rect(new Rectangle(x, top, s * 2, h), pole);
        var gilt = Kit.Mul(Kit.Gilt, glow);
        ui.Rect(new Rectangle(x - ui.U(2), top - ui.U(2), ui.U(5) + s, s), gilt); // the trident's bar
        foreach (int dx in new[] { -2, 0, 2 }) ui.Rect(new Rectangle(x + ui.U(dx) + (dx == 0 ? 0 : s / 2), top - ui.U(dx == 0 ? 7 : 5), s, ui.U(dx == 0 ? 5 : 3)), gilt);
        ui.Rect(new Rectangle(x - s, top + ui.U(1), ui.U(3) + s, ui.U(2)), Kit.Mul(gilt, 0.8f)); // the ring
        for (int k = 0; k < ui.U(3); k += s) // the hair: a knot under the ring, spreading
        {
            int hw = ui.U(1) + k;
            ui.Rect(new Rectangle(x + s - hw, top + ui.U(3) + k, hw * 2, s), hair);
        }
        for (int j = 0; j < 7; j++) // then hanging in strands of uneven length, swaying at their ends
        {
            int len = ui.U(6 + (j * 5 + 3) % 4 * 1.5f), sx = x + s - ui.U(3.5f) + ui.U(j);
            ui.Rect(new Rectangle(sx, top + ui.U(6), ui.U(1), len - ui.U(2)), j % 3 == 1 ? Kit.Mul(hair, 0.86f) : hair);
            ui.Rect(new Rectangle(sx + sway, top + ui.U(4) + len, ui.U(1), ui.U(2)), Kit.Mul(hair, 0.92f));
        }
        if (cloth.A == 0) return;
        int py = top + ui.U(17);
        ui.Rect(new Rectangle(x + 2 * s, py, ui.U(9), ui.U(5) + sway), cloth);
        ui.Rect(new Rectangle(x + 2 * s + ui.U(9), py + s, ui.U(3), ui.U(3) + sway), cloth);
    }

    /// A ger: the low dome of its roof with the poles showing, its crown ring, the lattice drum in felt, the girth ropes round
    /// it, and (ornate) the hooked band sewn under the roof. `cx, ground` is the middle of its foot.
    void Ger(int cx, int ground, int half, int drum, int roofH, Color felt, Color roof, Color band, TownLook l, TownColours c, bool ornate)
    {
        int s = Math.Max(1, ui.U(1));
        var rim0 = rimLight;
        if (l.Night && rimLight.A > 0) rimLight = Color.Lerp(roof, rimLight, 0.3f); // moonlight only grazes the felt
        Dome(cx, ground - drum, half + ui.U(2), roofH, roof);
        rimLight = rim0;
        for (int k = 1; k < 6; k++) // the roof poles under the felt
            ui.Line(new Vector2(cx, ground - drum - roofH + ui.U(2)), new Vector2(cx - half + k * half / 3, ground - drum), Kit.Mul(roof, 0.85f), ui.U(1));
        ui.Rect(new Rectangle(cx - ui.U(4), ground - drum - roofH - s, ui.U(8), ui.U(2)), Kit.Mul(roof, 0.55f)); // the crown ring
        ui.Rect(new Rectangle(cx - half, ground - drum, half * 2, drum), felt);
        ui.Rect(new Rectangle(cx - half, ground - drum, half * 2, ui.U(2)), Color.Black * 0.2f); // the eave's shadow
        foreach (int by in new[] { drum / 3, drum * 2 / 3 }) ui.Rect(new Rectangle(cx - half, ground - drum + by, half * 2, s), Kit.Mul(felt, 0.7f)); // girth ropes
        if (ornate) Ornament(cx - half + ui.U(2), cx + half - ui.U(2), ground - drum + ui.U(3), band, Math.Max(s, ui.U(1)));
        Edges(cx - half, ground - drum, half * 2, drum, felt);
    }

    /// A ger's door, painted red and framed, the hooked ornament on its lintel; lit and ajar at night.
    void GerDoor(Rectangle door, TownLook l, TownColours c, List<(int X, int W)> lights, bool lit)
    {
        int s = Math.Max(1, ui.U(1));
        var wood = SteppeWood(l);
        ui.Rect(new Rectangle(door.X - ui.U(2), door.Y - ui.U(4), door.Width + ui.U(4), door.Height + ui.U(4)), wood);
        Ornament(door.X - ui.U(1), door.Right + ui.U(1), door.Y - ui.U(4), Kit.Gilt * (l.Night ? 0.5f : 0.9f), s);
        if (lit && !l.Day)
        {
            ui.Rect(door, new Color(255, 200, 120));
            ui.Rect(new Rectangle(door.X, door.Y, door.Width / 2, door.Height), SteppeRed(l)); // one leaf swung in
            lights.Add((door.X, door.Width));
            int st = s, depth = ui.U(8);
            for (int k = 0; k < depth; k += st)
                ui.Rect(new Rectangle(door.X - k * 2, door.Bottom + k, door.Width + 4 * k, st), c.Lit * (0.6f * (1 - MathF.Floor(k * 3f / depth) / 3f)));
            return;
        }
        ui.Rect(door, SteppeRed(l));
        ui.Rect(new Rectangle(door.Center.X, door.Y, s, door.Height), Kit.Mul(SteppeRed(l), 0.6f));
        foreach (int dy in new[] { door.Height / 3, door.Height * 2 / 3 })
        {
            ui.Rect(new Rectangle(door.X + ui.U(2), door.Y + dy, door.Width / 2 - ui.U(3), s), Kit.Gilt * (l.Night ? 0.4f : 0.7f));
            ui.Rect(new Rectangle(door.Center.X + ui.U(2), door.Y + dy, door.Width / 2 - ui.U(3), s), Kit.Gilt * (l.Night ? 0.4f : 0.7f));
        }
    }

    // ---------- the feast tent ----------

    /// The steppe tavern, a feast tent: a great ger with the hooked band and a painted door, a felt fly on poles over the door,
    /// skins of mare's milk hung by it, horse-tail standards either side, a hitching rail with a horse, lanterns at night, a sign
    /// with a bowl.
    List<(int X, int W)> FeastTent(Rectangle r, TownLook l, TownColours c, Color cloth, float t)
    {
        Color felt = Felt(l, c), wood = SteppeWood(l), roof = l.Night ? new Color(84, 70, 70) : Color.Lerp(new Color(200, 184, 150), c.Rim, 0.08f);
        var lights = new List<(int X, int W)>();
        int s = Math.Max(1, ui.U(1)), cx = r.X + ui.U(78), half = ui.U(62), drum = ui.U(26);
        rimLight = c.Rim;
        Tug(r.X + ui.U(4), r.Bottom, ui.U(72), wood, WhiteHair(l), Color.Transparent, t, Glow(l));
        Tug(r.Right + ui.U(10), r.Bottom, ui.U(72), wood, l.Night ? new Color(40, 36, 36) : new Color(54, 46, 44), Color.Transparent, t, Glow(l)); // clear of the sign's post
        Ger(cx, r.Bottom, half, drum, ui.U(32), felt, roof, SteppeRed(l), l, c, true);
        Smoke(cx, r.Bottom - drum - ui.U(34), t, l.Night);
        // The fly: a felt awning on two poles over the door, its ropes pegged out.
        int fy = r.Bottom - ui.U(30), fx0 = cx - ui.U(22), fx1 = cx + ui.U(22);
        foreach (int px in new[] { fx0, fx1 - ui.U(2) }) ui.Rect(new Rectangle(px, fy, ui.U(2), r.Bottom - fy), wood);
        for (int k = 0; k < ui.U(6); k += s) ui.Rect(new Rectangle(fx0 - k, fy - ui.U(6) + k, fx1 - fx0 + 2 * k, s), Kit.Mul(cloth, l.Night ? 0.55f : 0.9f));
        Ornament(fx0 - ui.U(4), fx1 + ui.U(4), fy, Kit.Gilt * (l.Night ? 0.45f : 0.8f), s);
        ui.Line(new Vector2(fx0 - ui.U(6), fy), new Vector2(fx0 - ui.U(14), r.Bottom), Kit.Mul(wood, 1.3f), ui.U(1));
        ui.Line(new Vector2(fx1 + ui.U(6), fy), new Vector2(fx1 + ui.U(14), r.Bottom), Kit.Mul(wood, 1.3f), ui.U(1));
        GerDoor(new Rectangle(cx - ui.U(10), r.Bottom - ui.U(22), ui.U(20), ui.U(22)), l, c, lights, true);
        // Skins of mare's milk hung on the left pole, a low table with bowls on the right.
        var skin = l.Night ? new Color(80, 60, 46) : new Color(160, 118, 80);
        foreach (int dy in new[] { 6, 13 }) { ui.Disc(new Vector2(fx0 - ui.U(3), fy + ui.U(dy + 3)), ui.U(3), skin); ui.Rect(new Rectangle(fx0 - ui.U(3), fy + ui.U(dy - 1), s, ui.U(2)), wood); }
        ui.Rect(new Rectangle(fx1 + ui.U(4), r.Bottom - ui.U(6), ui.U(16), ui.U(2)), Kit.Mul(wood, 1.2f));
        foreach (int lx in new[] { 5, 17 }) ui.Rect(new Rectangle(fx1 + ui.U(lx), r.Bottom - ui.U(4), ui.U(2), ui.U(4)), wood);
        foreach (int bx in new[] { 7, 12, 16 }) ui.Rect(new Rectangle(fx1 + ui.U(bx), r.Bottom - ui.U(8), ui.U(3), ui.U(2)), l.Night ? new Color(150, 140, 130) : new Color(232, 226, 214));
        // The hitching rail and a horse at it, by the left side.
        int hx = r.X + ui.U(14);
        ui.Rect(new Rectangle(hx, r.Bottom - ui.U(12), ui.U(28), ui.U(2)), wood);
        foreach (int px in new[] { hx, hx + ui.U(26) }) ui.Rect(new Rectangle(px, r.Bottom - ui.U(12), ui.U(2), ui.U(12)), wood);
        Warhorse(hx + ui.U(4), r.Bottom, l, c, cloth);
        if (!l.Day) foreach (int px in new[] { fx0 + ui.U(1), fx1 - ui.U(1) }) Lantern(new Vector2(px, fy + ui.U(8)), c, wood);
        BowlSign(r.Right - ui.U(30), r.Bottom - drum - ui.U(16), l, c, wood);
        return lights;
    }

    /// The feast tent's sign: a board hung from a pole, a bowl of milk on it.
    void BowlSign(int ax, int ay, TownLook l, TownColours c, Color frame)
    {
        int s = Math.Max(1, ui.U(1));
        ui.Rect(new Rectangle(ax, ay, ui.U(26), ui.U(2)), frame);
        ui.Rect(new Rectangle(ax + ui.U(24), ay, ui.U(2), ui.U(30)), frame);
        ui.Rect(new Rectangle(ax + ui.U(4), ay + ui.U(2), s, ui.U(3)), frame);
        ui.Rect(new Rectangle(ax + ui.U(18), ay + ui.U(2), s, ui.U(3)), frame);
        var board = new Rectangle(ax + ui.U(1), ay + ui.U(5), ui.U(21), ui.U(14));
        if (!l.Day) ui.Disc(board.Center.ToVector2(), ui.U(14), c.Lit * 0.1f);
        ui.Rect(new Rectangle(board.X - s, board.Y - s, board.Width + 2 * s, board.Height + 2 * s), Kit.Gilt * (l.Night ? 0.5f : 0.85f));
        ui.Rect(board, SteppeRed(l));
        var bowl = l.Night ? new Color(200, 190, 170) : new Color(240, 232, 214);
        for (int k = 0; k < ui.U(5); k += s) ui.Rect(new Rectangle(board.X + ui.U(4) + k / 2, board.Y + ui.U(5) + k, ui.U(13) - k, s), bowl);
        ui.Rect(new Rectangle(board.X + ui.U(4), board.Y + ui.U(4), ui.U(13), s), Kit.Mul(bowl, 0.8f));
        ui.Rect(new Rectangle(board.X + ui.U(8), board.Y + ui.U(10), ui.U(5), s), Kit.Mul(bowl, 0.75f)); // its foot
    }

    // ---------- the khan's ordu ----------

    /// The steppe keep, the khan's ordu: a great ger with a gilt crown on a raised timber platform inside a lattice fence, two
    /// lesser gers beside it, horse-tail standards round it, the holder's pennant. `hero` paints the near one of a castle (on its
    /// earthen rise, with the gate in the fence, a cart and a horse, braziers at night); else a camp on the far rise.
    void KhanOrdu(int cx, int baseY, int scale, TownLook l, TownColours c, Color cloth, float t, bool hero)
    {
        int k = ui.U(scale), s = Math.Max(1, ui.U(1));
        Color felt = hero ? Felt(l, c) : Color.Lerp(Color.Lerp(c.Far, c.Mid, 0.45f), new Color(226, 214, 188), l.Day ? 0.35f : 0.1f);
        Color roof = hero ? (l.Night ? new Color(84, 70, 70) : new Color(204, 188, 154)) : Kit.Mul(felt, 0.9f);
        Color wood = hero ? SteppeWood(l) : Kit.Mul(felt, 0.6f), red = hero ? SteppeRed(l) : Kit.Mul(felt, 0.75f);
        if (hero)
        {
            int mh = ui.U(14);
            var turf = l.Snow ? c.Snow : l.Night ? Color.Lerp(c.Near, new Color(60, 66, 44), 0.4f) : new Color(150, 146, 90);
            for (int m = 0; m < mh; m += s)
            {
                float f = m / (float)mh;
                int hw = 16 * k + (int)(f * ui.U(40));
                ui.Rect(new Rectangle(cx - hw, baseY + m, hw * 2, s), m < ui.U(3) ? turf : Color.Lerp(Kit.Mul(turf, 0.8f), c.Ground, f * 0.8f));
            }
            for (int st = 0; st < 5; st++) // a ramp of planks
            {
                int sy = baseY + st * ui.U(3), sw = ui.U(18) + st * ui.U(4);
                ui.Rect(new Rectangle(cx - sw / 2, sy, sw, ui.U(3)), Kit.Mul(wood, 1.1f - st * 0.05f));
                ui.Rect(new Rectangle(cx - sw / 2, sy, sw, s), Color.Lerp(wood, c.Rim, 0.3f));
            }
        }
        var rim0 = rimLight;
        rimLight = hero ? c.Rim : Color.Transparent;
        var hairW = WhiteHair(l);
        if (!hero) hairW = Color.Lerp(felt, hairW, 0.5f);
        // The standards behind, then the lesser gers, then the great one on its platform.
        foreach (int sx in new[] { -14, 14 }) Tug(cx + sx * k, baseY, 18 * k, wood, hairW, Color.Transparent, t, Glow(l));
        Tug(cx + 7 * k, baseY, 22 * k, wood, hairW, cloth, t, Glow(l));
        foreach (int sx in new[] { -10, 10 }) Ger(cx + sx * k, baseY, 4 * k, 3 * k, 3 * k, Kit.Mul(felt, 0.95f), roof, red, l, c, false);
        var deck = new Rectangle(cx - 9 * k, baseY - 3 * k, 18 * k, 3 * k);
        ui.Rect(deck, wood);
        for (int x = deck.X + k; x < deck.Right; x += 2 * k) ui.Rect(new Rectangle(x, deck.Y + s, s, deck.Height - s), Kit.Mul(wood, 0.7f));
        Ger(cx, deck.Y, 8 * k, 6 * k, 6 * k, felt, roof, red, l, c, hero);
        ui.Rect(new Rectangle(cx - s, deck.Y - 13 * k, 2 * s, ui.U(4)), Kit.Gilt); // the gilt crown spike
        ui.Disc(new Vector2(cx, deck.Y - 12 * k), Math.Max(2, k * 0.7f), Kit.Gilt);
        if (hero) Smoke(cx + 2 * k, deck.Y - 12 * k, t, l.Night);
        var door = new Rectangle(cx - k - k / 2, deck.Y - 4 * k, 3 * k, 4 * k);
        if (hero) GerDoor(door, l, c, new List<(int X, int W)>(), false);
        else ui.Rect(door, l.Day ? Kit.Mul(red, 0.9f) : c.Lit * 0.8f);
        // The lattice fence round it all, a gap for the gate.
        var fence = hero ? Kit.Mul(wood, 1.15f) : wood;
        for (int x = -15; x < 15; x++)
        {
            if (x is >= -2 and < 2) continue;
            ui.Rect(new Rectangle(cx + x * k, baseY - 3 * k, s * 2, 3 * k), fence);
            ui.Line(new Vector2(cx + x * k, baseY - 3 * k), new Vector2(cx + (x + 1) * k, baseY), fence, ui.U(1));
            ui.Line(new Vector2(cx + (x + 1) * k, baseY - 3 * k), new Vector2(cx + x * k, baseY), fence, ui.U(1));
        }
        foreach (int side in new[] { -1, 1 }) ui.Rect(new Rectangle(side < 0 ? cx - 15 * k : cx + 2 * k, baseY - 3 * k, 13 * k, s * 2), fence);
        if (hero)
        {
            foreach (int gx in new[] { -2, 2 }) ui.Rect(new Rectangle(cx + gx * k - s, baseY - 6 * k, 2 * s, 6 * k), wood); // the gate posts
            ui.Rect(new Rectangle(cx - 2 * k - s, baseY - 6 * k, 4 * k + 2 * s, 2 * s), wood);
            Ornament(cx - 2 * k + s, cx + 2 * k - s, baseY - 6 * k + 2 * s, red, s);
            if (!l.Day)
                foreach (int side in new[] { -1, 1 }) FirePost(cx + side * 3 * k, baseY, 3 * k, l, c, wood, t);
            Cart(cx - 19 * k, baseY + ui.U(14), l, wood);
        }
        rimLight = rim0;
    }

    /// A felt cart: two big spoked wheels, a bed with a small ger lashed on it, the shafts down to the ground.
    void Cart(int x, int ground, TownLook l, Color wood)
    {
        int s = Math.Max(1, ui.U(1));
        ui.Rect(new Rectangle(x, ground - ui.U(12), ui.U(26), ui.U(3)), wood);
        Dome(x + ui.U(13), ground - ui.U(12), ui.U(11), ui.U(9), l.Night ? new Color(110, 100, 96) : new Color(210, 198, 170));
        foreach (int wx in new[] { 6, 20 })
        {
            var at = new Vector2(x + ui.U(wx), ground - ui.U(5));
            ui.Disc(at, ui.U(5), wood);
            ui.Disc(at, ui.U(4), l.Night ? new Color(30, 26, 34) : new Color(150, 140, 110));
            ui.Rect(new Rectangle((int)at.X - ui.U(4), (int)at.Y, ui.U(8), s), wood);
            ui.Rect(new Rectangle((int)at.X, (int)at.Y - ui.U(4), s, ui.U(8)), wood);
        }
        ui.Line(new Vector2(x + ui.U(26), ground - ui.U(10)), new Vector2(x + ui.U(40), ground), wood, ui.U(1));
    }

    // ---------- the war camp ----------

    /// The steppe barracks, the war camp: a lookout of four poles with a felt-screened platform and a standard over it, the
    /// warriors' long ger with bows racked by its door, and the yard.
    void WarCamp(Rectangle r, TownLook l, TownColours c, Color cloth, float t)
    {
        Color felt = Felt(l, c), wood = SteppeWood(l), roof = l.Night ? new Color(84, 70, 70) : new Color(200, 184, 150);
        int ground = r.Bottom, s = Math.Max(1, ui.U(1));
        Ger(r.X + ui.U(50), ground, ui.U(24), ui.U(18), ui.U(16), felt, roof, SteppeRed(l), l, c, true);
        GerDoor(new Rectangle(r.X + ui.U(44), ground - ui.U(15), ui.U(12), ui.U(15)), l, c, new List<(int X, int W)>(), true);
        // A bow rack by the door: a rail with three bows hung on it.
        ui.Rect(new Rectangle(r.X + ui.U(60), ground - ui.U(14), ui.U(12), s * 2), wood);
        foreach (int bx in new[] { 62, 66, 70 })
        {
            ui.Rect(new Rectangle(r.X + ui.U(bx), ground - ui.U(13), s, ui.U(9)), Kit.Mul(wood, 1.3f));
            ui.Rect(new Rectangle(r.X + ui.U(bx) - s, ground - ui.U(13), s, ui.U(2)), Kit.Mul(wood, 1.3f));
            ui.Rect(new Rectangle(r.X + ui.U(bx) - s, ground - ui.U(6), s, ui.U(2)), Kit.Mul(wood, 1.3f));
        }
        // The lookout: four splayed poles, cross-braced, a platform screened in felt, a standard.
        int top = ground - ui.U(36), lx = r.X + ui.U(2);
        ui.Line(new Vector2(lx, ground), new Vector2(lx + ui.U(6), top), wood, ui.U(2));
        ui.Line(new Vector2(lx + ui.U(24), ground), new Vector2(lx + ui.U(18), top), wood, ui.U(2));
        ui.Line(new Vector2(lx + ui.U(3), ground - ui.U(8)), new Vector2(lx + ui.U(20), top + ui.U(16)), wood, ui.U(1));
        ui.Line(new Vector2(lx + ui.U(21), ground - ui.U(8)), new Vector2(lx + ui.U(4), top + ui.U(16)), wood, ui.U(1));
        var plat = new Rectangle(lx + ui.U(2), top - ui.U(8), ui.U(22), ui.U(9));
        ui.Rect(plat, felt);
        Ornament(plat.X + ui.U(1), plat.Right - ui.U(1), plat.Y + ui.U(2), SteppeRed(l), s);
        ui.Rect(new Rectangle(plat.X, plat.Bottom - s, plat.Width, ui.U(2)), wood);
        Edges(plat.X, plat.Y, plat.Width, plat.Height, felt);
        if (!l.Day) ui.Disc(new Vector2(plat.Center.X, plat.Y - ui.U(1)), ui.U(5), c.Lit * 0.2f);
        Tug(plat.X + ui.U(10), plat.Y, ui.U(16), wood, WhiteHair(l), cloth, t, Glow(l));
        Yard(r.X + ui.U(76), r.Right, ground, l, cloth);
    }

    // ---------- the elders' ger and the well sweep ----------

    /// The steppe elders' house: a broad ger with the hooked band and painted door, a standard by it, a lantern after dusk.
    void ElderGer(Rectangle r, TownLook l, TownColours c, Color cloth, float t, List<(int X, int W)> lights)
    {
        Color felt = Felt(l, c), wood = SteppeWood(l), roof = l.Night ? new Color(84, 70, 70) : new Color(204, 188, 154);
        int ground = r.Bottom, cx = r.Center.X;
        rimLight = c.Rim;
        Tug(r.Right + ui.U(2), ground, ui.U(56), wood, WhiteHair(l), cloth, t, Glow(l));
        Ger(cx, ground, r.Width / 2, ui.U(20), ui.U(22), felt, roof, SteppeRed(l), l, c, true);
        Smoke(cx, ground - ui.U(44), t, l.Night);
        GerDoor(new Rectangle(cx - ui.U(7), ground - ui.U(16), ui.U(14), ui.U(16)), l, c, lights, true);
        if (!l.Day) Lantern(new Vector2(cx - ui.U(16), ground - ui.U(12)), c, wood);
    }

    /// The steppe well: a log trough and a sweep, a long pole pivoting on a forked post with a bucket on a rope at its end.
    void WellSweep(int x, int ground, TownLook l, TownColours c)
    {
        int s = Math.Max(1, ui.U(1));
        var wood = SteppeWood(l);
        ui.Rect(new Rectangle(x, ground - ui.U(7), ui.U(16), ui.U(7)), Kit.Mul(wood, 1.1f)); // the well's log frame
        ui.Rect(new Rectangle(x, ground - ui.U(7), ui.U(16), s), Color.Lerp(wood, c.Rim, 0.3f));
        ui.Rect(new Rectangle(x + ui.U(18), ground - ui.U(4), ui.U(14), ui.U(4)), Kit.Mul(wood, 0.9f)); // the trough
        int px = x + ui.U(24);
        ui.Rect(new Rectangle(px, ground - ui.U(24), ui.U(2), ui.U(24)), wood); // the forked post
        ui.Rect(new Rectangle(px - s, ground - ui.U(26), s, ui.U(3)), wood); ui.Rect(new Rectangle(px + ui.U(2), ground - ui.U(26), s, ui.U(3)), wood);
        ui.Line(new Vector2(x + ui.U(6), ground - ui.U(36)), new Vector2(x + ui.U(42), ground - ui.U(14)), wood, ui.U(2)); // the sweep
        ui.Rect(new Rectangle(x + ui.U(40), ground - ui.U(16), ui.U(5), ui.U(5)), Kit.Mul(wood, 0.8f)); // its counterweight
        ui.Rect(new Rectangle(x + ui.U(6), ground - ui.U(36), s, ui.U(26)), Kit.Mul(wood, 0.8f)); // the rope
        ui.Rect(new Rectangle(x + ui.U(4), ground - ui.U(11), ui.U(5), ui.U(4)), Kit.Mul(wood, 1.2f)); // the bucket
    }

    // ---------- the ovoo ----------

    /// The steppe landmark on the rise: an ovoo, a cairn of stones with a pole in its top, cloth strips on ropes running down from
    /// it, horse-tail standards either side; flat in the far colour, a fire by it at night.
    void Ovoo(int x, int ground, Color c, bool night, Color lit, Color cloth, float t)
    {
        int s = Math.Max(1, ui.U(1));
        var rim0 = rimLight;
        if (rimLight.A > 0) rimLight = Color.Lerp(c, rimLight, 0.3f);
        // The cairn: stones stepped up, lighter tops.
        for (int i = 0; i < 6; i++)
        {
            int w = ui.U(56 - i * 9), y = ground - ui.U(6) * (i + 1);
            ui.Rect(new Rectangle(x - w / 2, y, w, ui.U(6)), i % 2 == 0 ? c : Kit.Mul(c, 0.92f));
            for (int sx = x - w / 2 + ui.U(3); sx < x + w / 2 - ui.U(3); sx += ui.U(8)) ui.Rect(new Rectangle(sx, y, ui.U(5), s), Color.Lerp(c, rimLight.A > 0 ? rimLight : c, 0.4f));
        }
        int top = ground - ui.U(36);
        ui.Rect(new Rectangle(x - s, top - ui.U(44), ui.U(2), ui.U(44)), Kit.Mul(c, 0.8f)); // the pole
        ui.Rect(new Rectangle(x - ui.U(3), top - ui.U(46), ui.U(6) + s, s), Kit.Mul(c, 0.8f));
        // Ropes of cloth strips down to the ground either side.
        float dim = night ? 0.4f : 0.85f;
        Color[] strips = { new(70, 120, 186), new(236, 232, 222), Kit.Mul(cloth, 1.2f), new(220, 180, 70) };
        foreach (int dir in new[] { -1, 1 })
            for (int i = 0; i < 14; i++)
            {
                if (i < 13) // the rope the strips hang from
                {
                    float f0 = (i + 0.5f) / 14, f1 = (i + 1.5f) / 14;
                    ui.Line(new Vector2(x + dir * f0 * ui.U(30), top - ui.U(40) + f0 * ui.U(40)),
                        new Vector2(x + dir * f1 * ui.U(30), top - ui.U(40) + f1 * ui.U(40)), Kit.Mul(c, 0.7f), ui.U(1));
                }
            }
        foreach (int dir in new[] { -1, 1 })
            for (int i = 0; i < 14; i++)
            {
                float f = (i + 0.5f) / 14;
                int sx = x + (int)(dir * f * ui.U(30)), sy = top - ui.U(40) + (int)(f * ui.U(40));
                int flap = (int)MathF.Round(MathF.Sin(t * 4 + i) * s);
                ui.Rect(new Rectangle(sx, sy, ui.U(2), ui.U(4) + flap), Kit.Mul(strips[i % strips.Length], dim));
            }
        foreach (int dx in new[] { -46, 46 }) Tug(x + ui.U(dx), ground, ui.U(56), Kit.Mul(c, 0.8f), Kit.Mul(c, 1.08f), Color.Transparent, t, night ? 0.45f : 0.75f);
        if (night)
        {
            ui.Disc(new Vector2(x + ui.U(30), ground - ui.U(4)), ui.U(10), lit * 0.14f);
            ui.Rect(new Rectangle(x + ui.U(28), ground - ui.U(6), ui.U(4), ui.U(4)), lit * 0.9f);
        }
        rimLight = rim0;
    }
}
