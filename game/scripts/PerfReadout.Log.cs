using System;
using System.Globalization;
using System.Text;
using Godot;

namespace TideToll.Game;

/// <summary>
/// n10, the readout's file: holding F3 for <see cref="HoldSeconds"/> (or starting the game with --perf-log, or
/// --perf-log=N for N seconds) writes the readout's numbers once a second for 60 s to perf-readout.csv in the profile
/// folder (user://, on Windows %APPDATA%\Godot\app_userdata\Tide &amp; Toll\). Each row holds the numbers of the half
/// second before it (the 95th percentile and the worst over the last 120 frames). Hiding the readout ends the file.
/// </summary>
public partial class PerfReadout
{
    public const double HoldSeconds = 3.0;
    public const int DefaultLogSeconds = 60;
    public const string LogName = "perf-readout.csv";
    public const string Header = "t_s,fps,frame_ms,p95_ms,worst_ms,sim_ms,interface_ms,scripts_ms,draw_cpu_ms,gpu_ms,draws,objects,triangles,villagers,window_w,window_h,vsync,cap,scale_3d,adapter";

    /// <summary>Rows written to the file so far (this file), whether it is still being written, and its full path.</summary>
    public int LogRows { get; private set; }
    public bool Logging => _log != null;
    public string LogPath { get; private set; } = "";
    public int LogSeconds { get; private set; }

    private FileAccess? _log;
    private ulong _logStartMs, _nextRowMs;
    private bool _holding, _wasShown, _holdStarted;
    private ulong _pressMs;
    private string _doneNote = "";

    /// <summary>--perf-log (60 s) or --perf-log=N among the start-up arguments; 0 when absent.</summary>
    private static int StartArgSeconds() => ArgSeconds(OS.GetCmdlineUserArgs()) is int n && n > 0 ? n : ArgSeconds(OS.GetCmdlineArgs());

    public static int ArgSeconds(string[] args)
    {
        foreach (var a in args)
        {
            if (a == "--perf-log") return DefaultLogSeconds;
            if (a.StartsWith("--perf-log=") && int.TryParse(a.AsSpan(11), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n > 0)
                return Math.Min(n, 3600);
        }
        return 0;
    }

    private void KeyDown()
    {
        _pressMs = Time.GetTicksMsec();
        _holding = true;
        _holdStarted = false;
        _wasShown = Visible;
        if (!Visible) SetShown(true);
    }

    private void KeyUp()
    {
        if (!_holding) return;
        _holding = false;
        if (_wasShown && !_holdStarted) SetShown(false);
    }

    private void HoldFrame(ulong ms)
    {
        if (!_holding || _holdStarted) return;
        if (!Input.IsActionPressed(ActionName)) { KeyUp(); return; }
        if (ms - _pressMs >= (ulong)(HoldSeconds * 1000)) { _holdStarted = true; StartLog(DefaultLogSeconds); }
    }

    /// <summary>Starts (or restarts) the file: a header, then a row a second for the given seconds.</summary>
    public void StartLog(int seconds)
    {
        CloseLog();
        LogPath = Main.Profile + LogName;
        _log = FileAccess.Open(LogPath, FileAccess.ModeFlags.Write);
        if (_log == null) { _doneNote = "could not write " + LogName; return; }
        _log.StoreLine(Header);
        LogSeconds = seconds;
        LogRows = 0;
        _doneNote = "";
        _logStartMs = Time.GetTicksMsec();
        _nextRowMs = _logStartMs + 1000;
        GD.Print("Readout: writing " + seconds + " s to " + ProjectSettings.GlobalizePath(LogPath));
    }

    private void LogFrame(ulong ms)
    {
        if (_log == null || ms < _nextRowMs) return;
        _nextRowMs += 1000;
        if (_nextRowMs < ms) _nextRowMs = ms + 1000;   // a long stall: one row, not a burst
        var r = new StringBuilder(256);
        var inv = CultureInfo.InvariantCulture;
        float scale3d = GetViewport().Scaling3DScale;
        r.Append(inv, $"{(ms - _logStartMs) / 1000.0:0.0},{Fps:0},{FrameMs:0.00},{P95Ms:0.00},{WorstMs:0.00},{SimMs:0.000},{HudMs:0.000},{ScriptsMs:0.000},");
        r.Append(inv, $"{DrawCpuMs:0.000},{GpuMs:0.000},{Draws},{Objects},{Triangles},{Villagers},{WindowSize.X},{WindowSize.Y},{(VsyncOn ? 1 : 0)},{Cap},{scale3d:0.00},\"{Adapter.Replace('"', '\'')}\"");
        _log.StoreLine(r.ToString());
        _log.Flush();
        LogRows++;
        if (LogRows >= LogSeconds) StopLog();
    }

    private void StopLog()
    {
        if (_log == null) return;
        _doneNote = "saved " + LogName + " (" + LogRows + " s)";
        GD.Print("Readout: " + _doneNote + " at " + ProjectSettings.GlobalizePath(LogPath));
        CloseLog();
    }

    private void CloseLog()
    {
        if (_log == null) return;
        _log.Close();
        _log.Dispose();
        _log = null;
    }

    /// <summary>The box's last line while a file is written or just after.</summary>
    private void LogStatus(StringBuilder sb)
    {
        if (_log != null) sb.Append(CultureInfo.InvariantCulture, $"\nrecording {LogRows} of {LogSeconds} s");
        else if (_doneNote.Length > 0) sb.Append('\n').Append(_doneNote);
    }
}
