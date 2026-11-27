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
        using (var down = new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true, ShiftPressed = shift }) Input.ParseInputEvent(down);
        using (var up = new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false, ShiftPressed = shift }) Input.ParseInputEvent(up);
    }

    /// <summary>
    /// Night 3: the tests' common way out. Wrappers of engine objects that nothing holds any more (key events made for a
    /// rebinding, events read back from the InputMap) are released before the engine stops: a full collection, the
    /// pending finalizers, a second collection, then two frames so the released references reach the engine. Under
    /// load the engine could exit before the collector had run and report each of them as "Leaked unsafe reference".
    /// An object still held by managed code (a true leak: a static list, a live field) is not collected by this and is
    /// still reported, so the test still fails on it.
    /// Night 9: the game's own way out (CleanExit) does this and more: it frees the scene first, so the wrappers the
    /// scene drops are finalized while the engine is whole, not during its shutdown ("Handle is not initialized").
    /// </summary>
    public static void QuitClean(Node n, int code) => CleanExit.Quit(n.GetTree(), code);

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
