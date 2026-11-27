using System;
using Godot;

namespace TideToll.Game.Settings;

/// <summary>
/// The spread arriving on the open book (and leaving it): a leaf turning from the town's pages (the 3D book under the
/// spread) to the settings pages, drawn with the same <see cref="PageLeaf"/> and curve as a tab turn. Ahead of the leaf's
/// edge nothing of the spread is drawn, so the town's own pages show there; behind it the settings pages lie exactly on
/// the 3D pages. The ribbons slide out from under the page and fade in with the leaf. The owner (PauseMenu) sets
/// <see cref="Arrival"/> from its timeline (already eased); 1 is the spread at rest.
/// The ribbons' colour and pull follow their place on the turn's curve (<see cref="RibbonLook"/>), not the click.
/// </summary>
public partial class SettingsPages
{
    /// <summary>Draws a table and cover of its own around the pages (alone); false over the 3D book (PauseMenu).</summary>
    public bool OwnTable { get; init; } = true;
    private float _arrive = 1f;
    private SectionMask? _pagesMask;
    private readonly float[] _ribbonX = new float[4];
    private readonly StyleBoxFlat?[] _ribbonBox = new StyleBoxFlat?[4], _ribbonHover = new StyleBoxFlat?[4];
    private readonly Color[] _ribbonTint = new Color[4];

    /// <summary>0: none of the spread is down (the town's pages show); 1: the settings pages at rest.</summary>
    public float Arrival => _arrive;
    /// <summary>The arrival's leaf edge (canvas x) while it is under way.</summary>
    public float ArrivalEdge { get; private set; }

    /// <summary>Where the spread starts being drawn (canvas x): the arriving leaf's edge; nothing at all before the leaf
    /// has moved (the page's own edge would otherwise show past the right page); 0 at rest.</summary>
    private float ArrivalFloor => _arrive >= 1f ? 0f : _arrive <= 0f ? _canvas.X : RightEdge - (RightEdge - LeftEdge) * _arrive;

    /// <summary>Sets how far the arriving leaf has turned (0..1, eased by the owner).</summary>
    public void SetArrival(float e)
    {
        e = Math.Clamp(e, 0f, 1f);
        if (e == _arrive && _frame != null) return;
        _arrive = e;
        if (_from >= 0) ApplyState(); else SetRest();
    }

    /// <summary>Called at the end of SetRest: the arrival's leaf over the rest state while it is under way.</summary>
    private void ApplyArrival()
    {
        float l = LeftEdge, r = RightEdge, spine = Spine;
        if (_arrive >= 1f)
        {
            _pagesMask?.SetRegion(new Rect2(0, 0, _canvas.X, _canvas.Y));
            for (int k = 0; k < 4; k++) PlaceRibbon(k, 1f);
            return;
        }
        float x = r - (r - l) * _arrive, x0 = ArrivalFloor;
        ArrivalEdge = x;
        _pagesMask?.SetRegion(new Rect2(x0, 0, _canvas.X - x0, _canvas.Y));
        Clip(_showing, 0, _canvas.X);
        if (_leaf != null) _leaf.From = 1f;
        _leaf?.Place(SpreadArea, spine, x, _arrive <= 0f ? 0f : LiftAt(x));
        OrderForLeaf();
        for (int k = 0; k < 4; k++) PlaceRibbon(k, _arrive);
    }

    /// <summary>A ribbon slid in under the page by (1 - a) of its slide, at opacity a.</summary>
    private void PlaceRibbon(int i, float a)
    {
        if (_ribbons[i] is not { } b || !IsInstanceValid(b)) return;
        float x = _ribbonX[i] - (1f - a) * 40f * _s;
        if (b.Position.X != x) b.Position = new Vector2(x, b.Position.Y);
        if (b.Modulate.A != a) b.Modulate = new Color(1, 1, 1, a);
    }

    /// <summary>Styles ribbon i with boxes of its own, whose colour and border follow its pull (0 in, 1 chosen).</summary>
    private void StyleRibbon(Button b, int i, Color tint)
    {
        SettingsLook.Ribbon(b, _s, tint);
        _ribbonTint[i] = tint;
        _ribbonBox[i] = (StyleBoxFlat)b.GetThemeStylebox("normal").Duplicate();
        _ribbonHover[i] = (StyleBoxFlat)b.GetThemeStylebox("hover").Duplicate();
        b.AddThemeStyleboxOverride("normal", _ribbonBox[i]);
        b.AddThemeStyleboxOverride("pressed", _ribbonBox[i]);
        b.AddThemeStyleboxOverride("hover", _ribbonHover[i]);
        b.AddThemeStyleboxOverride("hover_pressed", _ribbonHover[i]);
        _ribbonX[i] = b.Position.X;
        RibbonLook(i);
    }

    /// <summary>The ribbon's colour and border for its pull now: the tint to paper as it comes out.</summary>
    private void RibbonLook(int i)
    {
        if (_ribbonBox[i] is not { } box || _ribbonHover[i] is not { } hover) return;
        float v = _ribbonOut[i];
        var bg = _ribbonTint[i].Lerp(UiKit.Paper, v);
        int w = Math.Max(1, UiKit.Px(_s, 1.5f + v));
        if (box.BgColor != bg) box.BgColor = bg;
        if (box.BorderWidthTop != w) box.BorderWidthTop = box.BorderWidthBottom = box.BorderWidthRight = w;
        var hb = _ribbonTint[i].Lightened(0.35f).Lerp(UiKit.Paper, v);
        if (hover.BgColor != hb) hover.BgColor = hb;
    }

    /// <summary>The ribbon's colour now (tests).</summary>
    public Color RibbonColour(int i) => _ribbonBox[i]?.BgColor ?? Colors.Transparent;
}
