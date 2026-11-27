using Godot;

namespace TideToll.Game;

/// <summary>
/// Night 3 (asset round 2, track BK): the materials of the book and its table, the ribbon, and the small things of a
/// writing desk lying round the book. Materials and decoration only: no size, position or node name of the book, its
/// pages, its boards or the table is changed here (the pause motion and the settings pages are built on them).
/// </summary>
public static partial class BookBuilder
{
    /// <summary>
    /// The page's paper as it appears on screen where it is lit (no shadow): paper.gdshader's custom light() keeps
    /// the lit page exactly the palette's Paper (#F2EAD8; the grain averages out to it). The interface's turning leaf
    /// matches this colour.
    /// </summary>
    public static readonly Color PaperOnScreen = Palette.Paper;
    /// <summary>Print on the page: full ink for text and rules, and a soft ink (not the interface's faded ink) for
    /// secondary lines, dark enough to read at the book view.</summary>
    public static readonly Color PageInk = Palette.Ink;
    public static readonly Color PageInkSoft = Palette.Ink.Lerp(Palette.Paper, 0.30f);

    /// <summary>Real centimetres of the desk things to the book's metres (a 30 cm page is 300 m).</summary>
    public const float DeskScale = 10f;

    private static StandardMaterial3D? _table, _ribbonMat;

    /// <summary>The table top: the plank texture (800 x 500 m a tile), laid by local position so it never stretches.</summary>
    public static StandardMaterial3D TableMaterial()
    {
        if (_table != null) return _table;
        _table = new StandardMaterial3D
        {
            AlbedoTexture = ModelLib.MipTexture("res://assets/textures/table-wood.png"),
            // A little lighter than the texture, as the lamp-lit wood of a desk (the sun alone leaves it near Mud).
            AlbedoColor = new Color(1.18f, 1.14f, 1.10f),
            Roughness = 1f, MetallicSpecular = 0.1f,
            Uv1Triplanar = true,
            Uv1Scale = new Vector3(1f / 800f, 1f / 500f, 1f / 500f),
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
        };
        return _table;
    }

    /// <summary>
    /// Leather for a board of <paramref name="size"/> (x, z): one whole board of the generated leather (grain, rubbed
    /// edges, a blind-tooled double frame and fleurons) mapped once over the face, by local position.
    /// </summary>
    public static StandardMaterial3D LeatherMaterial(Vector2 size)
    {
        return new StandardMaterial3D
        {
            AlbedoTexture = ModelLib.MipTexture("res://assets/textures/book-leather.png"),
            Roughness = 1f, MetallicSpecular = 0.1f,
            Uv1Triplanar = true,
            Uv1Scale = new Vector3(1f / size.X, 1f / 4f, 1f / size.Y),
            Uv1Offset = new Vector3(0.5f, 0.5f, 0.5f),
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
        };
    }

    /// <summary>The ink pot and quill, a candle, a seal stamp and a loose ribbon: at the table's edges of the view,
    /// never under the book (the open book reaches x 319, z 173) or where the shut cover's lettering is read.</summary>
    private static void AddDeskThings(Node3D table, float topY)
    {
        // Night 3 fix: the ink pot (and its quill) and the seal 40 m nearer the middle, so neither is cut by the view's edge.
        Desk(table, "Inkwell", "desk-inkwell", new Vector3(-372, topY, -192), -150f);
        Desk(table, "Candle", "desk-candle", new Vector3(402, topY, -246), 20f);
        Desk(table, "SealStamp", "desk-seal", new Vector3(372, topY, 192), -25f);
        Desk(table, "LooseRibbon", "desk-ribbon", new Vector3(-392, topY, 226), 70f);
    }

    private static void Desk(Node3D table, string name, string model, Vector3 at, float yawDeg)
    {
        if (!ModelLib.Has(model)) return;
        var m = ModelLib.Instance(model, name);
        m.Position = at;
        m.RotationDegrees = new Vector3(0, yawDeg, 0);
        m.Scale = Vector3.One * DeskScale;
        table.AddChild(m);
    }

    /// <summary>
    /// The bookmark: a silk ribbon from the gutter, over the foot of the pages and down onto the table, lying in a
    /// slight curve and cut in a swallowtail. Same node name and place as before ("Ribbon" at the foot of the gutter).
    /// </summary>
    private static void AddRibbon(Node3D book)
    {
        var at = new Vector3(0, -GutterSag + 0.2f, PageDepth / 2f + 20f);
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        const float w = 4.5f;
        float tableY = -PageThickness - 4f + 0.3f - at.Y;
        // centre line (x, y, z) relative to the node: along the gutter, over the page edge, down, along the table
        var pts = new System.Collections.Generic.List<Vector3>();
        for (int i = 0; i <= 20; i++)
        {
            float z = -60f + 120f * i / 20f;
            float y = z < -12f ? 0.2f : z < 2f ? Mathf.Lerp(0.2f, tableY, Mathf.SmoothStep(-12f, 2f, z)) : tableY;
            float x = z > 0f ? 5f * Mathf.Sin((z / 60f) * Mathf.Pi * 0.9f) : 0f;
            pts.Add(new Vector3(x, y, z));
        }
        var brick = Palette.Brick;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector3 a = pts[i], b = pts[i + 1];
            var d = (b - a).Normalized();
            var s = new Vector3(-d.Z, 0, d.X).Normalized() * w;
            // a soft fold line down the middle: one half a shade darker, so the silk reads as cloth, not a stick
            Quad(st, a - s, b - s, b, a, brick.Darkened(0.12f));
            Quad(st, a, b, b + s, a + s, brick);
        }
        var end = pts[^1];
        var tip = end + new Vector3(0, 0, 9f);
        Tri(st, end + new Vector3(-w, 0, 0), end, tip + new Vector3(-w, 0, 0), brick.Darkened(0.12f));
        Tri(st, end, end + new Vector3(w, 0, 0), tip + new Vector3(w, 0, 0), brick);
        st.GenerateNormals();
        _ribbonMat ??= new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true, Roughness = 0.8f, MetallicSpecular = 0.2f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        book.AddChild(new MeshInstance3D { Name = "Ribbon", Mesh = st.Commit(), Position = at, MaterialOverride = _ribbonMat });
    }

    private static void Quad(SurfaceTool st, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
    {
        Tri(st, a, b, c, col);
        Tri(st, a, c, d, col);
    }

    private static void Tri(SurfaceTool st, Vector3 a, Vector3 b, Vector3 c, Color col)
    {
        // wound so the face looks up (+Y)
        var n = (b - a).Cross(c - a);
        if (n.Y < 0) (b, c) = (c, b);
        st.SetColor(col); st.AddVertex(a);
        st.SetColor(col); st.AddVertex(b);
        st.SetColor(col); st.AddVertex(c);
    }
}
