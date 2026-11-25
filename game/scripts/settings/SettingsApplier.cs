using Godot;

namespace TideToll.Game.Settings;

/// <summary>
/// Puts a GameSettings store into effect: audio bus volumes (buses created when missing), the window through a
/// DisplaySeam, and key bindings into the InputMap. Attach() applies everything once and then follows every change.
/// The game's own code reads the rest (coin numbers, follow the town, autosave) from the store when it needs them.
/// </summary>
public sealed class SettingsApplier
{
    public GameSettings Store { get; }
    public DisplaySeam Display { get; }
    private readonly Window _window;
    private bool _windowActive = true;

    public SettingsApplier(GameSettings store, Window window, DisplaySeam? display = null)
    {
        Store = store;
        _window = window;
        Display = display ?? new DisplaySeam();
    }

    public void Attach()
    {
        ApplyAll();
        Store.Changed += OnChanged;
    }

    public void Detach() => Store.Changed -= OnChanged;

    public void ApplyAll()
    {
        EnsureBuses();
        foreach (var b in GameSettings.Buses) ApplyVolume(b);
        Display.ApplyWindow(_window, Store.WindowMode, Store.Resolution);
        Display.ApplyVsync(Store.Vsync);
        Display.ApplyFrameLimit(Store.FrameLimit);
        Display.ApplyContentScale(_window, Store.ContentScale);
        GameInput.Apply(Store.Bindings);
    }

    private void OnChanged(string key)
    {
        if (key.StartsWith(GameSettings.KVolume)) ApplyVolume(key[GameSettings.KVolume.Length..]);
        else switch (key)
        {
            case GameSettings.KMuteInactive: ApplyVolume("Master"); break;
            case GameSettings.KWindowMode:
            case GameSettings.KResolution: Display.ApplyWindow(_window, Store.WindowMode, Store.Resolution); break;
            case GameSettings.KVsync: Display.ApplyVsync(Store.Vsync); break;
            case GameSettings.KFrameLimit: Display.ApplyFrameLimit(Store.FrameLimit); break;
            case GameSettings.KInterface: Display.ApplyContentScale(_window, Store.ContentScale); break;
            case GameSettings.KKeys: GameInput.Apply(Store.Bindings); break;
        }
    }

    /// <summary>Called by SettingsRuntime when the window gains or loses focus.</summary>
    public void SetWindowActive(bool active)
    {
        _windowActive = active;
        ApplyVolume("Master");
    }

    /// <summary>Makes the five buses exist, each sending to Master.</summary>
    public static void EnsureBuses()
    {
        foreach (var name in GameSettings.Buses)
        {
            if (AudioServer.GetBusIndex(name) >= 0) continue;
            int i = AudioServer.BusCount;
            AudioServer.AddBus(i);
            AudioServer.SetBusName(i, name);
            AudioServer.SetBusSend(i, "Master");
        }
    }

    /// <summary>0 to 100 on a square curve (50 is about -12 dB); 0 mutes the bus.</summary>
    public static float VolumeDb(int v) => v <= 0 ? -80f : Mathf.LinearToDb(Mathf.Pow(v / 100f, 2f));

    public void ApplyVolume(string bus)
    {
        int i = AudioServer.GetBusIndex(bus);
        if (i < 0) { EnsureBuses(); i = AudioServer.GetBusIndex(bus); }
        int v = Store.Volume(bus);
        AudioServer.SetBusVolumeDb(i, VolumeDb(v));
        bool mute = v == 0 || (bus == "Master" && Store.MuteWhenInactive && !_windowActive);
        AudioServer.SetBusMute(i, mute);
    }
}
