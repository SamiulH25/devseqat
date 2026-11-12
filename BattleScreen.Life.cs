using Microsoft.Xna.Framework;

namespace Ironfield;

/// Life on the battlefield (docs/WORLD_LIFE.md WL6): the map's `MapLife` in its battle mode, fed the men's places and
/// where the dead lie. Herds at the field's edge run from the men, birds lift from the woods as the armies close, crows
/// gather once the dead lie thick. Drawing only, stepped with the sim's time so a pause stills it.
partial class BattleScreen
{
    MapLife? fieldLife;
    readonly List<Vector2> fieldMen = new();
    readonly List<Vector2> fieldDead = new();
    float deadAt;
    bool fieldLifeDirty;
    Quality lastLifeQuality = (Quality)(-1);

    void NewFieldLife() { fieldLife = new MapLife(battle.Terrain, battle.Terrain.Seed, battle: true); fieldLifeDirty = true; }

    void FieldLife(float dt)
    {
        if (fieldLife == null) return;
        if (quality != lastLifeQuality) { lastLifeQuality = quality; fieldLifeDirty = true; }
        if (dt <= 0 && !fieldLifeDirty) return;
        fieldMen.Clear();
        for (int i = 0; i < battle.Units.Count; i += 3) if (battle.Units[i].Active) fieldMen.Add(battle.Units[i].Pos);
        deadAt -= dt;
        if (deadAt <= 0) { deadAt = 2; Carrion(); }
        fieldLife.Update(dt, camFocus, zoom, fieldMen, fieldDead, battle.Weather.Hour, quality);
        fieldLifeDirty = true;
    }

    /// Where crows gather: 16 m cells where a dozen men or more lie dead, the thickest first.
    void Carrion()
    {
        var cells = new Dictionary<(int, int), (Vector2 Sum, int N)>();
        foreach (var u in battle.Units)
        {
            if (!u.Dead) continue;
            var k = ((int)MathF.Floor(u.Pos.X / 16), (int)MathF.Floor(u.Pos.Y / 16));
            var c = cells.GetValueOrDefault(k);
            cells[k] = (c.Sum + u.Pos, c.N + 1);
        }
        fieldDead.Clear();
        fieldDead.AddRange(cells.Values.Where(c => c.N >= 12).OrderByDescending(c => c.N).Take(3).Select(c => c.Sum / c.N));
    }

    void FieldLifeMesh()
    {
        if (fieldLife == null) { world.ExtraTris = 0; return; }
        if (!fieldLifeDirty) return;
        fieldLifeDirty = false;
        var (v, n) = fieldLife.Build();
        world.SetExtra(v, n);
    }
}
