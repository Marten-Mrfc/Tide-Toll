using System;
using System.Collections.Generic;
using Godot;
using TideToll.Sim;

namespace TideToll.Game;

/// <summary>
/// Ferries and drawbridges on the map, built from the simulation's crossing list when the edit version changes
/// (footbridges stay with EditLayer) and animated every frame from each crossing's state, interpolated between ticks:
/// - Ferry ("Ferry{id}"): a post on each bank with the rope between them, a dotted crossing line, and the raft
///   (drawn at 0.8 scale so it has room to cross a 3-cell river) sliding between its stages with <c>Progress</c>;
///   riders are drawn standing on it (WalkerLayer asks <see cref="RaftAt"/>).
/// - Drawbridge ("Drawbridge{id}"): the lift (frame, leaf, balance beam, chains and signal arm) with its pier on the
///   west bank, fixed spans over any water the 6 m leaf does not reach, and a ramp with a barrier at each end. The leaf
///   and beam turn up to 80 degrees with <c>Progress</c>, the chains follow the beam's end, the signal arm stands up
///   (boats' turn) whenever the bridge is not down and lies flat (walkers' turn) when it is, and the barriers drop
///   across the ramps while walkers must wait. Once a keeper is hired, the keeper's hut and the keeper appear.
/// Every piece is a model with the Living Map material, so the ink pass outlines it like the rest of the map.
/// </summary>
public partial class CrossingLayer : Node3D
{
    public const float RaftScale = 0.8f, MaxLeafDeg = 80f;
    /// <summary>Deck heights (metres above the water) where walkers stand: raft, drawbridge deck.</summary>
    public const float RaftDeck = 0.3f, DrawDeck = 0.9f;

    private sealed class View
    {
        public int Id;
        public CrossingType Type;
        public Node3D Root = null!;
        public Node3D? Raft, Leaf, Beam, Chains, Signal, KeeperHut;
        public readonly List<Node3D> Barriers = new();
        public float XA, XB, Z;           // raft travel (metres) and the row's centre
        public double Prev, Cur;          // progress at the previous and current tick
        public long Tick = -1;
        public float SignalA, BarrierA;   // eased arm angles, radians
        public float SpanW, SpanE;        // the water span, metres (overview mark)
        public bool Keeper;
    }

    private SimDriver _sim = null!;
    private readonly List<View> _views = new();
    /// <summary>Instances in the sign MultiMesh: ferry lines, overview marks for every crossing, drawbridge signals.</summary>
    public const int LineSlots = 64;
    private const int Stride = 16;
    private MultiMeshInstance3D _lines = null!;
    private readonly float[] _lineBuf = new float[LineSlots * Stride];
    private float _mpp = 0.25f;
    // Footbridges with the river rules on (EditLayer draws their models): their spans, for the overview mark.
    private readonly float[] _footW = new float[LineSlots], _footE = new float[LineSlots], _footZ = new float[LineSlots];
    private int _footCount;
    /// <summary>0 up close, 1 at the play view's overview: the crossings' map marks fade in with it (Main sets it from the zoom).</summary>
    public float Overview;
    /// <summary>Overview marks drawn in the last frame (tests): one per ferry, footbridge and drawbridge while Overview is above 0.</summary>
    public int OverviewMarks { get; private set; }
    /// <summary>Whether the closed-ford bar is drawn (the spring flood closes the ford; tests).</summary>
    public bool FordClosedShown { get; private set; }

    /// <summary>Rebuilds so far (tests), and the crossing views now drawn.</summary>
    public int Rebuilds { get; private set; }
    public int Count => _views.Count;

    public void Init(SimDriver sim)
    {
        _sim = sim;
        Name = "Crossings";
        var mat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/river_sign.gdshader"), RenderPriority = 8 };
        mat.SetShaderParameter("ink", Palette.Ink);
        mat.SetShaderParameter("paper", Palette.Paper);
        var quad = new PlaneMesh { Size = Vector2.One, Material = mat };
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = quad, InstanceCount = LineSlots };
        mm.VisibleInstanceCount = 0;
        _lines = new MultiMeshInstance3D { Name = "FerryLines", Multimesh = mm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 600 };
        AddChild(_lines);
        sim.Edited += OnEdited;
        sim.WorldReplaced += Rebuild;
        Rebuild();
    }

    public override void _ExitTree()
    {
        if (_sim == null) return;
        _sim.Edited -= OnEdited;
        _sim.WorldReplaced -= Rebuild;
    }

    private void OnEdited(int v) => Rebuild();

    /// <summary>The node drawing crossing <paramref name="id"/>, or null (tests).</summary>
    public Node3D? NodeFor(int id)
    {
        foreach (var v in _views) if (v.Id == id) return v.Root;
        return null;
    }

    /// <summary>Leaf angle drawn for a drawbridge, in degrees (tests); NaN if there is none.</summary>
    public float LeafDegrees(int id)
    {
        foreach (var v in _views) if (v.Id == id && v.Leaf != null) return Mathf.RadToDeg(v.Leaf.Rotation.X);
        return float.NaN;
    }

    /// <summary>Whether the keeper's hut is drawn for a drawbridge (tests).</summary>
    public bool KeeperShown(int id)
    {
        foreach (var v in _views) if (v.Id == id) return v.KeeperHut is { Visible: true };
        return false;
    }

    /// <summary>Map-local centre of a ferry's raft as drawn this frame; false if crossing id is no ferry.</summary>
    public bool RaftAt(int id, out Vector3 at)
    {
        foreach (var v in _views)
            if (v.Id == id && v.Raft != null) { at = v.Raft.Position; return true; }
        at = default;
        return false;
    }

    public void Rebuild()
    {
        Rebuilds++;
        foreach (var v in _views) { RemoveChild(v.Root); v.Root.QueueFree(); }
        _views.Clear();
        var w = _sim.World;
        var r = w.River;
        int lines = 0;
        if (r != null)
        {
            var xs = r.Crossings;
            for (int i = 0; i < xs.Count; i++)
            {
                var x = xs[i];
                if (x.Type == CrossingType.Ferry) { _views.Add(BuildFerry(w, x)); lines++; }
                else if (x.Type == CrossingType.Drawbridge) _views.Add(BuildDrawbridge(w, x));
            }
        }
        _footCount = 0;
        if (r != null)
        {
            var xs = r.Crossings;
            for (int i = 0; i < xs.Count && _footCount < LineSlots; i++)
            {
                if (xs[i].Type != CrossingType.Footbridge || xs[i].Cells.Count == 0) continue;
                var (fw, fe, fz) = Span(w, xs[i]);
                _footW[_footCount] = fw; _footE[_footCount] = fe; _footZ[_footCount] = fz;
                _footCount++;
            }
        }
        Sync(0);
    }

    private static (float xw, float xe, float z) Span(World w, Crossing x)
    {
        float cell = (float)w.Params.CellMeters;
        int n = w.Grid.Width;
        var cells = x.Cells;
        return ((cells[0] % n) * cell, (cells[cells.Count - 1] % n + 1) * cell, (x.Row + 0.5f) * cell);
    }

    private View BuildFerry(World w, Crossing x)
    {
        var (xw, xe, z) = Span(w, x);
        var v = new View { Id = x.Id, Type = x.Type, Root = new Node3D { Name = $"Ferry{x.Id}" }, Z = z, SpanW = xw, SpanE = xe };
        AddChild(v.Root);
        float s = RaftScale, zr = z - 1.95f * s;
        var pw = ModelLib.Instance("ferry-post", "PostW");
        pw.Position = new Vector3(xw - 0.8f, 0, zr);
        pw.Rotation = new Vector3(0, -MathF.PI / 2f, 0);
        var pe = ModelLib.Instance("ferry-post", "PostE");
        pe.Position = new Vector3(xe + 0.8f, 0, zr);
        pe.Rotation = new Vector3(0, MathF.PI / 2f, 0);
        v.Root.AddChild(pw);
        v.Root.AddChild(pe);
        // The rope from the west post's rope point to the east one's: the model runs along -Z for 1 m.
        var a = new Vector3(xw - 0.5f, 1.3f, zr);
        var b = new Vector3(xe + 0.5f, 1.3f, zr);
        var rope = ModelLib.Instance("ferry-rope", "Rope");
        rope.Position = a;
        rope.Rotation = new Vector3(0, -MathF.PI / 2f, 0);
        rope.Scale = new Vector3(1, 1, a.DistanceTo(b));
        v.Root.AddChild(rope);
        // The raft overlaps each bank by 0.9 m when it lies at a stage.
        float half = 3f * s;
        v.XA = xw + half - 0.9f;
        v.XB = xe - half + 0.9f;
        if (v.XB < v.XA) v.XA = v.XB = (xw + xe) / 2f;
        v.Raft = ModelLib.Instance("ferry-raft", "Raft");
        v.Raft.Scale = new Vector3(s, s, s);
        v.Raft.Rotation = new Vector3(0, MathF.PI / 2f, 0);
        v.Root.AddChild(v.Raft);
        v.Cur = v.Prev = x.Progress;
        return v;
    }

    private View BuildDrawbridge(World w, Crossing x)
    {
        var (xw, xe, z) = Span(w, x);
        float cell = (float)w.Params.CellMeters;
        int n = w.Grid.Width;
        var v = new View { Id = x.Id, Type = x.Type, Root = new Node3D { Name = $"Drawbridge{x.Id}" }, Z = z, SpanW = xw, SpanE = xe };
        AddChild(v.Root);
        float west = -MathF.PI / 2f; // the model's +Z (its pier side) toward the west bank
        var lift = new Node3D { Name = "Lift", Position = new Vector3(xw + 2f, 0, z), Rotation = new Vector3(0, west, 0) };
        v.Root.AddChild(lift);
        lift.AddChild(ModelLib.PartInstance("drawbridge-lift", "frame", "Frame"));
        v.Leaf = ModelLib.PartInstance("drawbridge-lift", "leaf", "Leaf");
        v.Beam = ModelLib.PartInstance("drawbridge-lift", "beam", "Beam");
        v.Chains = ModelLib.PartInstance("drawbridge-lift", "chains", "Chains");
        v.Signal = ModelLib.PartInstance("drawbridge-lift", "signal", "Signal");
        lift.AddChild(v.Leaf); lift.AddChild(v.Beam); lift.AddChild(v.Chains); lift.AddChild(v.Signal);
        // Water the 6 m leaf does not reach gets fixed deck modules.
        var cells = x.Cells;
        for (int j = 3; j < cells.Count; j++)
        {
            var span = ModelLib.Instance("drawbridge-span", $"Span{j}");
            span.Position = new Vector3((cells[j] % n + 0.5f) * cell, 0, z);
            span.Rotation = new Vector3(0, west, 0);
            v.Root.AddChild(span);
        }
        AddEnd(v, "EndW", xw - 3f, z, west);
        AddEnd(v, "EndE", xe + 1f, z, -west);
        // The keeper's hut on the pier's bank, north of the road, door toward it; the keeper by the signal post.
        var hut = new Node3D { Name = "Keeper", Visible = false };
        var house = ModelLib.Instance("keeper-hut", "Hut");
        house.Position = new Vector3(xw - 3.5f, 0, z - 5.2f);
        hut.AddChild(house);
        var keeper = ModelLib.Instance("villager", "Keeper");
        keeper.Position = new Vector3(xw - 1.2f, 0, z + 3.4f);
        keeper.Rotation = new Vector3(0, MathF.PI / 2f, 0);
        keeper.Scale = new Vector3(1.1f, 1.1f, 1.1f);
        hut.AddChild(keeper);
        v.Root.AddChild(hut);
        v.KeeperHut = hut;
        v.Cur = v.Prev = x.Progress;
        v.SignalA = x.State == BridgeState.Closed ? 0 : MathF.PI / 2f;
        v.BarrierA = x.State == BridgeState.Closed ? Mathf.DegToRad(80) : 0;
        return v;
    }

    private static void AddEnd(View v, string name, float x, float z, float yaw)
    {
        var end = new Node3D { Name = name, Position = new Vector3(x, 0, z), Rotation = new Vector3(0, yaw, 0) };
        end.AddChild(ModelLib.PartInstance("drawbridge-end", "ramp", "Ramp"));
        var bar = ModelLib.PartInstance("drawbridge-end", "barrier", "Barrier");
        end.AddChild(bar);
        v.Barriers.Add(bar);
        v.Root.AddChild(end);
    }

    public void SetMetresPerPixel(float mpp)
    {
        if (float.IsFinite(mpp) && mpp > 0) _mpp = mpp;
    }

    /// <summary>Signal glyphs drawn (tests): one per drawbridge, after the ferry lines.</summary>
    public int Signals { get; private set; }

    public override void _Process(double delta) => Sync((float)delta);

    /// <summary>
    /// Moves rafts, leaves, beams, chains, signals and barriers to this frame's state, and writes the sign MultiMesh:
    /// each ferry's dotted line, then (at overview zoom) a map mark per crossing that reads at a glance (the ferry's rope
    /// as a dotted line with the raft on it, the footbridge as a plank deck, the drawbridge as a deck with its gantry,
    /// open over the water while raised), then each drawbridge's signal disc (always drawn). Allocation-free.
    /// </summary>
    public void Sync(float dt)
    {
        var w = _sim.World;
        var r = w.River;
        OverviewMarks = 0;
        if (r == null || (_views.Count == 0 && _footCount == 0 && !r.Flooded)) { FordClosedShown = false; _lines.Multimesh.VisibleInstanceCount = 0; Signals = 0; return; }
        var xs = r.Crossings;
        float alpha = _sim.Alpha, ease = dt <= 0 ? 1f : 1f - MathF.Exp(-dt * 8f);
        float glyph = Math.Clamp(30f * _mpp, 3f, 90f);
        float deckH = MathF.Max(3.2f, 16f * _mpp), ov = Math.Clamp(Overview, 0f, 1f) * 0.95f;
        int k = 0;
        foreach (var v in _views)
        {
            Crossing? x = null;
            for (int i = 0; i < xs.Count; i++) if (xs[i].Id == v.Id) { x = xs[i]; break; }
            if (x == null) continue;
            if (w.Ticks != v.Tick)
            {
                v.Prev = v.Tick < 0 ? x.Progress : v.Cur;
                v.Cur = x.Progress;
                v.Tick = w.Ticks;
            }
            float p = (float)(v.Prev + (v.Cur - v.Prev) * alpha);
            if (v.Raft != null)
            {
                v.Raft.Position = new Vector3(v.XA + (v.XB - v.XA) * p, 0.02f, v.Z);
                // The dotted crossing line south of the raft's path (always), and the overview mark: rope and raft.
                float len = v.XB - v.XA + 6f;
                k = Put(k, (v.XA + v.XB) / 2f, 0.2f, v.Z + 2.6f, len, 0.7f, 3, len / 1.6f, 0.9f, 0);
                if (ov > 0)
                {
                    // The rope runs 4 m onto each bank to its posts; the raft is three quarters of the mark's height long.
                    float span = v.SpanE - v.SpanW + 8f, raftC = v.XA + (v.XB - v.XA) * p;
                    k = Put(k, (v.SpanW + v.SpanE) / 2f, 1.2f, v.Z, span, deckH, 5, (raftC - (v.SpanW - 4f)) / span, ov, deckH * 0.75f / span);
                    OverviewMarks++;
                }
                continue;
            }
            float a = Mathf.DegToRad(MaxLeafDeg) * p;
            v.Leaf!.Rotation = new Vector3(a, 0, 0);
            v.Beam!.Rotation = new Vector3(a, 0, 0);
            v.Chains!.Position = new Vector3(0, 5.9f + 6f * MathF.Sin(a), 2.0f - 6f * MathF.Cos(a));
            bool down = x.State == BridgeState.Closed;
            v.SignalA += ((down ? 0f : MathF.PI / 2f) - v.SignalA) * ease;
            v.Signal!.Rotation = new Vector3(0, 0, v.SignalA);
            v.BarrierA += ((down ? Mathf.DegToRad(80) : 0f) - v.BarrierA) * ease;
            foreach (var b in v.Barriers) b.Rotation = new Vector3(0, 0, v.BarrierA);
            if (ov > 0)
            {
                // The deck over the water with the gantry at the lift's hinge (west); the leaf's part opens while raised.
                float span = v.SpanE - v.SpanW + 2f;
                k = Put(k, (v.SpanW + v.SpanE) / 2f, 1.3f, v.Z, span, deckH * 1.5f, 7, p, ov, MathF.Min(1f, 7f / span));
                OverviewMarks++;
            }
            if (x.Keeper != v.Keeper) { v.Keeper = x.Keeper; v.KeeperHut!.Visible = x.Keeper; }
        }
        for (int f = 0; f < _footCount && ov > 0; f++)
        {
            float span = _footE[f] - _footW[f] + 2f;
            k = Put(k, (_footW[f] + _footE[f]) / 2f, 1.1f, _footZ[f], span, deckH, 6, 0, ov, 0);
            OverviewMarks++;
        }
        // A closed ford (spring flood): a vermillion bar across the river at the ford, at every zoom.
        FordClosedShown = false;
        int fy = w.Map.FordY;
        if (r.Flooded && fy >= 0 && fy < w.Grid.Height)
        {
            float cell = (float)w.Params.CellMeters, water = (float)(w.Map.RiverHalfW * 2 * cell) + 4f;
            int before = k;
            k = Put(k, w.RiverX[fy] * cell, 0.8f, (fy + 0.5f) * cell, water, MathF.Max(3f, 12f * _mpp), 8, 0, 1, 0);
            FordClosedShown = k > before;
        }
        // The signals last, so they lie over the marks: read from above at a fixed size on screen, just north-west of the pier.
        int signals = 0;
        foreach (var v in _views)
        {
            if (v.Leaf == null) continue;
            float sx = v.Leaf.GetParent<Node3D>().Position.X - 2f;
            int before = k;
            k = Put(k, sx - glyph * 0.6f, 6f, v.Z - glyph * 0.6f, glyph, glyph, 4, v.SignalA / (MathF.PI / 2f), 1, 0);
            if (k > before) signals++;
        }
        Signals = signals;
        if (k > 0) RenderingServer.MultimeshSetBuffer(_lines.Multimesh.GetRid(), _lineBuf);
        if (_lines.Multimesh.VisibleInstanceCount != k) _lines.Multimesh.VisibleInstanceCount = k;
    }

    /// <summary>One flat sign quad (sx metres along x, sz along z) at (x, y, z) with the shader's custom data; returns the next slot.</summary>
    private int Put(int k, float x, float y, float z, float sx, float sz, float kind, float fill, float alpha, float extra)
    {
        if (k >= LineSlots) return k;
        int o = k * Stride;
        _lineBuf[o + 0] = sx; _lineBuf[o + 1] = 0; _lineBuf[o + 2] = 0; _lineBuf[o + 3] = x;
        _lineBuf[o + 4] = 0; _lineBuf[o + 5] = 1; _lineBuf[o + 6] = 0; _lineBuf[o + 7] = y;
        _lineBuf[o + 8] = 0; _lineBuf[o + 9] = 0; _lineBuf[o + 10] = sz; _lineBuf[o + 11] = z;
        _lineBuf[o + 12] = kind; _lineBuf[o + 13] = fill; _lineBuf[o + 14] = alpha; _lineBuf[o + 15] = extra;
        return k + 1;
    }
}
