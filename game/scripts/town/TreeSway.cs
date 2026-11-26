using Godot;

namespace TideToll.Game;

/// <summary>
/// Night 3 (style sheet section 7, "crowns that sway"): the trees' material with a small vertex sway, so the wood
/// moves a little at no CPU cost. Everything above 2 m leans with the wind, more the higher it is (about 6 cm at the
/// top of an oak), each tree on its own phase from its world position; trunks stay put. Same look as ModelLib's
/// material: Lambert, no specular, the palette atlas sampled nearest, and the soft-ink stencil mark (trees are
/// written with ink "soft"). One material for all five kinds, built once.
/// </summary>
public static class TreeSway
{
    private static ShaderMaterial? _mat;

    private const string Code = @"
shader_type spatial;
render_mode diffuse_lambert, specular_disabled;
stencil_mode write, compare_always, 1;
uniform sampler2D albedo_tex : source_color, filter_nearest;
void vertex() {
    vec3 at = MODEL_MATRIX[3].xyz;
    float h = max(VERTEX.y - 2.0, 0.0);
    float t = TIME * 1.1 + at.x * 0.23 + at.z * 0.17;
    VERTEX.x += sin(t) * h * 0.011;
    VERTEX.z += sin(t * 0.77 + 1.3) * h * 0.008;
}
void fragment() {
    ALBEDO = texture(albedo_tex, UV).rgb;
    ROUGHNESS = 1.0;
    METALLIC = 0.0;
    SPECULAR = 0.0;
}
";

    /// <summary>The swaying material, sampling the same palette atlas as the model's own material.</summary>
    public static Material For(Mesh mesh)
    {
        if (_mat != null) return _mat;
        var src = mesh.SurfaceGetMaterial(0) as BaseMaterial3D;
        _mat = new ShaderMaterial { Shader = new Shader { Code = Code } };
        _mat.SetShaderParameter("albedo_tex", src?.AlbedoTexture ?? GD.Load<Texture2D>("res://assets/textures/palette.png"));
        return _mat;
    }
}
