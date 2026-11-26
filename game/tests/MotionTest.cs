using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Godot;
using TideToll.Game.Settings;

namespace TideToll.Game.Tests;

/// <summary>
/// game-motion (round 7): every book transition through real injected input, sampled every frame at 1920 x 1080
/// (headless, --fixed-fps): pausing, the New town question in and out, the cover to the settings, each tab change
/// forwards and backwards, the settings back to the cover, resuming, resuming after the settings; then the impatient
/// cases (Esc, Enter, clicks and tab changes in the middle of transitions). Each frame records the camera (view size,
/// pivot, pitch), the board's amount, the town's models' height, the opacity of the interface, the lettering, the
/// question and the settings spread, the share of the pages each settings section shows, the ribbons, the turning
/// leaf's edge, what is Visible, the managed bytes allocated and the CPU milliseconds of the frame.
/// Checks (MotionTest.Checks.cs, bounds there): continuity, eased ends, the parts' overlaps and each transition's
/// length, no Visible flip of anything on screen, no allocation or CPU hitch, the save written outside the motion,
/// the end states. Prints "MOTION END name secs=... state=..." lines: tools/motion-check.sh runs the scene at 30, 60 and
/// 120 frames a second and compares them (the same time to within two frames, the same end state).
/// </summary>
public partial class MotionTest : TownTestBase
{
    protected override string Marker => "MOTION PASS";
    protected override Vector2I WindowSize => new(1920, 1080);
    private PauseMenu M => Main.Menu;
    private SettingsPages P => Main.Menu.SettingsSpread;
    private int _fps = 60;

    private partial class Probe : Node
    {
        public long Start;
        public double LastMs;
        public bool IsEnd;
        public Probe Other = null!;
        public override void _Process(double delta)
        {
            if (!IsEnd) Start = Stopwatch.GetTimestamp();
            else LastMs = (Stopwatch.GetTimestamp() - Other.Start) * 1000.0 / Stopwatch.Frequency;
        }
    }
    private Probe _end = null!;

    /// <summary>Allocation between probes placed in Main's child order: which node allocated in a frame (for messages).</summary>
    private partial class AllocProbe : Node
    {
        public static long Last;
        public string After = "";
        public long Frame;
        public override void _Process(double delta)
        {
            long now = GC.GetAllocatedBytesForCurrentThread();
            Frame = now - Last;
            Last = now;
        }
    }
    private readonly List<AllocProbe> _allocProbes = new();
    private void PlaceAllocProbes()
    {
        var kids = Main.GetChildren();
        for (int i = kids.Count - 1; i >= 0; i--)
        {
            var p = new AllocProbe { Name = "AllocProbe" + i, After = kids[i].Name };
            Main.AddChild(p);
            Main.MoveChild(p, i + 1);
            _allocProbes.Add(p);
        }
        var first = new AllocProbe { Name = "AllocProbeFirst", After = "(input, the engine, the test)", ProcessPriority = -100000 };
        AddChild(first);
        _allocProbes.Add(first);
    }
    private int BiggestProbe()
    {
        int at = 0; long big = -1;
        for (int i = 0; i < _allocProbes.Count; i++) if (_allocProbes[i].Frame > big) { big = _allocProbes[i].Frame; at = i; }
        return at;
    }

    /// <summary>One frame's sample.</summary>
    private struct S
    {
        public float Size, PX, PZ, Pitch, Cover, Town, Hud, Let, Ask, Pag, Leaf, Turn;
        public float Sec0, Sec1, Sec2, Sec3, Rib0, Rib1, Rib2, Rib3;
        public bool VHud, VLet, VAsk, VPag, VTown, VLeft, VS0, VS1, VS2, VS3, Running;
        public long Alloc;
        public double Cpu;
        public int Book, Saves, State, Allocator;
        public long AllocatorBytes;
        public bool Prep, Turning;
        public int TurnId;
    }
    private readonly S[] _buf = new S[3000];
    private int _n;
    private long _allocPrev;

    private void Sample()
    {
        long a = GC.GetAllocatedBytesForCurrentThread();
        var rig = Main.Rig;
        var p = rig.PivotLocal;
        ref var s = ref _buf[Math.Min(_n, _buf.Length - 1)];
        s.Size = rig.Camera.Size; s.PX = p.X; s.PZ = p.Z; s.Pitch = rig.PitchNow;
        s.Cover = Main.Cover.Amount; s.Town = Main.Cover.TownScale;
        s.Hud = Main.Hud.Fade; s.Let = M.LetteringAlpha; s.Ask = M.QuestionAlpha; s.Pag = M.PagesAlpha;
        s.Leaf = P.LeafEdge; s.Turn = P.TurnProgress;
        s.Sec0 = P.SectionShown(0); s.Sec1 = P.SectionShown(1); s.Sec2 = P.SectionShown(2); s.Sec3 = P.SectionShown(3);
        s.Rib0 = P.RibbonOut(0); s.Rib1 = P.RibbonOut(1); s.Rib2 = P.RibbonOut(2); s.Rib3 = P.RibbonOut(3);
        s.VHud = Main.Hud.Visible; s.VLet = M.LetteringVisible; s.VAsk = M.QuestionVisible; s.VPag = P.Visible;
        s.VTown = Main.MapOrigin.Visible; s.VLeft = Main.Cover.LeftPageShown;
        s.VS0 = P.SectionVisible(0); s.VS1 = P.SectionVisible(1); s.VS2 = P.SectionVisible(2); s.VS3 = P.SectionVisible(3);
        s.Running = M.Motion.Running || P.Turning;
        s.Prep = M.Motion.Running && M.Motion.Time == 0f;
        s.Turning = P.Turning;
        s.TurnId = P.Turns;
        s.Book = Main.Audio.BookSounds; s.Saves = M.MenuSaves; s.State = (int)M.Now;
        s.Cpu = _end.LastMs;
        s.Allocator = BiggestProbe();
        s.AllocatorBytes = _allocProbes[s.Allocator].Frame;
        s.Alloc = _n == 0 ? 0 : a - _allocPrev;
        _allocPrev = a;
        _n++;
    }

    /// <summary>
    /// Sends the input, then samples every frame until the motion and any page turn are over and two still frames have
    /// passed. The first sample is the frame that handled the input.
    /// </summary>
    private Task Record(Action input, int maxFrames = 900) => Record(i => { if (i == 0) input(); }, maxFrames);

    /// <summary>As Record, with <paramref name="step"/> called before each frame (0 first): input in the middle of motion.</summary>
    private async Task Record(Action<int> step, int maxFrames = 900)
    {
        _n = 0;
        // Godot's tracker of C# wrappers (every InputEvent the test sends makes one) grows its table now and then, a
        // large allocation in whatever frame it happens; collected first, it has room, so the frames measure the game.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        // A still frame first (the state before the input), then the input.
        await Frames(1);
        Sample();
        int still = 0;
        for (int i = 0; i < maxFrames; i++)
        {
            step(i);
            await Frames(1);
            Sample();
            if (!_buf[_n - 1].Running && ++still >= 3 && i > 2) return;
            if (_buf[_n - 1].Running) still = 0;
        }
        throw new Exception("a transition never came to rest");
    }

    private Vector2 Screen(Control c) => GetViewport().GetFinalTransform() * c.GetGlobalRect().GetCenter();

    private void ClickNow(Control c)
    {
        var p = Screen(c);
        TestInput.Motion(p);
        TestInput.Button(p, MouseButton.Left, true);
        TestInput.Button(p, MouseButton.Left, false);
    }

    private async Task Rest()
    {
        await Until(() => !M.Motion.Running && !P.Turning, 900, "the book at rest");
        await Frames(3);
    }

    protected override async Task Run()
    {
        foreach (var a in OS.GetCmdlineUserArgs()) if (a.StartsWith("--fps=") && int.TryParse(a.Substring(6), out int f)) _fps = f;
        Input.UseAccumulatedInput = false;
        var start = new Probe { Name = "MotionStart", ProcessPriority = -100000 };
        _end = new Probe { Name = "MotionEnd", ProcessPriority = 100000, IsEnd = true, Other = start };
        AddChild(start); AddChild(_end);
        PlaceAllocProbes();
        // A town with buildings and trees standing, the view moved by hand, tilted.
        Main.Driver.RunBot(new TideToll.Sim.Town.TownBot(TideToll.Sim.Town.BotKind.Payback, 1), t => t.Level >= 4, 20 * 60 * 20);
        await Frames(30); // the level's gift turns following back on: let it, then move the view by hand
        Main.Rig.UserMoved = true;
        // An exact place (from the town's extent, not wherever the follow easing has got to), so every frame rate starts alike.
        var ext = Main.TownExtent().GetCenter();
        Main.Rig.LookAt(new Vector3(ext.X + 6, 0, ext.Y + 4), 90f, instant: true);
        await Frames(30);
        var pivot0 = Main.Rig.PivotLocal; float size0 = Main.Rig.Camera.Size;

        // ---- the staged transitions, each from rest
        await Record(() => TestInput.Key(Godot.Key.Escape));
        Analyse("pause", Kind.Close);
        End("pause", PauseMenu.State.Cover);
        Check(M.CoverShown && GetViewport().GuiGetFocusOwner() == M.ResumeButton, "the cover with Resume focused after pausing");

        TestInput.Key(Godot.Key.Down); await Frames(2);
        TestInput.Key(Godot.Key.Down); await Frames(2);
        Check(GetViewport().GuiGetFocusOwner() == M.NewTownButton, "Down, Down reaches New town");
        await Record(() => TestInput.Key(Godot.Key.Enter));
        Analyse("question-in", Kind.Ask);
        End("question-in", PauseMenu.State.Asking);
        Check(GetViewport().GuiGetFocusOwner() == M.NoButton, "the question starts on Keep this town");
        await Record(() => TestInput.Key(Godot.Key.Escape));
        Analyse("question-out", Kind.Ask);
        End("question-out", PauseMenu.State.Cover);
        Check(GetViewport().GuiGetFocusOwner() == M.NewTownButton, "back from the question on New town");

        TestInput.Key(Godot.Key.Up); await Frames(2);
        Check(GetViewport().GuiGetFocusOwner() == M.SettingsButton, "Up reaches Settings");
        await Record(() => TestInput.Key(Godot.Key.Enter));
        Analyse("settings-open", Kind.ToSettings);
        End("settings-open", PauseMenu.State.Settings);
        Check(P.FocusedName() == "ribbon.Sound" && P.Section == "Sound", "the spread opens on Sound with its ribbon focused");

        // Forwards by the mouse (the ribbons), backwards by the keyboard (Up to the ribbon, Enter).
        for (int i = 1; i < 4; i++)
        {
            var ribbon = P.Controls["ribbon." + SettingsPages.Sections[i]];
            await Record(() => ClickNow(ribbon));
            string name = $"tab-{SettingsPages.Sections[i - 1].ToLowerInvariant()}-{SettingsPages.Sections[i].ToLowerInvariant()}";
            Analyse(name, Kind.Turn, from: i - 1, to: i);
            End(name, PauseMenu.State.Settings);
            Check(P.Section == SettingsPages.Sections[i] && P.FocusedName() == "ribbon." + SettingsPages.Sections[i], $"{name}: the section or the focus");
        }
        for (int i = 2; i >= 0; i--)
        {
            TestInput.Key(Godot.Key.Up); await Frames(2);
            Check(P.FocusedName() == "ribbon." + SettingsPages.Sections[i], $"Up did not reach the {SettingsPages.Sections[i]} ribbon ({P.FocusedName()})");
            await Record(() => TestInput.Key(Godot.Key.Enter));
            string name = $"tab-{SettingsPages.Sections[i + 1].ToLowerInvariant()}-{SettingsPages.Sections[i].ToLowerInvariant()}";
            Analyse(name, Kind.Turn, from: i + 1, to: i);
            End(name, PauseMenu.State.Settings);
            Check(P.Section == SettingsPages.Sections[i] && P.FocusedName() == "ribbon." + SettingsPages.Sections[i], $"{name}: the section or the focus");
        }
        await Record(() => TestInput.Key(Godot.Key.Escape));
        Analyse("settings-close", Kind.FromSettings);
        End("settings-close", PauseMenu.State.Cover);
        Check(GetViewport().GuiGetFocusOwner() == M.SettingsButton, "back on the cover with Settings focused");

        TestInput.Key(Godot.Key.Up); await Frames(2);
        await Record(() => TestInput.Key(Godot.Key.Enter));
        Analyse("resume", Kind.Open);
        End("resume", PauseMenu.State.Playing);
        Check(Main.Rig.PivotLocal.DistanceTo(pivot0) < 1e-3f && MathF.Abs(Main.Rig.Camera.Size - size0) < 1e-3f,
            $"Resume did not put the camera back exactly: {Main.Rig.PivotLocal} {Main.Rig.Camera.Size} (was {pivot0} {size0})");

        // Resume after the settings: pause, Settings, Back (a click), Resume.
        TestInput.Key(Godot.Key.Escape);
        await Until(() => M.CoverShown, 900, "the cover");
        M.SettingsButton.GrabFocus();
        TestInput.Key(Godot.Key.Enter);
        await Until(() => M.Now == PauseMenu.State.Settings && !M.Motion.Running, 900, "the settings at rest");
        await Frames(2);
        ClickNow(P.Controls["back"]);
        await Until(() => M.CoverShown, 900, "the cover after Back");
        M.ResumeButton.GrabFocus();
        await Record(() => TestInput.Key(Godot.Key.Enter));
        Analyse("resume-after-settings", Kind.Open);
        End("resume-after-settings", PauseMenu.State.Playing);
        Check(Main.Rig.PivotLocal.DistanceTo(pivot0) < 1e-3f && MathF.Abs(Main.Rig.Camera.Size - size0) < 1e-3f, "Resume after the settings did not put the camera back");

        await Impatient(pivot0, size0);
        Summary();
    }
}
