using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// The screenshot tour for the owner's review (tools/tour.sh): one town, the default (seed 1), played in play order
/// from the first minute to the borough seal, one picture per shot of docs/review/shots-plan.json at its moment.
/// Between pictures the simulation's best-payback bot (or real input) plays at once without waiting on frames; for
/// each picture the town runs at normal speed and the view settles. Writes &lt;out&gt;/&lt;id&gt;.png and &lt;out&gt;/shots.json
/// (the plan with made, file, shows and wrong per shot). Arguments after "--": --out-dir=DIR (default build/tour),
/// --plan=FILE, --only=id,id (retake those shots; stops after the last of them; merges into an existing shots.json).
/// Uses its own profile folder (TownTestBase), and every setting it changes is put back.
/// </summary>
public partial class Tour : TownTestBase
{
    protected override string Marker => "TOUR PASS";
    protected override Vector2I WindowSize => GetWindow().Size;

    private string _dir = "", _plan = "";
    private HashSet<string>? _only;
    private readonly Dictionary<string, (bool made, string why)> _made = new();
    private readonly Dictionary<string, string> _extraWrong = new();
    private TownBot _bot = null!;
    private sealed class StopTour : Exception { }

    protected override async Task Run()
    {
        _dir = ProjectSettings.GlobalizePath("res://../build/tour");
        _plan = ProjectSettings.GlobalizePath("res://../docs/review/shots-plan.json");
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            if (a.StartsWith("--out-dir=")) _dir = a[10..];
            else if (a.StartsWith("--plan=")) _plan = a[7..];
            else if (a.StartsWith("--only=")) _only = new HashSet<string>(a[7..].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
        System.IO.Directory.CreateDirectory(_dir);
        Check(System.IO.File.Exists(_plan), "no plan at " + _plan);
        if (Array.IndexOf(OS.GetCmdlineUserArgs(), "--notes-only") >= 0) { RewriteNotes(); return; }
        var size = GetWindow().Size;
        Check(size == new Vector2I(1920, 1080), $"the tour needs 1920 x 1080 (window is {size})");
        Check(Main.Driver.Seed == 1, "the default town is seed 1");
        _bot = new TownBot(BotKind.Payback, 1) { NoPave = true, NoBell = true };
        try
        {
            await FirstAge();
            await RiverAge();
            await BookAndMenus(); // last, as in the plan: the zoom, pausing and the settings on the finished borough
        }
        catch (StopTour) { Notes.Add("only: stopped after the last asked shot"); }
        WriteJson();
        int made = 0; foreach (var m in _made.Values) if (m.made) made++;
        Notes.Add($"made={made} notmade={_made.Count - made} level={T.Level} time={T.Time:0}s dir={_dir}");
    }

    // ---- playing between pictures ----

    /// <summary>The bot plays at once (no frames) until the condition holds; false when it did not within the cap.</summary>
    private async Task<bool> Fast(Func<Town, bool> until, double maxMinutes)
    {
        Main.Driver.RunBot(_bot, until, (int)(maxMinutes * 60 * TownRules.TickHz));
        await Frames(2); // the events reach the interface (banners, the follow view) before the view is set
        return until(T);
    }

    /// <summary>The town runs on by itself (no bot) at once until the condition holds.</summary>
    private async Task<bool> Idle(Func<Town, bool> until, double maxMinutes)
    {
        int max = (int)(maxMinutes * 60 * TownRules.TickHz), n = 0;
        while (n < max && !until(T)) { T.Step(); n++; }
        await Frames(2);
        return until(T);
    }

    /// <summary>Waits real time with the town at normal speed (frames go by).</summary>
    private async Task Wait(int ms)
    {
        var t0 = Time.GetTicksMsec();
        while (Time.GetTicksMsec() - t0 < (ulong)ms) await Frames(1);
    }

    /// <summary>Waits (real time, at most maxMs) for a condition, at normal speed.</summary>
    private async Task<bool> WaitFor(Func<bool> cond, int maxMs)
    {
        var t0 = Time.GetTicksMsec();
        while (!cond()) { if (Time.GetTicksMsec() - t0 > (ulong)maxMs) return false; await Frames(1); }
        return true;
    }

    // ---- the view ----

    /// <summary>Camera on a map point (cells, continuous: cell x spans x..x+1), held by hand.</summary>
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

    /// <summary>New roads drawn in, new buildings popped in, then a moment for the drawing (and any ease) to settle.</summary>
    private async Task Settle(int ms = 1500)
    {
        await Frames(2);
        await WaitFor(() =>
        {
            for (int r = 0; r < T.RoadCount; r++) if (T.RoadOpen(r) && Main.Ground.RoadReveal(r) < 1f) return false;
            return Main.Buildings.Popping == 0;
        }, 30000);
        await Wait(ms);
    }

    /// <summary>Banners and notes gone (they would cover the subject).</summary>
    private async Task<bool> Calm(int maxMs = 30000)
    {
        bool ok = await WaitFor(() => Main.Hud.BannerText == "", maxMs);
        return await WaitFor(() => Main.Hud.NotesShown == 0, 9000) && ok;
    }

    // ---- pictures ----

    private bool Wanted(string id) => _only == null || _only.Contains(id);

    private async Task Shot(string id)
    {
        if (!Wanted(id)) { NotMade(id, "", skipped: true); return; }
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var img = GetViewport().GetTexture().GetImage();
        string path = System.IO.Path.Combine(_dir, id + ".png");
        Check(img.SavePng(path) == Error.Ok, "could not save " + path);
        _made[id] = (true, "");
        GD.Print($"TOUR SHOT {id} level={T.Level} t={T.Time:0}s");
        if (_only != null) { _only.Remove(id); if (_only.Count == 0) throw new StopTour(); }
    }

    private void NotMade(string id, string why, bool skipped = false)
    {
        if (skipped) return;
        if (!Wanted(id)) return;
        _made[id] = (false, why);
        GD.Print($"TOUR NOT MADE {id}: {why}");
        if (_only != null) { _only.Remove(id); if (_only.Count == 0) throw new StopTour(); }
    }

    private void Wrong(string id, string note) => _extraWrong[id] = _extraWrong.TryGetValue(id, out var w) ? w + " " + note : note;

    /// <summary>Text of every visible label and button of the interface (to tell whether the game shows something).</summary>
    private List<string> ShownTexts()
    {
        var list = new List<string>();
        void Walk(Node n)
        {
            if (n is CanvasItem ci && !ci.IsVisibleInTree()) return;
            if (n is Label l && l.Text.Length > 0) list.Add(l.Text);
            else if (n is Button b && b.Text.Length > 0) list.Add(b.Text);
            else if (n is RichTextLabel rt && rt.Text.Length > 0) list.Add(rt.GetParsedText());
            foreach (var c in n.GetChildren()) Walk(c);
        }
        Walk(Main.Hud);
        return list;
    }

    private bool ShowsText(string needle)
    {
        foreach (var s in ShownTexts()) if (s.Contains(needle, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>--notes-only: the pictures stay; shots.json gets the notes of TourNotes.cs again (after looking).</summary>
    private void RewriteNotes()
    {
        string outPath = System.IO.Path.Combine(_dir, "shots.json");
        Check(System.IO.File.Exists(outPath), "--notes-only needs a shots.json from a run in " + _dir);
        var doc = JsonNode.Parse(System.IO.File.ReadAllText(outPath))!.AsObject();
        int n = 0;
        foreach (var sec in doc["sections"]!.AsArray())
            foreach (var shot in sec!["shots"]!.AsArray())
            {
                var o = shot!.AsObject();
                string id = (string)o["id"]!;
                if ((bool?)o["made"] != true) continue;
                Check(System.IO.File.Exists(System.IO.Path.Combine(_dir, id + ".png")), "missing picture " + id);
                if (TourNotes.Shows.TryGetValue(id, out var shows)) o["shows"] = shows;
                if (TourNotes.Wrong.TryGetValue(id, out var w)) o["wrong"] = w;
                n++;
            }
        System.IO.File.WriteAllText(outPath, doc.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        Notes.Add($"notes rewritten for {n} made shots");
    }

    private void WriteJson()
    {
        var plan = JsonNode.Parse(System.IO.File.ReadAllText(_plan))!.AsObject();
        string outPath = System.IO.Path.Combine(_dir, "shots.json");
        // With --only, shots not retaken keep what the last whole run wrote.
        var old = new Dictionary<string, JsonObject>();
        if (_only != null && System.IO.File.Exists(outPath))
            foreach (var sec in JsonNode.Parse(System.IO.File.ReadAllText(outPath))!["sections"]!.AsArray())
                foreach (var s in sec!["shots"]!.AsArray()) old[(string)s!["id"]!] = s.AsObject();
        int made = 0, all = 0;
        foreach (var sec in plan["sections"]!.AsArray())
            foreach (var shot in sec!["shots"]!.AsArray())
            {
                var o = shot!.AsObject();
                string id = (string)o["id"]!;
                all++;
                if (!_made.ContainsKey(id) && old.TryGetValue(id, out var prev))
                {
                    foreach (var k in new[] { "made", "file", "shows", "wrong", "why" })
                        if (prev[k] is { } v) o[k] = v.DeepClone();
                    if ((bool?)o["made"] == true) made++;
                    continue;
                }
                // A retake after a crash (--only, no shots.json yet): a picture already on disk counts as made.
                if (_only != null && !_made.ContainsKey(id) && System.IO.File.Exists(System.IO.Path.Combine(_dir, id + ".png"))) _made[id] = (true, "");
                bool ok = _made.TryGetValue(id, out var m) && m.made;
                if (ok) made++;
                o["made"] = ok;
                o["file"] = ok ? id + ".png" : null;
                if (ok && TourNotes.Shows.TryGetValue(id, out var shows)) o["shows"] = shows;
                string wrong = ok && TourNotes.Wrong.TryGetValue(id, out var w) ? w : "";
                if (ok && _extraWrong.TryGetValue(id, out var x)) wrong = (wrong + " " + x).Trim();
                o["wrong"] = wrong.Length > 0 ? wrong : null;
                if (!ok) o["why"] = _made.TryGetValue(id, out var nm) ? nm.why : "not reached in this run";
            }
        plan["about"] = (string)plan["about"]! + $" Made by tools/tour.sh: {made} of {all} shots.";
        System.IO.File.WriteAllText(outPath, plan.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        Notes.Add($"shots.json {made}/{all}");
    }
}
