using Godot;

namespace TideToll.Game;

/// <summary>n7 trades: a small mark drawn in code: the Trades switch's check, the liked trade cap's corner fold. Drawn
/// once (Godot redraws it only when its size changes).</summary>
public partial class TradeMark : Control
{
    public enum Shape { Check, Fold }
    public Shape Kind;
    public Color Colour = UiKit.Ink;
    public float Width = 2f;
    public override void _Draw()
    {
        var s = Size;
        if (Kind == Shape.Check)
        {
            var pts = new[] { new Vector2(s.X * 0.22f, s.Y * 0.52f), new Vector2(s.X * 0.42f, s.Y * 0.72f), new Vector2(s.X * 0.80f, s.Y * 0.28f) };
            DrawPolyline(pts, Colour, Width, true);
        }
        else DrawColoredPolygon(new[] { new Vector2(0, 0), new Vector2(s.X, 0), new Vector2(s.X, s.Y) }, Colour);
    }
}
