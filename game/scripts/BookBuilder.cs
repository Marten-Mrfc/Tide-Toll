using Godot;
using TideToll.Sim;

namespace TideToll.Game;

/// <summary>
/// Builds the open book on the table from generated meshes. World units are metres; one map cell is 2 m, so the
/// 128-cell map is 256 m across and the book is sized around it. North is -Z, east is +X; the spine runs
/// north-south at x = 0. Depth so it reads as a book, not two rectangles: each page's top curves down into the
/// gutter and darkens toward it (baked into the mesh), the page stack shows as stepped edges along the outer
/// sides, and the pages use the paper shader so the rendered page is exactly the palette's Paper.
/// The map's north-west corner is "Book/RightPage/MapOrigin"; everything on the map hangs under it.
/// The left page is the chapter page; its words are drawn by the interface (ChapterPage) over the paper.
/// </summary>
public static partial class BookBuilder
{
    public const float MapSize = 256f;      // 128 cells x 2 m
    public const float PageWidth = 300f;    // x
    public const float PageDepth = 330f;    // z
    public const float PageThickness = 8f;
    public const float CoverLip = 8f;       // cover visible beyond the pages
    public const float Margin = (PageWidth - MapSize) / 2f;
    public const float GutterCurve = 18f;   // width of the curve into the spine
    public const float GutterSag = 2.4f;    // how far the page drops at the spine
    public const int StackSteps = 4;        // page edges visible along the outer sides
    public static Vector2 CoverSize => new(2 * PageWidth + 2 * CoverLip + 2f, PageDepth + 2 * CoverLip);

    private static ShaderMaterial? _paper;

    /// <summary>Returns the book root.</summary>
    public static Node3D Build(Node3D parent, SimParams p, int chapter = 1)
    {
        var table = new Node3D { Name = "Table" };
        parent.AddChild(table);
        // Night 3: a wooden table in the palette's earth tones (a generated plank texture with grain, knots, butt joints
        // and soft wear: assets/src/2d/book-desk.py), and the small things of a writing desk at the edges of the view.
        var top = AddBox(table, "Top", new Vector3(1600, 10, 1000), new Vector3(0, -PageThickness - 4 - 5, 0), Palette.Mud);
        top.MaterialOverride = TableMaterial();
        AddDeskThings(table, -PageThickness - 4);

        var book = new Node3D { Name = "Book" };
        parent.AddChild(book);
        var cover = CoverSize;
        var binding = AddBox(book, "Cover", new Vector3(cover.X, 4, cover.Y), new Vector3(0, -PageThickness - 2, 0), Palette.SootBrick);
        binding.MaterialOverride = LeatherMaterial(new Vector2(cover.X, cover.Y));

        var left = AddPage(book, "LeftPage", -1);
        var right = AddPage(book, "RightPage", +1);
        AddRibbon(book);
        FillLeftPage(left, chapter);
        FillRightPage(right, p, chapter);
        return book;
    }

    public static ShaderMaterial PaperMaterial()
    {
        if (_paper != null) return _paper;
        _paper = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/paper.gdshader") };
        _paper.SetShaderParameter("paper_tex", ModelLib.MipTexture("res://assets/textures/paper-page.png"));
        _paper.SetShaderParameter("paper", Palette.Paper);
        return _paper;
    }

    /// <summary>A page: curved top sheet, the stepped stack of page edges under it, all in paper.</summary>
    private static Node3D AddPage(Node3D book, string name, int side)
    {
        var page = new Node3D { Name = name, Position = new Vector3(side * (PageWidth / 2f + 1f), 0, 0) };
        book.AddChild(page);
        // The pages cast no shadow: a sheet shadowing itself through the shadow map's bias greys the paper, and
        // the cover under them already shadows the table.
        var off = GeometryInstance3D.ShadowCastingSetting.Off;
        page.AddChild(new MeshInstance3D { Name = "Paper", Mesh = PageMesh(side), MaterialOverride = PaperMaterial(), CastShadow = off });
        page.AddChild(new MeshInstance3D { Name = "Stack", Mesh = StackMesh(side), MaterialOverride = PaperMaterial(), CastShadow = off });
        return page;
    }

    /// <summary>
    /// The top sheet: flat at y = 0 over the map area, dropping smoothly into the gutter over the last
    /// GutterCurve metres, with vertex shading that darkens toward the spine (a soft gutter shadow).
    /// </summary>
    private static ArrayMesh PageMesh(int side)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        const int steps = 24;
        float hw = PageWidth / 2f, hd = PageDepth / 2f;
        // x from the spine edge (local -side*hw) outward; the curve lives in the first GutterCurve metres.
        float[] xs = new float[steps + 2];
        for (int i = 0; i <= steps; i++) xs[i] = GutterCurve * i / steps;
        xs[steps + 1] = PageWidth;
        for (int i = 0; i < xs.Length - 1; i++)
        {
            float a = xs[i], b = xs[i + 1];
            var pa = Point(side, a, hw); var pb = Point(side, b, hw);
            float sa = Shade(a), sb = Shade(b);
            var v = new[] { new Vector3(pa.X, pa.Y, -hd), new Vector3(pb.X, pb.Y, -hd), new Vector3(pb.X, pb.Y, hd), new Vector3(pa.X, pa.Y, hd) };
            float[] sh = { sa, sb, sb, sa };
            int[] tri = side > 0 ? new[] { 0, 1, 2, 0, 2, 3 } : new[] { 0, 2, 1, 0, 3, 2 };
            foreach (int k in tri) { st.SetColor(new Color(sh[k], sh[k], sh[k])); st.SetNormal(Vector3.Up); st.AddVertex(v[k]); }
        }
        st.GenerateNormals();
        return st.Commit();
    }

    // Local position of the sheet at distance d from the spine edge.
    private static Vector2 Point(int side, float d, float hw)
    {
        float t = Mathf.Clamp(d / GutterCurve, 0f, 1f);
        float y = -GutterSag * (1f - t) * (1f - t);
        return new Vector2(side * (d - hw), y);
    }

    private static float Shade(float d)
    {
        float t = Mathf.Clamp(d / (GutterCurve * 2.2f), 0f, 1f);
        return Mathf.Lerp(0.64f, 1f, Mathf.SmoothStep(0f, 1f, t));
    }

    /// <summary>
    /// Page edges: slabs under the top sheet, each a little lower and a little wider on the three outer sides,
    /// alternating paper and a shaded paper, so the stack shows as fine stepped lines (the ink pass draws them).
    /// </summary>
    private static ArrayMesh StackMesh(int side)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        float hw = PageWidth / 2f, hd = PageDepth / 2f;
        for (int k = 0; k <= StackSteps; k++)
        {
            // The body (k = 0) stays below the sheet's lowest point in the gutter, or it would cover the curve.
            float grow = k * 1.3f, top = -GutterSag - 0.2f - 0.6f * k, bottom = -PageThickness;
            float shade = k % 2 == 0 ? 0.95f : 0.80f;
            // Inner (spine) edge stays put; the outer edge and both ends grow.
            float inner = side > 0 ? -hw : hw, outer = side * (hw + grow);
            float x0 = Mathf.Min(inner, outer), x1 = Mathf.Max(inner, outer);
            Box(st, new Vector3(x0, bottom, -hd - grow), new Vector3(x1, top, hd + grow), shade);
        }
        st.GenerateNormals();
        return st.Commit();
    }

    private static void Box(SurfaceTool st, Vector3 a, Vector3 b, float shade)
    {
        var c = new Color(shade, shade, shade);
        Vector3[] p =
        {
            new(a.X, b.Y, a.Z), new(b.X, b.Y, a.Z), new(b.X, b.Y, b.Z), new(a.X, b.Y, b.Z), // top
            new(a.X, a.Y, a.Z), new(b.X, a.Y, a.Z), new(b.X, a.Y, b.Z), new(a.X, a.Y, b.Z), // bottom
        };
        int[][] faces = { new[] { 0, 1, 2, 3 }, new[] { 3, 2, 6, 7 }, new[] { 1, 0, 4, 5 }, new[] { 0, 3, 7, 4 }, new[] { 2, 1, 5, 6 } };
        foreach (var f in faces)
            foreach (int k in new[] { f[0], f[1], f[2], f[0], f[2], f[3] }) { st.SetColor(c); st.AddVertex(p[k]); }
    }

    private static void FillLeftPage(Node3D page, int chapter)
    {
        // The chapter page's words (title, how to play, the village and path-health entries, the legend) are written
        // by the interface over this page (GameUi / ChapterPage), so they stay sharp at every zoom; only the folio
        // is printed on the paper.
        AddLabel(page, "Folio", chapter == 2 ? "21" : "1", new Vector3(-130, 0.1f, 152), 900, Palette.FadedInk, UiKit.Serif);
    }

    private static void FillRightPage(Node3D page, SimParams p, int chapter)
    {
        float side = p.GridWidth * (float)p.CellMeters;
        // A hair above the paper so the map plane never fights the page for depth.
        var origin = new Node3D { Name = "MapOrigin", Position = new Vector3(-side / 2f, 0.02f, -side / 2f) };
        page.AddChild(origin);
        // Double ink frame: a heavy inner line and a hairline outside it, as on an engraved plan. Flat and unlit, so
        // it stays crisp and exactly Ink (thin enough that the depth ink pass adds nothing to it).
        AddFrame(page, "FrameInner", side + 2f, 1.4f);
        AddFrame(page, "FrameOuter", side + 10f, 0.6f);
        AddCompass(page, new Vector3(side / 2f - 10f, 0.04f, -side / 2f - 14f), 7f);
        // Chapter II's pages are further on in the book (folios 21 and 22).
        // The title in the chapter page's display face (Uncial Antiqua), folios in Alegreya, as printed in the book.
        AddLabel(page, "MapTitle", chapter == 2 ? "The Hamlet by the River" : "A Plan of the Hamlet", new Vector3(0, 0.1f, -side / 2f - 13f), 1000, Palette.Ink, UiKit.Display);
        AddLabel(page, "Folio", chapter == 2 ? "22" : "2", new Vector3(130, 0.1f, 152), 900, Palette.FadedInk, UiKit.Serif);
    }

    private static void AddFrame(Node3D page, string name, float size, float w)
    {
        var f = new Node3D { Name = name };
        page.AddChild(f);
        float h = size / 2f, y = 0.03f, t = 0.04f;
        AddFlat(f, "N", new Vector3(size + w, t, w), new Vector3(0, y, -h), Palette.Ink);
        AddFlat(f, "S", new Vector3(size + w, t, w), new Vector3(0, y, h), Palette.Ink);
        AddFlat(f, "W", new Vector3(w, t, size + w), new Vector3(-h, y, 0), Palette.Ink);
        AddFlat(f, "E", new Vector3(w, t, size + w), new Vector3(h, y, 0), Palette.Ink);
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
        mat.VertexColorIsSrgb = true;
        mat.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        page.AddChild(new MeshInstance3D { Name = "Compass", Mesh = st.Commit(), Position = at, MaterialOverride = mat });
        AddLabel(page, "CompassN", "N", at + new Vector3(0, 0, -r - 4f), 700, Palette.Ink, UiKit.Serif);
    }

    public static MeshInstance3D AddBox(Node3D parent, string name, Vector3 size, Vector3 pos, Color c)
    {
        var m = new MeshInstance3D { Name = name, Mesh = new BoxMesh { Size = size }, Position = pos, MaterialOverride = Palette.Matte(c) };
        parent.AddChild(m);
        return m;
    }

    /// <summary>Printed marks on the paper: unlit so they keep their exact colour, casting no shadow.</summary>
    private static void AddFlat(Node3D parent, string name, Vector3 size, Vector3 pos, Color c)
    {
        parent.AddChild(new MeshInstance3D
        {
            Name = name, Mesh = new BoxMesh { Size = size }, Position = pos, MaterialOverride = Palette.Unlit(c),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }

    /// <summary>Printed text lying flat on the page, reading north-up; <paramref name="size100"/> is the em in centimetres.</summary>
    private static FontFile? _plainPrint;
    /// <summary>A copy of the font with mipmapped glyphs, for print on the page that is not a distance field.</summary>
    public static FontFile PlainPrint(FontFile f)
    {
        if (_plainPrint != null && _plainPrint.ResourceName == f.ResourcePath) return _plainPrint;
        var m = (FontFile)f.Duplicate();
        m.GenerateMipmaps = true;
        m.Oversampling = 1f;
        m.ResourceName = f.ResourcePath;
        return _plainPrint = m;
    }

    public static Label3D AddLabel(Node3D parent, string name, string text, Vector3 pos, int size100, Color c, FontFile font)
    {
        // An MSDF font: one small atlas, crisp at every zoom. FontSize 100 px at size100 / 10000 m per pixel.
        // Round 4: not for Alegreya: the variable font's overlapping contours leave stray marks in a distance field
        // ("2:" after digits, dots over letters), so it is printed from a plain 100 px atlas with mipmaps.
        bool plain = font == UiKit.Serif;
        // Night 3: print on the page is set in the page's own inks (PageInk, PageInkSoft): the faded ink of the
        // interface read far too pale on paper (the summary's "Done" column). A thin outline in the same colour gives
        // the small serif print one more pixel of stroke, so it holds at the book view.
        if (c == Palette.Ink) c = PageInk;
        else if (c == Palette.FadedInk) c = PageInkSoft;
        var l = new Label3D
        {
            Name = name,
            Text = text,
            Position = pos,
            RotationDegrees = new Vector3(-90, 0, 0),
            Font = plain ? PlainPrint(font) : UiKit.PrintFont(font),
            TextureFilter = plain ? BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic : BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
            FontSize = 100,
            PixelSize = size100 / 10000f,
            Modulate = c,
            OutlineSize = plain && size100 <= 760 ? 3 : 0,
            OutlineModulate = c,
            Shaded = false,
            DoubleSided = false,
        };
        parent.AddChild(l);
        return l;
    }
}
