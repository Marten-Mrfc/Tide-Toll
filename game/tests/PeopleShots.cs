using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// n7 (villagers as people): pictures from the real game for the report (tools/people-shots.sh: xvfb, Mobile renderer,
/// 1920 x 1080). A town played by the payback bot to a crowd (seed 2, the mill open); then by real pointer motion and
/// clicks: the hover ring and the name tag on a villager, its card docked under the ledger (whole window), and the
/// busiest road with the town held still, drawn first with round 6's spread and then with the bodies pass (the same
/// people in the same places), at the normal view and close up.
/// </summary>
public partial class PeopleShots : TownTestBase
{
    protected override string Marker => "PEOPLE SHOTS PASS";
    protected override Vector2I WindowSize => GetWindow().Size;
    private string _dir = "";
    private Vector3 _passAt;

    protected override async Task Run()
    {
        _dir = ProjectSettings.GlobalizePath("res://../build/n7/people");
        foreach (var a in OS.GetCmdlineUserArgs()) if (a.StartsWith("--out-dir=")) _dir = a.Substring(10);
        System.IO.Directory.CreateDirectory(_dir);
        var bot = new TownBot(BotKind.Payback, 2);
        Main.Driver.SetPaused(true);
        Main.Driver.RunBot(bot, t => t.VillagerCount >= 18 && MillOpen(t), 20 * 60 * 40);
        Main.Driver.Advance(20);
        Main.Driver.SetSpeed(1);
        Main.Driver.SetPaused(false);
        await Frames(90);
        var vil = Main.Villagers; var hud = Main.Hud;
        // A road where people pass each other: run on until one road has walkers both ways, near its middle.
        int road = -1;
        for (int f = 0; f < 60 * 120 && road < 0; f++)
        {
            await Frames(1);
            road = PassingRoad();
        }
        if (road < 0) road = Math.Max(0, T.BusiestRoad);
        var rd = T.Plan.Roads[road];
        rd.At(rd.Length / 2, out double rx, out double ry, out _);
        var mid = _passAt != Vector3.Zero ? _passAt : new Vector3((float)rx * TownGround.Cell, 0, (float)ry * TownGround.Cell);
        Main.Rig.UserMoved = true;
        Main.Rig.LookAt(mid, 46f, instant: true);
        await Frames(2);
        // The town held still from here (the same people in every picture).
        Main.Driver.SetPaused(true);
        await Frames(10);
        // Hover: the clearest villager on screen near the middle.
        int who = Clearest(vil);
        Check(who >= 0, "a villager to point at");
        Input.WarpMouse(vil.ScreenOf(Main.Rig, who)); // the system pointer too (xvfb): the hover reads it
        TestInput.Motion(vil.ScreenOf(Main.Rig, who));
        await Frames(40);
        var under = GetViewport().GuiGetHoveredControl();
        Check(hud.HoverVillager == who, $"hovering {who} (got {hud.HoverVillager}; control under the pointer: {(under == null ? "none" : under.GetPath().ToString())}; target {hud.PersonTarget(vil.ScreenOf(Main.Rig, who))})");
        Crop("people-hover", vil.ScreenOf(Main.Rig, who), 960, 540);
        Crop("people-hover-close", vil.ScreenOf(Main.Rig, who), 480, 270, 2);
        await Click(vil.ScreenOf(Main.Rig, who));
        await Frames(40);
        Check(hud.SelectedVillager == who, "selected");
        Input.WarpMouse(new Vector2(1500, 900));
        TestInput.Motion(new Vector2(1500, 900));
        await Frames(20);
        Save("people-card", GetViewport().GetTexture().GetImage());
        Crop("people-card-close", hud.PersonCardRect.GetCenter() + new Vector2(140, -120), 640, 400, 2);
        hud.Select(-1);
        await Frames(30);
        Save("people-none", GetViewport().GetTexture().GetImage());   // n7 step 2 mock-up: the column with nothing selected
        // A busy road before and after: the town held still, the same people drawn by round 6's spread, then by the bodies pass.
        Main.Driver.SetPaused(true);
        for (int pass = 0; pass < 2; pass++)
        {
            TownVillagers.OldSpread = pass == 0;
            await Frames(60);
            string tag = pass == 0 ? "before" : "after";
            Main.Rig.LookAt(mid, 46f, instant: true);
            await Frames(10);
            Crop($"people-road-{tag}", Main.Rig.LocalToScreen(mid), 960, 540);
            Main.Rig.LookAt(mid, 18f, instant: true);
            await Frames(10);
            Crop($"people-road-{tag}-close", Main.Rig.LocalToScreen(mid), 960, 540);
        }
        TownVillagers.OldSpread = false;
        Notes.Add($"dir={_dir} villagers={T.VillagerCount} road={road} who={who} name={T.VillagerName(who)}");
    }

    /// <summary>A road with at least one walker each way within two cells of each other away from its ends (-1 none).</summary>
    private int PassingRoad()
    {
        var vil = Main.Villagers;
        int n = Math.Min(T.VillagerCount, TownVillagers.Capacity);
        for (int i = 0; i < n; i++)
        {
            int r = vil.RoadOf(i);
            if (r < 0) continue;
            var a = T.Villager(i); var rd = T.Plan.Roads[r];
            if (a.Dist < 2 || a.Dist > rd.Length - 2) continue;
            for (int j = 0; j < n; j++)
            {
                if (vil.RoadOf(j) != r) continue;
                var b = T.Villager(j);
                if (b.Dir != a.Dir && Math.Abs(b.Dist - a.Dist) < 2.0) { _passAt = new Vector3((float)a.X * TownGround.Cell, 0, (float)a.Y * TownGround.Cell); return r; }
            }
        }
        return -1;
    }

    private void Crop(string name, Vector2 centre, int w, int h, int scale = 1)
    {
        var img = GetViewport().GetTexture().GetImage();
        var sz = img.GetSize();
        int x = Math.Clamp((int)centre.X - w / 2, 0, sz.X - w), y = Math.Clamp((int)centre.Y - h / 2, 0, sz.Y - h);
        var c = img.GetRegion(new Rect2I(x, y, w, h));
        if (scale > 1) c.Resize(w * scale, h * scale, Image.Interpolation.Nearest);
        Save(name, c);
    }

    private void Save(string name, Image img) => img.SavePng(System.IO.Path.Combine(_dir, name + ".png"));

    private int Clearest(TownVillagers vil)
    {
        int n = Math.Min(T.VillagerCount, TownVillagers.Capacity), best = -1; float bestScore = 0;
        var c = GetViewport().GetVisibleRect().GetCenter();
        for (int i = 0; i < n; i++)
        {
            var p = vil.ScreenOf(Main.Rig, i);
            if (!GetViewport().GetVisibleRect().Grow(-200).HasPoint(p) || Main.Hud.OverInterface(p)) continue;
            float clear = float.MaxValue;
            for (int j = 0; j < n; j++) if (j != i) clear = Math.Min(clear, vil.ScreenOf(Main.Rig, j).DistanceTo(p));
            float score = Math.Min(clear, 60f) - p.DistanceTo(c) * 0.02f;
            if (score > bestScore) { bestScore = score; best = i; }
        }
        return best;
    }

    private static bool MillOpen(Town t)
    {
        for (int i = 0; i < t.WorkplaceCount; i++) { var w = t.Workplace(i); if (w.Open && w.Kind == PlotKind.Mill) return true; }
        return false;
    }
}
