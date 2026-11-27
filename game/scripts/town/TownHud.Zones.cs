using System;
using Godot;

namespace TideToll.Game;

/// <summary>
/// Night 3 (layout sheet v2, section 2): every zone in its own rectangle from <see cref="HudZones"/>, set last in the
/// frame, so no card is pushed by another. The Line zone (bottom centre, quiet card) says one thing: what the click
/// mode in use expects, else the town's hint. The Message zone (top centre) holds one banner at a time or the choice of
/// two; a banner raised while the choice is open or while zoomed out waits (the newest one) until the zone is free.
/// </summary>
public partial class TownHud
{
    private PanelContainer _lineCard = null!;
    private Label _lineText = null!;
    private string _lineShown = "";

    /// <summary>The Line zone's text as shown ("" when it is empty; tests).</summary>
    public string LineText => _lineCard.Visible && _lineCardF.On ? _lineShown : "";
    public Rect2 LineRect => _lineCard.Visible ? _lineCard.GetGlobalRect() : new Rect2();

    private void BuildLine()
    {
        var z = Zones;
        _lineCard = new PanelContainer { Name = "Line", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _lineCard.AddThemeStyleboxOverride("panel", UiKit.CardBox(_s, UiKit.CardKind.Quiet, 0));
        _lineCard.Position = z.Line.Position;
        _lineCard.Size = z.Line.Size;
        _root.AddChild(_lineCard);
        var holder = new Control { Name = "Box", MouseFilter = Control.MouseFilterEnum.Ignore };
        _lineCard.AddChild(holder);
        // The trimmed face (UiKit.CapFont): a mode's help may take two lines at the narrowest canvas.
        _lineText = UiKit.Fixed(_s, UiKit.Caption, UiKit.CapFont, UiKit.Ink, HorizontalAlignment.Center);
        _lineText.Name = "Text";
        _lineText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _lineText.MaxLinesVisible = 2;
        _lineText.Position = new Vector2(UiKit.U(_s, 12), 0);
        _lineText.Size = new Vector2(z.Line.Size.X - UiKit.U(_s, 24), z.Line.Size.Y);
        holder.AddChild(_lineText);
        _lineBack = UiKit.Fixed(_s, UiKit.Caption, UiKit.CapFont, UiKit.Ink, HorizontalAlignment.Center);
        _lineBack.Name = "TextBefore";
        _lineBack.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _lineBack.MaxLinesVisible = 2;
        _lineBack.Position = _lineText.Position;
        _lineBack.Size = _lineText.Size;
        _lineBack.Modulate = new Color(1, 1, 1, 0);
        holder.AddChild(_lineBack);
        _lineText.Text = _lineShown;
        FitLine();
        _lineCardF.Set(false);
        // The hint and the mode text are kept as labels of their own (tests read them); the line shows one of them.
        _hint = new Label { Name = "Hint", Visible = false };
        _lineCard.AddChild(_hint);
    }

    /// <summary>Places every zone for this canvas (fixed rectangles; nothing is pushed by anything else).</summary>
    private void ZoneFrame()
    {
        var z = Zones;
        LineFrame();
        if (_choice.Visible)
        {
            var r = z.Message(ChoiceHeight());
            _choice.Position = r.Position; // a zone never moves: the choice fades in its place
            _choice.Size = r.Size;
        }
        if (_tide.Visible) { _tide.Position = z.Tide.Position; _tide.Size = z.Tide.Size; }
        _ledgerPanel.Position = z.Ledger.Position;
        _bell.Position = z.Bell.Position;
        TipFrame();
    }

    /// <summary>The choice card's height in sheet units: as tall as its content, at most 200.</summary>
    private float ChoiceHeight() => HudZones.ChoiceMaxH;
}
