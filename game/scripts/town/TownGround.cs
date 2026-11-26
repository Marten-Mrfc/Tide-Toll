using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// The map page's ground for the town: the same plane and shader as before (shaders/ground.gdshader), with the town's
/// roads painted into its wear layer. Each road cell gets a drawn wear from its road: a new road is a faint trail
/// (drawn wear 0.4: bent grass and a broken line of soil), firming toward a solid track as the road wears in, and
/// packed earth once worn in. A road that opens draws itself in from its first node to its last over
/// <see cref="RevealSeconds"/>; the drawn wear eases toward its target, so wear never jumps. Gravel and cobbles are
/// strips on top (gravel_tex, cobble_tex: GravelStrip's capsule ramp at two texels a cell); crowded roads take a warm
/// wash (crowd_tex). Uploads happen <see cref="UploadsPerSecond"/> times a second, allocation-free.
/// </summary>
public partial class TownGround : MeshInstance3D
{
    public const int N = 128;
    public const float Cell = 2f;
    public double UploadsPerSecond = 10;
    public float Ease = 0.18f;
    /// <summary>Real seconds a new road takes to draw itself in.</summary>
    public const float RevealSeconds = 1.6f;
    /// <summary>Drawn wear of a brand-new trail, of a worn-in track, and the span between.</summary>
    public const float TrailWear = 0.40f, WornWear = 0.95f, WearSpan = 0.40f;

    public ShaderMaterial Material { get; private set; } = null!;
    public int Uploads { get; private set; }
    public int GravelCells { get; private set; }
    public int CobbleCells { get; private set; }
    /// <summary>Drawn wear of a cell (0..1), for tests.</summary>
    public float ShownWear(int x, int y) => _shown[y * N + x];
    /// <summary>Mean drawn wear over a road's cells (tests: a road visibly firms up).</summary>
    public float RoadShown(int r)
    {
        var cells = _cells[r];
        if (cells.Length == 0) return 0;
        float s = 0;
        foreach (int c in cells) s += _shown[c];
        return s / cells.Length;
    }
    /// <summary>Share of a road drawn in so far (0..1).</summary>
    public float RoadReveal(int r) => _reveal[r];

    private TownDriver _drv = null!;
    private int[][] _cells = Array.Empty<int[]>();
    private float[][] _cellDist = Array.Empty<float[]>();
    private float[] _reveal = Array.Empty<float>();
    private bool[] _wasOpen = Array.Empty<bool>();
    // Per road: how far (cells, x and y) its first and last points reach on into the plot whose door they serve, so a
    // paved strip runs up to the door instead of stopping a cell short of the wall.
    private Vector2[] _reachA = Array.Empty<Vector2>(), _reachB = Array.Empty<Vector2>();
    /// <summary>Cells a road's end runs on past the door cell toward the building.</summary>
    public const float DoorReach = 0.7f;
    private readonly float[] _target = new float[N * N], _shown = new float[N * N];
    private readonly byte[] _rg = new byte[N * N * 2], _crowd = new byte[N * N];
    private readonly byte[] _gravelTexels = new byte[N * GravelStrip.Res * N * GravelStrip.Res];
    private readonly byte[] _cobbleTexels = new byte[N * GravelStrip.Res * N * GravelStrip.Res];
    private Image _wearImg = null!, _crowdImg = null!, _gravelImg = null!, _cobbleImg = null!;
    private ImageTexture _wearTex = null!, _crowdTex = null!, _gravelTex = null!, _cobbleTex = null!;
    private double _since;
    private int _pavedSeen = -1;
    private bool _snap = true;
    private static readonly StringName TimeS = new("time_s"), CrowdOn = new("crowd_on");

    public void Init(TownDriver drv)
    {
        _drv = drv;
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
        float size = N * Cell;
        Mesh = new PlaneMesh { Size = new Vector2(size, size) };
        Position = new Vector3(size / 2f, 0, size / 2f);
        Material.SetShaderParameter("map_size", size);
        Material.SetShaderParameter("cells", (float)N);
        _wearImg = Image.CreateFromData(N, N, false, Image.Format.Rg8, _rg);
        _wearTex = ImageTexture.CreateFromImage(_wearImg);
        Material.SetShaderParameter("wear_tex", _wearTex);
        _crowdImg = Image.CreateFromData(N, N, false, Image.Format.R8, _crowd);
        _crowdTex = ImageTexture.CreateFromImage(_crowdImg);
        Material.SetShaderParameter("crowd_tex", _crowdTex);
        int gs = N * GravelStrip.Res;
        _gravelImg = Image.CreateFromData(gs, gs, false, Image.Format.R8, _gravelTexels);
        _gravelTex = ImageTexture.CreateFromImage(_gravelImg);
        Material.SetShaderParameter("gravel_tex", _gravelTex);
        _cobbleImg = Image.CreateFromData(gs, gs, false, Image.Format.R8, _cobbleTexels);
        _cobbleTex = ImageTexture.CreateFromImage(_cobbleImg);
        Material.SetShaderParameter("cobble_tex", _cobbleTex);
        drv.Replaced += Rebuild;
        Rebuild();
    }

    public override void _ExitTree()
    {
        if (_drv != null) _drv.Replaced -= Rebuild;
        // Round 6: release the C# wrappers of the ford mask now (the material keeps the texture while it lives), so no
        // wrapper outlives the scene at exit.
        _maskTex?.Dispose(); _maskImg?.Dispose(); _maskTex = null; _maskImg = null;
    }

    /// <summary>Fixed layers of a new town (river, ford) and each road's cells with their distance along the road.</summary>
    public void Rebuild()
    {
        var town = _drv.Town;
        var map = town.Plan.Map;
        var river = new byte[N * 4];
        var rf = MemoryMarshal.Cast<byte, float>(river.AsSpan());
        for (int y = 0; y < N; y++) rf[y] = map.RiverX[y] * Cell;
        Material.SetShaderParameter("river_tex", ImageTexture.CreateFromImage(Image.CreateFromData(1, N, false, Image.Format.Rf, river)));
        Material.SetShaderParameter("half_w", (float)(map.RiverHalfW * Cell));
        // Round 6 (W2): the first age's map ford (map.Ford) was drawn as a grey shallow band across the river from the
        // start. The ford is drawn only once the second age's ford link is open, on its own row (UpdateFord).
        _mask = new byte[N * N * 2];
        _maskImg = Image.CreateFromData(N, N, false, Image.Format.Rg8, _mask);
        _maskTex = ImageTexture.CreateFromImage(_maskImg);
        Material.SetShaderParameter("mask_tex", _maskTex);
        _fordShown = false;
        var roads = town.Plan.Roads;
        _cells = new int[roads.Length][];
        _cellDist = new float[roads.Length][];
        _reveal = new float[roads.Length];
        _wasOpen = new bool[roads.Length];
        _reachA = new Vector2[roads.Length];
        _reachB = new Vector2[roads.Length];
        for (int r = 0; r < roads.Length; r++)
        {
            var road = roads[r];
            List<int> cells = TownPlanner.RoadCells(road, N);
            _cells[r] = cells.ToArray();
            _cellDist[r] = new float[cells.Count];
            for (int k = 0; k < cells.Count; k++)
            {
                int cx = cells[k] % N, cy = cells[k] / N;
                double best = double.MaxValue, d = 0;
                for (int p = 0; p < road.Px.Length; p++)
                {
                    double dx = road.Px[p] - cx, dy = road.Py[p] - cy, q = dx * dx + dy * dy;
                    if (q < best) { best = q; d = road.PCum[p]; }
                }
                _cellDist[r][k] = (float)d;
            }
            foreach (var pl in town.Plan.Plots)
            {
                var into = new Vector2(pl.X + pl.W / 2f - (pl.DoorX + 0.5f), pl.Y + pl.H / 2f - (pl.DoorY + 0.5f));
                if (into.LengthSquared() < 1e-6f) continue;
                into = into.Normalized() * DoorReach;
                int lastPt = road.Xs.Length - 1;
                if (lastPt < 0) continue;
                if (pl.DoorX == road.Xs[0] && pl.DoorY == road.Ys[0]) _reachA[r] = into;
                if (pl.DoorX == road.Xs[lastPt] && pl.DoorY == road.Ys[lastPt]) _reachB[r] = into;
            }
            // Round 3: a track's worn earth reaches the door as the paved strips do: the cells from each end into the plot.
            AddReach(cells, _cellDist, r, road.Xs[0], road.Ys[0], _reachA[r], 0f);
            AddReach(cells, _cellDist, r, road.Xs[^1], road.Ys[^1], _reachB[r], (float)road.Length);
            bool open = town.RoadOpen(r);
            _wasOpen[r] = open;
            _reveal[r] = open ? 1f : 0f;
        }
        Array.Clear(_shown);
        _pavedSeen = -1;
        _snap = true;
        Upload();
    }

    private void AddReach(List<int> cells, float[][] dist, int r, int dx, int dy, Vector2 reach, float d)
    {
        if (reach == Vector2.Zero) return;
        int added = 0;
        var extra = new List<int>();
        for (float k = 0.25f; k <= 1.001f; k += 0.25f)
        {
            int cx = (int)MathF.Floor(dx + 0.5f + reach.X * k), cy = (int)MathF.Floor(dy + 0.5f + reach.Y * k);
            if (cx < 0 || cy < 0 || cx >= N || cy >= N) continue;
            int c = cy * N + cx;
            if (cells.Contains(c) || extra.Contains(c)) continue;
            extra.Add(c);
            added++;
        }
        if (added == 0) return;
        var all = new int[_cells[r].Length + added];
        _cells[r].CopyTo(all, 0);
        extra.CopyTo(all, _cells[r].Length);
        var ds = new float[all.Length];
        dist[r].CopyTo(ds, 0);
        for (int i = _cells[r].Length; i < all.Length; i++) ds[i] = d;
        _cells[r] = all;
        dist[r] = ds;
        cells.AddRange(extra);
    }

    public override void _Process(double delta)
    {
        Material.SetShaderParameter(TimeS, (float)_drv.AnimTime);
        var town = _drv.Town;
        UpdateFord(town);
        bool revealing = false;
        for (int r = 0; r < _reveal.Length; r++)
        {
            bool open = town.RoadOpen(r);
            if (open && !_wasOpen[r]) { _wasOpen[r] = true; _reveal[r] = 0.0001f; }
            if (open && _reveal[r] < 1f)
            {
                _reveal[r] = Math.Min(1f, _reveal[r] + (float)delta / RevealSeconds);
                revealing = true;
            }
        }
        if (LanesFrame(town, delta)) revealing = true;
        _since += delta;
        // While a road draws itself in, upload every frame so the line grows smoothly.
        if (revealing || _since >= 1.0 / UploadsPerSecond) Upload();
    }

    /// <summary>Recomputes every cell's drawn wear, the crowding and (when the paving changed) the paved strips.</summary>
    private byte[] _mask = Array.Empty<byte>();
    private Image? _maskImg;
    private ImageTexture? _maskTex;
    private bool _fordShown;
    /// <summary>True while the ground draws the ford's shallows (the second age's ford link is open).</summary>
    public bool FordShown => _fordShown;

    /// <summary>Round 6 (W2): the ford's shallows (mask R) only once the ford link is open, on the ford's row, water cells only.</summary>
    private void UpdateFord(Town town)
    {
        var rp = town.RiverPlan;
        int link = -1;
        if (rp != null)
            for (int i = 0; i < rp.Links.Length; i++)
                if (rp.Links[i].Kind == RiverLinkKind.Ford) { link = i; break; }
        bool want = link >= 0 && town.RiverLinkOpen(link);
        if (want == _fordShown || _maskImg == null) return;
        _fordShown = want;
        var map = town.Plan.Map;
        for (int i = 0; i < N * N; i++) _mask[2 * i] = 0;
        if (want)
        {
            int row = rp!.FordRow;
            for (int y = Math.Max(0, row - 1); y <= Math.Min(N - 1, row + 1); y++)
                for (int x = 0; x < N; x++)
                {
                    int c = y * N + x;
                    if (map.Water[c] != 0) _mask[2 * c] = 255;
                }
        }
        _maskImg.SetData(N, N, false, Image.Format.Rg8, _mask);
        _maskTex!.Update(_maskImg);
    }

    public void Upload()
    {
        var town = _drv.Town;
        float k = _snap ? 1f : Ease;
        _snap = false;
        _since = 0;
        Array.Clear(_target);
        Array.Clear(_crowd);
        bool anyCrowd = false;
        for (int r = 0; r < _cells.Length; r++)
        {
            if (!town.RoadOpen(r)) continue;
            var info = town.RoadInfo(r);
            float v = info.Surface != Surface.Track ? PavedWear : info.WornIn ? WornWear : TrailWear + WearSpan * (float)info.Wear;
            float front = _reveal[r] * (float)(town.Plan.Roads[r].Length + 3.0);
            float crowd = (float)Math.Clamp((info.Crowding - 0.85) / 0.9, 0, 1);
            byte cb = (byte)(crowd * 255f);
            if (cb > 0) anyCrowd = true;
            var cells = _cells[r];
            var dist = _cellDist[r];
            for (int i = 0; i < cells.Length; i++)
            {
                float a = Math.Clamp((front - dist[i]) / 3f, 0f, 1f);
                float t = v * a;
                int c = cells[i];
                if (t > _target[c]) _target[c] = t;
                if (cb > _crowd[c]) _crowd[c] = cb;
            }
        }
        LanesUpload(town);
        for (int i = 0; i < _shown.Length; i++)
        {
            // Growth eases; a reveal front (target far above shown) is followed fast so the line draws itself.
            float d = _target[i] - _shown[i];
            float v = _shown[i] + d * (d > 0.25f ? Math.Max(k, 0.6f) : k);
            _shown[i] = v;
            _rg[2 * i] = (byte)(Math.Clamp(v, 0f, 1f) * 255f + 0.5f);
        }
        _wearImg.SetData(N, N, false, Image.Format.Rg8, _rg);
        _wearTex.Update(_wearImg);
        _crowdImg.SetData(N, N, false, Image.Format.R8, _crowd);
        _crowdTex.Update(_crowdImg);
        Material.SetShaderParameter(CrowdOn, anyCrowd ? 1f : 0f);
        int sig = 0;
        for (int r = 0; r < _cells.Length; r++) if (town.RoadOpen(r)) sig += (int)town.RoadInfo(r).Surface * (r + 1) * 7 + 1;
        if (sig != _pavedSeen) UploadPaving(sig);
        Uploads++;
    }

    /// <summary>
    /// Half widths of the paved strips, cells (round 2): gravel a little wider than a footpath, cobbles a cart's width,
    /// 0.6 of a cell at most. Under a paved strip the worn earth is drawn as <see cref="PavedWear"/>, a light trampled
    /// verge rather than a wide band of soil.
    /// </summary>
    public const float GravelHalf = 0.22f, CobbleHalf = 0.30f, PavedWear = 0.5f;

    private void UploadPaving(int sig)
    {
        var town = _drv.Town;
        _pavedSeen = sig;
        Array.Clear(_gravelTexels);
        Array.Clear(_cobbleTexels);
        int g = 0, c = 0;
        // Cobbles last, so where a gravel and a cobbled road meet the cobbles are drawn whole.
        for (int pass = 0; pass < 2; pass++)
            for (int r = 0; r < _cells.Length; r++)
            {
                if (!town.RoadOpen(r)) continue;
                var s = town.RoadInfo(r).Surface;
                if (s == Surface.Track || (pass == 0) != (s == Surface.Gravel)) continue;
                var road = town.Plan.Roads[r];
                var tex = s == Surface.Gravel ? _gravelTexels : _cobbleTexels;
                float half = s == Surface.Gravel ? GravelHalf : CobbleHalf;
                int last = road.Xs.Length - 1;
                // The smooth curve the villagers walk (rules v2.1), never the grid route.
                for (int k = 0; k + 1 < road.Px.Length; k++)
                    GravelStrip.Segment(tex, N, (float)road.Px[k] + 0.5f, (float)road.Py[k] + 0.5f, (float)road.Px[k + 1] + 0.5f, (float)road.Py[k + 1] + 0.5f, half);
                // On up to the doors at both ends.
                if (last >= 0)
                {
                    var ra = _reachA[r]; var rb = _reachB[r];
                    if (ra != Vector2.Zero) GravelStrip.Segment(tex, N, road.Xs[0] + 0.5f, road.Ys[0] + 0.5f, road.Xs[0] + 0.5f + ra.X, road.Ys[0] + 0.5f + ra.Y, half);
                    if (rb != Vector2.Zero) GravelStrip.Segment(tex, N, road.Xs[last] + 0.5f, road.Ys[last] + 0.5f, road.Xs[last] + 0.5f + rb.X, road.Ys[last] + 0.5f + rb.Y, half);
                }
                if (s == Surface.Gravel) g += _cells[r].Length; else c += _cells[r].Length;
            }
        // A cobbled texel hides the gravel under it.
        for (int i = 0; i < _cobbleTexels.Length; i++) if (_cobbleTexels[i] > 127 && _gravelTexels[i] > 0) _gravelTexels[i] = 0;
        GravelCells = g;
        CobbleCells = c;
        int gs = N * GravelStrip.Res;
        _gravelImg.SetData(gs, gs, false, Image.Format.R8, _gravelTexels);
        _gravelTex.Update(_gravelImg);
        _cobbleImg.SetData(gs, gs, false, Image.Format.R8, _cobbleTexels);
        _cobbleTex.Update(_cobbleImg);
        Material.SetShaderParameter("gravel_on", GravelCells > 0 ? 1f : 0f);
        Material.SetShaderParameter("cobble_on", CobbleCells > 0 ? 1f : 0f);
    }
}
