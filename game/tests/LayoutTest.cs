using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// game-layout (night 3, layout sheet v2 section 8): the interface audit tour's states (u01..u41, same ids), each one
/// checked on the sheet's canvases instead of pictured: the HUD is built for each canvas (TownHud.ForceCanvas) and its
/// controls, found by their stable names, are held to the sheet. Rules: 1 zone rectangles, 2 no overlaps, 3 nothing cut
/// (also with the longest texts of LayoutFixture), 4 the middle free, 5 the attention budget (counted and printed, not
/// failed this round), 6 only the sheet's styles, 7 rows and key caps, 8 TipRect. Failures are collected and printed
/// (first 60) and fail the step at the end.
/// </summary>
public partial class LayoutTest : UiAudit
{
    protected override string AuditMarker => "LAYOUT PASS";

    /// <summary>The sheet's canvases: (canvas pixels, interface scale s) for 1920x1080 and 1280x720 windows at 80, 100
    /// and 150 percent, and 1366x768 and 2560x1440 windows at 100.</summary>
    public static readonly (Vector2 canvas, float s, string name)[] Canvases =
    {
        (new Vector2(2400, 1350), 1f, "1920x1080@80"), (new Vector2(1920, 1080), 1f, "1920x1080@100"), (new Vector2(1280, 720), 1f, "1920x1080@150"),
        (new Vector2(1600, 900), 0.78f, "1280x720@80"), (new Vector2(1280, 720), 0.78f, "1280x720@100"), (new Vector2(1280f / 1.5f, 480), 0.78f, "1280x720@150"),
        (new Vector2(1366, 768), 0.78f, "1366x768@100"), (new Vector2(2560, 1440), 1440f / 1080f, "2560x1440@100"),
    };

    private readonly List<string> _fails = new();
    private readonly SortedDictionary<string, int> _failKinds = new(StringComparer.Ordinal);
    private int _states, _checks, _maxLoud, _maxQuiet;
    private string _budgetNote = "";

    protected override Task? StateHook(string id) => CheckState(id);

    private void Fail(string rule, string id, string canvas, string what)
    {
        _failKinds[rule] = _failKinds.TryGetValue(rule, out int n) ? n + 1 : 1;
        if (_fails.Count < 400) _fails.Add($"{rule} {id} {canvas}: {what}");
    }

    private async Task CheckState(string id)
    {
        _states++;
        foreach (var (canvas, s, name) in Canvases)
        {
            Main.Hud.ForceCanvas(canvas, s);
            await Frames(3);
            LayoutRules.Check(Main, Main.Hud, new HudZones(canvas, s), id, name, Fail, ref _checks, out int loud, out int quiet, out string counted);
            if (loud > _maxLoud || quiet > _maxQuiet) _budgetNote = $"{id} {name}: {counted}";
            _maxLoud = Math.Max(_maxLoud, loud); _maxQuiet = Math.Max(_maxQuiet, quiet);
        }
        Main.Hud.ForceCanvas(null);
        await Frames(2);
        GD.Print($"LAYOUT state {id} checked on {Canvases.Length} canvases");
    }

    protected override async Task AfterTour()
    {
        // Rule 3 with the longest real text of every kind (LayoutFixture), on every canvas.
        foreach (var (canvas, s, name) in Canvases)
        {
            Main.Hud.ForceCanvas(canvas, s);
            await Frames(3);
            LayoutFixture.Apply(Main.Hud);
            await Frames(1);
            LayoutRules.CheckFit(Main.Hud, new HudZones(canvas, s), "fixture", name, Fail, ref _checks);
        }
        Main.Hud.ForceCanvas(null);
        await Frames(2);
        GD.Print($"LAYOUT budget (rule 5, reported only): most loud {_maxLoud} (limit 3), most quiet {_maxQuiet} (limit 4); worst: {_budgetNote}");
        foreach (var kv in _failKinds) GD.Print($"LAYOUT rule {kv.Key}: {kv.Value} failures");
        for (int i = 0; i < Math.Min(60, _fails.Count); i++) GD.Print("LAYOUT FAIL " + _fails[i]);
        Notes.Add($"states={_states} canvases={Canvases.Length} checks={_checks} failures={_fails.Count} loud<={_maxLoud} quiet<={_maxQuiet}");
        Check(_fails.Count == 0, $"{_fails.Count} layout failures (first: {(_fails.Count > 0 ? _fails[0] : "")})");
    }
}
