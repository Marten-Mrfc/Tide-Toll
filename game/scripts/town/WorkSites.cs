using System;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Work you can watch (n5): what a place answers to. The places' builder registers one per plot
/// (<see cref="WorkSites.Register"/>); the villagers call it. Every call is made from a frame's _Process, at most a few
/// a frame; none may allocate. Positions are map-local metres (as the villagers and buildings are drawn: a map cell
/// <c>x</c> is <c>x * TownGround.Cell</c>), heights in metres above the ground.
/// </summary>
public interface IWorkSite
{
    /// <summary>
    /// A unit of work was finished here: villager <paramref name="villager"/> at spot <paramref name="slot"/> of the
    /// plot takes up <paramref name="good"/> now (its load rises from <see cref="WorkSites.Goods"/> into the arms over
    /// <see cref="WorkSites.TakeSeconds"/>). The well's bucket tips, a field's front moves on, the mill's flour puff.
    /// </summary>
    void WorkFinished(int villager, int slot, Good good);
    /// <summary>A villager began work at spot <paramref name="slot"/> (the mill: the grain sack goes in at the door).</summary>
    void WorkStarted(int villager, int slot, Good carried);
    /// <summary>
    /// A load arrived here at the end of its arc from the villager's arms (the market's table, a cottage's door): the
    /// place takes it over from this moment (sets it on the table, the door shuts behind it). Never called for a good
    /// the simulation did not deliver.
    /// </summary>
    void Delivered(int villager, Good good, int amount);
    /// <summary>
    /// The places' builder may move a work spot (a field's worker to the front of their row): return true and the spot
    /// (map cells, cell centres at +0.5) and the heading the worker faces (atan2(dy, dx) on the map, y south); false keeps
    /// <see cref="WorkSites.WorkSpot"/>'s default.
    /// </summary>
    bool Spot(int slot, out float x, out float y, out float heading);
    /// <summary>Likewise for where the goods of spot <paramref name="slot"/> stand (true: use <paramref name="at"/>).</summary>
    bool GoodsAt(int slot, out Vector3 at);
}

/// <summary>
/// The shared interface between the villagers (TownVillagers*.cs) and the places (Work*.cs): for a workplace, where in
/// the drawing its work spots, its line and its goods are, and the calls "a unit of work was finished here" and "a load
/// arrived here". Defaults come from the plot and the simulation's own spots; a registered <see cref="IWorkSite"/> may
/// refine the spots and goods. Allocation-free.
/// </summary>
public static class WorkSites
{
    public const float Cell = TownGround.Cell;
    /// <summary>The gap between two people in a line, metres: without a handcart, and with one (Load step 4).</summary>
    public const float LineStep = 2.0f, LineStepCart = 2.3f;
    /// <summary>Real seconds a load takes to rise from the goods into the arms, and to fly from the arms to the table or door.</summary>
    public const float TakeSeconds = 0.35f, DropSeconds = 0.45f;

    private static readonly IWorkSite?[] Sites = new IWorkSite?[128];

    /// <summary>Registers the place drawn on plot <paramref name="plot"/> (null removes it). A new town clears none: re-register on rebuild.</summary>
    public static void Register(int plot, IWorkSite? site) { if (plot >= 0 && plot < Sites.Length) Sites[plot] = site; }
    public static IWorkSite? Site(int plot) => plot >= 0 && plot < Sites.Length ? Sites[plot] : null;
    public static void Clear() => Array.Clear(Sites);

    /// <summary>Counters for tests: units finished, deliveries landed, since start.</summary>
    public static long FinishedCount { get; private set; }
    public static long DeliveredCount { get; private set; }

    public static void Finished(int plot, int villager, int slot, Good good) { FinishedCount++; Site(plot)?.WorkFinished(villager, slot, good); }
    public static void Started(int plot, int villager, int slot, Good carried) => Site(plot)?.WorkStarted(villager, slot, carried);
    public static void Delivered(int plot, int villager, Good good, int amount) { DeliveredCount++; Site(plot)?.Delivered(villager, good, amount); }

    /// <summary>Unit vector from the plot's centre to its door (map cells), and the centre.</summary>
    public static void DoorDir(Plot p, out float cx, out float cy, out float dx, out float dy)
    {
        cx = p.X + p.W / 2f; cy = p.Y + p.H / 2f;
        dx = p.DoorX + 0.5f - cx; dy = p.DoorY + 0.5f - cy;
        float l = MathF.Sqrt(dx * dx + dy * dy);
        if (l < 1e-6f) { dx = 0; dy = 1; l = 1; }
        dx /= l; dy /= l;
    }

    /// <summary>
    /// The simulation's spot nearest to a villager's position: which work spot (slot) it holds. The simulation stands a
    /// worker exactly on <see cref="Town.WorkSpot"/>, so this is exact for workers.
    /// </summary>
    public static int SlotOf(Town t, int plot, double x, double y)
    {
        int best = 0; double bd = double.MaxValue;
        for (int s = 0; s < 8; s++)
        {
            t.WorkSpot(plot, s, out double sx, out double sy, out _);
            double d = (sx - x) * (sx - x) + (sy - y) * (sy - y);
            if (d < bd - 1e-9) { bd = d; best = s; }
        }
        return best;
    }

    /// <summary>
    /// Where a worker at spot <paramref name="slot"/> is drawn (map cells) and the heading it faces. The well and a
    /// field: the simulation's own spot (on the rim at the crank side; along its row). The mill: on the step in the
    /// doorway, side by side, facing in (the simulation's spot is inside the walls, where nobody can see the work).
    /// </summary>
    public static void WorkSpot(Town t, int plot, int slot, out float x, out float y, out float heading)
    {
        if (Site(plot) is { } s && s.Spot(slot, out x, out y, out heading)) return;
        var p = t.Plan.Plots[plot];
        if (p.Kind == PlotKind.Mill)
        {
            DoorDir(p, out _, out _, out float dx, out float dy);
            float side = (slot % 2 == 0 ? 1 : -1) * 0.5f * ((slot + 2) / 2);
            float ex = p.DoorX + 0.5f - dx * 0.62f, ey = p.DoorY + 0.5f - dy * 0.62f;
            x = ex - dy * side; y = ey + dx * side;
            heading = MathF.Atan2(-dy, -dx);
            return;
        }
        t.WorkSpot(plot, slot, out double wx, out double wy, out double h);
        x = (float)wx; y = (float)wy; heading = (float)h;
    }

    /// <summary>
    /// Where the goods of spot <paramref name="slot"/> stand before they are taken up (map-local metres, with height):
    /// the well's rim before the worker, a field's stook just behind its worker in the row, the mill's step beside the
    /// door (flour on one side), the stall's table, a cottage's door. For the market and a cottage <paramref name="slot"/>
    /// is ignored.
    /// </summary>
    public static Vector3 Goods(Town t, int plot, int slot)
    {
        if (Site(plot) is { } s && s.GoodsAt(slot, out var at)) return at;
        var p = t.Plan.Plots[plot];
        DoorDir(p, out float cx, out float cy, out float dx, out float dy);
        switch (p.Kind)
        {
            case PlotKind.Well:
            {
                WorkSpot(t, plot, slot, out float x, out float y, out _);
                return new Vector3((cx + (x - cx) * 0.62f) * Cell, 0.85f, (cy + (y - cy) * 0.62f) * Cell);
            }
            case PlotKind.Field:
            {
                WorkSpot(t, plot, slot, out float x, out float y, out float h);
                return new Vector3((x - MathF.Cos(h) * 0.55f) * Cell, 0.35f, (y - MathF.Sin(h) * 0.55f + 0.3f) * Cell);
            }
            case PlotKind.Mill:
            {
                // The flour side of the step: beyond the last worker's side, at the door.
                float ex = p.DoorX + 0.5f - dx * 0.35f, ey = p.DoorY + 0.5f - dy * 0.35f;
                float side = (slot % 2 == 0 ? 1 : -1) * 1.15f;
                return new Vector3((ex - dy * side) * Cell, 0.2f, (ey + dx * side) * Cell);
            }
            case PlotKind.Market:
                return new Vector3((p.DoorX + 0.5f - dx * 1.8f) * Cell, 0.9f, (p.DoorY + 0.5f - dy * 1.8f) * Cell);
            default: // a cottage (and anything else): its door
                return new Vector3((p.DoorX + 0.5f - dx * 0.7f) * Cell, 0.9f, (p.DoorY + 0.5f - dy * 0.7f) * Cell);
        }
    }

    /// <summary>The doorway of a plot (map-local metres, at the threshold): where the mill's grain sack goes in.</summary>
    public static Vector3 Doorway(Town t, int plot)
    {
        var p = t.Plan.Plots[plot];
        DoorDir(p, out _, out _, out float dx, out float dy);
        return new Vector3((p.DoorX + 0.5f - dx * 1.0f) * Cell, 0.5f, (p.DoorY + 0.5f - dy * 1.0f) * Cell);
    }

    /// <summary>The point the line faces from its front: the first work spot (map cells).</summary>
    public static void LineHead(Town t, int plot, out float x, out float y)
    {
        var p = t.Plan.Plots[plot];
        if (p.Kind == PlotKind.Field) { x = p.X + p.W / 2f; y = p.Y + p.H / 2f; return; }
        WorkSpot(t, plot, 0, out x, out y, out _);
    }

    /// <summary>
    /// The lane a workplace's line runs along: the road at its door node whose first queue spot is nearest the
    /// simulation's spot for place 0 (the lane the first in line came by), or the node's first open road.
    /// </summary>
    public static int Lane(Town t, int workplace)
    {
        var plan = t.Plan;
        int node = plan.Plots[t.Workplace(workplace).Plot].Node;
        var roads = plan.Nodes[node].Roads;
        if (roads.Length == 0) return -1;
        if (t.Workplace(workplace).QueueLength == 0) { for (int k = 0; k < roads.Length; k++) if (t.RoadOpen(roads[k])) return roads[k]; return roads[0]; }
        t.QueueSpot(workplace, 0, out double qx, out double qy, out _);
        int best = roads[0]; double bd = double.MaxValue;
        for (int k = 0; k < roads.Length; k++)
        {
            var rd = plan.Roads[roads[k]];
            double back = Math.Min(rd.Length, TownRules.QueueSpacing);
            rd.At(rd.A == node ? back : rd.Length - back, out double x, out double y, out _);
            double d = (x - qx) * (x - qx) + (y - qy) * (y - qy);
            if (d < bd) { bd = d; best = roads[k]; }
        }
        return best;
    }

    /// <summary>
    /// A point <paramref name="dist"/> cells back along a workplace's line (from its door node, along
    /// <paramref name="road"/>, and on along the straightest open road at its far end when the line is longer than the
    /// lane): map cells, and the heading back toward the front of the line (atan2(dy, dx)).
    /// </summary>
    public static void LinePoint(Town t, int workplace, int road, float dist, out float x, out float y, out float heading)
    {
        var plan = t.Plan;
        int node = plan.Plots[t.Workplace(workplace).Plot].Node;
        if (road < 0) { var nd = plan.Nodes[node]; x = nd.X + 0.5f; y = nd.Y + 0.5f; heading = 0; return; }
        double left = Math.Max(0, dist);
        for (int leg = 0; leg < 4; leg++)
        {
            var rd = plan.Roads[road];
            bool fromA = rd.A == node;
            if (left <= rd.Length || leg == 3)
            {
                double d = Math.Min(left, rd.Length);
                rd.At(fromA ? d : rd.Length - d, out double px, out double py, out double h);
                x = (float)px; y = (float)py;
                heading = (float)(fromA ? h + Math.PI : h);
                return;
            }
            left -= rd.Length;
            // On along the straightest open road from the far end.
            int far = fromA ? rd.B : rd.A;
            rd.At(fromA ? rd.Length : 0, out _, out _, out double hEnd);
            double dirOut = fromA ? hEnd : hEnd + Math.PI;
            int next = -1; double bestTurn = double.MaxValue;
            var rs = plan.Nodes[far].Roads;
            for (int k = 0; k < rs.Length; k++)
            {
                if (rs[k] == road || !t.RoadOpen(rs[k])) continue;
                var r2 = plan.Roads[rs[k]];
                bool a2 = r2.A == far;
                r2.At(a2 ? 0 : r2.Length, out _, out _, out double h2);
                double dir2 = a2 ? h2 : h2 + Math.PI;
                double turn = Math.Abs(Math.IEEERemainder(dir2 - dirOut, 2 * Math.PI));
                if (turn < bestTurn) { bestTurn = turn; next = rs[k]; }
            }
            if (next < 0)
            {
                rd.At(fromA ? rd.Length : 0, out double ex, out double ey, out double he);
                x = (float)ex; y = (float)ey; heading = (float)(fromA ? he + Math.PI : he);
                return;
            }
            node = far; road = next;
        }
        x = 0; y = 0; heading = 0;
    }
}
