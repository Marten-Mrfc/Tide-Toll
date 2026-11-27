using System;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// n7 (villagers as people, part 2): pointing at a villager. A pick in code, in screen space, with no physics: each
/// drawn figure is the segment from its feet to its head as the camera sees it; the nearest within a generous radius
/// wins, ties go to the figure in front, and a hovered figure keeps the hover until the pointer is well away from it or
/// another is clearly nearer (no flicker in a crowd). The rings under the feet (hover, and the selected villager's) are
/// drawn here and fade in and out; drawing only, the simulation is untouched.
/// </summary>
public partial class TownVillagers
{
    /// <summary>The villager the pointer is on (-1 none) and the one whose card is open (-1 none); set by the interface.</summary>
    public int HoverId = -1, SelectedId = -1;
    /// <summary>Hit radius on screen at interface scale 1, pixels: at least this, or more for a large figure.</summary>
    public const float PickRadiusPx = 14f;
    /// <summary>Ties within this many pixels go to the figure in front; a held hover gives way only to one this much nearer.</summary>
    public const float PickTiePx = 2f, PickSwitchPx = 5f;
    /// <summary>A held hover is let go beyond this many radii.</summary>
    public const float PickHoldRadii = 1.6f;

    private MeshInstance3D _hoverRing = null!, _selRing = null!;
    private StandardMaterial3D _hoverMat = null!, _selMat = null!;
    private float _hoverA, _selA;
    private int _hoverShown = -1;
    /// <summary>Opacity of the hover ring and the selected ring now (tests: they fade, never switch in one frame).</summary>
    public float HoverRingAlpha => _hoverA;
    public float SelectedRingAlpha => _selA;

    /// <summary>Where villager <paramref name="i"/>'s feet are drawn, in map-local space (what the eye sees, after the bodies pass).</summary>
    public Vector3 FeetLocal(int i) => new((_x[i] + _ox[i]) * Cell, 0f, (_y[i] + _oy[i]) * Cell);
    /// <summary>The drawn figure's height in map-local metres.</summary>
    public float FigureHeight => ModelHeight * WalkerScale * 0.9f;

    /// <summary>
    /// The villager drawn under a screen point, or -1: the figure whose feet-to-head segment on screen is nearest within
    /// <paramref name="radius"/> pixels (ties to the one in front, lower on the screen). <paramref name="keep"/> (the
    /// villager hovered now) stays picked while the pointer is within <see cref="PickHoldRadii"/> radii of it, unless
    /// another is <see cref="PickSwitchPx"/> pixels nearer.
    /// </summary>
    public int Pick(CameraRig rig, Vector2 p, float radius, int keep = -1)
    {
        int n = Drawn, best = -1;
        float bestD = float.MaxValue, bestFeet = float.MinValue, keepD = float.MaxValue;
        var up = new Vector3(0, FigureHeight, 0);
        for (int i = 0; i < n; i++)
        {
            if (!_seen[i]) continue;
            var foot = FeetLocal(i);
            Vector2 a = rig.LocalToScreen(foot), b = rig.LocalToScreen(foot + up);
            float d = SegmentDistance(p, a, b);
            if (i == keep) keepD = d;
            if (d > radius * PickHoldRadii) continue;
            if (d < bestD - PickTiePx || (d <= bestD + PickTiePx && a.Y > bestFeet)) { bestD = d; best = i; bestFeet = a.Y; }
        }
        if (keep >= 0 && keep < n && keepD <= radius * PickHoldRadii && !(best != keep && bestD < keepD - PickSwitchPx)) return keep;
        return bestD <= radius ? best : -1;
    }

    /// <summary>Screen point of a villager's middle (tests point at it as a player would).</summary>
    public Vector2 ScreenOf(CameraRig rig, int i) => rig.LocalToScreen(FeetLocal(i) + new Vector3(0, FigureHeight * 0.45f, 0));

    public static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float l2 = ab.LengthSquared();
        float u = l2 > 1e-6f ? Math.Clamp((p - a).Dot(ab) / l2, 0f, 1f) : 0f;
        return p.DistanceTo(a + ab * u);
    }

    private void InitRings()
    {
        _hoverMat = RingMaterial(); _selMat = RingMaterial();
        _hoverRing = new MeshInstance3D { Name = "HoverRing", Mesh = RingMesh(0.48f, 0.60f), MaterialOverride = _hoverMat, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        _selRing = new MeshInstance3D { Name = "SelectedRing", Mesh = RingMesh(0.46f, 0.66f), MaterialOverride = _selMat, Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_hoverRing);
        AddChild(_selRing);
    }

    private static StandardMaterial3D RingMaterial()
    {
        var m = Palette.Unlit(new Color(UiKit.Wheat, 0f), transparent: true);
        m.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        m.NoDepthTest = true;   // a ring on the ground stays seen over the shadow disc and the road
        m.RenderPriority = 2;
        return m;
    }

    /// <summary>A flat ring (annulus) on the ground, radii in model units (scaled by WalkerScale when placed).</summary>
    private static ArrayMesh RingMesh(float r0, float r1)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        const int seg = 32;
        for (int i = 0; i < seg; i++)
        {
            float a0 = i * MathF.Tau / seg, a1 = (i + 1) * MathF.Tau / seg;
            var i0 = new Vector3(MathF.Cos(a0) * r0, 0, MathF.Sin(a0) * r0); var o0 = new Vector3(MathF.Cos(a0) * r1, 0, MathF.Sin(a0) * r1);
            var i1 = new Vector3(MathF.Cos(a1) * r0, 0, MathF.Sin(a1) * r0); var o1 = new Vector3(MathF.Cos(a1) * r1, 0, MathF.Sin(a1) * r1);
            st.AddVertex(i0); st.AddVertex(o0); st.AddVertex(o1);
            st.AddVertex(i0); st.AddVertex(o1); st.AddVertex(i1);
        }
        return st.Commit();
    }

    /// <summary>The rings: under the hovered villager at once (0.1 s fade), under the selected one while its card is open.</summary>
    private void RingFrame(float dt, int n)
    {
        int h = HoverId >= 0 && HoverId < n && HoverId != SelectedId ? HoverId : -1;
        if (h >= 0) _hoverShown = h;
        _hoverA = Math.Clamp(_hoverA + (h >= 0 ? dt : -dt) / 0.1f, 0f, 1f);
        if (_hoverShown >= n) _hoverA = 0f;
        Ring(_hoverRing, _hoverMat, _hoverShown, _hoverA, 0.8f);
        bool sel = SelectedId >= 0 && SelectedId < n;
        _selA = Math.Clamp(_selA + (sel ? dt : -dt) / 0.12f, 0f, 1f);
        if (sel) _selShown = SelectedId;
        if (_selShown >= n) _selA = 0f;
        Ring(_selRing, _selMat, _selShown, _selA, 1f);
    }
    private int _selShown = -1;

    private void Ring(MeshInstance3D ring, StandardMaterial3D mat, int i, float a, float strength)
    {
        bool show = a > 0.001f && i >= 0;
        if (ring.Visible != show) ring.Visible = show;
        if (!show) return;
        mat.AlbedoColor = new Color(UiKit.Wheat, a * strength);
        ring.Transform = new Transform3D(Basis.Identity.Scaled(Vector3.One * WalkerScale), FeetLocal(i) + new Vector3(0, 0.06f, 0));
    }
}
