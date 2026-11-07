using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Xna.Framework.Input;

namespace Ironfield;

/// Every rebindable action. Field-view and command-view actions may share a key (Q couches on horseback, rotates the camera in command view).
public enum Act
{
    // field view
    Forward, Back, Left, Right, Sprint, Attack, Block, Couch, Interact, Rally, OrderWheel,
    // command view
    RotateLeft, RotateRight, AddToSelection,
    // orders (both views)
    Charge, Follow, FormHere, HoldPosition, Advance, FallBack, Spacing, FireAtWill, SelectAll,
    // general
    CommandView, Pause, Menu, Help, Graphics,
    // campaign map
    PartyScreen, Speed1, Speed2, Speed3, FollowParty, QuickSave, QuickLoad,
    // battle (later additions)
    Formation, Bash, SwapWeapon, NextDivision,
}

public enum MouseButton { Left, Right, Middle, X1, X2 }

/// A key or a mouse button.
public readonly record struct Bind(Keys? Key, MouseButton? Mouse)
{
    public override string ToString() => Mouse switch
    {
        MouseButton.Left => "LMB", MouseButton.Right => "RMB", MouseButton.Middle => "MMB", MouseButton.X1 => "MB4", MouseButton.X2 => "MB5",
        _ => Key switch
        {
            Keys.LeftShift or Keys.RightShift => "SHIFT",
            Keys.LeftControl or Keys.RightControl => "CTRL",
            Keys.LeftAlt or Keys.RightAlt => "ALT",
            Keys.OemTilde => "TILDE",
            Keys.Escape => "ESC",
            >= Keys.D0 and <= Keys.D9 => ((int)Key - (int)Keys.D0).ToString(),
            _ => Key?.ToString().ToUpperInvariant() ?? "",
        },
    };

    public static Bind? Parse(string s)
    {
        switch (s.Trim().ToUpperInvariant())
        {
            case "LMB" or "MOUSE1" or "MOUSELEFT": return new(null, MouseButton.Left);
            case "RMB" or "MOUSE2" or "MOUSERIGHT": return new(null, MouseButton.Right);
            case "MMB" or "MOUSE3" or "MOUSEMIDDLE": return new(null, MouseButton.Middle);
            case "MB4" or "MOUSE4": return new(null, MouseButton.X1);
            case "MB5" or "MOUSE5": return new(null, MouseButton.X2);
            case "SHIFT": return new(Keys.LeftShift, null);
            case "CTRL": return new(Keys.LeftControl, null);
            case "ALT": return new(Keys.LeftAlt, null);
            case "ESC": return new(Keys.Escape, null);
            case "TILDE": return new(Keys.OemTilde, null);
        }
        if (s.Length == 1 && char.IsDigit(s[0])) return new(Keys.D0 + (s[0] - '0'), null);
        return Enum.TryParse<Keys>(s.Trim(), true, out var k) ? new(k, null) : null;
    }
}

/// Key bindings and mouse settings. Defaults below; `controls.json` (next to the game or in the working directory) overrides them.
/// `dotnet run -- --write-controls` writes the defaults there as a template.
public sealed class Controls
{
    public const string FileName = "controls.json";

    public readonly Dictionary<Act, List<Bind>> Binds = new();
    public float Sensitivity = 1;
    public bool InvertY;
    public readonly List<string> Problems = new();

    KeyboardState kb, pkb;
    MouseState ms, pms;

    static readonly Dictionary<Act, string[]> Defaults = new()
    {
        [Act.Forward] = new[] { "W", "Up" }, [Act.Back] = new[] { "S", "Down" },
        [Act.Left] = new[] { "A", "Left" }, [Act.Right] = new[] { "D", "Right" },
        [Act.Sprint] = new[] { "SHIFT" }, [Act.Attack] = new[] { "LMB" }, [Act.Block] = new[] { "RMB" },
        [Act.Couch] = new[] { "Q" }, [Act.Interact] = new[] { "E" }, [Act.Rally] = new[] { "R" },
        [Act.OrderWheel] = new[] { "MMB", "Z" },
        [Act.RotateLeft] = new[] { "Q" }, [Act.RotateRight] = new[] { "E" }, [Act.AddToSelection] = new[] { "SHIFT" },
        [Act.Charge] = new[] { "C" }, [Act.Follow] = new[] { "F" }, [Act.FormHere] = new[] { "G" }, [Act.HoldPosition] = new[] { "H" },
        [Act.Advance] = new[] { "T" }, [Act.FallBack] = new[] { "B" }, [Act.Spacing] = new[] { "V" }, [Act.FireAtWill] = new[] { "X" },
        [Act.SelectAll] = new[] { "0", "TILDE" },
        [Act.CommandView] = new[] { "Tab" }, [Act.Pause] = new[] { "Space" }, [Act.Menu] = new[] { "ESC" }, [Act.Help] = new[] { "F1" },
        [Act.Graphics] = new[] { "F2" },
        [Act.PartyScreen] = new[] { "P" }, [Act.Speed1] = new[] { "1" }, [Act.Speed2] = new[] { "2" }, [Act.Speed3] = new[] { "3" },
        [Act.FollowParty] = new[] { "F" }, [Act.QuickSave] = new[] { "F5" }, [Act.QuickLoad] = new[] { "F9" },
        [Act.Formation] = new[] { "J" }, [Act.Bash] = new[] { "CTRL" }, [Act.SwapWeapon] = new[] { "N" },
        [Act.NextDivision] = new[] { "Y" },
    };

    public static readonly Dictionary<Act, string> Labels = new()
    {
        [Act.Forward] = "MOVE / REIN FORWARD", [Act.Back] = "MOVE / REIN BACK", [Act.Left] = "MOVE / TURN LEFT", [Act.Right] = "MOVE / TURN RIGHT",
        [Act.Sprint] = "SPRINT / GALLOP", [Act.Attack] = "ATTACK (HOLD = POWER)", [Act.Block] = "BLOCK (MATCH = PARRY)",
        [Act.Couch] = "COUCH LANCE", [Act.Interact] = "MOUNT / DISMOUNT", [Act.Rally] = "RALLY", [Act.OrderWheel] = "ORDER WHEEL (HOLD)",
        [Act.RotateLeft] = "ROTATE CAMERA LEFT", [Act.RotateRight] = "ROTATE CAMERA RIGHT", [Act.AddToSelection] = "ADD TO SELECTION",
        [Act.Charge] = "CHARGE", [Act.Follow] = "FOLLOW ME", [Act.FormHere] = "FORM LINE ON ME", [Act.HoldPosition] = "HOLD POSITION",
        [Act.Advance] = "ADVANCE", [Act.FallBack] = "FALL BACK", [Act.Spacing] = "LOOSE / TIGHT", [Act.FireAtWill] = "FIRE AT WILL / HOLD",
        [Act.SelectAll] = "SELECT ALL",
        [Act.CommandView] = "COMMAND VIEW", [Act.Pause] = "PAUSE", [Act.Menu] = "MENU", [Act.Help] = "CONTROLS", [Act.Graphics] = "GRAPHICS QUALITY",
        [Act.PartyScreen] = "PARTY SCREEN (MAP)", [Act.Speed1] = "SPEED X1 (MAP)", [Act.Speed2] = "SPEED X3 (MAP)", [Act.Speed3] = "SPEED X8 (MAP)",
        [Act.FollowParty] = "FOLLOW YOUR PARTY (MAP)", [Act.QuickSave] = "QUICK SAVE", [Act.QuickLoad] = "QUICK LOAD",
        [Act.Formation] = "CYCLE FORMATION", [Act.Bash] = "SHIELD BASH / KICK", [Act.SwapWeapon] = "SWAP WEAPON",
        [Act.NextDivision] = "NEXT DIVISION (GOALS)",
    };

    public Controls()
    {
        foreach (var (act, keys) in Defaults) Binds[act] = keys.Select(k => Bind.Parse(k)!.Value).ToList();
    }

    /// Where the settings screen writes your bindings (the user's Ironfield folder).
    public static string UserFile => Path.Combine(Settings.Folder, FileName);

    public void SaveUser()
    {
        Directory.CreateDirectory(Settings.Folder);
        var binds = new System.Text.Json.Nodes.JsonObject();
        foreach (var (act, list) in Binds) binds[act.ToString()] = new System.Text.Json.Nodes.JsonArray(list.Select(b => (System.Text.Json.Nodes.JsonNode)System.Text.Json.Nodes.JsonValue.Create(b.ToString())!).ToArray());
        var root = new System.Text.Json.Nodes.JsonObject { ["MouseSensitivity"] = Sensitivity, ["InvertMouseY"] = InvertY, ["Bindings"] = binds };
        File.WriteAllText(UserFile, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    public void ResetDefaults()
    {
        foreach (var (act, keys) in Defaults) Binds[act] = keys.Select(k => Bind.Parse(k)!.Value).ToList();
        Sensitivity = 1;
        InvertY = false;
    }

    /// The first key or mouse button pressed this frame (for rebinding).
    public Bind? AnyPressed()
    {
        foreach (var k in kb.GetPressedKeys()) if (!pkb.IsKeyDown(k)) return new Bind(k, null);
        foreach (MouseButton b in Enum.GetValues<MouseButton>())
            if (IsDown(new Bind(null, b), kb, ms) && !IsDown(new Bind(null, b), pkb, pms)) return new Bind(null, b);
        return null;
    }

    public static string? FindFile()
    {
        foreach (var dir in new[] { Settings.Folder, Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var p = Path.Combine(dir, FileName);
            if (File.Exists(p)) return p;
        }
        return null;
    }

    public static Controls Load()
    {
        var c = new Controls();
        var path = FindFile();
        if (path == null) return c;
        try
        {
            var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            if (root["MouseSensitivity"] is JsonNode sens) c.Sensitivity = Math.Clamp(sens.GetValue<float>(), 0.1f, 5);
            if (root["InvertMouseY"] is JsonNode inv) c.InvertY = inv.GetValue<bool>();
            if (root["Bindings"] is JsonObject binds)
                foreach (var (name, value) in binds)
                {
                    if (!Enum.TryParse<Act>(name, true, out var act)) { c.Problems.Add($"UNKNOWN ACTION {name}"); continue; }
                    var list = new List<Bind>();
                    foreach (var item in value!.AsArray())
                    {
                        var s = item!.GetValue<string>();
                        if (Bind.Parse(s) is Bind b) list.Add(b); else c.Problems.Add($"UNKNOWN KEY {s}");
                    }
                    c.Binds[act] = list;
                }
        }
        catch (Exception e) { c.Problems.Add("CONTROLS.JSON: " + e.Message); }
        return c;
    }

    public static void WriteDefaults(string path)
    {
        var d = new Controls();
        var binds = new JsonObject();
        foreach (var (act, list) in d.Binds) binds[act.ToString()] = new JsonArray(list.Select(b => (JsonNode)JsonValue.Create(b.ToString())!).ToArray());
        var root = new JsonObject { ["MouseSensitivity"] = 1.0, ["InvertMouseY"] = false, ["Bindings"] = binds };
        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    public void Update(KeyboardState k, MouseState m)
    {
        pkb = kb; pms = ms;
        kb = k; ms = m;
    }

    static bool IsDown(Bind b, KeyboardState k, MouseState m) => b.Key is Keys key ? k.IsKeyDown(key) : b.Mouse switch
    {
        MouseButton.Left => m.LeftButton == ButtonState.Pressed,
        MouseButton.Right => m.RightButton == ButtonState.Pressed,
        MouseButton.Middle => m.MiddleButton == ButtonState.Pressed,
        MouseButton.X1 => m.XButton1 == ButtonState.Pressed,
        MouseButton.X2 => m.XButton2 == ButtonState.Pressed,
        _ => false,
    };

    public bool Down(Act a) => Binds.TryGetValue(a, out var l) && l.Any(b => IsDown(b, kb, ms));
    public bool WasDown(Act a) => Binds.TryGetValue(a, out var l) && l.Any(b => IsDown(b, pkb, pms));
    public bool Pressed(Act a) => Down(a) && !WasDown(a);
    public bool Released(Act a) => !Down(a) && WasDown(a);
    public float Axis(Act pos, Act neg) => (Down(pos) ? 1 : 0) - (Down(neg) ? 1 : 0);

    /// The first key bound to an action ("W"), for compact hints like WASD.
    public string First(Act a) => Binds.TryGetValue(a, out var l) && l.Count > 0 ? l[0].ToString() : "-";

    /// "C" or "MMB/Z": what to press, for the HUD.
    public string Name(Act a) => Binds.TryGetValue(a, out var l) && l.Count > 0 ? string.Join("/", l.Select(b => b.ToString())) : "-";
}
