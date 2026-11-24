extends SceneTree
# Rasterises SVG icons with Godot's own SVG renderer (ThorVG), as the game will draw them. Used by
# assets/src/common/svg.py; run headless:
#   godot --headless --path tools/contact-sheet -s res://svg_to_png.gd -- --list=/abs/jobs.txt [--bg=#F2EAD8]
# jobs.txt: one job per line, TAB-separated: /abs/icon.svg  /abs/out.png  SIZE_PX

func _init() -> void:
	var a := {}
	for s in OS.get_cmdline_user_args():
		var kv := s.trim_prefix("--").split("=", true, 1)
		a[kv[0]] = kv[1] if kv.size() > 1 else "1"
	var lines := FileAccess.get_file_as_string(a.get("list", "")).split("\n", false)
	var bad := 0
	for line in lines:
		var p := line.split("\t")
		if p.size() < 3:
			continue
		var text := FileAccess.get_file_as_string(p[0])
		var size := int(p[2])
		var probe := Image.new()
		if text == "" or probe.load_svg_from_string(text, 1.0) != OK:
			printerr("cannot read SVG ", p[0])
			bad += 1
			continue
		var img := Image.new()
		img.load_svg_from_string(text, float(size) / float(max(probe.get_width(), 1)))
		img.convert(Image.FORMAT_RGBA8)
		if a.has("bg"):
			var out := Image.create(img.get_width(), img.get_height(), false, Image.FORMAT_RGBA8)
			out.fill(Color(a["bg"]))
			out.blend_rect(img, Rect2i(Vector2i.ZERO, img.get_size()), Vector2i.ZERO)
			img = out
		if img.save_png(p[1]) != OK:
			printerr("cannot write ", p[1])
			bad += 1
	print("SVG OK" if bad == 0 else "SVG FAIL %d" % bad)
	quit(1 if bad else 0)
