using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Ironfield;

/// MapScreen: the realm map (M). Every province in its realm's colour, borders between realms, towns, you, and a legend.
sealed partial class MapScreen
{
    const int AtlasSize = 256;
    int[]? atlasIds;          // province id per texel of the realm map, -1 off the land (built once per map)
    Texture2D? atlasTex;

    /// Which province each texel of the realm map shows. The land does not move, so this is worked out once.
    int[] AtlasIds()
    {
        if (atlasIds != null) return atlasIds;
        var ids = new int[AtlasSize * AtlasSize];
        float land = C.Map.ExtentUsed;
        for (int y = 0; y < AtlasSize; y++)
            for (int x = 0; x < AtlasSize; x++)
            {
                var w = World.MinimapToWorld(new Vector2((x + 0.5f) / AtlasSize, (y + 0.5f) / AtlasSize), terrain.MinimapRange);
                ids[y * AtlasSize + x] = MathF.Max(MathF.Abs(w.X), MathF.Abs(w.Y)) > land ? -1 : C.Map.ProvinceAt(w).Id;
            }
        return atlasIds = ids;
    }

    /// Paints the realm colours: a wash over each province, a dark line where two realms meet, a faint one between provinces.
    Texture2D AtlasTexture()
    {
        var ids = AtlasIds();
        var data = new Color[ids.Length];
        int Owner(int i) => ids[i] < 0 ? -2 : C.S.Provinces[ids[i]].Faction;
        for (int y = 0; y < AtlasSize; y++)
            for (int x = 0; x < AtlasSize; x++)
            {
                int i = y * AtlasSize + x;
                if (ids[i] < 0) continue;
                int f = Owner(i);
                bool realmEdge = false, provEdge = false;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= AtlasSize || ny >= AtlasSize) continue;
                    int j = ny * AtlasSize + nx;
                    if (ids[j] < 0) continue;
                    if (Owner(j) != f) realmEdge = true;
                    else if (ids[j] != ids[i]) provEdge = true;
                }
                var c = SideColor(f);
                data[i] = realmEdge ? new Color(16, 12, 10, 235) : provEdge ? Color.Lerp(c, Color.Black, 0.35f) * 0.7f : c * 0.5f;
            }
        atlasTex ??= new Texture2D(gd, AtlasSize, AtlasSize);
        atlasTex.SetData(data);
        return atlasTex;
    }

    void AtlasPanel()
    {
        var r = Modal(900, 560, "THE REALMS OF THE LAND");
        int legendW = ui.U(250);
        int size = Math.Min(r.Height - ui.U(84), r.Width - legendW - ui.U(40));
        var m = new Rectangle(r.X + ui.U(16), r.Y + ui.U(20), size, size);
        ui.Sb.Draw(world!.Minimap, m, Color.White);
        ui.Sb.Draw(AtlasTexture(), m, Color.White);
        Vector2 M(Vector2 w) { var q = World.WorldToMinimap(w, terrain.MinimapRange); return new Vector2(m.X + q.X * m.Width, m.Y + q.Y * m.Height); }
        foreach (var s in C.S.Settlements.Where(s => s.Kind != SettlementKind.Village))
        {
            var p = M(s.Pos);
            int d = ui.U(s.Kind == SettlementKind.Town ? 6 : 4);
            ui.Rect(new Rectangle((int)p.X - d / 2 - 1, (int)p.Y - d / 2 - 1, d + 2, d + 2), Color.Black);
            ui.Rect(new Rectangle((int)p.X - d / 2, (int)p.Y - d / 2, d, d), s.Kind == SettlementKind.Town ? Kit.Parchment : SideColor(s.Faction));
        }
        // Wells (docs/ARCANA.md AR2): a mark in the tradition's colour, larger as it is stronger, edged white when marked.
        void Dot(Vector2 at, int d, Color c) => ui.Rect(new Rectangle((int)at.X - d / 2, (int)at.Y - d / 2, d, d), c);
        foreach (var w in C.Sites)
        {
            var (t, n) = C.Well(w);
            if (n == 0) continue;
            var p = M(w.Pos);
            int d = ui.U(2 + 2 * n);
            if (C.S.WellMarks.ContainsKey(w.Id)) Dot(p, d + ui.U(3), Color.White);
            Dot(p, d + ui.U(1), Color.Black);
            Dot(p, d, TideColour(t));
        }
        var me = M(C.Player.Pos);
        ui.Rect(new Rectangle((int)me.X - ui.U(5), (int)me.Y - ui.U(5), ui.U(10), ui.U(10)), Color.Black);
        ui.Rect(new Rectangle((int)me.X - ui.U(4), (int)me.Y - ui.U(4), ui.U(8), ui.U(8)), Style.Accent);
        ui.Frame(m, Style.PanelEdge, ui.U(1));
        if (m.Contains(ui.Mouse))
        {
            var w = World.MinimapToWorld(new Vector2((ui.Mouse.X - m.X) / (float)m.Width, (ui.Mouse.Y - m.Y) / (float)m.Height), terrain.MinimapRange);
            var pv = C.Map.ProvinceAt(w);
            string tip = $"{pv.Title.ToUpperInvariant()}: {(pv.Faction >= 0 ? C.FactionNames[pv.Faction].ToUpperInvariant() : "NO REALM")}";
            ui.TextC(tip, m.Center.X, m.Bottom + ui.U(6), Style.Text, 1);
            if (ui.Clicked(m)) { focus = w; follow = false; panel = PanelKind.None; }
        }
        AtlasLegend(new Rectangle(m.Right + ui.U(20), m.Y, r.Right - m.Right - ui.U(36), m.Height));
        if (ui.Button(new Rectangle(r.Center.X - ui.U(90), r.Bottom - ui.U(40), ui.U(180), ui.U(30)), "CLOSE", note: ctl.Name(Act.AtlasScreen))) panel = CameFrom(PanelKind.Atlas);
    }

    /// The realms, largest first: shield, name, land held, and how they stand to you.
    void AtlasLegend(Rectangle r)
    {
        int y = r.Y;
        ui.Text("REALMS BY LAND HELD", r.X, y, Style.Accent, 1); y += ui.U(16);
        var realms = Enumerable.Range(0, C.S.Factions.Count).Where(f => C.S.Provinces.Any(p => p.Faction == f)).OrderByDescending(C.RealmShare).ToList();
        int row = Math.Min(ui.U(40), (r.Height - ui.U(80)) / Math.Max(1, realms.Count));
        foreach (int f in realms)
        {
            var hd = Style.Sides[Style.PaletteOf(f)];
            int sh = row - ui.U(6);
            ui.Shield(new Rectangle(r.X, y, (int)(sh / 1.2f), sh), hd);
            int tx = r.X + (int)(sh / 1.2f) + ui.U(8);
            string name = C.FactionNames[f].ToUpperInvariant();
            while (name.Length > 4 && ui.TW(name, 1) > r.Right - tx - ui.U(40)) name = name[..^2];
            ui.Text(name, tx, y + ui.U(2), hd.Ui, 1);
            ui.TextR(Campaign.Pct(C.RealmShare(f)), r.Right, y + ui.U(2), Style.Text, 1);
            var (st, sc) = Stance(f);
            ui.Text(st, tx, y + ui.U(15), sc, 1);
            y += row;
        }
        y += ui.U(8);
        foreach (var (c, label) in new[] { (Style.Accent, "YOUR COMPANY"), (Kit.Parchment, "TOWN"), (Style.Dim, "CASTLE, IN ITS REALM'S COLOUR"),
                     (TideColour("faith"), "WELL OF THE FAITH"), (TideColour("craft"), "WELL OF THE CRAFT") })
        {
            ui.Rect(new Rectangle(r.X + ui.U(2), y + ui.U(2), ui.U(7), ui.U(7)), c);
            ui.Text(label, r.X + ui.U(16), y, Style.Dim, 1);
            y += ui.U(13);
        }
        ui.Text("CLICK THE MAP TO LOOK THERE.", r.X, y + ui.U(4), Style.Dim, 1);
    }

    /// A tradition's colour on the map: pale blue for the faith (gold is your company and the towns), violet for the craft.
    static Color TideColour(string tradition) => tradition == "faith" ? new Color(170, 225, 255) : tradition == "craft" ? new Color(190, 130, 255) : Style.Dim;
}
