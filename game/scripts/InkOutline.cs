using Godot;

namespace TideToll.Game;

/// <summary>
/// The Living Map ink line for the whole scene (rendering proof 4): one screen-space edge pass on the depth
/// buffer (shaders/outline_screen.gdshader, the contact sheet's pass, unchanged), drawn twice as camera-child
/// quads: once for ordinary pixels (crease 0.45) and once for pixels marked "soft ink" in the stencil buffer by
/// the tree, field and reed materials (crease 1.5), so many-faceted crowns do not clog with ink.
/// The line width follows the zoom: about 1 px when the whole page is in view, 1.5 to 2 px at play zoom.
/// Needs a tight near/far range on the camera (CameraRig sets 200..400 m).
/// </summary>
public partial class InkOutline : Node3D
{
    private readonly ShaderMaterial[] _mats = new ShaderMaterial[2];
    private readonly MeshInstance3D[] _quads = new MeshInstance3D[2];
    private float _lastSize = -1, _lastH = -1;

    private static readonly StringName MetresPerPx = new("metres_per_px"), WidthPx = new("width_px"), InkAlpha = new("ink_alpha");

    /// <summary>Strength of the soft ink for a number of screen pixels per metre (at 1080p).</summary>
    public static float SoftInk(float pxPerMetre) => Mathf.Clamp(Mathf.Remap(pxPerMetre, 3.5f, 9f, 0.4f, 1f), 0.4f, 1f);

    public void Attach(Camera3D cam)
    {
        Name = "Ink";
        // Loaded as a resource (not read as a file), so it also works from an exported .pck.
        string code = GD.Load<Shader>("res://shaders/outline_screen.gdshader").Code;
        string[] cmp = { "compare_not_equal", "compare_equal" };
        float[] crease = { 0.45f, 1.5f };
        for (int i = 0; i < 2; i++)
        {
            var sh = new Shader { Code = code.Replace("\n// STENCIL_MODE\n", $"\nstencil_mode read, {cmp[i]}, 1;\n") };
            var m = new ShaderMaterial { Shader = sh };
            m.SetShaderParameter("crease", crease[i]);
            // n8: the soft-ink group (trees, crops, reeds, villagers, small props) keeps the one-pixel outline at every
            // window size: small things a few dozen pixels across were half outline with the growing weight.
            m.SetShaderParameter("max_step", i == 1 ? 1f : 8f);
            m.SetShaderParameter("ink", Palette.Ink);
            _mats[i] = m;
            var q = new MeshInstance3D
            {
                Name = i == 0 ? "InkHard" : "InkSoft",
                Mesh = new QuadMesh { Size = new Vector2(2, 2) },
                MaterialOverride = m,
                ExtraCullMargin = 16384f,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Position = new Vector3(0, 0, -1),
            };
            _quads[i] = q;
            AddChild(q);
        }
        cam.AddChild(this);
    }

    /// <summary>Line width in screen pixels (at 1080p) for a given number of screen pixels per metre.</summary>
    public static float LinePx(float pxPerMetre) => Mathf.Clamp(Mathf.Remap(pxPerMetre, 4f, 12f, 1.0f, 1.6f), 1.0f, 1.8f);

    /// <summary>Call every frame; only touches the materials when the zoom or window height changed.</summary>
    public void Update(Camera3D cam, Vector2 viewportPx)
    {
        if (cam.Size == _lastSize && viewportPx.Y == _lastH) return;
        if (viewportPx.Y < 1f || cam.Size <= 0f) return; // zero-height window: keep the last widths, never infinity
        _lastSize = cam.Size; _lastH = viewportPx.Y;
        float pxPerM = viewportPx.Y / cam.Size;
        float line = LinePx(pxPerM * 1080f / viewportPx.Y);
        foreach (var m in _mats)
        {
            m.SetShaderParameter(MetresPerPx, cam.Size / viewportPx.Y);
            m.SetShaderParameter(WidthPx, line * viewportPx.Y / 1080f);
        }
        // Soft ink (trees, reeds, crops) thins as the view zooms out, so a wooded edge reads as soft clumps, not a
        // scatter of heavy ink rings: full strength from play zoom in, about 40% with the whole page in view.
        _mats[1].SetShaderParameter(InkAlpha, SoftInk(pxPerM * 1080f / viewportPx.Y));
    }

    public bool InkVisible
    {
        get => _quads[0].Visible;
        set { foreach (var q in _quads) q.Visible = value; }
    }
}
