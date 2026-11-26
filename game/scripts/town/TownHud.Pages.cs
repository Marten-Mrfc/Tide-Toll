using System;
using Godot;

namespace TideToll.Game;

/// <summary>
/// The achievements page and the age page come in and go out on a short eased BookMotion each (opacity and a small
/// rise): made visible only at no opacity, hidden only once back at none. Turned round mid-way (toggled, Esc, the
/// other page opened) a page goes on from its opacity and place now. <c>AchievementsOpen</c> / <c>AgePageOpen</c> are
/// what was asked (open or opening), not whether a fading page still shows. The pause menu's quiet close comes once the
/// interface has faded out, so it hides at once (nothing of it can be seen then).
/// </summary>
public partial class TownHud
{
    /// <summary>A full entrance or exit, seconds; the rise, pixels at interface size 100.</summary>
    public const float PageSeconds = 0.22f, PageRise = 16f;

    /// <summary>One page's fade: where it is (A), where it goes, on its own timeline.</summary>
    public sealed class PageFade
    {
        public PanelContainer? Panel;
        public readonly BookMotion Motion = new();
        public float A, From, To;
        public bool On;
        /// <summary>Made visible this frame: drawn once at no opacity, its clock starts in the next frame.</summary>
        public bool Fresh;
    }

    private PageFade _achFade = new(), _ageFade = new();
    /// <summary>The pages' fades (tests and frame strips).</summary>
    public PageFade AchievementsFade => _achFade;
    public PageFade AgePageFade => _ageFade;

    private void NewPageFades()
    {
        _achFade = new PageFade { Panel = _achPanel };
        _ageFade = new PageFade { Panel = _agePanel };
    }

    private void ShowPage(PageFade f)
    {
        if (f.Panel == null) return;
        f.On = true;
        if (!f.Panel.Visible) { f.A = 0f; f.Panel.Modulate = new Color(1, 1, 1, 0); f.Panel.Visible = true; f.Fresh = true; }
        StartPage(f, 1f);
    }

    private void HidePage(PageFade f, bool instant)
    {
        if (f.Panel == null) return;
        f.On = false;
        if (instant || !f.Panel.Visible) { f.Motion.Stop(); f.A = 0f; ApplyPage(f); return; }
        StartPage(f, 0f);
    }

    private static void StartPage(PageFade f, float to)
    {
        f.From = f.A; f.To = to;
        // A page turned round part way goes the rest of the way in the matching share of the time.
        f.Motion.Start(Math.Max(0.06f, PageSeconds * Math.Abs(to - f.A)));
        if (Math.Abs(to - f.A) < 1e-4f) f.Motion.Finish();
    }

    private void StepPage(PageFade f, float dt)
    {
        if (f.Panel == null || !IsInstanceValid(f.Panel)) return;
        if (f.Fresh) { f.Fresh = false; ApplyPage(f); return; }
        if (f.Motion.Running)
        {
            f.Motion.Advance(dt);
            f.A = BookMotion.Mix(f.From, f.To, f.Motion.Ease(0f, f.Motion.Length));
        }
        ApplyPage(f);
    }

    private void ApplyPage(PageFade f)
    {
        var p = f.Panel!;
        if (p.Modulate.A != f.A) p.Modulate = new Color(1, 1, 1, f.A);
        if (!f.On && f.A <= 0f && p.Visible) p.Visible = false;
    }

    /// <summary>How far below its place a page is drawn now (it rises as it comes in).</summary>
    private float PageDrop(Control c)
    {
        var f = c == _achPanel ? _achFade : c == _agePanel ? _ageFade : null;
        return f == null ? 0f : (1f - f.A) * PageRise * _s;
    }
}
