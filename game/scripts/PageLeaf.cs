using System;
using Godot;

namespace TideToll.Game;

/// <summary>
/// A leaf of the book's paper turning (round L3: a leaf, not a wipe). The lifted part of the leaf lies between
/// <see cref="Spine"/> (where it is bound, or hangs from) and <see cref="Edge"/> (its free edge), across
/// <see cref="Area"/>'s height. It is foreshortened: the free edge, lifted toward the eye by <see cref="Lift"/>, stands
/// a little taller than the page at the spine (a trapezoid). While the free edge is on the side the leaf started from
/// (<see cref="From"/>), the leaf shows its front: the page it carries (what was drawn under it, read from the screen
/// as it was before the new page was drawn: a <see cref="BackBufferCopy"/> placed by the owner), squeezed toward the
/// spine. Past the spine it shows its back: plain paper of the book's tone with faint ruled lines. A soft shadow falls
/// on the page beyond the free edge, the last part of the leaf before its edge curls away from the light, and a pale
/// highlight runs along the curl. <see cref="Opaque"/> (the age change over the whole screen): both faces are paper.
/// Drawn by one shader on one rectangle plus the shadow's rectangles: nothing is allocated per frame.
/// </summary>
public partial class PageLeaf : Control
{
    public Rect2 Area;
    public float Spine, Edge;
    /// <summary>0 lying flat, 1 standing up: the foreshortening, the curl and the shadow.</summary>
    public float Lift = 1f;
    public bool Opaque;
    public float S = 1f;
    /// <summary>The side of the spine the leaf started from: +1 right (a page turned forward), -1 left.</summary>
    public float From = 1f;
    /// <summary>How much taller the free edge stands than the spine when the leaf stands up (share of the height).</summary>
    public const float Grow = 0.045f;
    private const int Strips = 14;

    /// <summary>The book's paper as the 3D pages show it on screen at the spread view (measured by the frame strips'
    /// "STRIPS paper" check); the leaf, the settings pages and the age-change leaf are drawn in it.</summary>
    public static readonly Color BookPaper = new(237f / 255f, 230f / 255f, 212f / 255f);

    private readonly ShaderMaterial _mat;
    private readonly ColorRect _face;
    private static readonly StringName PSpineLeft = "spine_left", PSampled = "sampled", PSqueeze = "squeeze", PGrow = "grow",
        PLift = "lift", PLines = "lines", PFaceW = "face_w", PS = "s", PPaper = "paper", PInk = "ink", PLight = "light", PBackA = "back_a", PCurl = "curl_w", PShade = "shade";

    /// <summary>The leaf's face (the shader rectangle). An owner that draws the page coming over the page going may move it
    /// into its own tree: before the page coming while the front shows (it reads the screen as it was before that page),
    /// after it while the back shows (SettingsPages.OrderForLeaf). Its place is set in the leaf's own coordinates, so its
    /// new parent must lie where the leaf lies.</summary>
    public Control Face => _face;
    /// <summary>How opaque the leaf's back is now (1 standing; going to 0 as it lands, so the page under it shows through
    /// before the leaf lies flat: round L3 review, the landing page popped in on the last frame). 1 when Opaque.</summary>
    public float BackAlpha { get; private set; } = 1f;
    /// <summary>The leaf shows its front (the page it carries, squeezed) now (tests).</summary>
    public bool ShowsFront { get; private set; }

    public PageLeaf()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _mat = new ShaderMaterial { Shader = new Shader { Code = FaceShader } };
        _mat.SetShaderParameter(PPaper, BookPaper);
        _mat.SetShaderParameter(PInk, UiKit.Ink);
        _mat.SetShaderParameter(PLight, UiKit.PaperHover);
        _face = new ColorRect { Name = "Face", MouseFilter = MouseFilterEnum.Ignore, Material = _mat, Color = Colors.White, Size = Vector2.Zero };
        AddChild(_face);
    }

    /// <summary>Sets the leaf and redraws; the leaf draws nothing while it has no width.</summary>
    public void Place(Rect2 area, float spine, float edge, float lift)
    {
        if (area == Area && spine == Spine && edge == Edge && lift == Lift) return;
        Area = area; Spine = spine; Edge = edge; Lift = lift;
        PlaceFace();
        QueueRedraw();
    }

    private void PlaceFace()
    {
        float x0 = Math.Min(Spine, Edge), w = Math.Abs(Edge - Spine), h = Area.Size.Y;
        if (w < 0.5f || Lift <= 0.001f) { if (_face.Size != Vector2.Zero) _face.Size = Vector2.Zero; ShowsFront = false; return; }
        float grow = Grow * Lift;
        _face.Position = new Vector2(x0, Area.Position.Y - h * grow * 0.5f);
        _face.Size = new Vector2(w, h * (1f + grow));
        bool spineLeft = Edge > Spine;
        float side = spineLeft ? 1f : -1f;
        bool front = !Opaque && side == From;
        ShowsFront = front;
        // The back fades out over the last part of its fall (its free edge past 60 % of the page's width from the spine,
        // about the last quarter of a turn's time): the page that lands is already there when the leaf lies flat. A leaf
        // that rises from a page (the settings closing) fades in the same way, so it never appears from nowhere.
        float half0 = spineLeft ? Area.End.X - Spine : Spine - Area.Position.X;
        float reach = Math.Clamp(w / Math.Max(1f, half0), 0f, 1f);
        float k = Math.Clamp((reach - 0.6f) / 0.4f, 0f, 1f);
        BackAlpha = front || Opaque ? 1f : 1f - k * k * (3f - 2f * k);
        _mat.SetShaderParameter(PBackA, BackAlpha);
        // The page the front carries spans the spine to the area's outer edge on its side; it is squeezed into the leaf.
        float half = spineLeft ? Area.End.X - Spine : Spine - Area.Position.X;
        _mat.SetShaderParameter(PSpineLeft, spineLeft);
        _mat.SetShaderParameter(PSampled, front);
        _mat.SetShaderParameter(PSqueeze, Math.Clamp(half / w, 1f, 60f));
        _mat.SetShaderParameter(PGrow, grow);
        _mat.SetShaderParameter(PLift, Lift);
        _mat.SetShaderParameter(PLines, h / (24f * S));
        _mat.SetShaderParameter(PFaceW, w);
        _mat.SetShaderParameter(PS, S);
        _mat.SetShaderParameter(PCurl, (Opaque ? 150f : 56f) * S);
        _mat.SetShaderParameter(PShade, Opaque ? 0.14f : 0f);
    }

    public override void _Draw()
    {
        float w = Math.Abs(Edge - Spine);
        if (Lift <= 0.001f || w < 0.5f) return;
        float top = Area.Position.Y, h = Area.Size.Y;
        float dir = Edge >= Spine ? 1f : -1f;   // from the spine toward the edge
        // Its shadow on the page beyond the edge, softest at its far end.
        // The age-change leaf spans the whole screen: its shadow and curl scale with it, or they vanish at that size.
        float sw = (Opaque ? 110f : 44f) * S * MathF.Pow(Lift, 0.6f);
        if (sw < 1f) return;
        var ink = UiKit.Ink;
        for (int i = 0; i < Strips; i++)
        {
            float t = (i + 0.5f) / Strips;
            float sx = dir > 0 ? Edge + sw * i / Strips : Edge - sw * (i + 1) / Strips;
            DrawRect(new Rect2(sx, top, sw / Strips + 0.75f, h), new Color(ink, 0.3f * Lift * BackAlpha * (1f - t) * (1f - t)));
        }
    }

    private const string FaceShader = @"
shader_type canvas_item;
render_mode unshaded;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
uniform bool spine_left = true;
uniform bool sampled = false;
uniform float squeeze = 1.0;
uniform float grow = 0.0;
uniform float lift = 0.0;
uniform float lines = 0.0;
uniform float face_w = 1.0;
uniform float s = 1.0;
uniform vec4 paper : source_color;
uniform vec4 ink : source_color;
uniform vec4 light : source_color;
uniform float back_a = 1.0;
uniform float curl_w = 56.0;
uniform float shade = 0.0;

void fragment() {
    // Screen units per unit of UV (the face is an upright rectangle): where the face lies on the screen.
    vec2 per = vec2(dFdx(SCREEN_UV.x) / max(1e-6, abs(dFdx(UV.x))) * sign(dFdx(UV.x)),
                    dFdy(SCREEN_UV.y) / max(1e-6, abs(dFdy(UV.y))) * sign(dFdy(UV.y)));
    vec2 origin = SCREEN_UV - UV * per;
    float f = spine_left ? UV.x : 1.0 - UV.x;          // 0 at the spine, 1 at the free edge
    float tall = (1.0 + grow * f) / (1.0 + grow);      // the leaf's height here, share of the face's
    float dy = UV.y - 0.5;
    float v = 0.5 + dy / tall;                         // 0..1 down the page
    vec3 col;
    if (sampled) {
        float spine_x = origin.x + (spine_left ? 0.0 : per.x);
        float sx = spine_x + (spine_left ? 1.0 : -1.0) * f * per.x * squeeze;
        float sy = origin.y + per.y * (0.5 - 0.5 / (1.0 + grow)) + v * per.y / (1.0 + grow);
        col = textureLod(screen_tex, vec2(sx, sy), 0.0).rgb;
        // Turned from the light as it rises: a little darker toward the free edge.
        col = mix(col, col * 0.9, lift * f * 0.7);
    } else {
        col = paper.rgb;
        float lv = v * lines;
        float d = abs(fract(lv + 0.5) - 0.5) / max(1e-4, fwidth(lv));
        float edge_ok = step(0.6, lv) * step(lv, lines - 0.6);
        col = mix(col, ink.rgb, 0.08 * (1.0 - smoothstep(0.5, 1.2, d)) * edge_ok);
        // The back curves into the gutter at the spine.
        col = mix(col, ink.rgb, 0.1 * pow(max(0.0, 1.0 - f / 0.18), 2.0));
    }
    // The curl: the last part before the free edge turns away from the light.
    float px = (1.0 - f) * face_w;                     // pixels from the free edge
    float cw = min(face_w, curl_w);
    // A wide leaf turns from the light as a whole: darker toward its free edge (the age-change leaf).
    col = mix(col, ink.rgb, shade * lift * f * f);
    float c = max(0.0, 1.0 - px / max(1.0, cw));
    col = mix(col, ink.rgb, 0.26 * pow(lift, 0.6) * c * c);
    // The highlight along the curl, pale paper catching the light.
    float hl = 1.0 - smoothstep(0.6 * s, 2.0 * s, px);
    col = mix(col, light.rgb, 0.85 * min(1.0, lift * 2.0) * hl);
    float a = 1.0 - smoothstep(0.5 * tall - 1.5 * fwidth(UV.y), 0.5 * tall, abs(dy));
    COLOR = vec4(col, a * (sampled ? 1.0 : back_a));
}
";
}
