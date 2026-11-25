using Godot;

namespace TideToll.Game.Settings;

/// <summary>
/// Start-up: add one of these to the tree first thing (e.g. as the first child of the main scene). It registers the
/// game's actions, loads user://settings.cfg into GameSettings.Current, applies it, follows changes, and mutes the
/// sound while the window is not active when the player asked for that.
/// </summary>
public partial class SettingsRuntime : Node
{
    public GameSettings Store { get; private set; } = null!;
    public SettingsApplier Applier { get; private set; } = null!;

    /// <summary>Set before adding to the tree to use another file or a test seam.</summary>
    public string SettingsPath { get; set; } = GameSettings.DefaultPath;
    public DisplaySeam? Display { get; set; }

    public override void _EnterTree()
    {
        GameInput.Register();
        Store = GameSettings.Load(SettingsPath);
        GameSettings.Current = Store;
        Applier = new SettingsApplier(Store, GetWindow(), Display);
        Applier.Attach();
    }

    public override void _ExitTree()
    {
        Applier.Detach();
        if (GameSettings.Current == Store) GameSettings.Current = null;
    }

    public override void _Notification(int what)
    {
        if (Applier == null) return;
        if (what == NotificationApplicationFocusOut) Applier.SetWindowActive(false);
        else if (what == NotificationApplicationFocusIn) Applier.SetWindowActive(true);
    }
}
