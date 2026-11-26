using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Godot;

namespace TideToll.Game.Tests;

/// <summary>
/// The interface audit's record of one state: every visible 2D control that draws something (labels, buttons,
/// panels, texture rects, colour rects, bars) anywhere in the tree, and every visible Label3D (plot and boat labels,
/// the book's print), with a stable name (node path, Godot's automatic "@Type@123" names replaced by Type#n among
/// siblings), its rectangle in window pixels, text (60 characters), opacity, font height in pixels, the text's own
/// size and the card or group it belongs to (nearest Panel/PanelContainer ancestor, else the first node under its
/// CanvasLayer).
/// </summary>
public static class UiAuditDump
{
    public static JsonObject Collect(Main main, Viewport vp, string id, int scale, int age, int level)
    {
        var items = new JsonArray();
        var final = vp.GetFinalTransform();
        Walk(vp, items, final);
        var cam = vp.GetCamera3D();
        if (cam != null) Walk3D(vp, items, cam, final);
        var win = vp.GetWindow();
        return new JsonObject
        {
            ["id"] = id,
            ["window"] = new JsonArray(win.Size.X, win.Size.Y),
            ["scale"] = scale,
            ["content_scale"] = win.ContentScaleFactor,
            ["age"] = age,
            ["level"] = level,
            ["stage"] = main.Stage.ToString(),
            ["items"] = items,
        };
    }

    private static bool Draws(Control c)
    {
        switch (c)
        {
            case Label l: return l.Text.Length > 0;
            case RichTextLabel r: return r.GetParsedText().Length > 0;
            case Button: return true;
            case TextureRect t: return t.Texture != null;
            case ColorRect cr: return cr.Color.A > 0.01f;
            case NinePatchRect n: return n.Texture != null;
            case Godot.Range: return c is ProgressBar || c is TextureProgressBar;
            case PanelContainer pc: return pc.HasThemeStylebox("panel") && pc.GetThemeStylebox("panel") is not StyleBoxEmpty;
            case Panel p: return p.HasThemeStylebox("panel") && p.GetThemeStylebox("panel") is not StyleBoxEmpty;
        }
        return false;
    }

    private static void Walk(Node n, JsonArray items, Transform2D final)
    {
        if (n is CanvasLayer layer && !layer.Visible) return;
        if (n is CanvasItem ci && !ci.Visible) return;
        if (n is Control c && Draws(c)) Add(c, items, final);
        foreach (var ch in n.GetChildren()) Walk(ch, items, final);
    }

    public static string Seg(Node n)
    {
        string s = n.Name;
        if (!s.StartsWith('@')) return s;
        var p = n.GetParent();
        string type = n.GetClass();
        int k = 0;
        if (p != null)
            foreach (var sib in p.GetChildren())
            {
                if (sib == n) break;
                if (sib.GetClass() == type && sib.Name.ToString().StartsWith('@')) k++;
            }
        return type + "#" + k;
    }

    public static string StableName(Node n)
    {
        var parts = new List<string>();
        for (var x = n; x != null && x is not Window; x = x.GetParent()) parts.Add(Seg(x));
        parts.Reverse();
        // Drop the test scene's own root and Main (the same in every run).
        int skip = 0;
        while (skip < parts.Count - 1 && (parts[skip] == "UiAudit" || parts[skip] == "Main")) skip++;
        return string.Join("/", parts.GetRange(skip, parts.Count - skip));
    }

    /// <summary>The nearest Panel/PanelContainer (a card); else the first ancestor under the CanvasLayer that is
    /// smaller than the window (full-window roots are only holders).</summary>
    private static string Group(Control n)
    {
        if (n is PanelContainer || n is Panel) return StableName(n);
        var chain = new List<Node>();
        for (var x = n.GetParent(); x != null && x is not Window && x is not CanvasLayer; x = x.GetParent())
        {
            if (x is PanelContainer || x is Panel) return StableName(x);
            chain.Add(x);
        }
        var win = n.GetViewportRect().Size;
        for (int i = chain.Count - 1; i >= 0; i--)
            if (chain[i] is not Control c || c.Size.X < win.X - 1 || c.Size.Y < win.Y - 1) return StableName(chain[i]);
        return StableName(n);
    }

    private static float Opacity(CanvasItem c)
    {
        float a = c.SelfModulate.A;
        for (Node? x = c; x != null; x = x.GetParent())
        {
            if (x is CanvasItem ci) a *= ci.Modulate.A;
            if (x is CanvasLayer) break;
        }
        return a;
    }

    private static void Add(Control c, JsonArray items, Transform2D final)
    {
        var xf = final * c.GetGlobalTransformWithCanvas();
        var r = xf * new Rect2(Vector2.Zero, c.Size);
        float sy = xf.Y.Length();
        var o = new JsonObject
        {
            ["name"] = StableName(c),
            ["type"] = c.GetClass(),
            ["group"] = Group(c),
            ["rect"] = R(r),
            ["opacity"] = Math.Round(Opacity(c), 3),
        };
        string text = c switch { Label l => l.Text, Button b => b.Text, RichTextLabel rt => rt.GetParsedText(), _ => "" };
        if (text.Length > 0)
        {
            o["text"] = text.Length > 60 ? text[..60] : text;
            var font = c.GetThemeFont("font");
            int fs = c.GetThemeFontSize("font_size");
            if (c is RichTextLabel) { font = c.GetThemeFont("normal_font"); fs = c.GetThemeFontSize("normal_font_size"); }
            if (font != null && fs > 0)
            {
                o["font_px"] = Math.Round(fs * sy, 1);
                Vector2 ts;
                if (c is Label l2 && l2.AutowrapMode != TextServer.AutowrapMode.Off)
                    ts = new Vector2(c.Size.X, l2.GetLineCount() * l2.GetLineHeight());
                else if (c is RichTextLabel rt2)
                    ts = new Vector2(c.Size.X, rt2.GetContentHeight());
                else
                    ts = font.GetMultilineStringSize(text, HorizontalAlignment.Left, -1, fs);
                o["text_size"] = new JsonArray(Math.Round(ts.X * sy, 1), Math.Round(ts.Y * sy, 1));
                if (c is Label l3 && (l3.ClipText || l3.TextOverrunBehavior != TextServer.OverrunBehavior.NoTrimming)) o["trims"] = true;
            }
        }
        items.Add(o);
    }

    private static void Walk3D(Node n, JsonArray items, Camera3D cam, Transform2D final)
    {
        if (n is Node3D n3 && !n3.Visible) return;
        if (n is CanvasItem ci && !ci.Visible) return;
        if (n is Label3D l && l.Text.Length > 0 && l.IsVisibleInTree() && !cam.IsPositionBehind(l.GlobalPosition))
        {
            var box = l.GetAabb();
            var sc = l.GlobalTransform.Basis.Scale;
            var centre = l.GlobalTransform * box.GetCenter();
            var right = cam.GlobalBasis.X * box.Size.X * sc.X * 0.5f;
            var up = cam.GlobalBasis.Y * box.Size.Y * sc.Y * 0.5f;
            var r = new Rect2(cam.UnprojectPosition(centre), Vector2.Zero);
            r = r.Expand(cam.UnprojectPosition(centre - right - up)).Expand(cam.UnprojectPosition(centre + right + up))
                 .Expand(cam.UnprojectPosition(centre + right - up)).Expand(cam.UnprojectPosition(centre - right + up));
            r = final * r;
            var win = cam.GetWindow().Size;
            if (r.Intersects(new Rect2(0, 0, win.X, win.Y)))
            {
                string t = l.Text;
                var owner = l.GetParent();
                items.Add(new JsonObject
                {
                    ["name"] = StableName(l),
                    ["type"] = "Label3D",
                    ["group"] = "3d:" + (owner != null ? StableName(owner) : "?"),
                    ["rect"] = R(r),
                    ["opacity"] = Math.Round(l.Modulate.A * (1f - l.Transparency), 3),
                    ["text"] = t.Length > 60 ? t[..60] : t,
                    ["font_px"] = Math.Round(r.Size.Y / Math.Max(1, t.Split('\n').Length), 1),
                });
            }
        }
        if (n is SubViewport) return;
        foreach (var ch in n.GetChildren()) Walk3D(ch, items, cam, final);
    }

    private static JsonArray R(Rect2 r) => new(Math.Round(r.Position.X, 1), Math.Round(r.Position.Y, 1), Math.Round(r.Size.X, 1), Math.Round(r.Size.Y, 1));
}
