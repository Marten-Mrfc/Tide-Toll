using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Godot;

namespace TideToll.Game.Tests;

/// <summary>
/// game-readout (n10): the performance readout under xvfb with the Mobile renderer. F3 shows and hides it and the choice
/// is written to settings.cfg with the next save (never in the frame of the key); hidden, it does not process (so it allocates nothing); shown, its numbers are present and
/// sane and are rebuilt about twice a second; its own cost a frame (the box's frame work and the two probes round the
/// interface) is under <see cref="CostGateMs"/> and <see cref="BytesGate"/> on average; holding F3 three seconds writes
/// perf-readout.csv a row a second; the box lies clear of every zone of the layout sheet at 1280 x 720 and 1920 x 1080.
/// </summary>
public partial class ReadoutTest : TownTestBase
{
    protected override string Marker => "READOUT PASS";
    protected override Vector2I WindowSize => new(1920, 1080);
    public const double CostGateMs = 0.05;
    public const long BytesGate = 300;

    private PerfReadout R => Main.Readout;

    protected override async Task Run()
    {
        Check(PerfReadout.ArgSeconds(new[] { "--perf-log" }) == 60 && PerfReadout.ArgSeconds(new[] { "--x", "--perf-log=5" }) == 5
            && PerfReadout.ArgSeconds(new[] { "--seed=3" }) == 0, "the --perf-log start-up argument is read");

        // Off by default, and then it does no work at all.
        await Frames(10);
        Check(!R.Shown && !R.IsProcessing() && !PerfReadout.Timing, "the readout is off by default and does not process");
        PerfReadout.Measure = true;
        R.SelfFrames = 0;
        await Frames(30);
        Check(R.SelfFrames == 0, "hidden, the readout runs no frame code");

        // F3 shows it and the settings file remembers it.
        await Key(Godot.Key.F3);
        Check(R.Shown && R.IsProcessing() && PerfReadout.Timing, "F3 shows the readout");
        // Never written in a frame of play: kept in the store, written with the town's next save.
        Check(Settings.GameSettings.Current!.ShowReadout && !SettingsText().Contains("readout=true"), "the key press wrote settings.cfg in a frame of play");
        Check(Main.Saver.SaveNow() && SettingsText().Contains("readout=true"), "settings.cfg remembers the readout shown after the next save");

        // About twice a second, and the numbers are sane.
        await Until(() => R.Updates >= 1, 2000, "a first update");
        int u0 = R.Updates, f0 = (int)Engine.GetProcessFrames();
        var clock = Stopwatch.StartNew();
        await Until(() => clock.Elapsed.TotalSeconds >= 3.0, 100000, "three seconds");
        double secs = clock.Elapsed.TotalSeconds;
        int per3 = R.Updates - u0, frames3 = (int)Engine.GetProcessFrames() - f0;
        // Twice a second on the clock; under a software renderer a frame can take longer than half a second, and then
        // there is one update a frame (never more than one a frame, never fewer than the half seconds allow).
        int want = (int)Math.Min(frames3, Math.Floor(secs / PerfReadout.UpdateSeconds));
        Check(per3 >= want - 1 && per3 <= want + 1 && per3 <= frames3, $"updated twice a second ({per3} updates in {secs:0.0} s, {frames3} frames)");
        Notes.Add($"{per3} updates in {secs:0.0} s ({frames3} frames);");
        Sane();

        // Its own cost a frame while it shows.
        // Under the software renderer a frame takes a large part of a second, so the twice-a-second update falls on far
        // more of the frames than it does at 60 fps. Measured: the frame work without the updates (per frame) and each
        // update (per update); the gate is their sum as at 60 frames a second (two updates in 60 frames). The raw
        // average over these frames is printed too.
        R.SelfTicks = 0; R.SelfBytes = 0; R.SelfFrames = 0; PerfReadout.ProbeSelfTicks = 0;
        R.SelfUpdateTicks = 0; R.SelfUpdateBytes = 0; R.SelfUpdates = 0;
        var cost = Stopwatch.StartNew();
        await Until(() => R.SelfFrames >= 120 && R.SelfUpdates >= 6, 100000, "120 frames and six updates");
        double tk = 1000.0 / Stopwatch.Frequency;
        double frameMs = (R.SelfTicks - R.SelfUpdateTicks + PerfReadout.ProbeSelfTicks) * tk / R.SelfFrames;
        double updateMs = R.SelfUpdateTicks * tk / R.SelfUpdates;
        double frameB = (double)(R.SelfBytes - R.SelfUpdateBytes) / R.SelfFrames, updateB = (double)R.SelfUpdateBytes / R.SelfUpdates;
        double ms = frameMs + updateMs * 2 / 60.0;
        long bytes = (long)Math.Ceiling(frameB + updateB * 2 / 60.0);
        double rawMs = (R.SelfTicks + PerfReadout.ProbeSelfTicks) * tk / R.SelfFrames;
        Notes.Add($"cost at 60 fps {ms * 1000:0.0} us/frame (gate {CostGateMs * 1000:0}), {bytes} B/frame (gate {BytesGate});");
        GD.Print($"READOUT cost: frame work {frameMs * 1000:0.0} us and {frameB:0} bytes a frame; an update {updateMs * 1000:0.0} us and {updateB:0} bytes; "
            + $"at 60 fps {ms:0.0000} ms and {bytes} bytes a frame; raw average here {rawMs:0.0000} ms and {R.SelfBytes / R.SelfFrames} bytes over {R.SelfFrames} frames, {R.SelfUpdates} updates in {cost.Elapsed.TotalSeconds:0.0} s");
        Check(ms < CostGateMs, $"readout cost {ms:0.0000} ms a frame (gate {CostGateMs})");
        Check(bytes < BytesGate, $"readout allocates {bytes} bytes a frame (gate {BytesGate})");
        PerfReadout.Measure = false;

        // Holding F3 three seconds writes the file; hiding ends it.
        string path = Main.Profile + PerfReadout.LogName;
        if (FileAccess.FileExists(path)) DirAccess.RemoveAbsolute(path);
        TestInput.KeyEvent(Godot.Key.F3, true);
        await Until(() => R.Logging, 100000, "the file to start after holding F3");
        TestInput.KeyEvent(Godot.Key.F3, false);
        await Frames(3);
        Check(R.Shown && R.Logging, "releasing after the hold keeps the readout and the file going");
        await Until(() => R.LogRows >= 3, 100000, "three rows");
        await Until(() => R.Text.Contains("recording"), 2000, "the box to say it is recording");
        await Key(Godot.Key.F3);
        Check(!R.Shown && !R.Logging && !R.IsProcessing() && !PerfReadout.Timing, "F3 hides the readout and ends the file");
        Check(!Settings.GameSettings.Current!.ShowReadout && SettingsText().Contains("readout=true"), "hiding wrote settings.cfg in a frame of play");
        Check(Main.Saver.SaveNow() && SettingsText().Contains("readout=false"), "settings.cfg remembers the readout hidden after the next save");
        CheckFile(path);

        // Its place, clear of every zone.
        foreach (var size in new[] { new Vector2I(1280, 720), new Vector2I(1920, 1080) })
            await Place(size);
    }

    private void Sane()
    {
        var r = R;
        GD.Print("READOUT numbers: " + r.Text.Replace('\n', '|'));
        Check(r.Fps > 0 && r.Fps < 2000, $"fps {r.Fps}");
        Check(r.FrameMs > 0 && r.FrameMs < 5000 && r.P95Ms >= r.FrameMs * 0.5 && r.WorstMs >= r.P95Ms, $"frame {r.FrameMs} p95 {r.P95Ms} worst {r.WorstMs}");
        Check(r.ScriptsMs > 0 && r.ScriptsMs < r.WorstMs + 1, $"scripts {r.ScriptsMs}");
        Check(r.SimMs >= 0 && r.SimMs <= r.ScriptsMs && r.HudMs > 0 && r.HudMs <= r.ScriptsMs, $"sim {r.SimMs} interface {r.HudMs} scripts {r.ScriptsMs}");
        Check(r.DrawCpuMs > 0 && r.DrawCpuMs < 5000 && r.GpuMs >= 0 && r.GpuMs < 5000, $"drawing cpu {r.DrawCpuMs} gpu {r.GpuMs}");
        Check(r.Draws > 10 && r.Objects > 10 && r.Triangles > 1000, $"draws {r.Draws} objects {r.Objects} triangles {r.Triangles}");
        Check(r.Villagers == T.VillagerCount && r.WindowSize == GetWindow().Size, "villagers and window");
        Check(r.Adapter.Length > 0 && r.Text.Contains(r.Adapter.Substring(0, Math.Min(10, r.Adapter.Length))), "the adapter's name");
        foreach (var w in new[] { "fps", "frame", "95th", "worst", "sim", "interface", "scripts", "cpu", "gpu", "draws", "objects", "triangles", "villagers", "vsync", "cap", "detail 100%" })
            Check(r.Text.Contains(w), "the box shows " + w);
        Notes.Add($"fps {r.Fps:0} frame {r.FrameMs:0.0} ms gpu {r.GpuMs:0.00} ms draws {r.Draws} on '{r.Adapter}';");
    }

    private static string SettingsText()
    {
        using var f = FileAccess.Open(Main.Profile + "settings.cfg", FileAccess.ModeFlags.Read);
        return f?.GetAsText() ?? "";
    }

    private void CheckFile(string path)
    {
        Check(FileAccess.FileExists(path), "perf-readout.csv written");
        using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        var lines = f.GetAsText().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Check(lines.Length >= 4 && lines[0] == PerfReadout.Header, $"header and rows ({lines.Length} lines)");
        int cols = PerfReadout.Header.Split(',').Length;
        for (int i = 1; i < lines.Length; i++)
        {
            var c = lines[i].Split(',');
            Check(c.Length >= cols, $"row {i} has {c.Length} columns");
            Check(double.TryParse(c[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double fm) && fm > 0, $"row {i} frame ms");
        }
        Notes.Add($"csv {lines.Length - 1} rows;");
    }

    private async Task Place(Vector2I size)
    {
        GetWindow().Size = size;
        await Frames(10);
        await Key(Godot.Key.F3);
        int u = R.Updates;
        await Until(() => R.Updates >= u + 2, 100000, "updates at " + size);
        var canvas = GetViewport().GetVisibleRect().Size;
        var z = new HudZones(canvas, UiKit.ScaleFor(GetWindow().Size.Y));
        var box = R.BoxRect;
        Check(new Rect2(Vector2.Zero, canvas).Encloses(box), $"box {box} inside the window at {size}");
        var zones = new (string, Rect2)[]
        {
            ("ledger", z.Ledger), ("person card", z.Person), ("bell", z.Bell), ("tide", z.Tide), ("shop", z.ShopMax),
            ("navigation", z.Nav), ("message", z.Message(HudZones.ChoiceMaxH)), ("line", z.Line), ("middle", z.Middle), ("page", z.Page),
        };
        foreach (var (name, r) in zones)
            if (name != "page") Check(!box.Intersects(r), $"box {box} clear of the {name} {r} at {size}");
        Notes.Add($"{size.X}x{size.Y} box {box.Position.X:0},{box.Position.Y:0} {box.Size.X:0}x{box.Size.Y:0};");
        GD.Print($"READOUT place {size}: box {box}, ledger {z.Ledger}, person {z.Person}, bell {z.Bell}");
        await Key(Godot.Key.F3);
        Check(!R.Shown, "hidden again");
    }
}
