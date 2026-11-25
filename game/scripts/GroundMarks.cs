using System;
using Godot;
using TideToll.Sim;

namespace TideToll.Game;

/// <summary>
/// What a town plan prints round its buildings, so a cottage reads as a homestead and not a small chest at overview
/// zoom: a soft drop shadow under each cottage and the well (toward the north-west, away from the sun), and an inked
/// yard outline round each house (a tended plot), the well (a round paved yard) and the market (its square), with a
/// gap where the door path runs in. ONE mesh and one draw call (shaders/ground_mark.gdshader), rebuilt when the world
/// is replaced; per frame only the line width follows the zoom.
/// </summary>
public partial class GroundMarks : MeshInstance3D
{
    private SimDriver _sim = null!;
    private ShaderMaterial _mat = null!;
    private float _mpp = -1;
    private static readonly StringName Mpp = new("metres_per_px");

    /// <summary>Marks in the mesh (tests): shadows and yards.</summary>
    public int Shadows { get; private set; }
    public int Yards { get; private set; }

    public void Init(SimDriver sim)
    {
        _sim = sim;
        Name = "Marks";
        CastShadow = ShadowCastingSetting.Off;
        _mat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/ground_mark.gdshader"), RenderPriority = -2 };
        _mat.SetShaderParameter("ink", Palette.Ink);
        MaterialOverride = _mat;
        sim.WorldReplaced += Rebuild;
        sim.HouseBuilt += OnHouseBuilt;
        Rebuild();
    }

    public override void _ExitTree()
    {
        if (_sim == null) return;
        _sim.WorldReplaced -= Rebuild;
        _sim.HouseBuilt -= OnHouseBuilt;
    }

    // A new cottage gets its drop shadow and inked yard like the first houses (the mesh is rebuilt: once per house).
    private void OnHouseBuilt(int index) => Rebuild();

    /// <summary>Metres per screen pixel, so the yard lines stay about 1.3 px wide (Main calls this each frame).</summary>
    public void SetMetresPerPixel(float mpp)
    {
        if (mpp == _mpp || !float.IsFinite(mpp) || mpp <= 0) return;
        _mpp = mpp;
        _mat.SetShaderParameter(Mpp, mpp);
    }

    public void Rebuild()
    {
        var w = _sim.World;
        float cell = (float)w.Params.CellMeters;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        st.SetCustomFormat(0, SurfaceTool.CustomFormat.RgbaFloat);
        Shadows = Yards = 0;
        foreach (var b in w.Buildings)
        {
            var centre = new Vector2((b.X + b.W / 2f) * cell, (b.Y + b.H / 2f) * cell);
            var half = new Vector2(b.W, b.H) * cell / 2f;
            var door = new Vector2((w.Grid.X(b.Door) + 0.5f) * cell, (w.Grid.Y(b.Door) + 0.5f) * cell);
            switch (b.Kind)
            {
                case BuildingKind.House:
                    // The cottage sits at the back of its footprint, the door cell in front: the shadow under the
                    // cottage, the yard round the whole plot.
                    Shadow(st, centre + new Vector2(-1.5f, -1.5f), half - new Vector2(0.4f, 0.6f), 0.34f);
                    Yard(st, centre, half + new Vector2(1.3f, 1.3f), 1.2f, door, new Color(Palette.BentGrass, 0.30f));
                    break;
                case BuildingKind.Well:
                    Shadow(st, centre + new Vector2(-0.6f, -0.6f), new Vector2(1.2f, 1.2f), 0.2f, 1.2f);
                    float r = Math.Max(half.X, half.Y) + 1.2f;
                    Yard(st, centre, new Vector2(r, r), r, door, new Color(Palette.Limewash, 0.30f));
                    break;
                case BuildingKind.Market:
                    Yard(st, centre, half + new Vector2(1.0f, 1.0f), 1.0f, door, new Color(Palette.TrailEarth, 0.28f));
                    break;
            }
        }
        Mesh = Shadows + Yards > 0 ? st.Commit() : null;
    }

    private void Shadow(SurfaceTool st, Vector2 c, Vector2 half, float alpha, float radius = 0.6f)
    {
        Quad(st, c, half, half + new Vector2(1.6f, 1.6f), new Color(Palette.Ink, alpha), new Color(0, radius, 0, 0), 0.04f);
        Shadows++;
    }

    private void Yard(SurfaceTool st, Vector2 c, Vector2 half, float radius, Vector2 door, Color fill)
    {
        // The gap: the door's direction from the centre, carried out to the outline.
        var d = door - c;
        Vector2 gap = MathF.Abs(d.Y) >= MathF.Abs(d.X)
            ? new Vector2(Mathf.Clamp(d.X, -half.X, half.X), MathF.Sign(d.Y) * half.Y)
            : new Vector2(MathF.Sign(d.X) * half.X, Mathf.Clamp(d.Y, -half.Y, half.Y));
        Quad(st, c, half, half + new Vector2(0.6f, 0.6f), fill, new Color(1, radius, gap.X, gap.Y), 0.05f);
        Yards++;
    }

    // One quad covering the mark plus room for its soft edge or line; UV is the point relative to the centre.
    private static void Quad(SurfaceTool st, Vector2 c, Vector2 half, Vector2 cover, Color colour, Color info, float y)
    {
        Vector2[] corners = { new(-1, -1), new(1, -1), new(1, 1), new(-1, 1) };
        int[] tri = { 0, 1, 2, 0, 2, 3 };
        foreach (int k in tri)
        {
            var p = corners[k] * cover;
            st.SetColor(colour);
            st.SetUV(p);
            st.SetUV2(half);
            st.SetCustom(0, info);
            st.SetNormal(Vector3.Up);
            st.AddVertex(new Vector3(c.X + p.X, y, c.Y + p.Y));
        }
    }
}
