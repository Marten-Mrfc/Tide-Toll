using Godot;

namespace TideToll.Game;

/// <summary>
/// The tool ghost on the map (shaders/ghost.gdshader): one MultiMesh of flat cell-sized quads drawn over everything.
/// A fence being dragged shows its cells in sky blue with marching dotted outlines where it can stand and hatched
/// vermillion with a cross where it cannot; a footbridge shows the cells it would cover, or a cross where none fits;
/// the eraser shows a dotted ring and hatches what it would remove. Pattern, not only colour, tells them apart.
/// The tool controller fills it with <see cref="Begin"/>, <see cref="Cell"/> / <see cref="Ring"/> and <see cref="End"/>
/// only when the ghost changes (pointer to a new cell), never per frame.
/// </summary>
public partial class ToolGhost : MultiMeshInstance3D
{
    public const int MaxPieces = 1024;
    public const float Ok = 0, Bad = 1, Remove = 2, RingKind = 3;
    public static readonly Color OkColour = Color.FromHtml("#56B4E9");
    public static readonly Color BadColour = Color.FromHtml("#D55E00");

    private float _cell = 2f;
    private float[] _buf = System.Array.Empty<float>();
    private int _count;
    private const int Stride = 12 + 4 + 4; // transform, colour, custom

    /// <summary>Pieces shown now (tests).</summary>
    public int Pieces => Multimesh.VisibleInstanceCount;

    public void Init(float cellMetres)
    {
        Name = "Ghost";
        _cell = cellMetres;
        var quad = new PlaneMesh { Size = Vector2.One };
        var mat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/ghost.gdshader") };
        mat.SetShaderParameter("ink", Palette.Ink);
        mat.RenderPriority = 10;
        quad.Material = mat;
        Multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            UseCustomData = true,
            Mesh = quad,
            InstanceCount = MaxPieces,
            VisibleInstanceCount = 0,
        };
        _buf = new float[MaxPieces * Stride];
        CastShadow = ShadowCastingSetting.Off;
        ExtraCullMargin = 400;
    }

    public void Begin() => _count = 0;

    /// <summary>A cell-sized piece at cell (x, y) of the given kind.</summary>
    public void Cell(int x, int y, float kind, Color c, float cells = 1f)
        => Piece((x + 0.5f) * _cell, (y + 0.5f) * _cell, cells * _cell, kind, c);

    /// <summary>A ring (or any piece) centred on a map-local point in cell units.</summary>
    public void Ring(float cx, float cy, float diameterCells, float kind = RingKind)
        => Piece(cx * _cell, cy * _cell, diameterCells * _cell, kind, kind == RingKind ? Palette.Ink : BadColour);

    private void Piece(float x, float z, float size, float kind, Color c)
    {
        if (_count >= MaxPieces) return;
        int o = _count * Stride;
        // Basis rows (x, y, z) then origin, as Godot's buffer layout for Transform3D: row-major 3x4.
        _buf[o + 0] = size; _buf[o + 1] = 0; _buf[o + 2] = 0; _buf[o + 3] = x;
        _buf[o + 4] = 0; _buf[o + 5] = 1; _buf[o + 6] = 0; _buf[o + 7] = 0.35f;
        _buf[o + 8] = 0; _buf[o + 9] = 0; _buf[o + 10] = size; _buf[o + 11] = z;
        _buf[o + 12] = c.R; _buf[o + 13] = c.G; _buf[o + 14] = c.B; _buf[o + 15] = c.A;
        _buf[o + 16] = kind; _buf[o + 17] = 0; _buf[o + 18] = 0; _buf[o + 19] = 0;
        _count++;
    }

    public void End()
    {
        if (_count > 0) RenderingServer.MultimeshSetBuffer(Multimesh.GetRid(), _buf);
        Multimesh.VisibleInstanceCount = _count;
    }

    public void Clear()
    {
        if (Multimesh.VisibleInstanceCount != 0) Multimesh.VisibleInstanceCount = 0;
        _count = 0;
    }
}
