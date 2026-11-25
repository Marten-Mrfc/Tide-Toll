using System;
using Godot;
using TideToll.Sim;

namespace TideToll.Game;

/// <summary>
/// The river half of <see cref="ToolController"/> (river rules on): the crossing tools (footbridge, ferry, drawbridge)
/// with their ghost, flow preview and reason in words, and working a drawbridge by hand: select it with a click (no
/// tool in hand), raise or lower it with a second click, the badge's lever or L, hire its keeper with the badge or K,
/// or work drawbridge 1 to 9 straight away with Ctrl+1 to 9.
/// </summary>
public partial class ToolController
{
    private string _crossingWhy = "";

    /// <summary>The drawbridge selected for the lever (crossing id), -1 none.</summary>
    public int SelectedBridge { get; private set; } = -1;
    /// <summary>Raised when the selected drawbridge changes.</summary>
    public event Action? SelectionChanged;
    /// <summary>Raised after a lever pull (true = raise) that the simulation took: for the bridge's sound and badge.</summary>
    public event Action<int, bool>? Lever;

    public bool RiverOn => W.River != null;

    /// <summary>The crossing a tool builds (the footbridge counts only with the river rules on).</summary>
    public static CrossingType? CrossingTool(Tool t) => t switch
    {
        Tool.Bridge => CrossingType.Footbridge,
        Tool.Ferry => CrossingType.Ferry,
        Tool.Drawbridge => CrossingType.Drawbridge,
        _ => null,
    };

    /// <summary>The hover ghost of a crossing tool at (x, y) in cells: its cells and preview, or a cross and the reason.</summary>
    private void CrossingHover(CrossingType t, float x, float y, int cx, int cy)
    {
        int n = W.Grid.Width;
        _bridgeCells.Clear();
        string? why = ToolTargets.CrossingReason(W, t, x, y);
        var change = W.CrossingWhatIf(t, x, y);
        if (change is { Crossing: { } cw })
        {
            _bridgeCells.AddRange(cw.Cells);
            bool afford = why == null;
            foreach (int c in _bridgeCells) Ghost.Cell(c % n, c / n, afford ? ToolGhost.Ok : ToolGhost.Bad, afford ? ToolGhost.OkColour : ToolGhost.BadColour);
            Previewer.Request(change.Value, FlowPreviewer.Key((char)('k' + (int)t), System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_bridgeCells)));
            _crossingWhy = why ?? "";
            Say(why ?? PriceHint(t));
        }
        else
        {
            Ghost.Cell(cx, cy, ToolGhost.Bad, ToolGhost.BadColour, 2f);
            Previewer.Clear();
            _crossingWhy = why ?? "Nothing can be built here.";
            Say(_crossingWhy);
        }
    }

    /// <summary>"Ferry: 40c to build, 5c a season. ..." for the status line while hovering a good spot.</summary>
    private string PriceHint(CrossingType t)
    {
        var r = W.River!.Rules;
        return t switch
        {
            CrossingType.Ferry => $"Ferry: {r.FerryPrice:0}c, then {r.FerryUpkeep + r.FerryWage:0}c a season. Click to build. Walkers wait for the raft; every boat passes.",
            CrossingType.Drawbridge => $"Drawbridge: {r.DrawbridgePrice:0}c, then {r.DrawbridgeUpkeep:0}c a season. Click to build. Barges pass only while it is raised.",
            _ => $"Footbridge: {r.FootbridgePrice:0}c. Click to build. Quick for walkers, but masted barges cannot pass under it.",
        };
    }

    private void CrossingUp(CrossingType t, Vector2 pos)
    {
        int down = _bridgeDownCell;
        _bridgeDownCell = -1;
        if (PointerOverUi || !_main.Rig.PickLocal(pos, out var l)) { Say($"{Cap(World.CrossingName(t))} cancelled."); return; }
        float x = l.X / CellM, y = l.Z / CellM;
        var change = W.CrossingWhatIf(t, x, y);
        if (change is not { Crossing: { } cw } || cw.Cells.Length == 0 || cw.Cells[0] != down) { Say($"{Cap(World.CrossingName(t))} cancelled."); return; }
        var res = Sim.PlaceCrossing(t, (int)MathF.Floor(x), (int)MathF.Floor(y));
        if (res.Ok)
        {
            SayBuilt(res.Reason + (t == CrossingType.Drawbridge ? ". Click it (no tool in hand) or press L to raise it for a barge." : "."));
            Placed?.Invoke(true);
        }
        else { Say(res.Reason + "."); Refused?.Invoke(); }
    }

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    // ------------------------------------------------------------------ working a drawbridge

    /// <summary>The drawbridge at or next to a cell (its deck, ends or lift), -1 none.</summary>
    public int DrawbridgeAt(int cx, int cy)
    {
        var r = W.River;
        if (r == null) return -1;
        int n = W.Grid.Width;
        var xs = r.Crossings;
        for (int i = 0; i < xs.Count; i++)
        {
            var x = xs[i];
            if (x.Type != CrossingType.Drawbridge || Math.Abs(cy - x.Row) > 2) continue;
            int x0 = x.WestEnd % n - 1, x1 = x.EastEnd % n;
            if (cx >= x0 && cx <= x1) return x.Id;
        }
        return -1;
    }

    /// <summary>No tool in hand: a click on a drawbridge selects it, a click on the selected one works its lever.</summary>
    private bool ClickBridge(Vector2 pos)
    {
        if (!RiverOn || !_main.Rig.PickCell(pos, out int cx, out int cy)) return false;
        int id = DrawbridgeAt(cx, cy);
        if (id < 0)
        {
            if (SelectedBridge >= 0) Select(-1);
            return false;
        }
        if (id == SelectedBridge) PullLever(id);
        else
        {
            Select(id);
            Say("Drawbridge selected: click it again, use the lever on its badge or press L to raise or lower it. K hires a keeper.");
        }
        return true;
    }

    public void Select(int id)
    {
        if (id == SelectedBridge) return;
        SelectedBridge = id;
        SelectionChanged?.Invoke();
    }

    /// <summary>The drawbridge L and K work: the selected one, else the first.</summary>
    public int LeverTarget()
    {
        var r = W.River;
        if (r == null) return -1;
        var xs = r.Crossings;
        int first = -1;
        for (int i = 0; i < xs.Count; i++)
        {
            if (xs[i].Type != CrossingType.Drawbridge) continue;
            if (xs[i].Id == SelectedBridge) return SelectedBridge;
            if (first < 0) first = xs[i].Id;
        }
        return first;
    }

    /// <summary>The n-th drawbridge (1-based, in building order), -1 none.</summary>
    public int NthBridge(int k)
    {
        var r = W.River;
        if (r == null) return -1;
        var xs = r.Crossings;
        for (int i = 0; i < xs.Count; i++) if (xs[i].Type == CrossingType.Drawbridge && --k == 0) return xs[i].Id;
        return -1;
    }

    /// <summary>Raises a drawbridge that is down (or lowering), lowers one that is up (or rising). Says what happened.</summary>
    public bool PullLever(int id)
    {
        Crossing? x = Find(id);
        if (x == null) { Say("Build a drawbridge first: the Drawbridge bookmark, then click on the river."); Refused?.Invoke(); return false; }
        bool raise = x.State is BridgeState.Closed or BridgeState.Lowering;
        // Only a raise that lets a waiting barge through counts toward the keeper (the library counts it): a raise with
        // no boat waiting says so.
        bool noCount = raise && !x.Keeper && x.RaisesToKeeper > 0 && x.BoatsWaiting == 0;
        var res = Sim.SetBridge(id, raise);
        if (!res.Ok) { Say(res.Reason + "."); Refused?.Invoke(); return false; }
        Say(raise ? "Raising the drawbridge: barges may pass, walkers wait at the barriers." + (noCount ? " " + BridgeBadges.NoCountText + " toward a keeper." : "")
            : "Lowering the drawbridge: walkers may cross again.");
        Lever?.Invoke(id, raise);
        return true;
    }

    public bool HireKeeper(int id)
    {
        var res = Sim.HireKeeper(id);
        Say(res.Ok ? res.Reason + ". The keeper raises the bridge for each barge and lowers it once it is through." : res.Reason + ".");
        if (res.Ok) Placed?.Invoke(true); else Refused?.Invoke();
        return res.Ok;
    }

    private Crossing? Find(int id)
    {
        var r = W.River;
        if (r == null || id < 0) return null;
        var xs = r.Crossings;
        for (int i = 0; i < xs.Count; i++) if (xs[i].Id == id) return xs[i];
        return null;
    }

    /// <summary>L, K and Ctrl+1..9 (the interface forwards them; true when taken).</summary>
    public bool RiverKey(Key key, bool ctrl)
    {
        if (!RiverOn) return false;
        if (ctrl && key >= Key.Key1 && key <= Key.Key9)
        {
            int id = NthBridge((int)(key - Key.Key0));
            if (id < 0) { Say($"There is no drawbridge {key - Key.Key0}."); return true; }
            PullLever(id);
            return true;
        }
        if (ctrl) return false;
        if (key == Key.L) { PullLever(LeverTarget()); return true; }
        if (key == Key.K)
        {
            int id = LeverTarget();
            if (id < 0) Say("Build a drawbridge first: only a drawbridge has a keeper.");
            else HireKeeper(id);
            return true;
        }
        return false;
    }

    /// <summary>After an edit or a new world: forget a selection whose bridge is gone.</summary>
    private void CheckSelection()
    {
        if (SelectedBridge >= 0 && Find(SelectedBridge) == null) Select(-1);
    }
}
