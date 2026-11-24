extends Node
# Contact sheet: renders a .glb from straight above plus tilted views on a Paper ground at a fixed scale, with the
# screen-space ink outline. Run it through tools/render-contact-sheet.sh, which wraps:
#
#   xvfb-run -a -s '-screen 0 1280x720x24' godot --path tools/contact-sheet --rendering-method mobile \
#     --rendering-driver vulkan --resolution 960x480 res://contact_sheet.tscn --quit-after 900 -- \
#     --model=/abs/model.glb --out=/abs/sheet.png --shader=/abs/outline_screen.gdshader [options]
#
# Panels (row 1): TOP (straight down, north up), TILT 45 deg from the south (+Z, the game camera), from the east (+X),
# from the north (-Z). --sides adds row 2: low side views (12 deg) at yaw 0, 90, 180, 270 (south, east, north, west).
# Options: --view=14 (metres across a panel, ortho, same scale everywhere), --panel=480 (px), --focus_y=1.5,
# --unlit (flat colour, for palette checks), --outline=screen|none, --line=1.5 (px), --crease=0.45,
# --noshadow, --tile (also save each panel as OUT-<panel>.png), --sun=0.9, --amb=0.45,
# --crease_soft=1.5 (crease threshold on nodes whose glTF extras say {"ink": "soft"}: trees, fields, reeds).
# Soft-ink nodes write 1 into the stencil buffer; the outline is drawn as two full-screen quads, one testing
# stencil != 1 with --crease and one testing stencil == 1 with --crease_soft (see outline_screen.gdshader).
const PAPER := Color("#F2EAD8")
var a := {}

func _ready() -> void:
	for s in OS.get_cmdline_user_args():
		var kv := s.trim_prefix("--").split("=", true, 1)
		a[kv[0]] = kv[1] if kv.size() > 1 else "1"
	call_deferred("_run")

func _fail(msg: String) -> void:
	push_error(msg)
	print("CONTACT SHEET FAIL ", msg)
	get_tree().quit(2)

func _load_model(path: String) -> Node3D:
	if path.begins_with("res://"):
		return (load(path) as PackedScene).instantiate()
	var doc := GLTFDocument.new()
	var st := GLTFState.new()
	if doc.append_from_file(path, st) != OK:
		return null
	return doc.generate_scene(st)

# Lambert, no specular: with ortho cameras on Mobile the default specular swings wildly between views.
func _ink_soft(n: Node) -> bool:
	var e = n.get_meta("extras", null)
	if e is Dictionary and String((e as Dictionary).get("ink", "")) == "soft":
		return true
	return n.get_parent() != null and n.get_parent() is Node3D and _ink_soft(n.get_parent())

func _flatten(n: Node, unlit_all: bool, stats: Dictionary) -> void:
	for c in n.get_children():
		_flatten(c, unlit_all, stats)
	if n is MeshInstance3D:
		var soft := _ink_soft(n)
		if soft:
			stats["soft"].append(String(n.name))
		var mi := n as MeshInstance3D
		stats["tris"] += mi.mesh.get_faces().size() / 3
		var box: AABB = mi.global_transform * mi.mesh.get_aabb()
		stats["aabb"] = box if stats["aabb"] == null else (stats["aabb"] as AABB).merge(box)
		stats["nodes"].append(String(mi.name))
		for i in mi.mesh.get_surface_count():
			var m := mi.mesh.surface_get_material(i)
			if m is StandardMaterial3D:
				var sm := (m as StandardMaterial3D).duplicate() as StandardMaterial3D
				sm.metallic_specular = 0.0
				sm.roughness = 1.0
				sm.diffuse_mode = BaseMaterial3D.DIFFUSE_LAMBERT
				if unlit_all:
					sm.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
				if sm.albedo_texture:
					sm.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST
				if soft:   # mark soft-ink pixels in the stencil buffer
					sm.stencil_mode = BaseMaterial3D.STENCIL_MODE_CUSTOM
					sm.stencil_flags = BaseMaterial3D.STENCIL_FLAG_WRITE
					sm.stencil_compare = BaseMaterial3D.STENCIL_COMPARE_ALWAYS
					sm.stencil_reference = 1
				mi.set_surface_override_material(i, sm)

func _run() -> void:
	if not a.has("model") or not a.has("out"):
		_fail("need --model=PATH and --out=PATH")
		return
	var view := float(a.get("view", "14"))
	var panel := int(a.get("panel", "480"))
	var unlit := a.has("unlit")
	var outline: String = a.get("outline", "screen")
	var vp := SubViewport.new()
	vp.size = Vector2i(panel, panel)
	vp.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	add_child(vp)
	var env := Environment.new()
	env.background_mode = Environment.BG_COLOR
	env.background_color = PAPER
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color = PAPER
	env.ambient_light_energy = 0.0 if unlit else float(a.get("amb", "0.45"))
	var we := WorldEnvironment.new()
	we.environment = env
	vp.add_child(we)
	var sun := DirectionalLight3D.new()   # one sun, from the south-east (+X, +Z), high
	vp.add_child(sun)
	sun.look_at_from_position(Vector3(1, 1.3, 1) * 20, Vector3.ZERO)
	sun.light_energy = 0.0 if unlit else float(a.get("sun", "0.9"))
	sun.shadow_enabled = not unlit and not a.has("noshadow")
	sun.directional_shadow_max_distance = 120.0
	sun.directional_shadow_mode = DirectionalLight3D.SHADOW_ORTHOGONAL
	sun.shadow_bias = 0.05
	var ground := MeshInstance3D.new()
	var pm := PlaneMesh.new()
	pm.size = Vector2(600, 600)   # big enough that no panel sees past it: a far-clip edge would be inked as a line
	ground.mesh = pm
	var gm := StandardMaterial3D.new()
	gm.albedo_color = PAPER
	gm.roughness = 1.0
	gm.metallic_specular = 0.0
	gm.diffuse_mode = BaseMaterial3D.DIFFUSE_LAMBERT
	if unlit:
		gm.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	ground.material_override = gm
	ground.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	vp.add_child(ground)
	var model := _load_model(a["model"])
	if model == null:
		_fail("cannot load " + String(a["model"]))
		return
	vp.add_child(model)
	var stats := {"tris": 0, "aabb": null, "nodes": [], "soft": []}
	_flatten(model, unlit, stats)
	var bb: AABB = stats["aabb"] if stats["aabb"] != null else AABB()
	print("MODEL tris=%d nodes=%s soft_ink=%s size=%.2fx%.2fx%.2f min=(%.2f,%.2f,%.2f) max=(%.2f,%.2f,%.2f)" % [
		stats["tris"], ",".join(stats["nodes"]), ",".join(stats["soft"]), bb.size.x, bb.size.y, bb.size.z,
		bb.position.x, bb.position.y, bb.position.z, bb.end.x, bb.end.y, bb.end.z])
	var cam := Camera3D.new()
	cam.projection = Camera3D.PROJECTION_ORTHOGONAL
	cam.size = view
	cam.near = float(a.get("near", "8"))   # low side views of wide models (fields, --view=22) reach the ground nearer than 30 m
	cam.far = float(a.get("far", "300"))
	vp.add_child(cam)
	if outline == "screen":
		var code := ""
		if a.has("shader"):
			code = FileAccess.get_file_as_string(a["shader"])
			if code == "":
				_fail("cannot read shader " + String(a["shader"]))
				return
		else:
			_fail("--outline=screen needs --shader=/abs/path/outline_screen.gdshader")
			return
		# Two passes of the same shader: ordinary pixels (stencil != 1) with --crease, soft-ink pixels with --crease_soft.
		for ps in [["compare_not_equal", float(a.get("crease", "0.45"))], ["compare_equal", float(a.get("crease_soft", "1.5"))]]:
			var sh := Shader.new()
			sh.code = code.replace("// STENCIL_MODE", "stencil_mode read, %s, 1;" % ps[0])
			var q := MeshInstance3D.new()
			var qm := QuadMesh.new()
			qm.size = Vector2(2, 2)
			q.mesh = qm
			var smat := ShaderMaterial.new()
			smat.shader = sh
			smat.set_shader_parameter("width_px", float(a.get("line", "1.5")))
			smat.set_shader_parameter("metres_per_px", view / panel)
			smat.set_shader_parameter("crease", ps[1])
			q.material_override = smat
			q.extra_cull_margin = 16384.0
			cam.add_child(q)
			q.position = Vector3(0, 0, -1)
	var fy := float(a.get("focus_y", "1.5"))
	var dist := float(a.get("dist", "60"))
	var views := [["top", 90.0, 0.0], ["tilt45-south", 45.0, 0.0], ["tilt45-east", 45.0, 90.0], ["tilt45-north", 45.0, 180.0]]
	if a.has("sides"):
		for y in [0.0, 90.0, 180.0, 270.0]:
			views.append(["side-yaw%d" % int(y), 12.0, y])
	var cols := 4
	var rows := (views.size() + cols - 1) / cols
	var sheet := Image.create(panel * cols, panel * rows, false, Image.FORMAT_RGB8)
	sheet.fill(PAPER)
	var k := 0
	for v in views:
		var elev := deg_to_rad(v[1])
		var yaw := deg_to_rad(v[2])
		var target := Vector3(0, fy, 0)
		# yaw 0 = camera on the +Z (south) side looking north, as the game camera does; yaw 90 = from the east (+X)
		var dir := Vector3(sin(yaw) * cos(elev), sin(elev), cos(yaw) * cos(elev))
		cam.position = target + dir * dist
		cam.look_at(target, Vector3.UP if v[1] < 89.0 else Vector3(0, 0, -1))
		await get_tree().process_frame
		await get_tree().process_frame
		await RenderingServer.frame_post_draw
		var img := vp.get_texture().get_image()
		img.convert(Image.FORMAT_RGB8)
		if a.has("tile"):
			img.save_png(String(a["out"]).get_basename() + "-%s.png" % v[0])
		sheet.blit_rect(img, Rect2i(0, 0, panel, panel), Vector2i((k % cols) * panel, (k / cols) * panel))
		k += 1
	if sheet.save_png(a["out"]) != OK:
		_fail("cannot write " + String(a["out"]))
		return
	print("CONTACT SHEET OK ", a["out"], " renderer=", RenderingServer.get_current_rendering_method())
	get_tree().quit(0)
