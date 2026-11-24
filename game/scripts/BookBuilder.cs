using Godot;
using TideToll.Sim;

namespace TideToll.Game;

/// <summary>
/// Builds the open book on the table from generated meshes (no imported art yet).
/// World units are metres; one map cell is 2 m, so the 128-cell map is 256 m across and
/// the book is sized around it. North is -Z, east is +X; the spine runs north-south at x = 0.
/// </summary>
public static class BookBuilder
{
    public const float MapSize = 256f;      // 128 cells x 2 m
    public const float PageWidth = 300f;    // x
    public const float PageDepth = 330f;    // z
    public const float PageThickness = 8f;
    public const float CoverLip = 8f;       // cover visible beyond the pages
    public const float Margin = (PageWidth - MapSize) / 2f;

    /// <summary>Returns the book root; the map's north-west corner is the node "Book/RightPage/MapOrigin".</summary>
    public static Node3D Build(Node3D parent, SimParams p)
    {
        var table = new Node3D { Name = "Table" };
        parent.AddChild(table);
        AddBox(table, "Top", new Vector3(1600, 10, 1000), new Vector3(0, -PageThickness - 4 - 5, 0), Palette.Mud);
        // Plank seams, so the table reads as wood rather than a flat brown field.
        for (int i = -5; i <= 5; i++)
            AddBox(table, $"Seam{i + 5}", new Vector3(1600, 0.4f, 1.2f), new Vector3(0, -PageThickness - 4 + 0.05f, i * 90f + 20f), Palette.Mud.Darkened(0.25f));
        AddInkwell(table, new Vector3(-420, -PageThickness - 4, -140));

        var book = new Node3D { Name = "Book" };
        parent.AddChild(book);
        AddBox(book, "Cover", new Vector3(2 * PageWidth + 2 * CoverLip, 4, PageDepth + 2 * CoverLip),
            new Vector3(0, -PageThickness - 2, 0), Palette.SootBrick);

        var left = AddPage(book, "LeftPage", -PageWidth / 2f - 1f);
        var right = AddPage(book, "RightPage", PageWidth / 2f + 1f);
        AddGutterShadow(book);
        AddRibbon(book);
        FillLeftPage(left);
        FillRightPage(right, p);
        return book;
    }

    private static Node3D AddPage(Node3D book, string name, float centreX)
    {
        var page = new Node3D { Name = name, Position = new Vector3(centreX, 0, 0) };
        book.AddChild(page);
        // Top surface at y = 0 so everything drawn on a page sits at y >= 0.
        AddBox(page, "Paper", new Vector3(PageWidth, PageThickness, PageDepth), new Vector3(0, -PageThickness / 2f, 0), Palette.Paper);
        return page;
    }

    private static void FillLeftPage(Node3D page)
    {
        AddLabel(page, "ChapterTitle", "Chapter I", new Vector3(0, 0.1f, -118), 2600, Palette.Ink);
        AddLabel(page, "ChapterName", "The Hamlet", new Vector3(0, 0.1f, -88), 1700, Palette.Ink);
        AddBox(page, "Rule", new Vector3(120, 0.2f, 1.2f), new Vector3(0, 0.1f, -66), Palette.Ink);
        // Printed lines of text, with ragged ends and paragraph breaks.
        var rng = new Mulberry32(11);
        float z = -48;
        for (int line = 0; line < 20; line++)
        {
            bool paraEnd = line % 6 == 5;
            float len = paraEnd ? 60f + 80f * (float)rng.NextDouble() : 200f + 30f * (float)rng.NextDouble();
            AddBox(page, $"Text{line}", new Vector3(len, 0.2f, 3.2f), new Vector3(-115 + len / 2f, 0.1f, z), Palette.FadedInk);
            z += paraEnd ? 14f : 9f;
        }
        AddLabel(page, "Folio", "1", new Vector3(-130, 0.1f, 152), 900, Palette.FadedInk);
    }

    private static void FillRightPage(Node3D page, SimParams p)
    {
        float side = p.GridWidth * (float)p.CellMeters;
        var origin = new Node3D { Name = "MapOrigin", Position = new Vector3(-side / 2f, 0, -side / 2f) };
        page.AddChild(origin);

        // The playfield: one vertex per cell corner, so the wear shader can work per cell later.
        var plane = new PlaneMesh
        {
            Size = new Vector2(side, side),
            SubdivideWidth = p.GridWidth - 1,
            SubdivideDepth = p.GridHeight - 1,
        };
        var map = new MeshInstance3D
        {
            Name = "Map",
            Mesh = plane,
            Position = new Vector3(side / 2f, 0.05f, side / 2f),
            MaterialOverride = Palette.Matte(Palette.Paper.Lerp(Palette.Meadow, 0.6f)),
        };
        origin.AddChild(map);

        // Double ink frame: a heavy inner line and a hairline outside it, as on an engraved plan.
        AddFrame(page, "FrameInner", side + 2f, 1.4f);
        AddFrame(page, "FrameOuter", side + 10f, 0.6f);
        AddCompass(page, new Vector3(side / 2f - 22f, 0.2f, -side / 2f + 22f), 14f);
        AddLabel(page, "MapTitle", "A Plan of the Hamlet", new Vector3(0, 0.1f, -side / 2f - 11f), 900, Palette.Ink);
        AddLabel(page, "Folio", "2", new Vector3(130, 0.1f, 152), 900, Palette.FadedInk);
    }

    private static void AddFrame(Node3D page, string name, float size, float w)
    {
        var f = new Node3D { Name = name };
        page.AddChild(f);
        float h = size / 2f;
        AddBox(f, "N", new Vector3(size + w, 0.3f, w), new Vector3(0, 0.15f, -h), Palette.Ink);
        AddBox(f, "S", new Vector3(size + w, 0.3f, w), new Vector3(0, 0.15f, h), Palette.Ink);
        AddBox(f, "W", new Vector3(w, 0.3f, size + w), new Vector3(-h, 0.15f, 0), Palette.Ink);
        AddBox(f, "E", new Vector3(w, 0.3f, size + w), new Vector3(h, 0.15f, 0), Palette.Ink);
    }

    /// <summary>A four-point star with a north mark; it will turn with the camera yaw later.</summary>
    private static void AddCompass(Node3D page, Vector3 at, float r)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < 4; i++)
        {
            float a = i * Mathf.Pi / 2f;
            var tip = new Vector3(Mathf.Sin(a), 0, -Mathf.Cos(a)) * r;
            var l = new Vector3(Mathf.Sin(a - Mathf.Pi / 4f), 0, -Mathf.Cos(a - Mathf.Pi / 4f)) * r * 0.25f;
            var rt = new Vector3(Mathf.Sin(a + Mathf.Pi / 4f), 0, -Mathf.Cos(a + Mathf.Pi / 4f)) * r * 0.25f;
            // Half of each point in ink, half in paper: the classic engraved rose.
            st.SetColor(Palette.Ink); st.AddVertex(Vector3.Zero); st.AddVertex(l); st.AddVertex(tip);
            st.SetColor(Palette.Limewash); st.AddVertex(Vector3.Zero); st.AddVertex(tip); st.AddVertex(rt);
        }
        var mat = Palette.Unlit(Colors.White);
        mat.VertexColorUseAsAlbedo = true;
        mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        page.AddChild(new MeshInstance3D { Name = "Compass", Mesh = st.Commit(), Position = at, MaterialOverride = mat });
        AddLabel(page, "CompassN", "N", at + new Vector3(0, 0, -r - 5f), 700, Palette.Ink);
    }

    private static void AddGutterShadow(Node3D book)
    {
        // A soft dark band down the spine, standing in for the curve of the pages.
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        float w = 20f, h = PageDepth / 2f, y = 0.3f;
        var dark = new Color(Palette.Ink, 0.35f);
        var clear = new Color(Palette.Ink, 0f);
        foreach (float s in new[] { -1f, 1f })
        {
            var a = new Vector3(0, y, -h); var b = new Vector3(s * w, y, -h);
            var c = new Vector3(s * w, y, h); var d = new Vector3(0, y, h);
            st.SetColor(dark); st.AddVertex(a); st.SetColor(clear); st.AddVertex(b); st.AddVertex(c);
            st.SetColor(dark); st.AddVertex(a); st.SetColor(clear); st.AddVertex(c); st.SetColor(dark); st.AddVertex(d);
        }
        var mat = Palette.Unlit(Colors.White, transparent: true);
        mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        book.AddChild(new MeshInstance3D { Name = "Gutter", Mesh = st.Commit(), MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    private static void AddRibbon(Node3D book)
    {
        AddBox(book, "Ribbon", new Vector3(9, 0.4f, 120), new Vector3(0, 0.4f, PageDepth / 2f + 20f), Palette.Brick);
    }

    private static void AddInkwell(Node3D table, Vector3 at)
    {
        var well = new MeshInstance3D
        {
            Name = "Inkwell",
            Mesh = new CylinderMesh { TopRadius = 22, BottomRadius = 26, Height = 24, RadialSegments = 16 },
            Position = at + new Vector3(0, 12, 0),
            MaterialOverride = Palette.Matte(Palette.CastIronGreen),
        };
        table.AddChild(well);
        well.AddChild(new MeshInstance3D
        {
            Name = "Ink",
            Mesh = new CylinderMesh { TopRadius = 10, BottomRadius = 10, Height = 1, RadialSegments = 16 },
            Position = new Vector3(0, 12.2f, 0),
            MaterialOverride = Palette.Matte(Palette.Ink),
        });
    }

    public static MeshInstance3D AddBox(Node3D parent, string name, Vector3 size, Vector3 pos, Color c)
    {
        var m = new MeshInstance3D { Name = name, Mesh = new BoxMesh { Size = size }, Position = pos, MaterialOverride = Palette.Matte(c) };
        parent.AddChild(m);
        return m;
    }

    private static void AddLabel(Node3D parent, string name, string text, Vector3 pos, int fontSize, Color c)
    {
        // Lies flat on the page, reading north-up. PixelSize keeps labels in metres.
        parent.AddChild(new Label3D
        {
            Name = name,
            Text = text,
            Position = pos,
            RotationDegrees = new Vector3(-90, 0, 0),
            FontSize = fontSize,
            PixelSize = 0.01f,
            Modulate = c,
            OutlineSize = 0,
            Shaded = false,
            DoubleSided = false,
        });
    }
}
