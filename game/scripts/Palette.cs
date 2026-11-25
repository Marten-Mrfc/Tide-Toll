using Godot;

namespace TideToll.Game;

/// <summary>
/// Living Map palette (Theme and Art tab, starting values). Colours are sRGB hex, as the doc gives them;
/// StandardMaterial3D treats albedo as sRGB, so they land on screen close to these values.
/// </summary>
public static class Palette
{
    public static readonly Color Paper = Color.FromHtml("#F2EAD8");
    public static readonly Color Ink = Color.FromHtml("#2B2620");
    public static readonly Color Meadow = Color.FromHtml("#7FA35B");
    public static readonly Color BentGrass = Color.FromHtml("#A9B86E");
    public static readonly Color TrailEarth = Color.FromHtml("#B89568");
    public static readonly Color PackedEarth = Color.FromHtml("#8C6A48");
    public static readonly Color Mud = Color.FromHtml("#5E4A36");
    public static readonly Color River = Color.FromHtml("#5B7F95");
    public static readonly Color DeepWater = Color.FromHtml("#3F5F75");
    public static readonly Color Thatch = Color.FromHtml("#C9A55A");
    public static readonly Color Brick = Color.FromHtml("#A4553A");
    public static readonly Color SootBrick = Color.FromHtml("#7A4436");
    public static readonly Color SlateRoof = Color.FromHtml("#5E6B78");
    public static readonly Color CastIronGreen = Color.FromHtml("#44604F");
    public static readonly Color Limewash = Color.FromHtml("#E9E2CF");

    /// <summary>Faded ink for printed text lines, so they read as text, not as walls.</summary>
    public static readonly Color FadedInk = Ink.Lerp(Paper, 0.55f);

    public static StandardMaterial3D Matte(Color c) => new()
    {
        AlbedoColor = c,
        Roughness = 1f,
        MetallicSpecular = 0.1f,
    };

    public static StandardMaterial3D Unlit(Color c, bool transparent = false) => new()
    {
        AlbedoColor = c,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = transparent ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
        VertexColorUseAsAlbedo = transparent,
    };
}
