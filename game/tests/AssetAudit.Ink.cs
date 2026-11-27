using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// Research n8 (ink lines and blur): `--ink-lab` pictures a few subjects at the play view (640 x 480 at the play
/// zoom's metres per pixel) with the ink pass swapped for a lab copy of outline_screen.gdshader that has switches:
/// a fixed sample step, a ramp instead of the hard step, a line in the colour under it multiplied down, an overall
/// strength, and a lighter weight for inner creases (second difference below lab_sil metres) than for silhouettes.
/// Also 3D anti-aliasing (FXAA, MSAA 4x, drawing at 2x and scaling down) and the cottage in the soft-ink group.
/// Each subject is also pictured with no ink and with nothing (the ground alone), for the measurements.
/// `--ink-windows` pictures the owner's cottage at the game's own follow zoom in windows of four sizes.
/// </summary>
public partial class AssetAudit
{
    private const string LabShader = @"
shader_type spatial;
render_mode unshaded, depth_test_disabled, cull_disabled, blend_mix, skip_vertex_transform;
//STENCIL
uniform sampler2D depth_tex : hint_depth_texture, filter_nearest, repeat_disable;
uniform sampler2D screen_tex : hint_screen_texture, filter_nearest, repeat_disable;
uniform vec4 ink : source_color = vec4(0.1683, 0.1441, 0.1236, 1.0);
uniform float width_px = 1.0;
uniform float metres_per_px = 0.05;
uniform float crease = 0.45;
uniform float ink_alpha = 1.0;
uniform float lab_w = 0.0;        // > 0: this sample step in pixels whatever the zoom
uniform float lab_ramp = 0.0;     // > 0: smoothstep from crease to crease * (1 + 3 * ramp) instead of the hard step
uniform float lab_mul_in = 0.0;   // inner creases: 1 = the colour under the line times lab_k, 0 = flat ink
uniform float lab_mul_sil = 0.0;  // silhouettes: the same
uniform float lab_k = 0.45;
uniform float lab_strength = 1.0; // alpha of every line
uniform float lab_inner = 1.0;    // alpha of inner creases (second difference below lab_sil metres)
uniform float lab_sil = 0.3;
uniform float lab_w_in = 0.0;     // > 0: inner creases found with this step (silhouettes keep lab_w)
void vertex() { POSITION = vec4(VERTEX.xy, 1.0, 1.0); }
float vz(ivec2 p, vec2 size, mat4 inv_proj) {
	p = clamp(p, ivec2(0), ivec2(size) - 1);
	float d = texelFetch(depth_tex, p, 0).r;
	vec4 v = inv_proj * vec4((vec2(p) + 0.5) / size * 2.0 - 1.0, d, 1.0);
	return -(v.z / v.w);
}
void fragment() {
	ivec2 p = ivec2(FRAGCOORD.xy);
	vec2 size = vec2(textureSize(depth_tex, 0));
	int w = lab_w > 0.0 ? int(lab_w) : max(int(width_px + 0.5), 1);
	mat4 ip = INV_PROJECTION_MATRIX;
	float zc = vz(p, size, ip);
	float mpp = metres_per_px * (size.y > 0.0 ? 1.0 : 1.0);
	float step_m = mpp * float(w);
	ivec2 hi = ivec2(size) - 1;
	bool okx = p.x - w >= 0 && p.x + w <= hi.x;
	bool oky = p.y - w >= 0 && p.y + w <= hi.y;
	float lx = okx ? abs(vz(p + ivec2(w, 0), size, ip) + vz(p - ivec2(w, 0), size, ip) - 2.0 * zc) : 0.0;
	float ly = oky ? abs(vz(p + ivec2(0, w), size, ip) + vz(p - ivec2(0, w), size, ip) - 2.0 * zc) : 0.0;
	float v = max(lx, ly);
	float c = crease * step_m;
	bool sil = v >= lab_sil;
	if (!sil && lab_w_in > 0.0) {
		int wi = int(lab_w_in);
		bool ix = p.x - wi >= 0 && p.x + wi <= hi.x, iy = p.y - wi >= 0 && p.y + wi <= hi.y;
		float ax = ix ? abs(vz(p + ivec2(wi, 0), size, ip) + vz(p - ivec2(wi, 0), size, ip) - 2.0 * zc) : 0.0;
		float ay = iy ? abs(vz(p + ivec2(0, wi), size, ip) + vz(p - ivec2(0, wi), size, ip) - 2.0 * zc) : 0.0;
		v = max(ax, ay);
		c = crease * metres_per_px * float(wi);
		sil = v >= lab_sil;
	}
	float e = lab_ramp > 0.0 ? smoothstep(c, c * (1.0 + 3.0 * lab_ramp), v) : step(c, v);
	vec3 sc = texelFetch(screen_tex, p, 0).rgb;
	float m = sil ? lab_mul_sil : lab_mul_in;
	ALBEDO = mix(ink.rgb, sc * lab_k, m);
	ALPHA = e * ink_alpha * lab_strength * (sil ? 1.0 : lab_inner);
}
";

    private sealed record LabVariant(string Name, Dictionary<string, float> P, string Aa = "", bool SoftSubject = false);

    private static LabVariant V(string name, params (string k, float v)[] p)
    {
        var d = new Dictionary<string, float>();
        foreach (var (k, v) in p) d[k] = v;
        return new LabVariant(name, d);
    }

    private static readonly string[] LabKeys = { "lab_w", "lab_ramp", "lab_mul_in", "lab_mul_sil", "lab_k", "lab_strength", "lab_inner", "lab_sil", "lab_w_in" };
    private static readonly float[] LabDefaults = { 0f, 0f, 0f, 0f, 0.45f, 1f, 1f, 0.3f, 0f };

    // The pictures are 640 x 480 at the 1080p play view's metres per pixel; the game at 1920 x 1080 samples with a
    // step of 2 px there (LinePx clamps to 1.8, rounded to 2), so every lab variant uses step 2 unless it says
    // otherwise. "now720" is the game's own shader in this window: step 1, as a 1280 x 720 window draws it.
    private List<LabVariant> Variants() => new()
    {
        V("now"),
        V("now720"),
        V("thin", ("lab_w", 1)),
        V("ramp", ("lab_ramp", 1)),
        V("multiply", ("lab_mul_in", 1), ("lab_mul_sil", 1)),
        V("strength60", ("lab_strength", 0.6f)),
        V("inner-light", ("lab_inner", 0.35f)),
        V("inner-colour", ("lab_mul_in", 1), ("lab_k", 0.55f)),
        V("thin-inner-colour", ("lab_w", 1), ("lab_mul_in", 1), ("lab_k", 0.55f)),
        V("rec", ("lab_w", 1), ("lab_mul_in", 1), ("lab_k", 0.6f), ("lab_ramp", 1)),
        V("rec2", ("lab_w", 2), ("lab_w_in", 1), ("lab_mul_in", 1), ("lab_k", 0.6f), ("lab_ramp", 1)),
        new LabVariant("soft-cottage", new(), SoftSubject: true),
        new LabVariant("fxaa", new(), Aa: "fxaa"),
        new LabVariant("msaa4", new(), Aa: "msaa4"),
        new LabVariant("ssaa2", new() { ["lab_w"] = 4 }, Aa: "ssaa2"),
        new LabVariant("ssaa2-thin", new() { ["lab_w"] = 2 }, Aa: "ssaa2"),
    };


    private ShaderMaterial[] _labMats = Array.Empty<ShaderMaterial>();
    private Shader?[] _nowShaders = new Shader?[2];

    private void LabInstall()
    {
        var ink = Main.Ink;
        string[] cmp = { "compare_not_equal", "compare_equal" };
        _labMats = new ShaderMaterial[2];
        string[] names = { "InkHard", "InkSoft" };
        for (int i = 0; i < 2; i++)
        {
            var q = ink.GetNode<MeshInstance3D>(names[i]);
            var m = (ShaderMaterial)q.MaterialOverride;
            _nowShaders[i] = m.Shader;
            _labMats[i] = m;
        }
        _labShaders = new Shader[2];
        for (int i = 0; i < 2; i++)
            _labShaders[i] = new Shader { Code = LabShader.Replace("//STENCIL", $"stencil_mode read, {cmp[i]}, 1;") };
    }
    private Shader[] _labShaders = Array.Empty<Shader>();

    private void LabApply(LabVariant v)
    {
        bool lab = v.Name != "now720" && v.Name != "noink" && v.Name != "nothing";
        for (int i = 0; i < 2; i++)
        {
            var m = _labMats[i];
            m.Shader = lab ? _labShaders[i] : _nowShaders[i];
            if (lab)
                for (int k = 0; k < LabKeys.Length; k++)
                    m.SetShaderParameter(LabKeys[k], v.P.TryGetValue(LabKeys[k], out var x) ? x : k == 0 ? 2f : LabDefaults[k]);
        }
        Main.Ink.InkVisible = v.Name != "noink" && v.Name != "nothing";
        var vp = GetViewport();
        vp.ScreenSpaceAA = v.Aa == "fxaa" ? Viewport.ScreenSpaceAAEnum.Fxaa : Viewport.ScreenSpaceAAEnum.Disabled;
        vp.Msaa3D = v.Aa == "msaa4" ? Viewport.Msaa.Msaa4X : Viewport.Msaa.Disabled;
        vp.Scaling3DScale = v.Aa == "ssaa2" ? 2f : 1f;
    }

    private static void MarkSoft(Node n)
    {
        if (n is MeshInstance3D mi && mi.Mesh != null)
            for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++)
            {
                var src = mi.GetActiveMaterial(s) as BaseMaterial3D;
                if (src == null) continue;
                var c = (BaseMaterial3D)src.Duplicate();
                c.StencilMode = BaseMaterial3D.StencilModeEnum.Custom;
                c.StencilFlags = 2;
                c.StencilCompare = BaseMaterial3D.StencilCompareEnum.Always;
                c.StencilReference = 1;
                mi.SetSurfaceOverrideMaterial(s, c);
            }
        foreach (var ch in n.GetChildren()) MarkSoft(ch);
    }

    private sealed record LabSubject(string Name, Func<Node3D> Build, float Zoom);

    private async Task InkLab(float playSize, float walker)
    {
        LabInstall();
        var subjects = new List<LabSubject>();
        for (int k = 0; k < 3; k++)
        {
            int kk = k;
            subjects.Add(new($"cottage{k}-ext2", () => Wrap(TownBuildings.CottageNode(kk, 2, 6f, 6f)), 1f));
            subjects.Add(new($"cottage{k}-ext0", () => Wrap(TownBuildings.CottageNode(kk, 0, 6f, 6f)), 1f));
        }
        subjects.Add(new("cottage0-ext2-in", () => Wrap(TownBuildings.CottageNode(0, 2, 6f, 6f)), 0.5f));
        subjects.Add(new("cottage0-ext2-out", () => Wrap(TownBuildings.CottageNode(0, 2, 6f, 6f)), 2f));
        subjects.Add(new("mill", () => Model("mill", 1f), 1f));
        subjects.Add(new("market-stall", () => Model("market-stall", 1f), 1f));
        subjects.Add(new("field-ripe", () => Model("field-ripe", 1f), 1f));
        subjects.Add(new("villager", () => Carrier(Good.Grain, 0, walker), 1f));
        subjects.Add(new("tree-oak", () => Model("tree-oak", 1f), 1f));
        subjects.Add(new("street", () => Street(walker), 1.2f));
        var variants = Variants();
        bool only = _only != null;
        foreach (var s in subjects)
        {
            if (only && !_only!.Contains(s.Name)) continue;
            var at = new Vector3((_spot.X + 0.5f) * TownGround.Cell, 0, (_spot.Y + 0.5f) * TownGround.Cell);
            var subject = s.Build();
            subject.Position = at;
            Main.MapOrigin.AddChild(subject);
            var box = Bounds(subject);
            var c = box.GetCenter();
            float size = playSize * s.Zoom;
            Main.Rig.Yaw.Rotation = Vector3.Zero;
            Main.Rig.Place(Centre(at + new Vector3(c.X, 0, c.Z), c.Y, size, 0f), size);
            bool full = s.Name == "cottage0-ext2" || s.Name == "street";
            var list = new List<LabVariant>();
            list.Add(new LabVariant("noink", new()));
            list.Add(new LabVariant("nothing", new()));
            foreach (var v in variants)
                if (_inkNames != null) { if (_inkNames.Contains(v.Name)) list.Add(v); }
                else if (full || v.Name == "now" || v.Name == "rec" || v.Name == "rec2" || v.Name == "thin" || v.Name == "inner-colour") list.Add(v);
            foreach (var v in list)
            {
                subject.Visible = v.Name != "nothing";
                if (v.SoftSubject) MarkSoft(subject);
                LabApply(v);
                await Frames(3);
                await Snap($"ink-{s.Name}-{v.Name}");
                if (v.SoftSubject) { subject.QueueFree(); subject = s.Build(); subject.Position = at; Main.MapOrigin.AddChild(subject); }
            }
            LabApply(new LabVariant("now720", new()));
            subject.QueueFree();
            await Frames(1);
        }
    }

    /// <summary>The town the bot plays to the charter, at the play view (1080p metres per pixel, in a 1280 x 720
    /// window) and the whole page in view, for each variant named (all lab variants when none).</summary>
    private async Task InkTown(float play1080, HashSet<string>? names)
    {
        LabInstall();
        if (Main.MapOrigin.GetNodeOrNull<Node3D>("Trees") is { } trees) trees.Visible = true;
        if (Main.MapOrigin.GetNodeOrNull<Node3D>("Life") is { } life) life.Visible = true;
        var bot = new TownBot(BotKind.Payback, 1);
        Main.Driver.RunBot(bot, t => t.HasCharter, 20 * 60 * 60 * 12);
        Main.Driver.SetPaused(true);
        GetWindow().Size = new Vector2I(1280, 720);
        await Frames(10);
        Main.Rig.UserMoved = false;
        Main.Follow(instant: true);
        await Frames(10);
        Main.Rig.UserMoved = true;
        float play = play1080 * 720f / 1080f;
        var target = Main.Rig.TargetPivot;
        var views = new (string name, float size)[] { ("town", play), ("page", Main.Rig.PlaySize) };
        foreach (var (vn, size) in views)
        {
            Main.Rig.Place(target, size);
            await Frames(6);
            var list = new List<LabVariant> { new("noink", new()) };
            foreach (var v in Variants()) if (names == null || names.Contains(v.Name)) list.Add(v);
            foreach (var v in list)
            {
                LabApply(v);
                Main.Rig.UserMoved = true;
                await Frames(4);
                var img = GetViewport().GetTexture().GetImage();
                img.SavePng(System.IO.Path.Combine(_dir, $"ink-{vn}-{v.Name}.png"));
            }
            LabApply(new LabVariant("now720", new()));
        }
        if (OS.GetCmdlineUserArgs().Contains("--ink-cost"))
        {
            // Frame cost at 1920 x 1080, the play view: wall time of 40 frames each, twice, after 10 frames to settle
            // (software rendering: only the differences between variants mean anything, not the milliseconds).
            GetWindow().Size = new Vector2I(1920, 1080);
            await Frames(10);
            Main.Rig.Place(target, play1080);
            var vpr = GetViewport().GetViewportRid();
            RenderingServer.ViewportSetMeasureRenderTime(vpr, true);
            var all = new List<LabVariant> { new("noink", new()), new("now720", new()) };
            foreach (var v in Variants()) if (names == null || names.Contains(v.Name)) all.Add(v);
            all.Add(new LabVariant("fxaa", new(), Aa: "fxaa"));
            all.Add(new LabVariant("msaa4", new(), Aa: "msaa4"));
            all.Add(new LabVariant("ssaa2", new() { ["lab_w"] = 4 }, Aa: "ssaa2"));
            for (int round = 0; round < 2; round++)
                foreach (var v in all)
                {
                    LabApply(v);
                    Main.Rig.UserMoved = true;
                    await Frames(10);
                    ulong t0 = Time.GetTicksUsec();
                    double gpu = 0;
                    for (int f = 0; f < 40; f++) { await Frames(1); gpu += RenderingServer.ViewportGetMeasuredRenderTimeGpu(vpr); }
                    double ms = (Time.GetTicksUsec() - t0) / 1000.0 / 40.0;
                    GD.Print($"INKCOST round={round} variant={v.Name} wall_ms={ms:0.0} gpu_ms={gpu / 40.0:0.00}");
                }
            LabApply(new LabVariant("now720", new()));
        }
        Notes.Add($"town: play {play:0.0} page {Main.Rig.PlaySize:0.0}");
    }

    private static Node3D Wrap(Node3D inner)
    {
        var n = new Node3D { Name = "Subject" };
        n.AddChild(inner);
        return n;
    }

    /// <summary>The owner's cottage at the game's own follow zoom for each window size, full window pictures.</summary>
    private async Task InkWindows()
    {
        LabInstall();
        var at = new Vector3((_spot.X + 0.5f) * TownGround.Cell, 0, (_spot.Y + 0.5f) * TownGround.Cell);
        var subject = Wrap(TownBuildings.CottageNode(0, 2, 6f, 6f));
        subject.Position = at;
        Main.MapOrigin.AddChild(subject);
        var c = Bounds(subject).GetCenter();
        foreach (var sz in new[] { new Vector2I(1920, 1080), new Vector2I(1280, 720), new Vector2I(1600, 900), new Vector2I(2560, 1440) })
        {
            GetWindow().Size = sz;
            await Frames(6);
            Main.Rig.UserMoved = false;
            Main.Follow(instant: true);
            await Frames(3);
            float follow = Main.Rig.Camera.Size;
            Main.Rig.UserMoved = true;
            Main.Rig.Yaw.Rotation = Vector3.Zero;
            Main.Rig.Place(Centre(at + new Vector3(c.X, 0, c.Z), c.Y, follow, 0f), follow);
            await Frames(4);
            var img = GetViewport().GetTexture().GetImage();
            Notes.Add($"window {sz.X}x{sz.Y}: follow size {follow:0.0} m, {sz.Y / follow:0.00} px/m, image {img.GetSize()}");
            img.SavePng(System.IO.Path.Combine(_dir, $"win-{sz.X}x{sz.Y}.png"));
            // And the same metres per pixel as 1080p play in every window: the 1080p picture's px/m.
        }
        subject.QueueFree();
        GetWindow().Size = new Vector2I(640, 480);
        await Frames(4);
    }
}
