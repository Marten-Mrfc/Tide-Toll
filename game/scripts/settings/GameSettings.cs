using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Godot;

namespace TideToll.Game.Settings;

public enum WindowMode { Windowed, Borderless, Fullscreen }

/// <summary>
/// One store for every setting, kept in user://settings.cfg (ConfigFile syntax). Every setter clamps its value,
/// saves the file and raises Changed with the setting's key (see the Key constants), so the game can react.
/// Loading never fails: a missing file, a damaged file or a bad line gives the default for what could not be read,
/// and nothing is printed. Bindings are stored per action as "Key1,Key2".
/// </summary>
public sealed class GameSettings
{
    public const string DefaultPath = "user://settings.cfg";
    public static readonly string[] Buses = { "Master", "Music", "Ambience", "Effects", "Interface" };
    public static readonly int[] FrameLimits = { 30, 60, 120, 0 };          // 0 = no limit
    public static readonly int[] AutosaveChoices = { 0, 1, 2, 5 };          // minutes; 0 = off
    public const int MinInterface = 80, MaxInterface = 150;

    // Keys raised by Changed. A volume raises "volume:<Bus>"; a binding raises "keys".
    public const string KVolume = "volume:", KMuteInactive = "mute_inactive", KWindowMode = "window_mode",
        KResolution = "resolution", KVsync = "vsync", KFrameLimit = "frame_limit", KInterface = "interface_size",
        KCoinNumbers = "coin_numbers", KFollowTown = "follow_town", KAutosave = "autosave_minutes", KKeys = "keys";

    /// <summary>The store the game started with (set by SettingsRuntime); null until then.</summary>
    public static GameSettings? Current { get; set; }

    public event Action<string>? Changed;

    public string Path { get; }
    /// <summary>True when the last Load found a file it could read at least one value from.</summary>
    public bool LoadedFromFile { get; private set; }

    private readonly int[] _volume = new int[Buses.Length];
    private Dictionary<string, Key[]> _keys = GameInput.Defaults();

    public bool MuteWhenInactive { get; private set; }
    public WindowMode WindowMode { get; private set; }
    public Vector2I Resolution { get; private set; }
    public bool Vsync { get; private set; }
    public int FrameLimit { get; private set; }
    public int InterfaceSize { get; private set; }
    public bool ShowCoinNumbers { get; private set; }
    public bool FollowTown { get; private set; }
    public int AutosaveMinutes { get; private set; }
    /// <summary>Night 3: the first-minute hints already shown, one bit each (message-system section 4).</summary>
    public int HintsSeen { get; private set; }
    public bool HintSeen(int hint) => (HintsSeen & (1 << hint)) != 0;
    public void SetHintSeen(int hint)
    {
        int v = HintsSeen | (1 << hint);
        if (v == HintsSeen) return;
        // The file is read first: another store of the same file (the settings pages) may have written it since, and
        // this one must not put its older values back. No Changed event: nothing on screen shows it.
        Read();
        HintsSeen |= v;
        Save();
    }
    /// <summary>The first-minute hints show again (the next town that starts).</summary>
    public void ShowHintsAgain() { if (HintsSeen != 0) { HintsSeen = 0; Commit(KHints); } }
    public const string KHints = "hints_seen";

    public GameSettings(string path = DefaultPath)
    {
        Path = path;
        ResetValues();
    }

    /// <summary>Reads the store from its file (defaults for anything missing or unreadable).</summary>
    public static GameSettings Load(string path = DefaultPath)
    {
        var s = new GameSettings(path);
        s.Read();
        return s;
    }

    private void ResetValues()
    {
        int[] v = { 80, 70, 70, 80, 80 };
        Array.Copy(v, _volume, v.Length);
        MuteWhenInactive = false;
        HintsSeen = 0;
        WindowMode = WindowMode.Windowed;
        Resolution = new Vector2I(1280, 720);
        Vsync = true;
        FrameLimit = 0;
        InterfaceSize = 100;
        ShowCoinNumbers = true;
        FollowTown = true;
        AutosaveMinutes = 2;
        _keys = GameInput.Defaults();
    }

    // ---- reading values ----
    public int Volume(string bus) => _volume[Math.Max(0, Array.IndexOf(Buses, bus))];
    public float ContentScale => InterfaceSize / 100f;
    public Key KeyFor(string action, int slot) => _keys.TryGetValue(action, out var k) ? k[slot] : Key.None;
    public IReadOnlyDictionary<string, Key[]> Bindings => _keys;

    /// <summary>The action and slot that already use this key, if any (other than the given action and slot).</summary>
    public (string action, int slot)? FindKey(Key key, string exceptAction = "", int exceptSlot = -1)
    {
        if (key == Key.None) return null;
        foreach (var a in GameInput.Actions)
            for (int i = 0; i < GameInput.Slots; i++)
                if (_keys[a.Name][i] == key && !(a.Name == exceptAction && i == exceptSlot)) return (a.Name, i);
        return null;
    }

    // ---- changing values ----
    public void SetVolume(string bus, int value)
    {
        int i = Array.IndexOf(Buses, bus);
        if (i < 0) return;
        value = Math.Clamp(value, 0, 100);
        if (_volume[i] == value) return;
        _volume[i] = value;
        Commit(KVolume + bus);
    }

    public void SetMuteWhenInactive(bool on) { if (MuteWhenInactive != on) { MuteWhenInactive = on; Commit(KMuteInactive); } }
    public void SetWindowMode(WindowMode m) { if (WindowMode != m) { WindowMode = m; Commit(KWindowMode); } }
    public void SetResolution(Vector2I r)
    {
        r = new Vector2I(Math.Clamp(r.X, 640, 7680), Math.Clamp(r.Y, 360, 4320));
        if (Resolution != r) { Resolution = r; Commit(KResolution); }
    }
    public void SetVsync(bool on) { if (Vsync != on) { Vsync = on; Commit(KVsync); } }
    public void SetFrameLimit(int fps)
    {
        if (Array.IndexOf(FrameLimits, fps) < 0) fps = 0;
        if (FrameLimit != fps) { FrameLimit = fps; Commit(KFrameLimit); }
    }
    public void SetInterfaceSize(int percent)
    {
        percent = Math.Clamp((int)Math.Round(percent / 10.0) * 10, MinInterface, MaxInterface);
        if (InterfaceSize != percent) { InterfaceSize = percent; Commit(KInterface); }
    }
    public void SetShowCoinNumbers(bool on) { if (ShowCoinNumbers != on) { ShowCoinNumbers = on; Commit(KCoinNumbers); } }
    public void SetFollowTown(bool on) { if (FollowTown != on) { FollowTown = on; Commit(KFollowTown); } }
    public void SetAutosaveMinutes(int m)
    {
        if (Array.IndexOf(AutosaveChoices, m) < 0) m = 2;
        if (AutosaveMinutes != m) { AutosaveMinutes = m; Commit(KAutosave); }
    }

    public void SetKey(string action, int slot, Key key)
    {
        if (!_keys.TryGetValue(action, out var k) || slot < 0 || slot >= GameInput.Slots || k[slot] == key) return;
        k[slot] = key;
        Commit(KKeys);
    }

    /// <summary>Gives (action, slot) the key and the other action's slot this one's old key: one save, one event.</summary>
    public void SwapKey(string action, int slot, string other, int otherSlot)
    {
        var old = _keys[action][slot];
        _keys[action][slot] = _keys[other][otherSlot];
        _keys[other][otherSlot] = old;
        Commit(KKeys);
    }

    public void RestoreDefaultKeys() { _keys = GameInput.Defaults(); Commit(KKeys); }

    private void Commit(string key)
    {
        Save();
        Changed?.Invoke(key);
    }

    // ---- the file ----
    public void Save()
    {
        var sb = new StringBuilder();
        sb.Append("; Tide & Toll settings. Written by the game; a bad line is ignored.\n\n[sound]\n");
        for (int i = 0; i < Buses.Length; i++) sb.Append(Buses[i].ToLowerInvariant()).Append('=').Append(_volume[i]).Append('\n');
        sb.Append("mute_inactive=").Append(B(MuteWhenInactive)).Append("\n\n[display]\n");
        sb.Append("window_mode=\"").Append(WindowMode.ToString().ToLowerInvariant()).Append("\"\n");
        sb.Append("resolution=\"").Append(Resolution.X).Append('x').Append(Resolution.Y).Append("\"\n");
        sb.Append("vsync=").Append(B(Vsync)).Append('\n');
        sb.Append("frame_limit=").Append(FrameLimit).Append('\n');
        sb.Append("interface_size=").Append(InterfaceSize).Append('\n');
        sb.Append("coin_numbers=").Append(B(ShowCoinNumbers)).Append('\n');
        sb.Append("follow_town=").Append(B(FollowTown)).Append("\n\n[saving]\n");
        sb.Append("autosave_minutes=").Append(AutosaveMinutes).Append("\n\n[hints]\n");
        sb.Append("hints_seen=").Append(HintsSeen).Append("\n\n[keys]\n");
        foreach (var a in GameInput.Actions)
        {
            var k = _keys[a.Name];
            sb.Append(a.Name).Append("=\"").Append(GameInput.StoreName(k[0])).Append(',').Append(GameInput.StoreName(k[1])).Append("\"\n");
        }
        using var f = FileAccess.Open(Path, FileAccess.ModeFlags.Write);
        f?.StoreString(sb.ToString());
    }

    private static string B(bool b) => b ? "true" : "false";

    private void Read()
    {
        ResetValues();
        LoadedFromFile = false;
        if (!FileAccess.FileExists(Path)) return;
        string text;
        using (var f = FileAccess.Open(Path, FileAccess.ModeFlags.Read))
        {
            if (f == null) return;
            var bytes = f.GetBuffer((long)f.GetLength());
            try { text = new UTF8Encoding(false, true).GetString(bytes); }
            catch (Exception) { return; }   // not text at all: defaults
        }
        string section = "";
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == ';' || line[0] == '#') continue;
            if (line[0] == '[' && line[^1] == ']') { section = line[1..^1].Trim(); continue; }
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            var key = line[..eq].Trim();
            var val = line[(eq + 1)..].Trim().Trim('"');
            if (ReadValue(section, key, val)) LoadedFromFile = true;
        }
    }

    private bool ReadValue(string section, string key, string val)
    {
        bool okInt = int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n);
        bool okBool = bool.TryParse(val, out bool b);
        switch (section)
        {
            case "sound":
                if (key == "mute_inactive" && okBool) { MuteWhenInactive = b; return true; }
                for (int i = 0; i < Buses.Length; i++)
                    if (key == Buses[i].ToLowerInvariant() && okInt && n >= 0 && n <= 100) { _volume[i] = n; return true; }
                return false;
            case "display":
                switch (key)
                {
                    case "window_mode":
                        foreach (WindowMode m in Enum.GetValues<WindowMode>())
                            if (m.ToString().ToLowerInvariant() == val) { WindowMode = m; return true; }
                        return false;
                    case "resolution":
                        var p = val.Split('x');
                        if (p.Length == 2 && int.TryParse(p[0], out int w) && int.TryParse(p[1], out int h)
                            && w >= 640 && h >= 360 && w <= 7680 && h <= 4320) { Resolution = new Vector2I(w, h); return true; }
                        return false;
                    case "vsync" when okBool: Vsync = b; return true;
                    case "frame_limit" when okInt && Array.IndexOf(FrameLimits, n) >= 0: FrameLimit = n; return true;
                    case "interface_size" when okInt && n >= MinInterface && n <= MaxInterface && n % 10 == 0: InterfaceSize = n; return true;
                    case "coin_numbers" when okBool: ShowCoinNumbers = b; return true;
                    case "follow_town" when okBool: FollowTown = b; return true;
                }
                return false;
            case "saving":
                if (key == "autosave_minutes" && okInt && Array.IndexOf(AutosaveChoices, n) >= 0) { AutosaveMinutes = n; return true; }
                return false;
            case "hints":
                if (key == "hints_seen" && okInt && n >= 0 && n < 256) { HintsSeen = n; return true; }
                return false;
            case "keys":
                if (!_keys.ContainsKey(key)) return false;
                var parts = val.Split(',');
                if (parts.Length != GameInput.Slots) return false;
                var keys = new Key[GameInput.Slots];
                for (int i = 0; i < keys.Length; i++)
                {
                    keys[i] = GameInput.Parse(parts[i]);
                    if (keys[i] == Key.None && parts[i].Trim().Length > 0) return false;   // an unknown key name
                }
                _keys[key] = keys;
                return true;
        }
        return false;
    }
}
