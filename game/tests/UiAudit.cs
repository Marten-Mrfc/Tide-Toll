using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// The interface audit tour (tools/ui-audit.sh): one town (seed 1) from the first minute to the borough seal, one
/// picture per interface state (u01..u41, stable ids) plus, per state, every visible interface control that draws
/// something with its rectangle in window pixels (UiAuditDump). Works at any window size and interface scale; finds
/// controls through the HUD's named hooks (Mark, BellButton, Menu buttons), never through pixel positions.
/// Arguments after "--": --out-dir=DIR, --scale=80|100|150, --only=id,id (stops after the last of them).
/// </summary>
public partial class UiAudit : TownTestBase
{
    // Own profile folder per run tag (--tag=, given by tools/ui-audit.sh): two runs at once never share a save.
    protected override string Marker
    {
        get
        {
            foreach (var a in OS.GetCmdlineUserArgs()) if (a.StartsWith("--tag=")) return "UIAUDIT PASS " + a[6..];
            return "UIAUDIT PASS";
        }
    }
    protected override Vector2I WindowSize => GetWindow().Size;

    private string _dir = "";
    private int _scale = 100;
    private HashSet<string>? _only;
    private readonly JsonArray _shots = new();
    private readonly List<string> _notMade = new();
    private TownBot _bot = null!;
    private sealed class StopTour : Exception { }

    protected override async Task Run()
    {
        _dir = ProjectSettings.GlobalizePath("res://../build/n3/ui-audit");
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            if (a.StartsWith("--out-dir=")) _dir = a[10..];
            else if (a.StartsWith("--scale=")) _scale = int.Parse(a[8..]);
            else if (a.StartsWith("--only=")) _only = new HashSet<string>(a[7..].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
        System.IO.Directory.CreateDirectory(_dir);
        Check(Main.Driver.Seed == 1, "the default town is seed 1");
        if (Main.Settings.Store.InterfaceSize != _scale)
        {
            Main.Settings.Store.SetInterfaceSize(_scale);
            await Frames(6);
        }
        Check(Math.Abs(GetWindow().ContentScaleFactor - _scale / 100f) < 1e-3f, $"interface scale {_scale} applied");
        _bot = new TownBot(BotKind.Payback, 1) { NoPave = true, NoBell = true };
        try
        {
            await FirstAge();
            await FirstAgeLate();
            await MenusAndBook("first");
            await SecondAge();
            await SecondAgeLate();
        }
        catch (StopTour) { Notes.Add("only: stopped after the last asked state"); }
        WriteShots();
        Notes.Add($"made={_shots.Count} notmade={_notMade.Count} level={T.Level} dir={_dir}");
    }

    // ---- playing between pictures (as Tour.cs) ----

    private async Task<bool> Fast(Func<Town, bool> until, double maxMinutes)
    {
        Main.Driver.RunBot(_bot, until, (int)(maxMinutes * 60 * TownRules.TickHz));
        await Frames(2);
        return until(T);
    }

    private async Task<bool> Idle(Func<Town, bool> until, double maxMinutes)
    {
        int max = (int)(maxMinutes * 60 * TownRules.TickHz), n = 0;
        while (n < max && !until(T)) { T.Step(); n++; }
        await Frames(2);
        return until(T);
    }

    private async Task Wait(int ms)
    {
        var t0 = Time.GetTicksMsec();
        while (Time.GetTicksMsec() - t0 < (ulong)ms) await Frames(1);
    }

    private async Task<bool> WaitFor(Func<bool> cond, int maxMs)
    {
        var t0 = Time.GetTicksMsec();
        while (!cond()) { if (Time.GetTicksMsec() - t0 > (ulong)maxMs) return false; await Frames(1); }
        return true;
    }

    private async Task Look(double x, double y, float size)
    {
        Main.Rig.UserMoved = true;
        Main.Rig.LookAt(new Vector3((float)x * TownGround.Cell, 0, (float)y * TownGround.Cell), size, instant: true);
        await Frames(2);
    }

    private Task LookPlot(int plot, float size)
    {
        var p = T.Plan.Plots[plot];
        return Look(p.X + p.W / 2.0, p.Y + p.H / 2.0, size);
    }

    private Task LookRoad(int r, float size)
    {
        var road = T.Plan.Roads[r];
        int k = road.Xs.Length / 2;
        return Look(road.Xs[k] + 0.5, road.Ys[k] + 0.5, size);
    }

    private async Task FollowView()
    {
        Main.Rig.UserMoved = false;
        Main.Follow(instant: true);
        await Frames(2);
    }

    private async Task Settle(int ms = 1000)
    {
        await Frames(2);
        await WaitFor(() =>
        {
            for (int r = 0; r < T.RoadCount; r++) if (T.RoadOpen(r) && Main.Ground.RoadReveal(r) < 1f) return false;
            return Main.Buildings.Popping == 0;
        }, 20000);
        await Wait(ms);
    }

    /// <summary>Banners and notes gone ("at rest").</summary>
    private async Task<bool> Calm(int maxMs = 25000)
    {
        bool ok = await WaitFor(() => Main.Hud.BannerText == "", maxMs);
        return await WaitFor(() => Main.Hud.NotesShown == 0, 9000) && ok;
    }

    private async Task ClosePage()
    {
        if (!Main.Hud.AgePageOpen && !Main.Hud.AchievementsOpen) return;
        if (Main.Hud.AgePageOpen) Main.Hud.CloseAgePage();
        else await Key(Godot.Key.Tab);
        await WaitFor(() => !Main.Hud.AgePageOpen && !Main.Hud.AchievementsOpen, 5000);
        await Frames(2);
    }

    private async Task ClearChoice()
    {
        if (T.ChoiceOpen) T.ChooseSide(0);
        await WaitFor(() => !Main.Hud.ChoiceCardVisible, 8000);
    }

    // ---- pictures ----

    private bool Wanted(string id) => _only == null || _only.Contains(id);

    private async Task Shot(string id, string title, string how)
    {
        if (!Wanted(id)) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var img = GetViewport().GetTexture().GetImage();
        var dump = UiAuditDump.Collect(Main, GetViewport(), id, _scale, T.InRiverAge ? 2 : 1, T.Level);
        string path = System.IO.Path.Combine(_dir, id + ".png");
        Check(img.SavePng(path) == Error.Ok, "could not save " + path);
        System.IO.File.WriteAllText(System.IO.Path.Combine(_dir, id + ".json"), dump.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        _shots.Add(new JsonObject { ["id"] = id, ["title"] = title, ["how"] = how, ["level"] = T.Level, ["made"] = true });
        GD.Print($"UIAUDIT SHOT {id} level={T.Level} t={T.Time:0}s items={dump["items"]!.AsArray().Count}");
        Done(id);
    }

    private void NotMade(string id, string title, string why)
    {
        if (!Wanted(id)) return;
        _shots.Add(new JsonObject { ["id"] = id, ["title"] = title, ["how"] = why, ["made"] = false });
        _notMade.Add(id);
        GD.Print($"UIAUDIT NOT MADE {id}: {why}");
        Done(id);
    }

    private void Done(string id)
    {
        if (_only == null) return;
        _only.Remove(id);
        if (_only.Count == 0) throw new StopTour();
    }

    private void WriteShots()
    {
        string outPath = System.IO.Path.Combine(_dir, "shots.json");
        var byId = new SortedDictionary<string, JsonNode>(StringComparer.Ordinal);
        if (_only != null && System.IO.File.Exists(outPath))
            foreach (var s in JsonNode.Parse(System.IO.File.ReadAllText(outPath))!["shots"]!.AsArray()) byId[(string)s!["id"]!] = s.DeepClone();
        foreach (var s in _shots) byId[(string)s!["id"]!] = s.DeepClone();
        var arr = new JsonArray();
        foreach (var v in byId.Values) arr.Add(v);
        var doc = new JsonObject
        {
            ["window"] = $"{GetWindow().Size.X}x{GetWindow().Size.Y}", ["scale"] = _scale, ["seed"] = 1,
            ["about"] = "Interface audit tour (tools/ui-audit.sh): one town, seed 1, played by the payback bot between states; states staged as described in 'how'.",
            ["shots"] = arr,
        };
        System.IO.File.WriteAllText(outPath, doc.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>Kinds of line on the shop now: affordable, short of coin, locked, finished.</summary>
    private (bool buy, bool coin, bool locked, bool done) ShopKinds()
    {
        bool buy = false, coin = false, locked = false, done = false;
        foreach (var it in Main.Hud.ShownItems)
        {
            if (it.Affordable) buy = true;
            else if (TownHud.IsLocked(it)) locked = true;
            else if (TownHud.IsFinished(it)) done = true;
            else coin = true;
        }
        return (buy, coin, locked, done);
    }
}
