using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TideToll.Game.Settings;

namespace TideToll.Game.Tests;

/// <summary>
/// Round 8's checks: the settings arriving and leaving by a leaf (the settings shown exactly behind its edge), the
/// ribbons' eased colour, the achievements and age pages' entrance and exit (continuity, eased ends, Visible flips only
/// at no opacity, the impatient cases), and the 2D settings pages lying on the 3D pages at two window sizes and three
/// interface sizes.
/// </summary>
public partial class MotionTest
{
    private static float Lum(Color c) => c.R + c.G + c.B;

    /// <summary>While the spread arrives or leaves, the section on show covers exactly what the leaf has turned.</summary>
    private void LeafBehind(string name)
    {
        float worst = 0f;
        for (int i = 0; i < _n; i++)
        {
            ref var s = ref _buf[i];
            if (!s.VPag) continue;
            float shown = Math.Max(Math.Max(s.Sec0, s.Sec1), Math.Max(s.Sec2, s.Sec3));
            worst = Math.Max(worst, MathF.Abs(shown - s.Pag));
        }
        Check(worst <= 0.01f, $"{name}: the settings shown differ from the leaf's place by {worst:0.000} of the spread");
        GD.Print($"MOTION the leaf {name}: settings behind the leaf's edge to within {worst:0.0000} of the spread");
    }

    /// <summary>The ribbons' colour follows the turn: no frame changes it by more than a quarter of its whole change.</summary>
    private void RibbonEased(string name, int from, int to)
    {
        foreach (int k in new[] { from, to })
        {
            float V(int i) => k switch { 0 => _buf[i].Rc0, 1 => _buf[i].Rc1, 2 => _buf[i].Rc2, _ => _buf[i].Rc3 };
            float total = MathF.Abs(V(_n - 1) - V(0)), step = 0f;
            int moving = 0;
            for (int i = 1; i < _n; i++) { float d = MathF.Abs(V(i) - V(i - 1)); step = Math.Max(step, d); if (d > 1e-5f) moving++; }
            Check(total > 0.01f, $"{name}: ribbon {k} did not change colour ({total:0.000})");
            Check(step <= total * 0.25f && moving >= 4, $"{name}: ribbon {k}'s colour changes {step / total:0.00} of its change in one frame, over {moving} frames");
        }
    }

    // ------------------------------------------------------------------ the achievements and age pages

    private readonly List<(float a, bool v, bool hudShown)> _pg = new();

    private TownHud.PageFade Fade(bool ach) => ach ? Main.Hud.AchievementsFade : Main.Hud.AgePageFade;

    /// <summary>Samples one page every frame while <paramref name="step"/> acts, until both pages are at rest.</summary>
    private async Task RecordPage(bool ach, Action<int> step, int maxFrames = 300)
    {
        _pg.Clear();
        var f = Fade(ach);
        _pg.Add((f.A, f.Panel!.Visible, Main.Hud.Visible && Main.Hud.Fade > 0f));
        int still = 0;
        for (int i = 0; i < maxFrames; i++)
        {
            step(i);
            await Frames(1);
            f = Fade(ach);
            _pg.Add((f.A, f.Panel!.Visible, Main.Hud.Visible && Main.Hud.Fade > 0f));
            bool moving = Main.Hud.AchievementsFade.Motion.Running || Main.Hud.AgePageFade.Motion.Running || M.Motion.Running;
            if (!moving && ++still >= 3 && i > 2) return;
            if (moving) still = 0;
        }
        throw new Exception("a page never came to rest");
    }

    private void PageChecks(string name, bool eased, float want)
    {
        // Smootherstep's top speed is 1.875 of the mean: a full 0.22 s fade moves at most this much in a frame (20 % margin).
        float bound = 1.875f / TownHud.PageSeconds / _fps * 1.2f, peak = 0f;
        int first = -1, last = -1;
        for (int i = 1; i < _pg.Count; i++)
        {
            float d = MathF.Abs(_pg[i].a - _pg[i - 1].a);
            if (_pg[i].hudShown) Check(d <= bound, $"{name}: the page jumps {d:0.000} in one frame at frame {i} (bound {bound:0.000})");
            if (_pg[i].v != _pg[i - 1].v && _pg[i].hudShown) Check(_pg[i].v ? _pg[i - 1].a <= 0f && _pg[i].a <= 0.02f : _pg[i].a <= 0f, $"{name}: the page switched {(_pg[i].v ? "on" : "off")} at opacity {_pg[i].a:0.000}/{_pg[i - 1].a:0.000}");
            if (d > 1e-5f) { if (first < 0) first = i; last = i; peak = Math.Max(peak, d); }
        }
        Check(MathF.Abs(_pg[^1].a - want) < 1e-5f && _pg[^1].v == want > 0f, $"{name}: ends at {_pg[^1].a:0.000} visible {_pg[^1].v}, not {want}");
        if (!eased || first < 0 || _fps < 60) return;
        float secs = (last - first + 1) / (float)_fps;
        Check(MathF.Abs(secs - TownHud.PageSeconds) <= 2.5f / _fps, $"{name}: {secs:0.000} s, not {TownHud.PageSeconds}");
        float d0 = MathF.Abs(_pg[first].a - _pg[first - 1].a), d1 = MathF.Abs(_pg[last].a - _pg[last - 1].a);
        Check(d0 <= peak * EndSlow && d1 <= peak * EndSlow, $"{name}: starts at {d0 / peak:0.00} and stops at {d1 / peak:0.00} of its top speed (bound {EndSlow})");
        GD.Print($"MOTION the page {name}: {secs:0.000} s, largest step {peak:0.000} (bound {bound:0.000}), ends {d0 / peak:0.00}/{d1 / peak:0.00} of top speed");
    }

    private async Task HudPages()
    {
        var hud = Main.Hud;
        Check(M.Now == PauseMenu.State.Playing && hud.Fade == 1f, "the pages' test starts in the town");
        await RecordPage(true, i => { if (i == 0) hud.OpenAchievements(); });
        PageChecks("achievements-in", true, 1f);
        float y = hud.AchievementsFade.Panel!.Position.Y;
        await RecordPage(true, i => { if (i == 0) hud.CloseAchievements(); });
        PageChecks("achievements-out", true, 0f);
        // Toggled quickly: open, close after a few frames, open again.
        await RecordPage(true, i => { if (i == 0 || i == 9) hud.ToggleAchievements(); if (i == 5) hud.ToggleAchievements(); });
        PageChecks("achievements-toggled", false, 1f);
        Check(MathF.Abs(hud.AchievementsFade.Panel!.Position.Y - y) < 1f, "the page did not come back to its place");
        // Esc while it goes out, then Tab again, then Esc as it comes in.
        await RecordPage(true, i => { if (i == 0) TestInput.Key(Godot.Key.Escape); if (i == 4) TestInput.Key(Godot.Key.Tab); if (i == 8) TestInput.Key(Godot.Key.Escape); });
        PageChecks("achievements-esc", false, 0f);
        Check(!M.IsPaused, "Esc on the achievements page paused the game");
        // The age page opened while the achievements are open: one goes as the other comes.
        hud.OpenAchievements();
        await Frames(30);
        await RecordPage(false, i => { if (i == 0) hud.OpenAgePage(); });
        PageChecks("age-in", true, 1f);
        Check(!hud.AchievementsOpen && !hud.AchievementsFade.Panel!.Visible, "the achievements stayed under the age page");
        await RecordPage(false, i => { if (i == 0) hud.CloseAgePage(); if (i == 3) hud.OpenAgePage(); });
        PageChecks("age-turned", false, 1f);
        // The pause takes over mid-way: the page fades with the interface, then closes quietly.
        await RecordPage(false, i => { if (i == 0) hud.CloseAgePage(); if (i == 2) TestInput.Key(Godot.Key.Escape); });
        PageChecks("age-paused", false, 0f);
        Check(M.Now == PauseMenu.State.Cover, "the pause did not take over");
        TestInput.Key(Godot.Key.Escape);
        await Until(() => M.Now == PauseMenu.State.Playing && !M.Motion.Running, 900, "the town");
        Check(!hud.AgePageOpen && !hud.AgePageFade.Panel!.Visible && !hud.AchievementsFade.Panel!.Visible, "a page came back after the pause");
    }

    // ------------------------------------------------------------------ the pages on the 3D pages

    private async Task PagesAlign()
    {
        var win = GetWindow();
        var size0 = win.Size; float scale0 = win.ContentScaleFactor;
        TestInput.Key(Godot.Key.Escape);
        await Until(() => M.CoverShown && !M.Motion.Running, 900, "the cover");
        M.SettingsButton.GrabFocus();
        TestInput.Key(Godot.Key.Enter);
        await Until(() => M.Now == PauseMenu.State.Settings && !M.Motion.Running, 900, "the settings");
        float worstAll = 0f;
        foreach (var size in new[] { new Vector2I(1920, 1080), new Vector2I(1280, 720) })
        foreach (float ui in new[] { 0.8f, 1.0f, 1.5f })
        {
            win.Size = size;
            win.ContentScaleFactor = ui;
            await Frames(4);
            var cam = Main.Rig.Camera;
            var book = Main.Book.GlobalTransform;
            float hx = Godot.Mathf.Abs(BookBuilder.PageWidth + 1f), hz = BookBuilder.PageDepth / 2f;
            var a = cam.UnprojectPosition(book * new Vector3(-hx, 0, -hz));
            var b = cam.UnprojectPosition(book * new Vector3(hx, 0, hz));
            var r3 = new Rect2(new Vector2(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y)), (a - b).Abs());
            var r2 = P.PagesRect;
            // Canvas units to the window's pixels.
            float px = win.Size.Y / GetViewport().GetVisibleRect().Size.Y;
            float worst = px * Math.Max(Math.Max(MathF.Abs(r3.Position.X - r2.Position.X), MathF.Abs(r3.Position.Y - r2.Position.Y)),
                Math.Max(MathF.Abs(r3.End.X - r2.End.X), MathF.Abs(r3.End.Y - r2.End.Y)));
            worstAll = Math.Max(worstAll, worst);
            GD.Print($"MOTION the pages at {size.X}x{size.Y} interface {ui * 100:0}%: 2D {r2.Position.X:0.0},{r2.Position.Y:0.0} {r2.Size.X:0.0}x{r2.Size.Y:0.0}, 3D {r3.Position.X:0.0},{r3.Position.Y:0.0} {r3.Size.X:0.0}x{r3.Size.Y:0.0}: worst corner {worst:0.00} px");
            Check(worst <= 1.5f, $"the 2D pages lie {worst:0.00} px off the 3D pages at {size.X}x{size.Y}, interface {ui * 100:0} %");
        }
        win.Size = size0; win.ContentScaleFactor = scale0;
        await Frames(4);
        TestInput.Key(Godot.Key.Escape);
        await Until(() => M.CoverShown && !M.Motion.Running, 900, "the cover");
        TestInput.Key(Godot.Key.Escape);
        await Until(() => M.Now == PauseMenu.State.Playing && !M.Motion.Running, 900, "the town");
        Notes.Add($"pages-align={worstAll:0.00}px");
    }
}
