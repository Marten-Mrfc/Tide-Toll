using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// Night 3 (feel): frame strips of each reward on the map (tools/feel-strips.sh: xvfb, Mobile renderer, 1920 x 1080).
/// The town is paused, the view centred on the reward at the play zoom, the reward started by an event shaped as the
/// simulation raises it (from the town's plan), and the rewards' clock (<see cref="TownFeel.StripStep"/>) stepped
/// 0.08 s per saved frame: twelve frames cover 0.96 s. Each frame saves the middle 960 x 540 of the screen at its
/// real pixels (what a player sees at the play zoom, uncropped by scaling) as --out-dir/frames/feel-NAME-NN.png.
/// </summary>
public partial class FeelStrips : TownTestBase
{
    protected override string Marker => "FEEL STRIPS PASS";
    protected override Vector2I WindowSize => GetWindow().Size;
    private string _dir = "";
    private TownFeel F => Main.MapOrigin.GetNode<TownFeel>("Feel");

    protected override async Task Run()
    {
        _dir = ProjectSettings.GlobalizePath("res://../build/n3/feel");
        foreach (var a in OS.GetCmdlineUserArgs()) if (a.StartsWith("--out-dir=")) _dir = a.Substring(10);
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(_dir, "frames"));
        bool river = Array.IndexOf(OS.GetCmdlineUserArgs(), "--river-only") >= 0;
        var bot = new TownBot(BotKind.Payback, 1);
        var T = Main.Driver.Town;
        if (!river)
        {
            // Trail dust first, while the trails are new: a villager walking a trail not yet worn in.
            Main.Driver.RunBot(bot, t => t.VillagerCount >= 2 && Walker(t) >= 0, 20 * 60 * 10);
            await Frames(10);
            Main.Driver.SetPaused(true);
            Main.Hud.ClearMessagesNow();
            int v = Walker(T);
            if (v >= 0) await Stage("trail-dust", new Vector3((float)T.Villager(v).X * TownGround.Cell, 0, (float)T.Villager(v).Y * TownGround.Cell), null);
            else Check(false, "the strips: no villager on a new trail");
            Main.Driver.SetPaused(false);
            Main.Driver.RunBot(bot, t => t.Level >= 6 && t.VillagerCount >= 6, 20 * 60 * 30);
            await Frames(30);
            Main.Driver.SetPaused(true);
            Main.Hud.ClearMessagesNow();
            // The market built and open (the coin's and the request's building), as a town at this level has it.
            if (!HasOpenMarket(T))
            {
                Main.Driver.SetPaused(false);
                Main.Driver.RunBot(bot, HasOpenMarket, 20 * 60 * 30);
                await Frames(10);
                Main.Driver.SetPaused(true);
                Main.Hud.ClearMessagesNow();
            }
            var plan = T.Plan;
            int market = Find(T, PlotKind.Market), cottage = Find(T, PlotKind.Cottage), bell = Find(T, PlotKind.BellGreen), well = Find(T, PlotKind.Well);
            var m = plan.Plots[market];
            await Stage("coin", Door(m), () => F.On(T, new TownEvent { Kind = TownEventKind.Delivery, X = m.DoorX + 0.5, Y = m.DoorY + 0.5, Plot = market }));
            var c = plan.Plots[cottage];
            await Stage("flourish", Door(c), () => F.On(T, new TownEvent { Kind = TownEventKind.VillagerArrived, Plot = cottage, X = c.DoorX + 0.5, Y = c.DoorY + 0.5 }));
            await Stage("extend", Door(c), () => F.On(T, new TownEvent { Kind = TownEventKind.CottageExtended, Plot = cottage, Level = 1, X = c.DoorX + 0.5, Y = c.DoorY + 0.5 }));
            int road = 0;
            for (int r = 0; r < T.RoadCount; r++) if (T.RoadOpen(r) && plan.Roads[r].Cum[^1] > plan.Roads[road].Cum[^1] * (T.RoadOpen(road) ? 1 : 0)) road = r;
            var rd = plan.Roads[road];
            int mid = rd.Px.Length / 2;
            rd.At(rd.Length / 2, out double mx, out double my, out _);
            await Stage("pave", new Vector3((float)(mx + 0.5) * TownGround.Cell, 0, (float)(my + 0.5) * TownGround.Cell),
                () => F.On(T, new TownEvent { Kind = TownEventKind.RoadPaved, Index = road, Surface = Surface.Gravel }));
            if (bell >= 0)
            {
                // The town really rings its bell (the swing is TownBuildings', drawn on the town's own clock), so the
                // town runs for this strip; the rings come from the town's own BellRung event.
                if (!T.BellReady) Main.Driver.RunBot(new TownBot(BotKind.Payback, 1) { NoBell = true }, t => t.BellReady, 20 * 60 * 5);
                Main.Driver.SetPaused(false);
                Main.Hud.ClearMessagesNow();
                bool rung = T.RingBell();
                Check(rung, "the strips: the bell could not be rung");
                await Stage("bell", Centre(plan.Plots[bell]), null, realTime: true);
                Main.Driver.SetPaused(true);
                Main.Hud.ClearMessagesNow();
            }
            else Check(false, "the strips: no bell green at level 6");
            var w = plan.Plots[well];
            // The asker: an open market (its building stands), as the town would have it.
            int openMarket = -1;
            if (HasOpenMarket(T)) openMarket = Find(T, PlotKind.Market);
            Check(openMarket >= 0, "the strips: no open market for the request");
            var om = plan.Plots[openMarket >= 0 ? openMarket : market];
            await Stage("request", Door(om), () => F.On(T, new TownEvent { Kind = TownEventKind.RequestMet, Index = 0 }));
            int lvl = T.Level;
            while (lvl > 1 && plan.Level(lvl).Plots.Length == 0) lvl--;   // the latest level whose gift has a plot
            var lp = plan.Level(lvl).Plots;
            var at = lp.Length > 0 ? Centre(plan.Plots[lp[0]]) : Door(w);
            await Stage("gift", at, () => F.On(T, new TownEvent { Kind = TownEventKind.LevelReached, Level = lvl }));
            Main.Driver.SetPaused(false);
        }
        // The river: the landing (a boat moors) and the ford (someone wades).
        Main.Driver.RunBot(bot, t => t.HasCharter, 20 * 60 * 60);
        T.RingNextAge();
        Main.Driver.RunBot(bot, t => t.InRiverAge && t.RiverPlan != null && t.CrossingExists(RiverLinkKind.Ford) && t.BoatCount > 0, 20 * 60 * 60);
        await Frames(30);
        Main.Driver.SetPaused(true);
        Main.Hud.ClearMessagesNow();
        var rp = T.RiverPlan!;
        int li = rp.LinkOf(RiverLinkKind.Ford);
        if (li >= 0)
        {
            var l = rp.Links[li];
            double row = l.Py[0], xm = (l.Px[0] + l.Px[^1]) / 2;
            var landing = rp.Plots[0];
            foreach (var pl in rp.Plots) if (pl.Kind == RiverPlotKind.Landing) landing = pl;
            // A sale at the landing (the event carries the landing's own point); its rings go on the water at the
            // nearest berth, where a boat moors (the mooring's rings are the same, at the boat).
            double lx = rp.Px[RiverPoint.Landing], ly = rp.Py[RiverPoint.Landing];
            var lat = new Vector3((float)(lx + rp.BerthX[0]) / 2f * TownGround.Cell, 0, (float)(ly + rp.BerthY[0]) / 2f * TownGround.Cell);
            await Stage("landing", lat, () => F.On(T, new TownEvent { Kind = TownEventKind.LandingDelivery, X = lx, Y = ly, Index = -1 }));
            await Stage("ford", new Vector3((float)xm * TownGround.Cell, 0, (float)row * TownGround.Cell), null, ford: (xm, row));
        }
        else Check(false, "the strips: no ford");
        Notes.Add($"most live {F.MostLive}, skipped {F.Skipped}");
        Notes.Add("dir=" + _dir);
    }

    /// <summary>A villager walking a trail that is not yet worn in (-1 none).</summary>
    private static int Walker(Town t)
    {
        for (int i = 0; i < t.VillagerCount; i++)
        {
            var vi = t.Villager(i);
            if (vi.Road < 0) continue;
            var r = t.RoadInfo(vi.Road);
            if (!r.WornIn && r.Surface == Surface.Track && r.Wear < 0.6) return i;
        }
        return -1;
    }

    private static bool HasOpenMarket(Town t)
    {
        for (int i = 0; i < t.WorkplaceCount; i++) { var wp = t.Workplace(i); if (wp.Open && wp.Kind == PlotKind.Market) return true; }
        foreach (var p in t.Plan.Plots) if (p.Kind == PlotKind.Market && t.PlotWorking(p.Id)) return true;
        return false;
    }

    private static int Find(Town t, PlotKind k)
    {
        // An open workplace of the kind first (its building stands), else any given plot of it.
        for (int i = 0; i < t.WorkplaceCount; i++) { var wp = t.Workplace(i); if (wp.Open && wp.Kind == k) return wp.Plot; }
        foreach (var p in t.Plan.Plots) if (p.Kind == k && t.PlotWorking(p.Id)) return p.Id;
        foreach (var p in t.Plan.Plots) if (p.Kind == k && t.PlotGiven(p.Id)) return p.Id;
        return -1;
    }

    private static Vector3 Door(Plot p) => new((p.DoorX + 0.5f) * TownGround.Cell, 0, (p.DoorY + 0.5f) * TownGround.Cell);
    private static Vector3 Centre(Plot p) => new((p.X + p.W / 2f) * TownGround.Cell, 0, (p.Y + p.H / 2f) * TownGround.Cell);

    /// <summary>Centres the view on the spot at the play zoom, starts the reward and saves twelve frames 0.08 s apart.</summary>
    private async Task Stage(string name, Vector3 at, Action? start, (double x, double row)? ford = null, bool realTime = false)
    {
        foreach (var o in OS.GetCmdlineUserArgs())
            if (o.StartsWith("--only=") && Array.IndexOf(o.Substring(7).Split(','), name) < 0) return;
        // The play zoom: the size the view takes when it follows the town (Home), then centred on the reward.
        Main.Rig.UserMoved = false;
        Main.Follow(instant: true);
        await Frames(2);
        float size = Main.Rig.Camera.Size;
        Main.Rig.UserMoved = true;
        Main.Rig.LookAt(at, size, instant: true);
        TownFeel.StripStep = realTime ? -1f : 0f;
        if (!realTime) F.Clear();
        await Frames(4);
        start?.Invoke();
        for (int k = 0; k < 12; k++)
        {
            if (ford is { } fd && k % 3 == 0)
                F.StoneRing(Math.Floor(fd.x) + 0.5 + (k / 3 - 1), fd.row);
            // (A test's frame wait resumes before the nodes' own frame: the step is taken once, by the next frame.)
            TownFeel.StripNext = name == "trail-dust" ? 0.2f : 0.08f;
            if (realTime) { await Frames(1); await Save("feel-" + name, k); continue; }
            await Frames(2);
            await Save("feel-" + name, k);
        }
        GD.Print($"FEEL strip {name}: size {size:0.0}, live {F.Live}, most {F.MostLive}");
        TownFeel.StripStep = -1f;
    }

    private async Task Save(string name, int k)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var img = GetViewport().GetTexture().GetImage();
        var sz = img.GetSize();
        var crop = img.GetRegion(new Rect2I(sz.X / 2 - 480, sz.Y / 2 - 270, 960, 540));
        crop.SavePng(System.IO.Path.Combine(_dir, "frames", $"{name}-{k:00}.png"));
    }
}
