using System;
using System.Runtime.InteropServices;
using Godot;
using TideToll.Sim;

namespace TideToll.Game;

/// <summary>
/// The map page's ground: one plane with the wear shader (shaders/ground.gdshader). The simulation's wear and
/// hollow-way grids go into a small RG8 texture a few times a second, not every frame (16 KB per upload,
/// about 0.1 ms). The displayed wear eases toward the simulation's over a second or two, as the Art tab asks
/// ("smooth wear visually so paths grow instead of flickering"); a new world snaps it.
/// River centre line, ford and crop cells are fixed per map and uploaded when the world is replaced.
/// </summary>
public partial class MapGround : MeshInstance3D
{
    /// <summary>Wear uploads per real second.</summary>
    public double UploadsPerSecond = 5.0;
    /// <summary>Share of the gap to the simulation's wear closed per upload (about 1 s time constant at 5 Hz).</summary>
    public float Ease = 0.35f;

    public ShaderMaterial Material { get; private set; } = null!;
    /// <summary>Number of wear uploads so far (tests and stats).</summary>
    public int Uploads { get; private set; }

    private SimDriver _sim = null!;
    private int _n;
    private float _size;
    private byte[] _rg = Array.Empty<byte>();
    private float[] _shown = Array.Empty<float>();
    private Image? _wearImg;
    private ImageTexture? _wearTex;
    private double _sinceUpload;
    private bool _snap;
    private float _overlay, _overlayTarget;

    private static readonly StringName TimeS = new("time_s"), Overlay = new("overlay");

    public void Init(SimDriver sim)
    {
        _sim = sim;
        Name = "Ground";
        CastShadow = ShadowCastingSetting.Off;
        Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/ground.gdshader") };
        string[] g = { "grass", "trampled", "trail", "track", "hollow-way" };
        for (int i = 0; i < 5; i++) Material.SetShaderParameter("g" + i, ModelLib.MipTexture($"res://assets/textures/ground-{g[i]}.png"));
        Material.SetShaderParameter("ramp_tex", GD.Load<Texture2D>("res://assets/textures/wear-ramp.png"));
        Material.SetShaderParameter("paper_tex", ModelLib.MipTexture("res://assets/textures/paper-page.png"));
        Material.SetShaderParameter("water_tex", ModelLib.MipTexture("res://assets/textures/water-river.png"));
        Material.SetShaderParameter("ink", Palette.Ink);
        Material.SetShaderParameter("water_col", Palette.River);
        Material.SetShaderParameter("deep_col", Palette.DeepWater);
        Material.SetShaderParameter("crop_col", Palette.Thatch);
        Material.SetShaderParameter("crop_dark", Palette.BentGrass);
        Material.SetShaderParameter("paper", Palette.Paper);
        MaterialOverride = Material;
        _sim.WorldReplaced += Rebuild;
        Rebuild();
    }

    public override void _ExitTree()
    {
        if (_sim != null) _sim.WorldReplaced -= Rebuild;
    }

    /// <summary>Show or hide the wear heat map (eased over a quarter second). The UI's wear-overlay button calls this.</summary>
    public bool WearOverlay
    {
        get => _overlayTarget > 0.5f;
        set => _overlayTarget = value ? 1f : 0f;
    }

    /// <summary>Uploads the fixed layers of a new map and snaps the displayed wear to the simulation's.</summary>
    public void Rebuild()
    {
        var w = _sim.World;
        var p = w.Params;
        _n = w.Grid.Width;
        _size = _n * (float)p.CellMeters;
        float cell = (float)p.CellMeters;
        Mesh = new PlaneMesh { Size = new Vector2(_size, _size) };
        Position = new Vector3(_size / 2f, 0, _size / 2f); // MapOrigin is the map's north-west corner
        Material.SetShaderParameter("map_size", _size);
        Material.SetShaderParameter("cells", (float)_n);
        Material.SetShaderParameter("half_w", (float)(w.Map.RiverHalfW * cell));

        // River centre per row, in metres; a 1 x N float image sampled at (0.5, v).
        var river = new byte[_n * 4];
        var rf = MemoryMarshal.Cast<byte, float>(river.AsSpan());
        for (int y = 0; y < _n; y++) rf[y] = w.RiverX[y] * cell;
        Material.SetShaderParameter("river_tex", ImageTexture.CreateFromImage(Image.CreateFromData(1, _n, false, Image.Format.Rf, river)));

        // Ford in R, crop in G; R8 values are 0..1 in the shader, so "set" is 255.
        var mask = new byte[_n * _n * 2];
        for (int i = 0; i < _n * _n; i++)
        {
            mask[2 * i] = (byte)(w.Ford[i] != 0 ? 255 : 0);
            mask[2 * i + 1] = (byte)(w.Crop[i] != 0 ? 255 : 0);
        }
        Material.SetShaderParameter("mask_tex", ImageTexture.CreateFromImage(Image.CreateFromData(_n, _n, false, Image.Format.Rg8, mask)));

        if (_rg.Length != _n * _n * 2)
        {
            _rg = new byte[_n * _n * 2];
            _shown = new float[_n * _n];
            _wearImg = Image.CreateFromData(_n, _n, false, Image.Format.Rg8, _rg);
            _wearTex = ImageTexture.CreateFromImage(_wearImg);
            Material.SetShaderParameter("wear_tex", _wearTex);
        }
        _snap = true;
        Upload();
    }

    public override void _Process(double delta)
    {
        Material.SetShaderParameter(TimeS, (float)_sim.AnimTime);
        if (_overlay != _overlayTarget)
        {
            _overlay = Mathf.MoveToward(_overlay, _overlayTarget, (float)delta * 4f);
            Material.SetShaderParameter(Overlay, _overlay);
        }
        _sinceUpload += delta;
        if (_sinceUpload >= 1.0 / UploadsPerSecond && !_sim.Paused) Upload();
    }

    /// <summary>Copies wear (eased) and the hollow flag into the texture. Allocation-free.</summary>
    public void Upload()
    {
        _sinceUpload = 0;
        var wear = _sim.World.Wear;
        var hollow = _sim.World.Hollow;
        float k = _snap ? 1f : Ease;
        _snap = false;
        for (int i = 0; i < _shown.Length; i++)
        {
            float v = _shown[i] + (wear[i] - _shown[i]) * k;
            _shown[i] = v;
            _rg[2 * i] = (byte)(v * 2.55f + 0.5f);
            _rg[2 * i + 1] = hollow[i] != 0 ? (byte)255 : (byte)0;
        }
        _wearImg!.SetData(_n, _n, false, Image.Format.Rg8, _rg);
        _wearTex!.Update(_wearImg);
        Uploads++;
    }

    /// <summary>Snap the displayed wear to the simulation's on the next upload (after a fast-forward, for tests).</summary>
    public void SnapNext() => _snap = true;
}
