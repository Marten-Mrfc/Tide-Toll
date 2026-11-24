using Godot;

namespace TideToll.Game;

/// <summary>Screen UI: a paper title card and an fps readout. Built in code; no theme file yet.</summary>
public partial class Hud : CanvasLayer
{
    private Label _fps = null!;

    public override void _Ready()
    {
        var card = PaperPanel();
        card.Name = "TitleCard";
        card.Position = new Vector2(16, 16);
        var box = new VBoxContainer();
        box.AddChild(MakeLabel("Tide & Toll", 28, Palette.Ink));
        box.AddChild(MakeLabel("A book with a map you play on", 14, Palette.FadedInk.Darkened(0.3f)));
        card.AddChild(box);
        AddChild(card);

        var fpsCard = PaperPanel();
        fpsCard.Name = "FpsCard";
        fpsCard.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        fpsCard.GrowHorizontal = Control.GrowDirection.Begin;
        fpsCard.Position = new Vector2(-16, 16);
        _fps = MakeLabel("fps --", 14, Palette.Ink);
        _fps.Name = "Fps";
        fpsCard.AddChild(_fps);
        AddChild(fpsCard);
    }

    private double _since = 1;

    // Text is rebuilt twice a second, not every frame: string formatting allocates.
    public override void _Process(double delta)
    {
        _since += delta;
        if (_since < 0.5) return;
        _since = 0;
        var sim = (GetParent() as Main)?.Sim;
        _fps.Text = sim == null
            ? $"fps {Engine.GetFramesPerSecond():0}"
            : $"fps {Engine.GetFramesPerSecond():0}  ·  sim {sim.MsPerTick:0.00} ms/tick  ·  {sim.TicksPerSecond:0} ticks/s";
    }

    private static PanelContainer PaperPanel()
    {
        var style = new StyleBoxFlat
        {
            BgColor = Palette.Paper,
            BorderColor = Palette.Ink,
            ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 8, ContentMarginBottom = 8,
        };
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(3);
        var p = new PanelContainer();
        p.AddThemeStyleboxOverride("panel", style);
        return p;
    }

    private static Label MakeLabel(string text, int size, Color c)
    {
        var l = new Label { Text = text };
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", c);
        return l;
    }
}
