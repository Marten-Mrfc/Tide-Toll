using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Godot;
using TideToll.Game.Settings;

namespace TideToll.Game;

/// <summary>
/// n10, the performance readout (docs/plans/n10/performance-research.md section 5b). F3 shows and hides a small paper
/// box in the left rail, above the bell (the room the layout sheet leaves between the ledger and the timers): frames a
/// second, the frame's milliseconds (mean, 95th percentile and worst of the last 120 frames), the simulation, the
/// interface and all scripts in milliseconds, the drawing's CPU and GPU milliseconds as the rendering server measures
/// them, draws, objects, triangles, villagers, the window, vsync, the frame cap and the graphics adapter. Off by
/// default and remembered in settings.cfg ([debug] readout); F3 is not one of the game's listed keys.
/// The text is rebuilt twice a second, never per frame. Hidden, the node does not process and the clocks it reads
/// (TownDriver, the probes round the interface) are off, so nothing runs or is allocated for it.
/// Holding F3 for three seconds, or starting with --perf-log, writes 60 s of the numbers once a second to
/// perf-readout.csv in the profile folder (PerfReadout.Log.cs).
/// </summary>
public partial class PerfReadout : CanvasLayer
{
    /// <summary>True while the readout shows: TownDriver and the interface probes take their timestamps.</summary>
    public static bool Timing;
    /// <summary>Stopwatch timestamp of the frame's first script (TownDriver, priority -100).</summary>
    public static long FrameStart;
    /// <summary>Stopwatch ticks spent in the simulation's steps, and in the interface, since the last update.</summary>
    public static long SimTicks, HudTicks;
    private static long _hudStart;

    public const string Action = "tt_readout";
    private static readonly StringName ActionName = new(Action);
    public const int Ring = 120;
    public const double UpdateSeconds = 0.5;

    // The numbers of the last update (tests read them; the box prints them).
    public double Fps { get; private set; }
    public double FrameMs { get; private set; }
    public double P95Ms { get; private set; }
    public double WorstMs { get; private set; }
    public double SimMs { get; private set; }
    public double HudMs { get; private set; }
    public double ScriptsMs { get; private set; }
    public double DrawCpuMs { get; private set; }
    public double GpuMs { get; private set; }
    public long Draws { get; private set; }
    public long Objects { get; private set; }
    public long Triangles { get; private set; }
    public int Villagers { get; private set; }
    public Vector2I WindowSize { get; private set; }
    public bool VsyncOn { get; private set; }
    public int Cap { get; private set; }
    public string Adapter { get; private set; } = "";
    public string Text => _label?.Text ?? "";
    public bool Shown => Visible;
    public int Updates { get; private set; }

    /// <summary>Tests: time and managed bytes of the readout's own frame work, summed while <see cref="Measure"/> is on.</summary>
    public static bool Measure;
    public long SelfTicks, SelfBytes, SelfFrames;
    /// <summary>Tests: the part of the above spent in the twice-a-second updates, and their number.</summary>
    public long SelfUpdateTicks, SelfUpdateBytes, SelfUpdates;
    public static long ProbeSelfTicks;

    private Main _main = null!;
    private PanelContainer _box = null!;
    private Label _label = null!;
    private Probe _hudIn = null!, _hudOut = null!;
    private Rid _vp;
    private readonly double[] _ring = new double[Ring], _sorted = new double[Ring];
    private int _ringAt, _ringN;
    private ulong _lastUs;
    private long _scriptTicks;
    private double _cpuSum, _gpuSum;
    private int _frames;
    private ulong _nextUpdateMs;
    private float _builtFor = -1f;
    private readonly StringBuilder _sb = new(512);
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>A timestamp node placed just before (In) or after (Out) the interface in Main's child order.</summary>
    private partial class Probe : Node
    {
        public bool IsOut;
        public override void _Process(double delta)
        {
            long now = Stopwatch.GetTimestamp();
            if (IsOut) HudTicks += now - _hudStart; else _hudStart = now;
            if (Measure) ProbeSelfTicks += Stopwatch.GetTimestamp() - now;
        }
    }

    public void Init(Main main)
    {
        _main = main;
        Name = "PerfReadout";
        Layer = 7;   // over the interface (5) and the page turn (6), under the pause menu (20)
        ProcessPriority = 100000;   // the frame's last script: the end of "scripts"
        ProcessMode = ProcessModeEnum.Always;
        if (!InputMap.HasAction(Action))
        {
            InputMap.AddAction(Action);
            InputMap.ActionAddEvent(Action, new InputEventKey { PhysicalKeycode = Key.F3 });
        }
        _hudIn = new Probe { Name = "ReadoutHudIn" };
        _hudOut = new Probe { Name = "ReadoutHudOut", IsOut = true };
        main.AddChild(_hudIn);
        main.MoveChild(_hudIn, main.Hud.GetIndex());
        main.AddChild(_hudOut);
        main.MoveChild(_hudOut, main.Hud.GetIndex() + 1);
        Build();
        bool on = GameSettings.Current?.ShowReadout ?? false;
        int logSeconds = StartArgSeconds();
        SetShown(on || logSeconds > 0, remember: false);
        if (logSeconds > 0) StartLog(logSeconds);
    }

    public override void _Ready()
    {
        _vp = GetViewport().GetViewportRid();
        string vendor = RenderingServer.GetVideoAdapterVendor();
        Adapter = RenderingServer.GetVideoAdapterName() + (vendor.Length > 0 ? " (" + vendor + ")" : "")
            + ", " + RenderingServer.GetCurrentRenderingDriverName() + " " + RenderingServer.GetCurrentRenderingMethod();
        if (Visible) RenderingServer.ViewportSetMeasureRenderTime(_vp, true);
    }

    public override void _ExitTree()
    {
        if (Timing) Timing = false;
        CloseLog();
    }

    /// <summary>Shows or hides the box; the clocks run only while it shows. remember: write it to settings.cfg.</summary>
    public void SetShown(bool on, bool remember = true)
    {
        Visible = on;
        SetProcess(on);
        _hudIn.SetProcess(on);
        _hudOut.SetProcess(on);
        Timing = on;
        if (_vp.IsValid) RenderingServer.ViewportSetMeasureRenderTime(_vp, on);
        if (!on) StopLog();
        ResetWindow();
        _ringN = 0; _ringAt = 0; _lastUs = 0;
        _nextUpdateMs = Time.GetTicksMsec() + (ulong)(UpdateSeconds * 1000);
        if (remember) GameSettings.Current?.SetShowReadout(on);
    }

    private void ResetWindow()
    {
        SimTicks = 0; HudTicks = 0; _scriptTicks = 0; _cpuSum = 0; _gpuSum = 0; _frames = 0;
    }

    public override void _Input(InputEvent e)
    {
        if (e is not InputEventKey) return;
        if (e.IsActionPressed(ActionName)) KeyDown();
        else if (e.IsActionReleased(ActionName)) KeyUp();
    }

    public override void _Process(double delta)
    {
        long now = Stopwatch.GetTimestamp();
        long b0 = Measure ? GC.GetAllocatedBytesForCurrentThread() : 0;
        if (FrameStart != 0) _scriptTicks += now - FrameStart;
        // The real time between frames (the engine's delta can be smoothed or capped, which would hide a hitch).
        ulong us = Time.GetTicksUsec();
        _ring[_ringAt] = _lastUs == 0 ? delta * 1000.0 : (us - _lastUs) / 1000.0;
        _lastUs = us;
        _ringAt = (_ringAt + 1) % Ring;
        if (_ringN < Ring) _ringN++;
        _cpuSum += RenderingServer.ViewportGetMeasuredRenderTimeCpu(_vp);
        _gpuSum += RenderingServer.ViewportGetMeasuredRenderTimeGpu(_vp);
        _frames++;
        ulong ms = Time.GetTicksMsec();
        HoldFrame(ms);
        if (ms >= _nextUpdateMs)
        {
            // Twice a second on the clock (no drift); a frame slower than half a second updates once.
            _nextUpdateMs += (ulong)(UpdateSeconds * 1000);
            if (_nextUpdateMs <= ms) _nextUpdateMs = ms + (ulong)(UpdateSeconds * 1000);
            long u0 = Measure ? Stopwatch.GetTimestamp() : 0, ub0 = Measure ? GC.GetAllocatedBytesForCurrentThread() : 0;
            Update();
            LogFrame(ms);
            if (Measure)
            {
                SelfUpdateTicks += Stopwatch.GetTimestamp() - u0;
                SelfUpdateBytes += GC.GetAllocatedBytesForCurrentThread() - ub0;
                SelfUpdates++;
            }
        }
        if (Measure)
        {
            SelfTicks += Stopwatch.GetTimestamp() - now;
            SelfBytes += GC.GetAllocatedBytesForCurrentThread() - b0;
            SelfFrames++;
        }
    }

    /// <summary>Twice a second: the numbers of the last half second, the text, the box's place.</summary>
    private void Update()
    {
        int n = Math.Max(1, _frames);
        double tick = 1000.0 / Stopwatch.Frequency;
        SimMs = SimTicks * tick / n;
        HudMs = HudTicks * tick / n;
        ScriptsMs = _scriptTicks * tick / n;
        DrawCpuMs = _cpuSum / n;
        GpuMs = _gpuSum / n;
        ResetWindow();
        double sum = 0, worst = 0;
        for (int i = 0; i < _ringN; i++) { sum += _ring[i]; worst = Math.Max(worst, _ring[i]); _sorted[i] = _ring[i]; }
        Array.Sort(_sorted, 0, _ringN);
        FrameMs = sum / Math.Max(1, _ringN);
        P95Ms = _ringN > 0 ? _sorted[Math.Min(_ringN - 1, (int)Math.Ceiling(_ringN * 0.95) - 1)] : 0;
        WorstMs = worst;
        Fps = Engine.GetFramesPerSecond();
        Draws = (long)RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalDrawCallsInFrame);
        Objects = (long)RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalObjectsInFrame);
        Triangles = (long)RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalPrimitivesInFrame);
        Villagers = _main.Driver.Town?.VillagerCount ?? 0;
        var win = GetWindow();
        WindowSize = win.Size;
        VsyncOn = DisplayServer.WindowGetVsyncMode() != DisplayServer.VSyncMode.Disabled;
        Cap = Engine.MaxFps;
        Updates++;

        var sb = _sb.Clear();
        sb.Append(Inv, $"fps {Fps:0}   frame {FrameMs:0.0} ms\n");
        sb.Append(Inv, $"95th {P95Ms:0.0}   worst {WorstMs:0.0} ms\n");
        sb.Append(Inv, $"sim {SimMs:0.00}  interface {HudMs:0.00}\n");
        sb.Append(Inv, $"scripts {ScriptsMs:0.00} ms\n");
        sb.Append(Inv, $"drawing cpu {DrawCpuMs:0.00}  gpu {GpuMs:0.00} ms\n");
        sb.Append(Inv, $"draws {Draws}   objects {Objects}\n");
        sb.Append(Inv, $"triangles {Triangles / 1000.0:0.0}k   villagers {Villagers}\n");
        sb.Append(Inv, $"{WindowSize.X} x {WindowSize.Y}  vsync {(VsyncOn ? "on" : "off")}  cap ");
        if (Cap > 0) sb.Append(Cap); else sb.Append("none");
        sb.Append('\n').Append(Adapter);
        LogStatus(sb);
        _label.Text = sb.ToString();
        Place();
    }

    private void Build()
    {
        foreach (var c in GetChildren()) { RemoveChild(c); c.QueueFree(); }
        float s = UiKit.ScaleFor(GetWindow().Size.Y);
        _builtFor = GetWindow().Size.Y;
        var card = UiKit.Card(s, UiKit.Paper, 1f);
        card.SetContentMarginAll(UiKit.Px(s, 8));
        _box = new PanelContainer { Name = "Box", MouseFilter = Control.MouseFilterEnum.Ignore, ClipContents = true };
        _box.AddThemeStyleboxOverride("panel", card);
        AddChild(_box);
        _label = new Label { Name = "Numbers", MouseFilter = Control.MouseFilterEnum.Ignore, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis };
        _label.AddThemeFontSizeOverride("font_size", UiKit.FontPx(s, 13));
        _label.AddThemeFontOverride("font", UiKit.TabRegular);
        _label.AddThemeColorOverride("font_color", UiKit.Ink);
        _label.AddThemeConstantOverride("line_spacing", 0);
        _box.AddChild(_label);
        Place();
    }

    /// <summary>The box's rectangle: the left rail's width, its foot 8 units above the bell (layout sheet section 2).</summary>
    public Rect2 BoxRect => _box.GetGlobalRect();

    private void Place()
    {
        if (GetWindow().Size.Y != _builtFor) { Build(); return; }
        var canvas = GetViewport().GetVisibleRect().Size;
        var z = new HudZones(canvas, UiKit.ScaleFor(GetWindow().Size.Y));
        float w = z.Rail, h = _box.GetCombinedMinimumSize().Y;
        _box.Size = new Vector2(w, h);
        _box.Position = new Vector2(z.P(HudZones.Margin), z.Bell.Position.Y - z.P(HudZones.Gap) - h);
    }
}
