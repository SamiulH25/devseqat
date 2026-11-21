using Microsoft.Xna.Framework;

namespace Ironfield;

/// Cairns over your named men where they fell (docs/pre-demo/COMPANY.md C7): a mound of grey stones, a spear stood in it with a
/// pennon in your colours. Drawing only (`World.Extra`), from `CampaignState.Cairns`.
sealed partial class MapLife
{
    readonly List<(int Id, Vector2 Pos, Color Cloth)> cairns = new();

    static readonly Color CairnStone = new(150, 146, 138), CairnDark = new(96, 94, 90), CairnShaft = new(150, 116, 78), CairnSteel = new(200, 200, 206);

    public void SetCairns(IEnumerable<(int Id, Vector2 Pos, Color Cloth)> list)
    {
        cairns.Clear();
        cairns.AddRange(list);
    }

    void BuildCairns()
    {
        foreach (var (id, pos, cloth) in cairns)
        {
            var r = new Random(id * 131 + 7);
            var b = new Vector3(pos.X, t.Height(pos), pos.Y);
            const float k = 1.4f; // big enough to read at the map's zoom
            mesh.Tag = Vector2.Zero;
            mesh.Blob(b + Vector3.Up * 0.1f, new Vector3(1.5f, 0.5f, 1.4f) * k, CairnDark, r, 0.2f); // the base, half sunk
            for (int i = 0; i < 7; i++) // stones heaped on it, smaller toward the top
            {
                float a = i * 2.4f + r.NextSingle(), d = i < 5 ? 0.75f : 0.25f, h = i < 5 ? 0.45f : 0.95f;
                var s = new Vector3(MathF.Cos(a) * d, h + r.NextSingle() * 0.1f, MathF.Sin(a) * d) * k;
                float z = (i < 5 ? 0.5f : 0.38f) * k;
                mesh.Lump(b + s, new Vector3(z, z * 0.8f, z), i % 2 == 0 ? CairnStone : CairnDark, r, 0.25f);
            }
            float top = 0.2f + 3.0f * k;
            mesh.Box(b + new Vector3(0.1f, 0.2f, 0), 0.14f, 0.14f, 3.0f * k, 0, CairnShaft); // the spear
            mesh.Box(b + new Vector3(0.1f, top, 0), 0.1f, 0.1f, 0.45f, 0, CairnSteel); // its head
            mesh.Box(b + new Vector3(0.6f, top - 0.6f, 0), 1.0f, 0.05f, 0.5f, 0, cloth); // the pennon, and its two tails
            mesh.Box(b + new Vector3(1.25f, top - 0.6f, 0), 0.3f, 0.05f, 0.2f, 0, cloth);
            mesh.Box(b + new Vector3(1.25f, top - 0.3f, 0), 0.3f, 0.05f, 0.2f, 0, cloth);
            mesh.Box(b + new Vector3(0.6f, top - 0.38f, 0), 1.0f, 0.055f, 0.08f, 0, Color.Lerp(cloth, Color.White, 0.6f)); // a pale stripe
        }
    }
}
