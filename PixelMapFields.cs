using Microsoft.Xna.Framework;

namespace Ironfield;

public static partial class PixelMap
{
    /// Fields for the pixel map: strips on one lattice over the whole world (so no two fields ever overlap), each cell
    /// worked by the nearest village, town or castle within reach of it. Around a place the cells form a ragged
    /// patchwork; every few columns a grass lane runs between the blocks; a block's outer edge is hedged and the
    /// strips inside are parted by a dark furrow. Drawing only: the map's own plots (WorldLand.LayFields) are left alone.
    sealed partial class View
    {
        /// A strip's width (east-west) and length (north-south), in metres.
        const float CellW = 6, CellL = 13;
        /// Every this many columns a grass lane instead of a strip.
        const int LaneEvery = 5;

        readonly Dictionary<(int, int), sbyte> cells = new();
        HashSet<(int, int)>? treeCells;

        static readonly FieldKind[] Crops = { FieldKind.Grain, FieldKind.Grain, FieldKind.Grain, FieldKind.Green, FieldKind.Green, FieldKind.Ploughed, FieldKind.Ploughed, FieldKind.Fallow, FieldKind.Pasture, FieldKind.Pasture };

        /// The crop a lattice cell holds, or -1 for none.
        sbyte CellAt(int ci, int cj)
        {
            if (cells.TryGetValue((ci, cj), out var v)) return v;
            v = WorkCell(ci, cj);
            cells[(ci, cj)] = v;
            return v;
        }

        sbyte WorkCell(int ci, int cj)
        {
            if (Hash(ci, 977) % (uint)LaneEvery == 0 || Hash(cj, 4111) % 7 == 0) return -1; // lanes at uneven columns and rows: blocks of many sizes
            var c = new Vector2((ci + 0.5f) * CellW, (cj + 0.5f) * CellL);
            Settlement? best = null;
            float bd = float.MaxValue;
            foreach (var s in this.c.S.Settlements)
            {
                float d = Vector2.Distance(s.Pos, c);
                if (d < bd) { bd = d; best = s; }
            }
            if (best == null) return -1;
            var land = Lands.Of(m.ProvinceAt(best.Pos).Kind);
            float reach = best.Kind switch { SettlementKind.Town => 52, SettlementKind.Village => 40, _ => 26 } * (0.45f + 0.55f * land.Fields);
            float core = best.Kind switch { SettlementKind.Town => 17, SettlementKind.Castle => 14, _ => 10 };
            if (bd < core || bd > reach * (0.7f + 0.6f * Terrain.Noise(c.X * 0.04f, c.Y * 0.04f, 61))) return -1;
            // Only on open, dry, level ground clear of roads, water, the shore and the woods.
            for (int i = 0; i < 5; i++)
            {
                var q = i == 4 ? c : c + new Vector2((i & 1) == 0 ? -CellW / 2 : CellW / 2, (i & 2) == 0 ? -CellL / 2 : CellL / 2);
                if (t.WaterDepth(q) > 0 || t.Height(q) < 0.8f || m.Designed && m.Landness(q) < 0.97f) return -1;
                foreach (var l in t.Lakes) if (l.Inside(q, 2)) return -1;
            }
            if (m.RoadDist(c) < 3.5f) return -1;
            float slope = MathF.Abs(t.Height(c + new Vector2(CellW / 2, 0)) - t.Height(c - new Vector2(CellW / 2, 0))) / CellW
                        + MathF.Abs(t.Height(c + new Vector2(0, CellL / 2)) - t.Height(c - new Vector2(0, CellL / 2))) / CellL;
            if (slope > 0.22f) return -1;
            if (TreeCells.Contains((ci, cj))) return -1;
            uint h = Hash(ci * 31 + 7, cj * 17 + 3);
            var crop = Crops[h % (uint)Crops.Length];
            if (land.Barren) crop = (h & 1) == 0 ? FieldKind.Ploughed : FieldKind.Fallow;
            return (sbyte)crop;
        }

        HashSet<(int, int)> TreeCells => treeCells ??= t.Foliage
            .Where(f => f.Kind is FoliageKind.Broadleaf or FoliageKind.Pine or FoliageKind.Birch or FoliageKind.Bush)
            .Select(f => ((int)MathF.Floor(f.Pos.X / CellW), (int)MathF.Floor(f.Pos.Y / CellL))).ToHashSet();

        /// The field at p, if any: a strip with furrows along its length, a dark parting at its west and north edges, a
        /// hedge where the block ends.
        Sample? FieldAt(Vector2 p, float light)
        {
            int ci = (int)MathF.Floor(p.X / CellW), cj = (int)MathF.Floor(p.Y / CellL);
            sbyte v = CellAt(ci, cj);
            if (v < 0) return null;
            var kind = (FieldKind)v;
            float u = p.X - ci * CellW, w = p.Y - cj * CellL;
            float edge = 1.4f / k; // about a pixel and a half
            bool hedgeW = u < edge * 1.4f && CellAt(ci - 1, cj) < 0, hedgeE = u > CellW - edge * 1.4f && CellAt(ci + 1, cj) < 0;
            bool hedgeN = w < edge * 1.2f / SinP && CellAt(ci, cj - 1) < 0, hedgeS = w > CellL - edge * 1.2f / SinP && CellAt(ci, cj + 1) < 0;
            if (hedgeW || hedgeE || hedgeN || hedgeS) return new Sample(Stuff.Field, Ramps.Hedge, 1, light, p);
            var ramp = Ramps.Field(kind, (int)Hash(ci, cj), m.Season);
            if (u < 0.9f / k || w < 0.9f / (k * SinP)) return new Sample(Stuff.Field, ramp, 0, light * 0.5f, p); // the parting
            bool across = Hash(ci * 3 + 1, cj * 5 + 2) % 3 == 0; // some strips are ploughed the other way
            int furrow = kind is FieldKind.Ploughed or FieldKind.Grain or FieldKind.Green ? ((int)MathF.Floor((across ? w * SinP : u) * k / 2) & 1) : 0;
            return new Sample(Stuff.Field, ramp, 2 - furrow, light, p, Texture: kind is FieldKind.Pasture or FieldKind.Fallow);
        }
    }
}
