using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// n10, the drawing's matrix (performance research section 5a): the end-of-age town (or, with --town=river, the sealed
/// second-age town) played by the bot, adopted and held still (the simulation paused), at the window's size; then
/// per variant: the mean and 95th percentile of the real frame time, and the rendering server's draws, objects and
/// primitives. Run under xvfb (software Vulkan): the times are only good for ratios between variants of one run.
/// Arguments after --: --town=age1|river, --variants=a,b,... (baseline, noink, nosoft, noshadow, notrees,
/// novillagers, flatground, zoomout, closest, scale50), --frames=N (measured, default 30), --warm=N (default 10),
/// --shots (with time stopped and the animals and the chimney smoke hidden, a picture of the views baseline, zoomout and closest to --out-dir, for before/after
/// comparisons), --out-dir=DIR. Lines "DRAW town variant WxH mean_ms p95_ms draws objects primitives".
/// </summary>
public partial class PerfDraw : TownTestBase
{
    protected override string Marker => "PERF DRAW PASS";
    protected override Vector2I WindowSize => GetWindow().Size;

    protected override async Task Run()
    {
        string townKind = "age1", outDir = ProjectSettings.GlobalizePath("res://../build/perf-draw");
        string[] variants = { "baseline" };
        int frames = 30, warm = 10;
        bool shots = false;
        string shotVariant = "baseline";
        int gcSeconds = 0;
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            if (a.StartsWith("--town=")) townKind = a[7..];
            else if (a.StartsWith("--variants=")) variants = a[11..].Split(',');
            else if (a.StartsWith("--frames=")) frames = int.Parse(a[9..]);
            else if (a.StartsWith("--warm=")) warm = int.Parse(a[7..]);
            else if (a == "--shots") shots = true;
            else if (a.StartsWith("--gc=")) gcSeconds = int.Parse(a[5..]);
            else if (a.StartsWith("--shot-variant=")) shotVariant = a[15..];
            else if (a.StartsWith("--out-dir=")) outDir = a[10..];
        }
        System.IO.Directory.CreateDirectory(outDir);
        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        Engine.MaxFps = 0;
        Main.Driver.Adopt(MakeTown(townKind));
        Main.Driver.SetPaused(true);
        Main.Rig.UserMoved = false;
        Main.Follow(instant: true);
        await Frames(10);
        await Until(() =>
        {
            for (int r = 0; r < T.RoadCount; r++) if (T.RoadOpen(r) && Main.Ground.RoadReveal(r) < 1f) return false;
            return Main.Buildings.Popping == 0;
        }, 2000, "the drawing to catch up");
        var home = (Main.Rig.PivotLocal, Main.Rig.TargetSize);
        var size = GetWindow().Size;
        if (gcSeconds > 0) { await Collector(gcSeconds); return; }
        if (shots)
        {
            Engine.TimeScale = 0;
            // The animals, birds and smoke move at random (two runs differed there by about 800 pixels): out of the pictures.
            foreach (var c in Main.MapOrigin.GetChildren()) if (c is TownLife life) life.Visible = false;
            foreach (var c in Main.MapOrigin.FindChildren("*", "CPUParticles3D", true, false)) ((CpuParticles3D)c).Visible = false;   // the smoke, too
            Apply(shotVariant, home);
            foreach (var view in new[] { "baseline", "zoomout", "closest" })
            {
                var undo = Apply(view, home);
                await Frames(6);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                string tag = shotVariant == "baseline" ? "" : "-" + shotVariant;
                GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(outDir, $"view-{townKind}-{view}{tag}-{size.X}x{size.Y}.png"));
                undo();
            }
            Engine.TimeScale = 1;
            Notes.Add("shots in " + outDir);
            return;
        }
        var times = new double[frames];
        foreach (var v in variants)
        {
            var undo = Apply(v, home);
            await Frames(warm);
            long last = Stopwatch.GetTimestamp();
            for (int i = 0; i < frames; i++)
            {
                await Frames(1);
                long now = Stopwatch.GetTimestamp();
                times[i] = (now - last) * 1000.0 / Stopwatch.Frequency;
                last = now;
            }
            long draws = (long)RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalDrawCallsInFrame);
            long objects = (long)RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalObjectsInFrame);
            long prims = (long)RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalPrimitivesInFrame);
            double sum = 0;
            foreach (var t in times) sum += t;
            var sorted = (double[])times.Clone();
            Array.Sort(sorted);
            double p95 = sorted[Math.Min(frames - 1, (int)Math.Ceiling(frames * 0.95) - 1)];
            GD.Print($"DRAW {townKind} {v} {size.X}x{size.Y} mean_ms {sum / frames:0.0} p95_ms {p95:0.0} draws {draws} objects {objects} primitives {prims} villagers {T.VillagerCount}");
            undo();
            await Frames(5);
        }
        Notes.Add($"{townKind} {string.Join(",", variants)}");
    }

    /// <summary>--gc=N: the town running at 1x for N real seconds; the .NET collector's counts and the bytes allocated.</summary>
    private async Task Collector(int seconds)
    {
        Main.Driver.SetPaused(false);
        Main.Driver.SetSpeed(1);
        await Frames(30);
        int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
        long a0 = GC.GetTotalAllocatedBytes(false), f0 = (long)Engine.GetProcessFrames();
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < seconds) await Frames(1);
        long frames = (long)Engine.GetProcessFrames() - f0, bytes = GC.GetTotalAllocatedBytes(false) - a0;
        GD.Print($"GC {T.VillagerCount} villagers, {sw.Elapsed.TotalSeconds:0} s, {frames} frames: gen0 {GC.CollectionCount(0) - g0} gen1 {GC.CollectionCount(1) - g1} gen2 {GC.CollectionCount(2) - g2}; "
            + $"{bytes / 1024} KB allocated ({bytes / Math.Max(1, frames)} bytes a frame); autosaves {Main.Saver.Saves}; heap {GC.GetTotalMemory(false) / 1024} KB");
    }

    private static Town MakeTown(string kind)
    {
        var town = new Town(1);
        var bot = new TownBot(BotKind.Payback, 1);
        int ticks = 0;
        while (!town.HasCharter && ticks < 20 * 60 * 60 * 10) { town.Step(); bot.Act(town); ticks++; if (ticks % 600 == 0) town.DrainEvents(new List<TownEvent>()); }
        for (int i = 0; i < 600; i++) { town.Step(); bot.Act(town); }
        if (kind != "river") return town;
        town = TownSave.Load(TownSave.Save(town));
        bot = new TownBot(BotKind.Payback, 1) { PlayOn = true };
        ticks = 0;
        while (!town.HasSeal && ticks < 20 * 60 * 60 * 10) { town.Step(); bot.Act(town); ticks++; if (ticks % 600 == 0) town.DrainEvents(new List<TownEvent>()); }
        for (int i = 0; i < 600; i++) { town.Step(); bot.Act(town); }
        return town;
    }

    /// <summary>Applies one variant; returns how to undo it.</summary>
    private Action Apply(string v, (Vector3 pivot, float size) home)
    {
        var m = Main;
        switch (v)
        {
            case "baseline": return () => { };
            case "noink": m.Ink.InkVisible = false; return () => m.Ink.InkVisible = true;
            case "nosoft":
                var soft = m.Ink.GetNode<MeshInstance3D>("InkSoft");
                soft.Visible = false; return () => soft.Visible = true;
            case "noshadow":
                var sun = m.GetNode<DirectionalLight3D>("Sun");
                sun.ShadowEnabled = false; return () => sun.ShadowEnabled = true;
            case "notrees":
                var trees = m.Trees!;   // planted again for an adopted town (its node name is then not "Trees")
                trees.Visible = false; return () => trees.Visible = true;
            case "novillagers": m.Villagers.Visible = false; return () => m.Villagers.Visible = true;
            case "flatground":
                var old = m.Ground.MaterialOverride;
                m.Ground.MaterialOverride = new StandardMaterial3D { AlbedoColor = Palette.Paper };
                return () => m.Ground.MaterialOverride = old;
            case "zoomout":
                m.Rig.UserMoved = true;
                m.Rig.LookAt(home.pivot, m.Rig.MaxSize, instant: true);
                return () => m.Rig.LookAt(home.pivot, home.size, instant: true);
            case "closest":
                m.Rig.UserMoved = true;
                m.Rig.LookAt(home.pivot, m.Rig.MinSize, instant: true);
                return () => m.Rig.LookAt(home.pivot, home.size, instant: true);
            case "shadow350":
                var sun2 = m.GetNode<DirectionalLight3D>("Sun");
                float was = sun2.DirectionalShadowMaxDistance;
                sun2.DirectionalShadowMaxDistance = 350f; return () => sun2.DirectionalShadowMaxDistance = was;
            case "scale50":
                GetViewport().Scaling3DScale = 0.5f; return () => GetViewport().Scaling3DScale = 1f;
        }
        throw new Exception("unknown variant " + v);
    }
}
