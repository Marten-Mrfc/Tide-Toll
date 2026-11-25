using Godot;

namespace TideToll.Game;

/// <summary>
/// Rain as a light layer over the map page (Chapter I, the toy's effects.ts rain): one ColorRect with a procedural
/// streak shader (shaders/rain.gdshader), laid over the map's rectangle on screen each frame and clipped to the window,
/// under the place tags and the interface. Its level follows the weather's rain (Weather.Rain: strength, ramped in and
/// out); its clock runs with real time while the game runs and stops while paused. Hidden, and nothing set, while dry.
/// Allocation-free.
/// </summary>
public partial class RainLayer : ColorRect
{
    private Main _main = null!;
    private ShaderMaterial _mat = null!;
    private float _s = 1;
    private double _clock;
    private float _shown = -1;
    private Rect2 _at;
    private static readonly StringName RainS = new("rain"), ClockS = new("clock"), SizeS = new("size_px"), ScaleS = new("px_scale");

    /// <summary>The rain level drawn now (0..1; tests and screenshots).</summary>
    public float Level => Visible ? _shown : 0f;

    public void Build(Main main, float s)
    {
        _main = main;
        _s = s;
        Name = "Rain";
        MouseFilter = MouseFilterEnum.Ignore;
        Color = Colors.White;
        _mat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/rain.gdshader") };
        _mat.SetShaderParameter(ScaleS, s);
        Material = _mat;
        Visible = false;
    }

    public override void _Process(double delta)
    {
        var w = _main.Sim.World;
        float rain = w.RainOn ? (float)w.Weather.Rain : 0f;
        if (rain <= 0.001f)
        {
            if (Visible) { Visible = false; _shown = 0; }
            return;
        }
        if (!_main.Sim.Paused && !_main.Sim.Held) _clock += delta;
        // The map's rectangle on screen (its two corners), clipped to the window.
        var rig = _main.Rig;
        float size = w.Grid.Width * (float)w.Params.CellMeters;
        var a = rig.LocalToScreen(Vector3.Zero);
        var b = rig.LocalToScreen(new Vector3(size, 0, size));
        var r = new Rect2(a, b - a).Abs().Intersection(GetViewportRect());
        if (r.Size.X < 1 || r.Size.Y < 1) { Visible = false; return; }
        if (r != _at)
        {
            _at = r;
            Position = r.Position;
            Size = r.Size;
            _mat.SetShaderParameter(SizeS, r.Size);
        }
        if (rain != _shown) { _shown = rain; _mat.SetShaderParameter(RainS, rain); }
        _mat.SetShaderParameter(ClockS, (float)(_clock % 600.0));
        if (!Visible) Visible = true;
    }
}
