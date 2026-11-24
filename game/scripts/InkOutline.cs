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

    private static readonly StringName MetresPerPx = new("metres_per_px"), WidthPx = new("width_px");

    public void Attach(Camera3D cam)
    {
        Name = "Ink";
        string code = FileAccess.GetFileAsString("res://shaders/outline_screen.gdshader");
        string[] cmp = { "compare_not_equal", "compare_equal" };
        float[] crease = { 0.45f, 1.5f };
        for (int i = 0; i < 2; i++)
        {
            var sh = new Shader { Code = code.Replace("// STENCIL_MODE", $"stencil_mode read, {cmp[i]}, 1;") };
            var m = new ShaderMaterial { Shader = sh };
            m.SetShaderParameter("crease", crease[i]);
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
        _lastSize = cam.Size; _lastH = viewportPx.Y;
        float pxPerM = viewportPx.Y / cam.Size;
        float line = LinePx(pxPerM * 1080f / viewportPx.Y);
        foreach (var m in _mats)
        {
            m.SetShaderParameter(MetresPerPx, cam.Size / viewportPx.Y);
            m.SetShaderParameter(WidthPx, line * viewportPx.Y / 1080f);
        }
    }

    public bool InkVisible
    {
        get => _quads[0].Visible;
        set { foreach (var q in _quads) q.Visible = value; }
    }
}
