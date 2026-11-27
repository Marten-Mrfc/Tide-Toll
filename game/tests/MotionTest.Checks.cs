using System;
using System.Collections.Generic;
using System.Text;
using Godot;
using TideToll.Game.Settings;

namespace TideToll.Game.Tests;

/// <summary>The checks on the sampled frames, with their bounds, and the summary lines.</summary>
public partial class MotionTest
{
    /// <summary>
    /// Continuity at 60 fps: an opacity moves at most StepAlpha of its range (0..1) in one frame; the camera, the board,
    /// the models, a section's share, a ribbon and the leaf at most StepMotion of their range.
    /// </summary>
    public const float StepAlpha = 0.2f, StepMotion = 0.12f;
    /// <summary>Eased ends: a staged part's first and last moving frames go at most EndSlow of its top speed; its speed
    /// changes between neighbouring frames by at most Jerk of its top speed.</summary>
    public const float EndSlow = 0.15f, Jerk = 0.35f;
    /// <summary>No hitch: managed bytes allocated in any one frame of a transition (the test's own sampling and awaits
    /// included), and CPU milliseconds of the game's work in any one frame (headless).</summary>
    public const long AllocGate = 8192;
    public const double CpuGate = 6.0;
    /// <summary>The frame that takes the input and prepares a book transition (its clock starts in the next frame; the
    /// left page's text is written in it): nothing moves in it, so it has gates of its own.</summary>
    public const long PrepAllocGate = 32768;
    public const double PrepCpuGate = 20.0;
    private long _prepAllocWorst, _saveFrameAlloc;
    private double _saveFrameCpu;
    private double _prepCpuWorst;
    private bool _cpuInfo;
    private int _cpuOver;

    private enum Kind { Close, Open, ToSettings, FromSettings, Ask, Turn, Impatient }

    private static readonly string[] ChName = { "camera size", "camera pivot", "pitch", "board", "town models", "interface", "lettering", "question", "settings spread", "leaf", "Sound", "Display", "Keys", "Saving", "ribbon Sound", "ribbon Display", "ribbon Keys", "ribbon Saving" };
    private const int Channels = 18;
    private static bool IsAlpha(int c) => c is >= 5 and <= 8;

    private float Val(int i, int c)
    {
        ref var s = ref _buf[i];
        return c switch
        {
            0 => s.Size,
            1 => MathF.Sqrt((s.PX - _buf[0].PX) * (s.PX - _buf[0].PX) + (s.PZ - _buf[0].PZ) * (s.PZ - _buf[0].PZ)),
            2 => s.Pitch, 3 => s.Cover, 4 => s.Town, 5 => s.Hud, 6 => s.Let, 7 => s.Ask, 8 => s.Pag, 9 => LeafAt(i),
            10 => s.Sec0, 11 => s.Sec1, 12 => s.Sec2, 13 => s.Sec3,
            14 => s.Rib0, 15 => s.Rib1, 16 => s.Rib2, _ => s.Rib3,
        };
    }

    private float PivotJerk(int i) => new Vector2(_buf[i].PX - 2 * _buf[i - 1].PX + _buf[i - 2].PX, _buf[i].PZ - 2 * _buf[i - 1].PZ + _buf[i - 2].PZ).Length();
    private float PivotPeak()
    {
        float pk = 0;
        for (int i = 1; i < _n; i++) pk = Math.Max(pk, new Vector2(_buf[i].PX - _buf[i - 1].PX, _buf[i].PZ - _buf[i - 1].PZ).Length());
        return pk;
    }

    /// <summary>The leaf's edge has a place only while a page turns (it draws nothing at rest): at rest, the nearest turning frame's.</summary>
    private float LeafAt(int i)
    {
        if (_buf[i].Turning) return _buf[i].Leaf;
        for (int k = i + 1; k < _n; k++) if (_buf[k].Turning) return _buf[k].Leaf;
        for (int k = i - 1; k >= 0; k--) if (_buf[k].Turning) return _buf[k].Leaf;
        return 0f;
    }

    /// <summary>The natural range of a channel (opacities, the board, shares: 1), or what it moved through here.</summary>
    private float Range(int c)
    {
        if (c is >= 3 and <= 8 or >= 10) return c == 4 ? 1f - BookCover.SunkScale : 1f;
        if (c == 9) return P.PagesRect.Size.X; // the leaf crosses the whole spread
        float lo = float.MaxValue, hi = float.MinValue;
        for (int i = 0; i < _n; i++) { float v = Val(i, c); lo = Math.Min(lo, v); hi = Math.Max(hi, v); }
        // A move turned round early covers little ground: the camera's range is never taken as less than 100 m of view
        // height, 50 m of pivot or 20 degrees of pitch.
        float floor = c switch { 0 => 100f, 1 => 50f, 2 => 20f, _ => 0f };
        return Math.Max(hi - lo, floor);
    }

    private readonly List<string> _ends = new();
    private readonly List<string> _lines = new();
    private long _allocWorst;
    private double _cpuWorst, _cpuSum;
    private int _cpuCount;
    private string _allocWorstAt = "", _cpuWorstAt = "";

    /// <summary>First and last sample index where a channel moves (0 based on the step into that sample), or -1.</summary>
    private (int first, int last) Window(int c, float eps)
    {
        int first = -1, last = -1;
        for (int i = 1; i < _n; i++)
            if (MathF.Abs(Val(i, c) - Val(i - 1, c)) > eps) { if (first < 0) first = i; last = i; }
        return (first, last);
    }

    private float Secs(int frames) => frames / (float)_fps;

    private void Analyse(string name, Kind kind, int from = -1, int to = -1)
    {
        var sb = new StringBuilder();
        // Length: from the frame that handled the input to the frame the motion ended.
        int endAt = -1;
        for (int i = 1; i < _n; i++) if (!_buf[i].Running) { endAt = i; break; }
        Check(endAt > 0, $"{name}: no motion recorded");
        float secs = Secs(endAt);
        sb.Append($"MOTION {name}: {secs:0.000} s ({endAt} frames)");

        // Continuity and eased ends, channel by channel.
        float worstShare = 0; string worstCh = "";
        for (int c = 0; c < Channels; c++)
        {
            float range = Range(c);
            float moved = 0;
            for (int i = 1; i < _n; i++) moved = Math.Max(moved, MathF.Abs(Val(i, c) - Val(0, c)));
            if (range < 1e-4f || moved < range * 0.01f || moved < 1e-4f) continue;
            float peak = 0, bound = IsAlpha(c) ? StepAlpha : StepMotion;
            for (int i = 1; i < _n; i++)
            {
                // Each turn's leaf is a leaf of its own (the next one starts at the other edge, drawing nothing yet).
                if (c == 9 && _buf[i].TurnId != _buf[i - 1].TurnId) continue;
                float d = MathF.Abs(Val(i, c) - Val(i - 1, c));
                peak = Math.Max(peak, d);
                float share = d / range;
                if (share > worstShare) { worstShare = share; worstCh = ChName[c]; }
                if (_fps == 60) Check(share <= bound, $"{name}: {ChName[c]} jumps {share:0.000} of its range in one frame at frame {i} (bound {bound})");
            }
            // The shape of the curves is checked at 60 fps and finer (at 30 a 0.2 s fade is six frames: too coarse to judge).
            // Round L3: impatient cases too (speed is carried over when one transition takes over another).
            if (peak <= 0 || _fps < 60) continue;
            var (first, last) = Window(c, peak * 1e-4f);
            float d0 = MathF.Abs(Val(first, c) - Val(first - 1, c)), d1 = MathF.Abs(Val(last, c) - Val(last - 1, c));
            Check(d0 <= peak * EndSlow, $"{name}: {ChName[c]} starts at speed {d0 / peak:0.00} of its top speed (bound {EndSlow})");
            Check(d1 <= peak * EndSlow, $"{name}: {ChName[c]} stops at speed {d1 / peak:0.00} of its top speed (bound {EndSlow})");
            for (int i = 2; i < _n; i++)
            {
                if (c == 9 && (_buf[i].TurnId != _buf[i - 1].TurnId || _buf[i - 1].TurnId != _buf[i - 2].TurnId)) continue;
                float a = (Val(i, c) - Val(i - 1, c)) - (Val(i - 1, c) - Val(i - 2, c));
                // Round L3: the pivot moves in the plane; its distance from the first frame bends wherever the path passes
                // near that point (a turned-round move does), so its change of speed is the vector's, against its top speed.
                if (c == 1) { a = PivotJerk(i); peak = Math.Max(peak, PivotPeak()); }
                Check(MathF.Abs(a) <= peak * Jerk, $"{name}: {ChName[c]} speed jumps {MathF.Abs(a) / peak:0.00} of its top speed at frame {i} (bound {Jerk})");
            }
        }
        sb.Append($"; largest step {worstShare:0.000} of range ({worstCh})");
        Flips(name);
        Hitches(name, endAt, kind == Kind.Impatient);
        if (kind != Kind.Impatient) Phases(name, kind, secs, from, to);
        _lines.Add(sb.ToString());
        GD.Print(sb.ToString());
    }

    /// <summary>Nothing on screen is switched on or off: a Visible change only where its opacity (or share) is nothing,
    /// and the town's models and the left page only under the shut board.</summary>
    private void Flips(string name)
    {
        for (int i = 1; i < _n; i++)
        {
            ref var a = ref _buf[i - 1]; ref var b = ref _buf[i];
            void F(bool va, bool vb, float xa, float xb, float eps, string what)
            {
                // Off: it shows nothing in the frame it is hidden; on: nothing yet in the first frame it is shown.
                if (va != vb) Check(xb <= eps, $"{name}: {what} switched {(vb ? "on" : "off")} at frame {i} while showing ({xb:0.000})");
            }
            F(a.VHud, b.VHud, a.Hud, b.Hud, 0.02f, "the interface");
            F(a.VLet, b.VLet, a.Let, b.Let, 0.02f, "the lettering");
            F(a.VAsk, b.VAsk, a.Ask, b.Ask, 0.02f, "the question");
            F(a.VPag, b.VPag, a.Pag, b.Pag, 0.02f, "the settings spread");
            F(a.VS0, b.VS0, a.Sec0, b.Sec0, 0.02f, "Sound"); F(a.VS1, b.VS1, a.Sec1, b.Sec1, 0.02f, "Display");
            F(a.VS2, b.VS2, a.Sec2, b.Sec2, 0.02f, "Keys"); F(a.VS3, b.VS3, a.Sec3, b.Sec3, 0.02f, "Saving");
            if (a.VTown != b.VTown || a.VLeft != b.VLeft)
                Check(a.Cover >= 0.9999f && b.Cover >= 0.9999f, $"{name}: the town or the left page switched at frame {i} with the board at {b.Cover:0.000}");
        }
    }

    /// <summary>Every frame of the motion (the input's frame included) under the allocation and CPU gates; the save
    /// written only after the motion's last frame.</summary>
    private void Hitches(string name, int endAt, bool impatient)
    {
        // An impatient case runs on through several transitions: every frame of it counts.
        if (impatient) endAt = _n - 1;
        for (int i = 1; i <= endAt && i < _n; i++)
        {
            ref var s = ref _buf[i];
            if (s.Prep)
            {
                Check(i == 1 || impatient, $"{name}: a frame without motion at frame {i}");
                _prepAllocWorst = Math.Max(_prepAllocWorst, s.Alloc); _prepCpuWorst = Math.Max(_prepCpuWorst, s.Cpu);
                Check(s.Alloc <= PrepAllocGate && s.Cpu <= PrepCpuGate, $"{name}: the frame before the motion allocated {s.Alloc} B ({_allocProbes[s.Allocator].After}) and took {s.Cpu:0.00} ms (gates {PrepAllocGate} B, {PrepCpuGate} ms)");
                continue;
            }
            // The pause's save is written in the frame after a motion's last, at rest: counted apart (information).
            if (!s.Running && s.Saves != _buf[i - 1].Saves) { _saveFrameAlloc = Math.Max(_saveFrameAlloc, s.Alloc); _saveFrameCpu = Math.Max(_saveFrameCpu, s.Cpu); continue; }
            if (s.Alloc > _allocWorst) { _allocWorst = s.Alloc; _allocWorstAt = $"{name} frame {i}"; }
            if (s.Cpu > _cpuWorst) { _cpuWorst = s.Cpu; _cpuWorstAt = $"{name} frame {i}"; }
            _cpuSum += s.Cpu; _cpuCount++;
            Check(s.Alloc <= AllocGate, $"{name}: frame {i} allocated {s.Alloc} bytes (gate {AllocGate}), most of it in {_allocProbes[s.Allocator].After} ({s.AllocatorBytes} B)");
            // --cpu-info (a working switch, never used by ./check): a frame over the CPU gate is printed, not fatal, so the
            // shape checks of every case can be read on a loaded machine. The gate itself is unchanged.
            if (_cpuInfo && s.Cpu > CpuGate) { _cpuOver++; GD.Print($"MOTION CPU over the gate: {name}: frame {i} took {s.Cpu:0.00} ms (gate {CpuGate})"); }
            else Check(s.Cpu <= CpuGate, $"{name}: frame {i} took {s.Cpu:0.00} ms of CPU (gate {CpuGate})");
            if (s.Running && s.Saves != _buf[i - 1].Saves) Check(false, $"{name}: the town was saved in frame {i}, a frame of the motion");
        }
    }

    private float When(int i) => Secs(i);

    /// <summary>The parts in their order, overlapping as designed, and the length in its range.</summary>
    private void Phases(string name, Kind kind, float secs, int from, int to)
    {
        float eps = 1e-5f;
        var cam = Window(0, Range(0) * eps + 1e-4f); var cov = Window(3, eps); var hud = Window(5, eps);
        var let = Window(6, eps); var ask = Window(7, eps); var pag = Window(8, eps);
        int touch = -1;
        // The landing: the board's first highest point (it touches, then settles a hair).
        for (int i = 1; i + 1 < _n; i++) if (_buf[i].Cover > 0.99f && _buf[i].Cover >= _buf[i + 1].Cover && _buf[i].Cover > _buf[i - 1].Cover) { touch = i; break; }
        int book = -1;
        for (int i = 1; i < _n; i++) if (_buf[i].Book > _buf[i - 1].Book) { book = i; break; }
        void Len(float lo, float hi) => Check(secs >= lo && secs <= hi, $"{name}: {secs:0.000} s, outside {lo}..{hi} s");
        switch (kind)
        {
            case Kind.Close:
            case Kind.FromSettings:
                Len(kind == Kind.Close ? 1.1f : 0.9f, kind == Kind.Close ? 1.4f : 1.25f);
                if (kind == Kind.Close) Check(hud.last > 0 && When(hud.last) <= 0.3f, $"{name}: the interface fades out in {When(hud.last):0.00} s (quickly: by 0.3 s)");
                else
                {
                    // Round 8: the leaf turns back to the town's pages from the start; the camera and the board move only after it.
                    Check(pag.first > 0 && pag.first <= 2, $"{name}: the leaf turns back from the start");
                    Check(cam.first > pag.last && cov.first > pag.last, $"{name}: the camera ({cam.first}) and the board ({cov.first}) wait for the leaf ({pag.last})");
                    LeafBehind(name);
                }
                Check(cam.first > 0 && cov.first > cam.first && cov.first < cam.last, $"{name}: the board starts while the camera is still arriving (camera {cam.first}..{cam.last}, board {cov.first})");
                Check(touch > 0 && book > 0 && Math.Abs(book - touch) <= 1, $"{name}: the book-close sound at frame {book}, the board lands at {touch}");
                Check(let.first > touch && let.first < let.last, $"{name}: the lettering comes in after the board lands (lettering {let.first}..{let.last}, touch {touch})");
                Check(_buf[_n - 1].Let == 1f && _buf[_n - 1].Cover == 1f && _buf[_n - 1].Town <= BookCover.SunkScale + 1e-4f, $"{name}: the end state of the cover");
                int saved = -1;
                for (int i = 1; i < _n; i++) if (_buf[i].Saves > _buf[i - 1].Saves) { saved = i; break; }
                if (kind == Kind.Close) Check(saved > 0 && !_buf[saved - 1].Running && !_buf[saved].Running, $"{name}: the pause's save (frame {saved}) is written after the motion's last frame");
                break;
            case Kind.Open:
            case Kind.ToSettings:
                Len(kind == Kind.Open ? 0.8f : 0.75f, kind == Kind.Open ? 1.1f : 1.05f);
                Check(let.last > 0 && let.last <= cov.last, $"{name}: the lettering goes before the board is up");
                Check(cov.first > 0 && cam.first >= cov.first && cam.first < cov.last, $"{name}: the camera moves while the board opens (board {cov.first}..{cov.last}, camera {cam.first})");
                Check(book > 0 && Math.Abs(book - cov.first) <= 1, $"{name}: the book-open sound at frame {book}, the board starts at {cov.first}");
                if (kind == Kind.Open) Check(hud.first > 0 && hud.first < cam.last && _buf[_n - 1].Hud == 1f, $"{name}: the interface comes back before the camera arrives ({hud.first} < {cam.last})");
                else
                {
                    // Round 8 (was: the spread cross-fades in as the camera settles): a leaf turns the settings pages in
                    // only once the camera and the board are at rest, the settings behind its edge on every frame.
                    Check(pag.first > cam.last && pag.first > cov.last && _buf[_n - 1].Pag == 1f, $"{name}: the leaf starts after the camera ({cam.last}) and the board ({cov.last}) are at rest ({pag.first})");
                    float leaf = When(pag.last) - When(pag.first - 1);
                    Check(leaf >= 0.3f && leaf <= 0.42f, $"{name}: the leaf turns in {leaf:0.000} s (about 0.35)");
                    LeafBehind(name);
                }
                break;
            case Kind.Ask:
                Len(0.25f, 0.4f);
                bool inward = _buf[_n - 1].Ask > 0.5f;
                // Round 8 (was: the two cross): never both above 15 percent; the one leaving goes first.
                Check(let.first > 0 && ask.first > 0 && (inward ? let.first < ask.first : ask.first < let.first), $"{name}: the one leaving goes first ({let.first}..{let.last}, {ask.first}..{ask.last})");
                float both = 0f;
                for (int i = 0; i < _n; i++) both = Math.Max(both, Math.Min(_buf[i].Let, _buf[i].Ask));
                Check(both <= 0.15f, $"{name}: the lettering and the question both at {both:0.000} at once (at most 0.15)");
                GD.Print($"MOTION the question {name}: both at once at most {both:0.000}");
                Check(inward ? _buf[_n - 1].Ask == 1f && !_buf[_n - 1].VLet : _buf[_n - 1].Let == 1f && !_buf[_n - 1].VAsk, $"{name}: the end state");
                break;
            case Kind.Turn:
            {
                Len(0.3f, 0.45f);
                int turns = 0;
                for (int i = 1; i < _n; i++) if (_buf[i].Book > _buf[i - 1].Book) turns++;
                Check(turns == 1, $"{name}: the page-turn sound played {turns} times");
                RibbonEased(name, from, to);
                // No blank frame: the two sections between them show the whole spread on every frame.
                for (int i = 0; i < _n; i++)
                {
                    float sum = Val(i, 10 + from) + Val(i, 10 + to);
                    Check(sum >= 0.999f, $"{name}: frame {i} shows only {sum:0.000} of the spread");
                }
                float l0 = _buf[1].Leaf, l1 = _buf[_n - 1].Leaf;
                for (int i = 2; i < _n; i++) if (_buf[i].Running) l1 = _buf[i].Leaf;
                Check(to > from ? l1 < l0 : l1 > l0, $"{name}: the leaf went the wrong way ({l0:0} to {l1:0})");
                Check(Val(_n - 1, 14 + to) == 1f && Val(_n - 1, 14 + from) == 0f && Val(_n - 1, 10 + to) == 1f && Val(_n - 1, 10 + from) == 0f, $"{name}: the ribbons or sections at the end");
                break;
            }
        }
    }

    /// <summary>The end state, for the comparison across frame rates.</summary>
    private void End(string name, PauseMenu.State want)
    {
        Check(M.Now == want, $"{name}: ended in {M.Now}, not {want}");
        Settled(name);
        var f = GetViewport().GuiGetFocusOwner();
        var p = Main.Rig.PivotLocal;
        string state = $"{M.Now} focus={f?.Name ?? "-"} section={P.Section} cam={Main.Rig.Camera.Size:0.000},{p.X:0.000},{p.Z:0.000} pitch={Main.Rig.PitchNow:0.00} "
            + $"board={Main.Cover.Amount:0.0000} town={Main.Cover.TownScale:0.0000} hud={(Main.Hud.Visible ? 1 : 0)}{Main.Hud.Fade:0.000} let={(M.LetteringVisible ? 1 : 0)}{M.LetteringAlpha:0.000} "
            + $"ask={(M.QuestionVisible ? 1 : 0)}{M.QuestionAlpha:0.000} pages={(P.Visible ? 1 : 0)}{M.PagesAlpha:0.000} paused={Main.Driver.Paused} locked={Main.Rig.InputLocked}";
        int endAt = 0;
        for (int i = 1; i < _n; i++) if (!_buf[i].Running) { endAt = i; break; }
        string line = $"MOTION END {name} secs={Secs(endAt):0.000} state={state}";
        _ends.Add(line);
        GD.Print(line);
    }

    /// <summary>A state at rest is exactly right: the parts that belong to it shown fully, the rest hidden, the input
    /// lock, the pause and the focus as they should be.</summary>
    private void Settled(string what)
    {
        Check(!M.Motion.Running && !P.Turning, $"{what}: still moving");
        var f = GetViewport().GuiGetFocusOwner();
        bool paused = Main.Driver.Paused, locked = Main.Rig.InputLocked;
        switch (M.Now)
        {
            case PauseMenu.State.Playing:
                Check(Main.Hud.Visible && Main.Hud.Fade == 1f && !locked && !paused, $"{what}: playing without the interface, input or the town running");
                Check(Main.Cover.Amount == 0f && Main.Cover.TownScale == 1f && Main.MapOrigin.Visible && Main.Cover.LeftPageShown, $"{what}: the book is not fully open");
                Check(!M.LetteringVisible && !M.QuestionVisible && !P.Visible, $"{what}: menu parts left over the game");
                break;
            case PauseMenu.State.Cover:
                Check(M.LetteringVisible && M.LetteringAlpha == 1f && !M.QuestionVisible && !P.Visible, $"{what}: the cover's lettering is not all there");
                Check(Main.Cover.Amount == 1f && !Main.MapOrigin.Visible && !Main.Hud.Visible && paused && locked, $"{what}: the cover without the book shut, the town paused or the input locked");
                Check(f == M.ResumeButton || f == M.SettingsButton || f == M.NewTownButton || f == M.QuitButton, $"{what}: no cover item has the focus ({f?.Name})");
                break;
            case PauseMenu.State.Settings:
                Check(P.Visible && P.Live && M.PagesAlpha == 1f && Main.Cover.Amount == 0f && !M.LetteringVisible && paused && locked, $"{what}: the settings spread is not wholly there");
                Check(P.FocusedName() != null, $"{what}: no settings control has the focus ({f?.Name})");
                break;
            case PauseMenu.State.Asking:
                Check(M.QuestionVisible && M.QuestionAlpha == 1f && !M.LetteringVisible && (f == M.YesButton || f == M.NoButton), $"{what}: the question is not wholly there");
                break;
            default:
                throw new Exception($"{what}: at rest in {M.Now}");
        }
    }

    private void Summary()
    {
        GD.Print($"MOTION bounds: step per frame at 60 fps {StepAlpha} (opacity) / {StepMotion} (motion) of range; ends {EndSlow} and speed change {Jerk} of top speed; "
            + $"alloc {AllocGate} B and cpu {CpuGate} ms per frame");
        GD.Print($"MOTION the frame before a book transition's first (input, the left page written): worst alloc {_prepAllocWorst} B, cpu {_prepCpuWorst:0.00} ms (gates {PrepAllocGate} B, {PrepCpuGate} ms)");
        GD.Print($"MOTION the save's frame (after a motion's last, at rest): alloc {_saveFrameAlloc} B, cpu {_saveFrameCpu:0.00} ms (information)");
        GD.Print($"MOTION worst frame: alloc {_allocWorst} B ({_allocWorstAt}), cpu {_cpuWorst:0.00} ms ({_cpuWorstAt}), cpu mean {(_cpuCount > 0 ? _cpuSum / _cpuCount : 0):0.00} ms over {_cpuCount} frames");
        Notes.Add($"fps={_fps} transitions={_ends.Count} alloc-max={_allocWorst}B cpu-max={_cpuWorst:0.00}ms");
    }
}
