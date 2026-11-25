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
/// With the river rules on, a second small RG8 texture carries the river's seasons: R = the ice-wear layer (uploaded
/// with the wear while the river is frozen, cleared once at the thaw), G = the flood band (fixed per map). The flood,
/// the mud after it and the ice are eased shader values (the wash fills outward from the bank as the river rises).
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
    private byte[] _season = Array.Empty<byte>();
    private Image? _seasonImg;
    private ImageTexture? _seasonTex;
    private bool _iceShown;
    private float _flood, _ice, _mud;
    private byte[] _gravelTexels = Array.Empty<byte>(), _gravelSeen = Array.Empty<byte>();
    private Image? _gravelImg;
    private ImageTexture? _gravelTex;

    /// <summary>Gravel cells drawn, and uploads of the gravel strip (tests).</summary>
    public int GravelCells { get; private set; }
    public int GravelUploads { get; private set; }

    private static readonly StringName TimeS = new("time_s"), Overlay = new("overlay"), FloodS = new("flood"), IceS = new("ice"), MudS = new("mud");

    /// <summary>The flood wash (0..1, eased: it fills as the river rises), the ice and the mud as drawn (tests).</summary>
    public float Flood => _flood;
    /// <summary>The flood band's dotted edge is marked on the dry map (river rules on; tests).</summary>
    public bool BandMarked { get; private set; }
    public float Ice => _ice;
    public float Mud => _mud;
    /// <summary>Uploads of the ice-wear layer so far (tests and stats).</summary>
    public int IceUploads { get; private set; }

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
        _sim.Edited += OnEdited;
        Rebuild();
    }

    public override void _ExitTree()
    {
        if (_sim == null) return;
        _sim.WorldReplaced -= Rebuild;
        _sim.Edited -= OnEdited;
    }

    private void OnEdited(int version) => UploadGravel(false);

    /// <summary>
    /// The gravel strip (GravelStrip) into its own linear texture at two texels a cell, when the gravel grid changed
    /// (a brush, an erase, a fence laid over gravel). Edits only, never per frame.
    /// </summary>
    public void UploadGravel(bool force)
    {
        var gravel = _sim.World.Gravel;
        int size = _n * GravelStrip.Res;
        if (_gravelTexels.Length != size * size)
        {
            _gravelTexels = new byte[size * size];
            _gravelSeen = new byte[_n * _n];
            _gravelImg = Image.CreateFromData(size, size, false, Image.Format.R8, _gravelTexels);
            _gravelTex = ImageTexture.CreateFromImage(_gravelImg);
            Material.SetShaderParameter("gravel_tex", _gravelTex);
            force = true;
        }
        if (!force && gravel.SequenceEqual(_gravelSeen)) return;
        gravel.CopyTo(_gravelSeen);
        GravelCells = GravelStrip.Rasterise(gravel, _n, _gravelTexels);
        _gravelImg!.SetData(size, size, false, Image.Format.R8, _gravelTexels);
        _gravelTex!.Update(_gravelImg);
        Material.SetShaderParameter("gravel_on", GravelCells > 0 ? 1f : 0f);
        GravelUploads++;
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
        // The seasons texture: G = flood band (fixed), R = ice wear (zero until a freeze).
        if (_season.Length != _n * _n * 2)
        {
            _season = new byte[_n * _n * 2];
            _seasonImg = Image.CreateFromData(_n, _n, false, Image.Format.Rg8, _season);
            _seasonTex = ImageTexture.CreateFromImage(_seasonImg);
            Material.SetShaderParameter("season_tex", _seasonTex);
        }
        Array.Clear(_season);
        var r = w.River;
        if (r != null)
        {
            var band = r.FloodBand;
            for (int i = 0; i < band.Length && i < _n * _n; i++) _season[2 * i + 1] = band[i] != 0 ? (byte)255 : (byte)0;
            Material.SetShaderParameter("band_m", (float)(r.Rules.FloodBandCells * cell));
        }
        BandMarked = r != null;
        Material.SetShaderParameter("band_mark", BandMarked ? 1f : 0f);
        _seasonImg!.SetData(_n, _n, false, Image.Format.Rg8, _season);
        _seasonTex!.Update(_seasonImg);
        if (_wetMud.Length != _n * _n) InitRain();
        else { Array.Clear(_wetMud); _mudShown = true; _dryFor = double.NaN; _wet = -1; }
        _iceShown = false;
        _flood = _ice = _mud = -1; // set at once on the next frame
        _snap = true;
        Upload();
        UploadGravel(true);
    }

    public override void _Process(double delta)
    {
        Material.SetShaderParameter(TimeS, (float)_sim.AnimTime);
        if (_overlay != _overlayTarget)
        {
            _overlay = Mathf.MoveToward(_overlay, _overlayTarget, (float)delta * 4f);
            Material.SetShaderParameter(Overlay, _overlay);
        }
        Seasons((float)delta);
        RainFrame((float)delta);
        _sinceUpload += delta;
        if (_sinceUpload >= 1.0 / UploadsPerSecond && !_sim.Paused) Upload();
    }

    /// <summary>Eases the flood, ice and mud toward the river's state (a new world snaps them). Allocation-free.</summary>
    private void Seasons(float dt)
    {
        var r = _sim.World.River;
        float fT = r != null && r.Flooded ? 1f : 0f, iT = r != null && r.Frozen ? 1f : 0f, mT = r != null && r.Muddy ? 1f : 0f;
        bool snap = _flood < 0;
        if ((iT > 0) != _iceShown) UploadIce(); // a freeze or a thaw shows at once, even while paused
        EaseTo(ref _flood, fT, snap ? 1f : dt * 0.45f, FloodS);   // the water rises over about two seconds
        EaseTo(ref _ice, iT, snap ? 1f : dt * 0.6f, IceS);
        EaseTo(ref _mud, mT, snap ? 1f : dt * 0.5f, MudS);
    }

    private void EaseTo(ref float v, float target, float step, StringName name)
    {
        float nv = v < 0 ? target : Mathf.MoveToward(v, target, step);
        if (nv == v) return;
        v = nv;
        Material.SetShaderParameter(name, v);
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
        UploadIce();
        UploadMud();
    }

    /// <summary>The ice-wear layer into the seasons texture while frozen, and once more (cleared) after the thaw.</summary>
    private void UploadIce()
    {
        var r = _sim.World.River;
        bool frozen = r != null && r.Frozen;
        if (!frozen && !_iceShown) return;
        if (frozen)
        {
            var ice = r!.IceWear;
            for (int i = 0; i < ice.Length && i < _n * _n; i++) _season[2 * i] = (byte)(Math.Clamp(ice[i], 0f, 100f) * 2.55f + 0.5f);
        }
        else for (int i = 0; i < _n * _n; i++) _season[2 * i] = 0;
        _iceShown = frozen;
        _seasonImg!.SetData(_n, _n, false, Image.Format.Rg8, _season);
        _seasonTex!.Update(_seasonImg);
        IceUploads++;
    }

    /// <summary>Snap the displayed wear to the simulation's on the next upload (after a fast-forward, for tests).</summary>
    public void SnapNext() => _snap = true;
}
