using System.Threading.Tasks;
using Godot;

namespace TideToll.Game.Tests;

/// <summary>
/// Real input for the tests: events go through Input.ParseInputEvent, the same path the operating system's events
/// take, so they reach the interface controls first and the map only if no control takes them.
/// </summary>
public static class TestInput
{
    public static void Motion(Vector2 at, Vector2 relative = default)
    {
        var e = new InputEventMouseMotion { Position = at, GlobalPosition = at, Relative = relative };
        Input.ParseInputEvent(e);
    }

    public static void Button(Vector2 at, MouseButton button, bool pressed)
    {
        var e = new InputEventMouseButton { Position = at, GlobalPosition = at, ButtonIndex = button, Pressed = pressed,
            ButtonMask = pressed ? (MouseButtonMask)(1 << ((int)button - 1)) : 0 };
        Input.ParseInputEvent(e);
    }

    public static void Key(Key key, bool shift = false)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true, ShiftPressed = shift });
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false, ShiftPressed = shift });
    }

    /// <summary>Waits n process frames (input parsed in one frame is handled by the next).</summary>
    public static async Task Frames(Node n, int count)
    {
        for (int i = 0; i < count; i++) await n.ToSignal(n.GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    /// <summary>Screen centre of a map cell in the current view.</summary>
    public static Vector2 CellScreen(Main main, int x, int y)
        => main.Rig.LocalToScreen(new Vector3((x + 0.5f) * 2f, 0, (y + 0.5f) * 2f));

    /// <summary>Screen centre of a control.</summary>
    public static Vector2 Centre(Control c) => c.GetGlobalRect().GetCenter();
}
