using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// Work you can watch (n5): interaction strips (tools/work-strips.sh: xvfb, Mobile renderer, 1920 x 1080, --fixed-fps 60).
/// A real town is played by the payback bot to the middle of the first age (the mill and the market open), then left to
/// run on with nobody buying. For each job a copy of the town (<see cref="TownSave"/>, the same steps give the same
/// town) is run ahead to find one villager's scene: joining the line, starting work, finishing, walking away (or, for
/// the market and coming home, the delivery). The real town is then played to that moment frame by frame at 1x with
/// the renderer switched off (cheap), and at eight moments of the scene the town is held, one frame saved at the
/// normal view (the start view's metres per pixel, <see cref="NormalSize"/>) and one close up (<see cref="CloseSize"/>,
/// centred on the villager), each the middle 960 x 540 of the screen at its real pixels:
/// --out-dir/frames/work-JOB-{normal,close}-NN.png with a caption in work-JOB-NN.txt. tools/work-sheet.py joins them.
/// Nothing here depends on how the villagers are drawn, so the same tool takes the "before" and the "after" strips.
/// </summary>
public partial class WorkStrips : TownTestBase
{
    protected override string Marker => "WORK STRIPS PASS";
    protected override Vector2I WindowSize => GetWindow().Size;
    /// <summary>The normal view: 46 m of height in the view at 1080 px (the start view's metres per pixel at 1080p).</summary>
    public const float NormalSize = 46f;
    public const float CloseSize = 15f;
    private string _dir = "";

    private enum Job { Water, Reap, Mill, Market, Home }

    private struct Scene
    {
        public int Villager, Workplace, Plot;
        public double Join, Start, Finish, Deliver; // game seconds (-1: not part of this scene)
    }

    protected override async Task Run()
    {
        _dir = ProjectSettings.GlobalizePath("res://../build/n5/strips");
        string only = "";
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            if (a.StartsWith("--out-dir=")) _dir = a.Substring(10);
            if (a.StartsWith("--only=")) only = a.Substring(7);
        }
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(_dir, "frames"));
        RenderingServer.RenderLoopEnabled = false;
        var bot = new TownBot(BotKind.Payback, 1);
        Main.Driver.SetPaused(true);
        // The middle of the first age: the mill (level 13) and the market stall bought and open.
        Main.Driver.RunBot(bot, t => Open(t, PlotKind.Mill) >= 0 && Open(t, PlotKind.Market) >= 0 && t.VillagerCount >= 10, 20 * 60 * 40);
        Check(Open(T, PlotKind.Mill) >= 0, "the strips: the mill never opened");
        // Let the town settle into its rhythm with nobody buying (the bot's last purchases send people everywhere).
        Main.Driver.Advance(20);
        Main.Driver.SetSpeed(1);
        Main.Driver.SetPaused(false);
        await Frames(30);
        Notes.Add($"level={T.Level} villagers={T.VillagerCount} tools={T.ToolsLevel} load={T.LoadLevel} minute={T.Time / 60:0.0}");
        foreach (Job j in new[] { Job.Water, Job.Reap, Job.Mill, Job.Market, Job.Home })
        {
            string name = j.ToString().ToLowerInvariant();
            if (only.Length > 0 && Array.IndexOf(only.Split(','), name) < 0) continue;
            var sc = Find(j);
            if (sc.Villager < 0) { Check(false, $"the strips: no scene found for {name}"); continue; }
            await Film(name, j, sc);
        }
        RenderingServer.RenderLoopEnabled = true;
        Notes.Add("dir=" + _dir);
    }

    private static int Open(Town t, PlotKind k)
    {
        for (int i = 0; i < t.WorkplaceCount; i++) { var w = t.Workplace(i); if (w.Open && w.Kind == k) return i; }
        if (k == PlotKind.Market) foreach (var p in t.Plan.Plots) if (p.Kind == k && t.PlotWorking(p.Id)) return p.Id;
        return -1;
    }

    /// <summary>Runs a copy of the town ahead (at most 150 game seconds) for the first full scene of the job.</summary>
    private Scene Find(Job j)
    {
        var c = TownSave.Load(TownSave.Save(T));
        int n = c.VillagerCount;
        var phase = new VillagerPhase[n];
        var join = new double[n]; var start = new double[n]; var ahead = new int[n];
        var at = new int[n];
        for (int i = 0; i < n; i++) { var v = c.Villager(i); phase[i] = v.Phase; join[i] = -1; start[i] = -1; at[i] = v.Workplace; }
        var ev = new List<TownEvent>();
        var best = new Scene { Villager = -1 };
        PlotKind want = j == Job.Water ? PlotKind.Well : j == Job.Reap ? PlotKind.Field : PlotKind.Mill;
        double finishOf = -1; int fv = -1;
        for (int k = 0; k < 1500; k++)
        {
            c.Step();
            ev.Clear();
            c.DrainEvents(ev);
            double now = c.Time;
            for (int i = 0; i < n; i++)
            {
                var v = c.Villager(i);
                if (v.Phase != phase[i])
                {
                    if (v.Phase == VillagerPhase.Queued && v.Workplace >= 0) { join[i] = now; ahead[i] = v.QueuePosition; at[i] = v.Workplace; start[i] = -1; }
                    if (v.Phase == VillagerPhase.Working && v.Workplace >= 0)
                    {
                        if (phase[i] != VillagerPhase.Queued) { join[i] = -1; at[i] = v.Workplace; }
                        start[i] = now;
                    }
                    if (phase[i] == VillagerPhase.Working && at[i] >= 0 && start[i] >= 0)
                    {
                        var wp = c.Workplace(at[i]);
                        bool lineJob = j is Job.Water or Job.Reap or Job.Mill;
                        // A line job: the first villager who waited behind someone (the line moving up) and finished.
                        if (lineJob && wp.Kind == want && join[i] >= 0 && best.Villager < 0 && (ahead[i] >= 1 || k > 900))
                            return new Scene { Villager = i, Workplace = at[i], Plot = wp.Plot, Join = join[i], Start = start[i], Finish = now, Deliver = -1 };
                        if (!lineJob && fv < 0) { finishOf = now; fv = i; }
                        if (!lineJob && fv == i) finishOf = now;
                    }
                    phase[i] = v.Phase;
                }
            }
            foreach (var e in ev)
            {
                if (e.Kind != TownEventKind.Delivery || e.Villager < 0 || e.Villager >= n) continue;
                var p = c.Plan.Plots[e.Plot];
                bool market = p.Kind == PlotKind.Market;
                if (j == Job.Market && market && now - 6 > T.Time)
                    return new Scene { Villager = e.Villager, Workplace = -1, Plot = e.Plot, Join = -1, Start = -1, Finish = -1, Deliver = now };
                if (j == Job.Home && !market && e.Good == Good.Water && now - 6 > T.Time)
                    return new Scene { Villager = e.Villager, Workplace = -1, Plot = e.Plot, Join = -1, Start = -1, Finish = -1, Deliver = now };
            }
        }
        return best;
    }

    /// <summary>The eight moments of a scene (game seconds).</summary>
    private static double[] Moments(Scene s)
    {
        if (s.Deliver >= 0)
        {
            double d = s.Deliver;
            return new[] { d - 3.0, d - 1.6, d - 0.7, d - 0.25, d + 0.1, d + 0.35, d + 0.8, d + 2.0 };
        }
        double j = s.Join, st = s.Start, f = s.Finish, w = f - st;
        return new[] { j + 0.2, j + (st - j) * 0.6, st + 0.15, st + w * 0.35, st + w * 0.7, f - 0.1, f + 0.3, f + 2.0 };
    }

    private async Task Film(string name, Job j, Scene sc)
    {
        var m = Moments(sc);
        // Up to two seconds before the first moment at once, then frame by frame at 1x (the drawing settles).
        double lead = m[0] - 2.0 - T.Time;
        Main.Driver.SetPaused(true);
        if (lead > 0) for (int k = 0; k < (int)(lead * TownRules.TickHz); k++) T.Step();
        Main.Driver.SetPaused(false);
        var plan = T.Plan;
        var p = plan.Plots[sc.Plot];
        // The normal view's framing stays put: the plot's door, a little toward the plot.
        var door = new Vector3((p.DoorX + 0.5f) * TownGround.Cell, 0, (p.DoorY + 0.5f) * TownGround.Cell);
        var mid = new Vector3((p.X + p.W / 2f) * TownGround.Cell, 0, (p.Y + p.H / 2f) * TownGround.Cell);
        var frame = door.Lerp(mid, 0.3f);
        for (int k = 0; k < m.Length; k++)
        {
            for (int f = 0; f < 60 * 60 && T.Time < m[k]; f++) await Frames(1);
            var v = T.Villager(sc.Villager);
            string cap = $"{name} {k + 1}: t{T.Time - (sc.Deliver >= 0 ? sc.Deliver : sc.Start):+0.0;-0.0}s {v.Phase}" +
                         (v.Phase == VillagerPhase.Queued ? $" place {v.QueuePosition}" : "") + (v.Carry != Good.None ? $" carrying {v.Carry}" : "");
            System.IO.File.WriteAllText(System.IO.Path.Combine(_dir, "frames", $"work-{name}-{k:00}.txt"), cap);
            Main.Driver.SetPaused(true);
            Main.Hud.ClearMessagesNow();
            Main.Rig.UserMoved = true;
            Main.Rig.LookAt(frame, NormalSize, instant: true);
            await Capture($"work-{name}-normal", k);
            var sh = Main.Villagers.Shown(sc.Villager);
            Main.Rig.LookAt(new Vector3(sh.X * TownGround.Cell, 0, sh.Y * TownGround.Cell), CloseSize, instant: true);
            await Capture($"work-{name}-close", k);
            Main.Driver.SetPaused(false);
            GD.Print("WORK frame " + cap);
        }
    }

    private async Task Capture(string name, int k)
    {
        // Two frames with the renderer on: the first lets the zoom set the villagers' size, the second is saved.
        RenderingServer.RenderLoopEnabled = true;
        await Frames(2);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var img = GetViewport().GetTexture().GetImage();
        var sz = img.GetSize();
        var crop = img.GetRegion(new Rect2I(sz.X / 2 - 480, sz.Y / 2 - 270, 960, 540));
        crop.SavePng(System.IO.Path.Combine(_dir, "frames", $"{name}-{k:00}.png"));
        RenderingServer.RenderLoopEnabled = false;
    }
}
