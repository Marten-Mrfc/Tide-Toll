using System;
using System.Collections.Generic;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Night 3, round 3 (asset track LF): every cottage comes with a garden of its own, on free ground beside it (never on a
/// lane, a queue spot, a touch's spot or a neighbour's garden): a vegetable bed, a washing line whose cloths lift in the
/// wind, a woodpile, a bench, beehives, a flower bed or a hen coop, chosen per cottage from its place in the town so
/// that a street varies. Planned once per town from the plan and the seed; a garden pops in with its cottage.
/// Separate from the simulation's touches (<see cref="TownTouches"/>).
/// </summary>
public partial class TownLife
{
    private static readonly string[] GardenModels =
        { "garden-vegetables", "garden-washing-line", "prop-woodpile", "prop-bench", "prop-beehives", "prop-flower-bed", "prop-hen-coop", "dress-bunting" };
    /// <summary>Longest side on the ground in metres, and the cells it takes along and across the cottage's side.</summary>
    private static readonly float[] GardenMetres = { 2.6f, 4.0f, 1.8f, 1.8f, 2.0f, 2.2f, 2.4f, 5.0f };
    private static readonly int[] GardenAlong = { 2, 2, 1, 1, 1, 2, 2, 3 };
    private const int CoopKind = 6, BuntingKind = 7;

    private struct GardenPlan { public int Plot; public bool River; public int Kind; public Vector3 At; public float Yaw; }
    private readonly List<GardenPlan> _gardenPlans = new();
    private readonly List<Node3D?> _gardens = new();
    private readonly List<float> _gardenPop = new();
    private readonly List<float> _gardenScale = new();
    private static ShaderMaterial? _clothMat, _flagMat;

    private void ClearGardens()
    {
        foreach (var g in _gardens) if (g != null) { RemoveChild(g); g.QueueFree(); }
        _gardens.Clear(); _gardenPop.Clear(); _gardenScale.Clear(); _gardenPlans.Clear();
    }

    private int CoopGarden()
    {
        for (int i = 0; i < _gardenPlans.Count; i++) if (_gardenPlans[i].Kind == CoopKind) return i;
        return -1;
    }

    private void PlanGardens(TownPlan plan, uint seed)
    {
        int n = TownGround.N;
        var used = new bool[n * n];
        foreach (var s in _drv.Town.TouchSpots) Mark(used, s.CellX - 1, s.CellY - 1, s.CellX + 2, s.CellY + 2);
        int last = -1, count = 0;
        bool coop = false;
        for (int i = 0; i < plan.Plots.Length; i++)
        {
            var p = plan.Plots[i];
            if (p.Kind != PlotKind.Cottage) continue;
            // the coop goes to the second cottage that has room for it (the hens live there)
            int kind = !coop && count >= 1 ? CoopKind : (int)(Hash(seed, 200u + (uint)i) % 6u);
            if (kind == last) kind = (kind + 1) % 6;
            if (Place(plan, used, p.X, p.Y, p.W, p.H, p.DoorX, p.DoorY, kind, i, false)
                || (kind != 5 && Place(plan, used, p.X, p.Y, p.W, p.H, p.DoorX, p.DoorY, kind = 5, i, false)))
            { last = kind; count++; coop |= kind == CoopKind; }
        }
        // Bunting beside the market stall, on its free side.
        for (int i = 0; i < plan.Plots.Length; i++)
        {
            var p = plan.Plots[i];
            if (p.Kind == PlotKind.Market) { Place(plan, used, p.X, p.Y, p.W, p.H, p.DoorX, p.DoorY, BuntingKind, i, false); break; }
        }
        var rp = TownSpace.River(plan);
        foreach (var p in rp.Plots)
        {
            if (p.Kind != RiverPlotKind.Cottage) continue;
            int kind = (int)(Hash(seed, 300u + (uint)p.Id) % 6u);
            if (kind == last) kind = (kind + 1) % 6;
            int dx = (int)Math.Round(rp.Px[p.Point] - 0.5), dy = (int)Math.Round(rp.Py[p.Point] - 0.5);
            if (Place(plan, used, p.X, p.Y, p.W, p.H, dx, dy, kind, p.Id, true)
                || (kind != 5 && Place(plan, used, p.X, p.Y, p.W, p.H, dx, dy, kind = 5, p.Id, true))) last = kind;
        }
        for (int i = 0; i < _gardenPlans.Count; i++) { _gardens.Add(null); _gardenPop.Add(0f); _gardenScale.Add(1f); }
    }

    /// <summary>Tries the cottage's sides, the back first (away from its door), then the two sides, then the front.</summary>
    private bool Place(TownPlan plan, bool[] used, int px, int py, int pw, int ph, int doorX, int doorY, int kind, int plot, bool river)
    {
        float cx = px + pw * 0.5f, cy = py + ph * 0.5f, ddx = doorX + 0.5f - cx, ddy = doorY + 0.5f - cy;
        // sides: 0 north (y-), 1 south (y+), 2 west (x-), 3 east (x+)
        int front = MathF.Abs(ddx) > MathF.Abs(ddy) ? (ddx > 0 ? 3 : 2) : (ddy > 0 ? 1 : 0);
        int back = front ^ 1;
        int[] order = { back, back < 2 ? 2 : 0, back < 2 ? 3 : 1, front };
        int along = GardenAlong[kind];
        foreach (int side in order)
        {
            // the cells the garden takes: `along` cells along the side, one across, two cells out from the plot
            int x0, y0, x1, y1;
            bool horizontal = side < 2;
            if (horizontal)
            {
                int y = side == 0 ? py - 2 : py + ph + 1;
                x0 = px + (pw - along) / 2; x1 = x0 + along; y0 = y; y1 = y + 1;
            }
            else
            {
                int x = side == 2 ? px - 2 : px + pw + 1;
                y0 = py + (ph - along) / 2; y1 = y0 + along; x0 = x; x1 = x + 1;
            }
            if (!TownSpace.FreeRect(plan, x0, y0, x1, y1) || Any(used, x0, y0, x1, y1)) continue;
            // 3 cells off every road and queue spot (after the review: a woodpile by the road read as a load)
            // (a flower bed, the fallback, may stand nearer: low and flat, it never reads as a load; the hen coop too,
            // with its hens round it it reads as a coop)
            if (kind != BuntingKind && kind != 5 && kind != CoopKind && TownSpace.NearRoadRect(plan, x0, y0, x1, y1)) continue;
            Mark(used, x0 - 1, y0 - 1, x1 + 1, y1 + 1);
            var at = new Vector3((x0 + x1) * 0.5f * C, 0, (y0 + y1) * 0.5f * C);
            // the long side along the cottage's wall; a bench and a coop face the cottage
            float yaw = horizontal ? (side == 0 ? 0f : MathF.PI) : (side == 2 ? MathF.PI / 2f : -MathF.PI / 2f);
            _gardenPlans.Add(new GardenPlan { Plot = plot, River = river, Kind = kind, At = at, Yaw = yaw });
            return true;
        }
        return false;
    }

    private static bool Any(bool[] t, int x0, int y0, int x1, int y1)
    {
        int n = TownGround.N;
        for (int y = Math.Max(0, y0); y < Math.Min(n, y1); y++)
            for (int x = Math.Max(0, x0); x < Math.Min(n, x1); x++) if (t[y * n + x]) return true;
        return false;
    }

    private static void Mark(bool[] t, int x0, int y0, int x1, int y1)
    {
        int n = TownGround.N;
        for (int y = Math.Max(0, y0); y < Math.Min(n, y1); y++)
            for (int x = Math.Max(0, x0); x < Math.Min(n, x1); x++) t[y * n + x] = true;
    }

    private void ProcessGardens(Town t, float dt)
    {
        bool instant = _drv.StepsRun == 0;
        for (int i = 0; i < _gardenPlans.Count; i++)
        {
            var g = _gardenPlans[i];
            bool given = g.River ? t.InRiverAge && t.RiverPlotGiven(g.Plot) : t.PlotGiven(g.Plot);
            var node = _gardens[i];
            if (!given)
            {
                if (node != null) { RemoveChild(node); node.QueueFree(); _gardens[i] = null; _gardenPop[i] = 0f; }
                continue;
            }
            if (node == null)
            {
                node = MakeGarden(g, out float gs);
                _gardenScale[i] = gs;
                if (node == null) continue;
                _gardens[i] = node;
                _gardenPop[i] = instant ? 1f : 0.0001f;
            }
            if (_gardenPop[i] < 1f)
            {
                _gardenPop[i] = Math.Min(1f, _gardenPop[i] + dt / TownTouches.PopSeconds);
                node.Scale = Vector3.One * _gardenScale[i] * PopEase(_gardenPop[i]);
            }
        }
    }

    private Node3D? MakeGarden(in GardenPlan g, out float s)
    {
        s = 1f;
        string model = GardenModels[g.Kind];
        if (!ModelLib.Has(model)) return null;
        var mi = ModelLib.Instance(model, "Model");
        var a = mi.Mesh.GetAabb();
        s = GardenMetres[g.Kind] / Math.Max(0.05f, Math.Max(a.Size.X, a.Size.Z));
        if (a.Size.Z > a.Size.X) mi.Rotation = new Vector3(0, MathF.PI / 2f, 0);   // long side along local X
        if (model == "garden-washing-line") mi.MaterialOverride = ClothMaterial(mi.Mesh, ref _clothMat, 1.69f, 1.9f);
        if (model == "dress-bunting") mi.MaterialOverride = ClothMaterial(mi.Mesh, ref _flagMat, 1.9f, 2.4f);
        var node = new Node3D { Name = "Garden" + g.Plot + (g.River ? "r" : ""), Position = g.At, Rotation = new Vector3(0, g.Yaw, 0) };
        node.Scale = Vector3.One * s * PopEase(_drv.StepsRun == 0 ? 1f : 0.0001f);
        node.AddChild(mi);
        AddChild(node);
        return node;
    }

    private static float PopEase(float p)
    {
        const float c = 1.4f;
        float x = p - 1f;
        return Math.Max(0.001f, 1f + (c + 1f) * x * x * x + c * x * x);
    }

    // The washing's cloths lift and flutter in the wind (everything below the line between the posts); posts stay.
    private const string ClothCode = @"
shader_type spatial;
render_mode diffuse_lambert, specular_disabled;
uniform sampler2D albedo_tex : source_color, filter_nearest;
uniform float line_y = 1.69;
uniform float half_x = 1.9;
void vertex() {
    float w = clamp((line_y - VERTEX.y) / 0.8, 0.0, 1.0) * step(abs(VERTEX.x), half_x);
    float t = TIME * 1.7 + VERTEX.x * 1.4 + MODEL_MATRIX[3].x * 0.13;
    float gust = 0.6 + 0.4 * sin(TIME * 0.37 + MODEL_MATRIX[3].z * 0.1);
    VERTEX.z += w * gust * (0.32 + 0.1 * sin(t));
    VERTEX.y += w * w * gust * (0.16 + 0.05 * sin(t * 1.3));
}
void fragment() {
    ALBEDO = texture(albedo_tex, UV).rgb;
    ROUGHNESS = 1.0;
    METALLIC = 0.0;
    SPECULAR = 0.0;
}
";

    private static Shader? _clothShader;

    private static Material ClothMaterial(Mesh mesh, ref ShaderMaterial? mat, float lineY, float halfX)
    {
        if (mat != null) return mat;
        var src = mesh.SurfaceGetMaterial(0) as BaseMaterial3D;
        _clothShader ??= new Shader { Code = ClothCode };
        mat = new ShaderMaterial { Shader = _clothShader };
        mat.SetShaderParameter("albedo_tex", src?.AlbedoTexture ?? GD.Load<Texture2D>("res://assets/textures/palette.png"));
        mat.SetShaderParameter("line_y", lineY);
        mat.SetShaderParameter("half_x", halfX);
        return mat;
    }
}
