using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// game-ink (n8): the share of each model's pixels that the ink line covers, at the play view, against a ceiling per
/// kind (style sheet section 3). Each subject is pictured three times in a 640 x 480 window at the 1080p play view's
/// metres per pixel with the 1080p line width: as drawn, with the ink pass hidden, and with the subject hidden (the
/// ground alone). Model pixels = where the second differs from the third (cast shadows left out: a uniform darkening),
/// grown by 3 px for the outer line; ink = how far each pixel moved toward Ink between the first and the second,
/// summed. Prints "INK name share=.. ceiling=.." per subject and fails on any share above its ceiling.
/// --ink-board=DIR instead writes the before/after pictures at 1920 x 1080 (InkBoard).
/// </summary>
public partial class AssetAudit
{
    /// <summary>Ceilings (weighted ink share of the model's pixels). Buildings 30, the street 32, figures 50.</summary>
    public static readonly (string name, float ceiling)[] InkCeilings =
    {
        ("cottage-a0", 0.30f), ("cottage-a1", 0.30f), ("cottage-a2", 0.30f), ("cottage-b0", 0.30f), ("cottage-b1", 0.30f),
        ("cottage-b2", 0.30f), ("cottage-c0", 0.30f), ("cottage-c1", 0.30f), ("cottage-c2", 0.30f),
        ("weaver-cottage", 0.30f), ("mill", 0.30f), ("market-stall", 0.35f), ("well", 0.35f),
        ("street", 0.32f), ("villagers-group", 0.50f),
    };

    private Node3D InkSubject(string name, float walker)
    {
        if (name.StartsWith("cottage-"))
        {
            int v = "abc".IndexOf(name[8]), e = name[9] - '0';
            return Wrap(TownBuildings.CottageNode(v, e, 6f, 6f));
        }
        if (name == "street") return Street(walker);
        if (name == "villagers-group")
        {
            var n = new Node3D { Name = "Subject" };
            for (int i = 0; i < 6; i++)
            {
                var f = TownVillagers.Figure(i, walker, i % 2 == 0 ? Good.None : (Good)(1 + i % 5), 0, i % 2);
                f.Position = new Vector3((i % 3 - 1) * 1.6f * walker, 0, (i / 3) * 1.8f * walker);
                f.Rotation = new Vector3(0, 0.4f * (i - 2), 0);
                n.AddChild(f);
            }
            return n;
        }
        return Model(name, 1f);
    }

    /// <summary>The ink pass's line weight as a 1080p window draws it at this zoom (the check runs in 640 x 480).</summary>
    private void InkAs1080(float camSize1080)
    {
        float w = InkOutline.LinePx(1080f / camSize1080);
        foreach (var q in new[] { "InkHard", "InkSoft" })
        {
            var m = (ShaderMaterial)Main.Ink.GetNode<MeshInstance3D>(q).MaterialOverride;
            m.SetShaderParameter("width_px", w);
        }
    }

    private async Task InkCheck(float playSize, float play1080, float walker)
    {
        var fails = new List<string>();
        foreach (var (name, ceiling) in InkCeilings)
        {
            if (_only != null && !_only.Contains(name)) continue;
            var at = new Vector3((_spot.X + 0.5f) * TownGround.Cell, 0, (_spot.Y + 0.5f) * TownGround.Cell);
            var subject = InkSubject(name, walker);
            subject.Position = at;
            Main.MapOrigin.AddChild(subject);
            var c = Bounds(subject).GetCenter();
            float size = name == "street" ? playSize * 1.2f : playSize;
            Main.Rig.Yaw.Rotation = Vector3.Zero;
            Main.Rig.Place(Centre(at + new Vector3(c.X, 0, c.Z), c.Y, size, 0f), size);
            Main.Rig.UserMoved = true;
            await Frames(3);
            InkAs1080(name == "street" ? play1080 * 1.2f : play1080);
            await Frames(2);
            var drawn = GetViewport().GetTexture().GetImage();
            Main.Ink.InkVisible = false;
            await Frames(2);
            var noink = GetViewport().GetTexture().GetImage();
            subject.Visible = false;
            await Frames(2);
            var ground = GetViewport().GetTexture().GetImage();
            Main.Ink.InkVisible = true;
            subject.QueueFree();
            if (_dir.Length > 0) drawn.SavePng(System.IO.Path.Combine(_dir, $"ink-check-{name}.png"));
            float share = InkShare(drawn, noink, ground);
            string line = $"INK {name} share={share:0.000} ceiling={ceiling:0.00}";
            GD.Print(line);
            Notes.Add($"{name}={share:0.000}");
            if (share > ceiling) fails.Add(line);
            await Frames(1);
        }
        Check(fails.Count == 0, "ink above its ceiling: " + string.Join("; ", fails));
    }

    /// <summary>Weighted ink share of the model's pixels (see the class summary). Runs once per subject, not per frame.</summary>
    public static float InkShare(Image drawn, Image noink, Image ground)
    {
        int w = drawn.GetWidth(), h = drawn.GetHeight();
        byte[] a = drawn.GetData(), b = noink.GetData(), g = ground.GetData();
        int bpp = a.Length / (w * h);
        var model = new bool[w * h];
        for (int i = 0; i < w * h; i++)
        {
            int o = i * bpp;
            float d = 0, rmax = 0, rmin = 99, rsum = 0;
            for (int k = 0; k < 3; k++)
            {
                d = Math.Max(d, Math.Abs(b[o + k] - g[o + k]));
                float r = (b[o + k] + 1f) / (g[o + k] + 1f);
                rmax = Math.Max(rmax, r); rmin = Math.Min(rmin, r); rsum += r;
            }
            bool shadow = rmax - rmin < 0.12f && rsum / 3f < 0.97f && rsum / 3f > 0.3f;
            model[i] = d > 18 && !shadow;
        }
        for (int pass = 0; pass < 3; pass++)
        {
            var grown = (bool[])model.Clone();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (model[i]) continue;
                    grown[i] = (x > 0 && model[i - 1]) || (x < w - 1 && model[i + 1]) || (y > 0 && model[i - w]) || (y < h - 1 && model[i + w]);
                }
            model = grown;
        }
        int[] inkc = { 43, 38, 32 };
        double area = 0, sum = 0;
        for (int i = 0; i < w * h; i++)
        {
            if (!model[i]) continue;
            area++;
            int o = i * bpp;
            float diff = 0, full = 0;
            for (int k = 0; k < 3; k++)
            {
                diff = Math.Max(diff, Math.Abs(a[o + k] - b[o + k]));
                full = Math.Max(full, Math.Abs(b[o + k] - inkc[k]));
            }
            sum += Math.Clamp(diff / (full + 1f), 0f, 1f);
        }
        return area < 1 ? 0f : (float)(sum / area);
    }
}
