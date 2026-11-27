using Godot;

namespace TideToll.Game;

/// <summary>
/// Night 3, L2 (message-system.md section 3): the pointer's tip and the "Suggested" tag of the Pave and Extend modes
/// arrive and leave by a 0.15 s eased fade (BookMotion) instead of flipping <c>Visible</c>; their text changes in place.
/// </summary>
public partial class TownHud
{
    public const float TipTagSeconds = 0.15f;
    private readonly MessageFader _tipF = new(TipTagSeconds), _sugF = new(TipTagSeconds);
    private bool _tipWant, _sugWant;

    /// <summary>The pointer tip's and the suggestion's eased opacity (tests).</summary>
    public float HoverTipAlpha => _tipF.Value;
    public float SuggestAlpha => _sugF.Value;

    private void ModeFadeFrame(float dt)
    {
        _tipF.Want(_tipWant); // the mode works at every zoom: so do its tip and its tag
        _tipF.Step(dt);
        _sugF.Want(_sugWant);
        _sugF.Step(dt);
        FadeShow(_hoverTip, _tipF.Value);
        FadeShow(_suggest, _sugF.Value);
    }

    /// <summary>Shown at this opacity; switched off only once it reaches none.</summary>
    private static void FadeShow(Control c, float a)
    {
        bool v = a > 0f;
        if (c.Visible != v) c.Visible = v;
        if (v && c.Modulate.A != a) c.Modulate = new Color(1, 1, 1, a);
    }
}
