using System;
using System.Collections.Generic;
using Godot;
using TideToll.Sim;

namespace TideToll.Game;

/// <summary>
/// Fences and footbridges drawn from the simulation's state, rebuilt only when the edit version changes
/// (SimDriver.Edited) or the world is replaced, never per frame.
/// - "Fences": fence cells become straightened runs (FenceLines, as the toy draws them), covered with woven
///   wattle pieces stretched to fit each run (and thickened to read from above), and posts at ends and corners. Two MultiMeshes
///   ("Fences/Wattle" and "Fences/Posts"), so a long fence costs two draw calls.
/// - "Bridges": one node per footbridge (drawbridges are bridges in the simulation too; CrossingLayer draws them) ("Bridge0", ...) with a landing on each bank cell and a 2 m span on
///   every water cell it covers, as the footbridge models are built to be used.
/// </summary>
public partial class EditLayer : Node3D
{
    private SimDriver _sim = null!;
    private Node3D _fences = null!, _bridges = null!;
    private MultiMeshInstance3D _wattle = null!, _posts = null!;
    private readonly List<Transform3D> _w = new(), _p = new();
    // From straight above a 0.22 m wattle is a hairline. Fences are drawn thicker than life, as people are drawn
    // 1.5x, so a fence reads as a firm line at play zoom; posts likewise.
    private const float Thick = 2.4f, Height = 1.2f;
    private static readonly Vector3 PostScale = new(2.0f, 1.3f, 2.0f);

    /// <summary>Rebuilds so far (tests: an edit must rebuild once).</summary>
    public int Rebuilds { get; private set; }
    public int WattlePieces => _wattle.Multimesh.VisibleInstanceCount;
    public int PostPieces => _posts.Multimesh.VisibleInstanceCount;
    public Node3D BridgesNode => _bridges;

    public void Init(SimDriver sim)
    {
        _sim = sim;
        Name = "Edits";
        _fences = new Node3D { Name = "Fences" };
        _bridges = new Node3D { Name = "Bridges" };
        AddChild(_fences);
        AddChild(_bridges);
        _wattle = ModelLib.Multi("fence-wattle", "Wattle", 512);
        _posts = ModelLib.Multi("fence-post", "Posts", 512);
        _fences.AddChild(_wattle);
        _fences.AddChild(_posts);
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

    private void OnEdited(int version) => Rebuild();

    public void Rebuild()
    {
        Rebuilds++;
        var w = _sim.World;
        BuildFences(w);
        BuildBridges(w);
    }

    private void BuildFences(World w)
    {
        float cell = (float)w.Params.CellMeters;
        _w.Clear(); _p.Clear();
        foreach (var line in FenceLines.Build(w.Fence, w.Grid.Width))
        {
            if (line.Count == 2)
            {
                // A lone fenced cell: a post with a short hurdle across it.
                var c = new Vector3(line[0] * cell, 0, line[1] * cell);
                _p.Add(new Transform3D(Basis.FromScale(PostScale), c));
                _w.Add(new Transform3D(Basis.FromScale(new Vector3(0.75f, Height, Thick)), c));
                continue;
            }
            for (int i = 0; i + 3 < line.Count; i += 2)
            {
                var a = new Vector3(line[i] * cell, 0, line[i + 1] * cell);
                var b = new Vector3(line[i + 2] * cell, 0, line[i + 3] * cell);
                var d = b - a;
                float len = d.Length();
                if (len < 1e-3f) continue;
                int k = Math.Max(1, (int)MathF.Round(len / 2f));
                float piece = len / k;
                // Local X runs along the fence: stretch X to the piece length, thicken it, then yaw X toward d.
                var basis = new Basis(Vector3.Up, MathF.Atan2(-d.Z, d.X)) * Basis.FromScale(new Vector3(piece / 2f, Height, Thick));
                for (int j = 0; j < k; j++) _w.Add(new Transform3D(basis, a + d * ((j + 0.5f) / k)));
            }
            for (int i = 0; i < line.Count; i += 2)
                _p.Add(new Transform3D(Basis.FromScale(PostScale), new Vector3(line[i] * cell, 0, line[i + 1] * cell)));
        }
        Fill(_wattle, _w);
        Fill(_posts, _p);
    }

    private static void Fill(MultiMeshInstance3D mmi, List<Transform3D> xfs)
    {
        var mm = mmi.Multimesh;
        if (xfs.Count > mm.InstanceCount)
        {
            // Grow (rare: only for very long fences); this reallocates, which is fine on an edit.
            int cap = mm.InstanceCount;
            while (cap < xfs.Count) cap *= 2;
            mm.VisibleInstanceCount = -1;
            mm.InstanceCount = cap;
        }
        for (int i = 0; i < xfs.Count; i++) mm.SetInstanceTransform(i, xfs[i]);
        mm.VisibleInstanceCount = xfs.Count;
    }

    private void BuildBridges(World w)
    {
        foreach (var c in _bridges.GetChildren()) { _bridges.RemoveChild(c); c.QueueFree(); }
        float cell = (float)w.Params.CellMeters;
        int n = w.Grid.Width;
        foreach (var br in w.Bridges)
        {
            if (IsDrawbridge(w, br.Id)) continue; // CrossingLayer draws those
            var node = new Node3D { Name = $"Bridge{br.Id}" };
            _bridges.AddChild(node);
            int first = int.MaxValue, last = int.MinValue;
            foreach (int c in br.Cells) { first = Math.Min(first, c); last = Math.Max(last, c); }
            float z = (br.Row + 0.5f) * cell;
            // Landings on the bank cells either side: the ramp's foot (+Z) points away from the water.
            Piece(node, "footbridge-end", "EndW", (first % n - 0.5f) * cell, z, -MathF.PI / 2f);
            Piece(node, "footbridge-end", "EndE", (last % n + 1.5f) * cell, z, MathF.PI / 2f);
            int i = 0;
            foreach (int c in br.Cells) Piece(node, "footbridge-span", $"Span{i++}", (c % n + 0.5f) * cell, z, MathF.PI / 2f);
        }
    }

    /// <summary>Whether a bridge belongs to a drawbridge (river rules), which CrossingLayer draws.</summary>
    public static bool IsDrawbridge(World w, int bridgeId)
    {
        var r = w.River;
        if (r == null) return false;
        var xs = r.Crossings;
        for (int i = 0; i < xs.Count; i++) if (xs[i].BridgeId == bridgeId) return xs[i].Type == CrossingType.Drawbridge;
        return false;
    }

    private static void Piece(Node3D parent, string model, string name, float x, float z, float yaw)
    {
        var mi = ModelLib.Instance(model, name);
        mi.Position = new Vector3(x, 0, z);
        mi.Rotation = new Vector3(0, yaw, 0);
        parent.AddChild(mi);
    }
}
