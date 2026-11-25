using System.Collections.Generic;
using Godot;

namespace TideToll.Game.Settings;

/// <summary>
/// The game's actions, from one table: their InputMap names, plain English names for the Keys page, and default keys.
/// Every action has two key slots (the second may be empty). Keys are physical keys (the place on the keyboard), so
/// W, A, S, D stay where they are on any layout. Register() puts the table into Godot's InputMap; Apply() puts a
/// player's bindings there.
/// </summary>
public static class GameInput
{
    public const int Slots = 2;

    public readonly record struct Action(string Name, string Label, Key Key1, Key Key2);

    public static readonly Action[] Actions =
    {
        new("tt_move_left", "Move left", Key.A, Key.Left),
        new("tt_move_right", "Move right", Key.D, Key.Right),
        new("tt_move_up", "Move up", Key.W, Key.Up),
        new("tt_move_down", "Move down", Key.S, Key.Down),
        new("tt_zoom_in", "Zoom in", Key.Equal, Key.KpAdd),
        new("tt_zoom_out", "Zoom out", Key.Minus, Key.KpSubtract),
        new("tt_home_view", "Back to the town", Key.Home, Key.None),
        new("tt_pause", "Pause", Key.Escape, Key.None),
        new("tt_bell", "Ring the bell", Key.Space, Key.None),
        new("tt_pave", "Pave a road", Key.P, Key.None),
        new("tt_buy_1", "Buy a villager", Key.Key1, Key.None),
        new("tt_buy_2", "Buy pace", Key.Key2, Key.None),
        new("tt_buy_3", "Buy load", Key.Key3, Key.None),
        new("tt_buy_4", "Buy paving", Key.Key4, Key.None),
        new("tt_buy_5", "Extend a cottage", Key.Key5, Key.E),
        new("tt_buy_6", "Buy tools", Key.Key6, Key.None),
        new("tt_buy_7", "Raise the Water price", Key.Key7, Key.None),
        new("tt_buy_8", "Raise the Grain price", Key.Key8, Key.None),
        new("tt_buy_9", "Raise the Flour price", Key.Key9, Key.None),
        new("tt_buy_0", "A louder bell", Key.Key0, Key.None),
        new("tt_offer_1", "Buy a building", Key.F, Key.None),
        new("tt_offer_2", "Buy a second building", Key.G, Key.None),
        new("tt_offer_3", "Buy a third building", Key.H, Key.None),
        new("tt_achievements", "Achievements", Key.Tab, Key.None),
    };

    public static int IndexOf(string action)
    {
        for (int i = 0; i < Actions.Length; i++)
            if (Actions[i].Name == action) return i;
        return -1;
    }

    /// <summary>The default bindings: action name to its two keys.</summary>
    public static Dictionary<string, Key[]> Defaults()
    {
        var d = new Dictionary<string, Key[]>();
        foreach (var a in Actions) d[a.Name] = new[] { a.Key1, a.Key2 };
        return d;
    }

    /// <summary>Adds every action to the InputMap (once) with its default keys.</summary>
    public static void Register() => Apply(Defaults());

    /// <summary>Replaces the keys of every action in the InputMap with these bindings (missing actions get defaults).</summary>
    public static void Apply(IReadOnlyDictionary<string, Key[]> bindings)
    {
        foreach (var a in Actions)
        {
            if (!InputMap.HasAction(a.Name)) InputMap.AddAction(a.Name);
            InputMap.ActionEraseEvents(a.Name);
            var keys = bindings.TryGetValue(a.Name, out var k) ? k : new[] { a.Key1, a.Key2 };
            foreach (var key in keys)
                if (key != Key.None) InputMap.ActionAddEvent(a.Name, new InputEventKey { PhysicalKeycode = key });
        }
    }

    /// <summary>The key of a key event: its physical key when known, else its key code.</summary>
    public static Key KeyOf(InputEventKey e) => e.PhysicalKeycode != Key.None ? e.PhysicalKeycode : e.Keycode;

    /// <summary>A short name for a key, as printed on the Keys page.</summary>
    public static string KeyName(Key k) => k switch
    {
        Key.None => "None",
        Key.Equal => "Plus",
        Key.Minus => "Minus",
        Key.KpAdd => "Num +",
        Key.KpSubtract => "Num -",
        Key.Escape => "Esc",
        Key.Left => "Left arrow",
        Key.Right => "Right arrow",
        Key.Up => "Up arrow",
        Key.Down => "Down arrow",
        >= Key.Key0 and <= Key.Key9 => ((int)(k - Key.Key0)).ToString(),
        _ => OS.GetKeycodeString(k),
    };

    /// <summary>The stored name of a key (Godot's own key name, read back by Parse).</summary>
    public static string StoreName(Key k) => k == Key.None ? "" : OS.GetKeycodeString(k);

    public static Key Parse(string s)
    {
        s = s.Trim();
        if (s.Length == 0) return Key.None;
        var k = OS.FindKeycodeFromString(s);
        return k;
    }
}
