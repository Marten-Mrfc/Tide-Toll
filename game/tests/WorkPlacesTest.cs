using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// game-work-places (night 5, build sheet "How it is judged", the places' share): a first age played by the town bot in
/// the running game (speed <see cref="Speed"/>, the bot acting once a frame, every layer drawing), sampled every frame:
/// - a field never shows less than a third gold (what is drawn and what is meant), and its fronts move only in a frame
///   where this test, from the simulation's own views, sees work finished at that field, one step per unit;
/// - the mill's sails turn faster with two at work than with one than with none (measured from the drawn angle);
/// - the mill's door is open exactly while someone works or waits there (fully open after its swing, fully shut after);
/// - every delivery to the market is put on the table when the villager's arc lands (within a second; none missed);
/// - a cottage's door opens for its people;
/// - nothing changes in one frame: field pieces, the well's bucket, the mill's door and the cottage doors change by at
///   most the share a 0.15 s ease allows in one frame;
/// - the work places allocate nothing in a frame, but for the frames where a place is built or rebuilt;
/// - a town loaded (adopted) shows its fields lived in at once: part cut, at least a third gold, no growing in.
/// Run at 60 frames a second for the whole age and at 30 and 120 for the first part (--fps=N --short).
/// </summary>
public partial class WorkPlacesTest : TownTestBase
{
    protected override string Marker => "WORK PLACES PASS";
    public const double Speed = 16;
    private int _fps = 60;
    private bool _short;

    private partial class AllocProbe : Node
    {
        public long Mark, Bytes;
        public bool End;
        public AllocProbe? Begin;
        public override void _Process(double delta)
        {
            long now = GC.GetAllocatedBytesForCurrentThread();
            if (!End) Mark = now; else Bytes = now - Begin!.Mark;
        }
    }

    protected override async Task Run()
    {
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            if (a.StartsWith("--fps=") && int.TryParse(a.Substring(6), out int f)) _fps = f;
            if (a == "--short") _short = true;
        }
        var w = Main.Work;
        Check(w != null, "WorkPlaces is in the scene");
        // allocation probes either side of the work places in MapOrigin's process order
        var b = new AllocProbe { Name = "WorkAllocBegin" };
        var e = new AllocProbe { Name = "WorkAllocEnd", End = true, Begin = b };
        Main.MapOrigin.AddChild(b); Main.MapOrigin.MoveChild(b, w!.GetIndex());
        Main.MapOrigin.AddChild(e); Main.MapOrigin.MoveChild(e, w.GetIndex() + 1);

        var bot = new TownBot(BotKind.Payback, 1);
        Main.Driver.SetSpeed(Speed);
        await Frames(5);
        var t0 = T;
        int plots = t0.Plan.Plots.Length;
        int market = -1, mill = -1;
        for (int p = 0; p < plots; p++) { if (t0.Plan.Plots[p].Kind == PlotKind.Market) market = p; if (t0.Plan.Plots[p].Kind == PlotKind.Mill) mill = p; }

        var phase = new VillagerPhase[128];
        var wpOf = new int[128];
        for (int i = 0; i < 128; i++) wpOf[i] = -1;
        var steps = new int[plots];
        var unitsCum = new int[plots]; var unitsPrev = new int[plots]; var drawnSteps = new int[plots];
        var fronts = new int[plots * 8];
        for (int i = 0; i < fronts.Length; i++) fronts[i] = -2;
        float minGold = 1f, minDrawnGold = 1f, worstPiece = 0f, worstBucket = 0f, worstDoor = 0f, worstCottage = 0f;
        int frontMoves = 0, frontBad = 0, unitsSeen = 0, doorBad = 0, doorFrames = 0, marketDeliveries = 0, cottageOpens = 0, cottageBad = 0;
        float wantedFor = 0f, unwantedFor = 0f;
        bool lastWanted = false;
        var cottageNear = new float[plots];
        var rawBusy = new int[3];
        var marketRing = new int[_fps];
        int marketBad = 0;
        var sailSum = new double[3]; var sailN = new int[3];
        int busyHeld = -1; float busyFor = 0f; float lastSail = 0f; bool haveSail = false;
        long allocFrames = 0, allocMax = 0, frames = 0;
        int hoistSeenAt = -1, ropeSeenAt = -1;
        float dt = 1f / _fps;
        float ease = dt / WorkPlaces.MinEase + 1e-4f;
        int maxFrames = _short ? 30 * _fps * 4 : 60 * 60 * _fps;   // short: two minutes; else up to an hour of real frames at the rate

        // After the charter the town plays on at speed 1 for at most two minutes more, until the mill has been seen with
        // one at work long enough to measure (at speed 16 a lone miller is a moment of a few frames).
        int slowFrames = 0;
        while ((!T.HasCharter || (!_short && sailN[1] < 60 && slowFrames < 120 * _fps)) && frames < maxFrames)
        {
            if (T.HasCharter) { if (slowFrames++ == 0) Main.Driver.SetSpeed(1); }
            bot.Act(T);
            await Frames(1);
            frames++;
            var t = T;
            // ---- allocation (frames where a place's nodes are built are let through, and counted) ----
            if (frames > 10 && e.Bytes > 0) { allocFrames++; allocMax = Math.Max(allocMax, e.Bytes); }
            // ---- units of work finished, from the simulation's views ----
            Array.Clear(steps);
            Array.Copy(unitsCum, unitsPrev, plots);
            int n = Math.Min(t.VillagerCount, 128);
            for (int i = 0; i < n; i++)
            {
                var v = t.Villager(i);
                int wp = v.Phase == VillagerPhase.Working ? v.Workplace : -1;
                if (frames > 1 && phase[i] == VillagerPhase.Working && wpOf[i] >= 0 && wp != wpOf[i])
                {
                    var wv = t.Workplace(wpOf[i]);
                    steps[wv.Plot]++;
                    unitsCum[wv.Plot]++;
                    unitsSeen++;
                }
                phase[i] = v.Phase; wpOf[i] = wp;
            }
            // ---- fields ----
            for (int p = 0; p < plots; p++)
            {
                if (t.Plan.Plots[p].Kind != PlotKind.Field || !Main.Buildings.PlotDrawn(p)) continue;
                minGold = Math.Min(minGold, w.Fields.GoldShare(p));
                if (!w.Fields.GrowingIn(p)) minDrawnGold = Math.Min(minDrawnGold, w.Fields.DrawnGoldShare(p));
                int moved = 0;
                for (int r = 0; r < Math.Min(8, w.Fields.RowsAt(p)); r++)
                {
                    int f = w.Fields.FrontOf(p, r);
                    if (fronts[p * 8 + r] != -2 && f != fronts[p * 8 + r]) moved++;
                    fronts[p * 8 + r] = f;
                }
                // the drawing learns of a unit in the frame the simulation shows it or the next: never more steps than
                // units seen, never fewer than the units seen a frame before, and a front moves only with a step
                int fs = w.Fields.StepsAt(p);
                if (fs > unitsCum[p] || fs < unitsPrev[p]) frontBad++;
                if (moved > 0 && fs == drawnSteps[p]) frontBad++;
                drawnSteps[p] = fs;
                frontMoves += moved;
            }
            worstPiece = Math.Max(worstPiece, w.Fields.LastLargestChange);
            worstBucket = Math.Max(worstBucket, w.Well.LastLargestChange);
            // ---- the mill ----
            if (mill >= 0 && Main.Buildings.PlotDrawn(mill))
            {
                int wp = w.WorkplaceOf(mill);
                var mv = t.Workplace(wp);
                bool wanted = mv.Open && (mv.Busy > 0 || mv.QueueLength > 0);
                if (wanted != lastWanted) { wantedFor = 0; unwantedFor = 0; lastWanted = wanted; }
                if (wanted) wantedFor += dt; else unwantedFor += dt;
                doorFrames++;
                if (w.Mill.DoorWanted != wanted) doorBad++;
                if (wanted && wantedFor > MillWorks.DoorSeconds + 2 * dt && w.Mill.DoorOpen < 0.999f) doorBad++;
                if (!wanted && unwantedFor > MillWorks.DoorSeconds + 2 * dt && w.Mill.DoorOpen > 0.001f) doorBad++;
                worstDoor = Math.Max(worstDoor, w.Mill.LastDoorChange);
                // sails: the drawn angle's rate per busy count, once the count has held for longer than the rate takes to ease (0.15 s)
                int busy = Math.Min(2, mv.Busy);
                rawBusy[busy]++;
                if (busy != busyHeld) { busyHeld = busy; busyFor = 0; }
                busyFor += dt;
                float ang = Main.Buildings.SailAngle;
                if (haveSail && busyFor > WorkPlaces.MinEase + 2 * dt && !Main.Driver.Paused)
                {
                    float d = Mathf.Wrap(ang - lastSail, -MathF.PI, MathF.PI);
                    sailSum[busy] += d / dt / Math.Max(0.25f, w.Pace); sailN[busy]++;   // radians a second at game speed 1
                }
                lastSail = ang; haveSail = true;
                if (w.Mill.HoistShown && hoistSeenAt < 0) hoistSeenAt = t.ToolsLevel;
            }
            if (w.Well.SecondRope && ropeSeenAt < 0) ropeSeenAt = t.ToolsLevel;
            // ---- the market ----
            foreach (var ev in Main.Driver.Events) if (ev.Kind == TownEventKind.Delivery && ev.Plot == market) marketDeliveries++;
            // joined, a load is on the table when the villager's arc lands (WorkSites.DropSeconds after the delivery):
            // never more on the table than delivered, never fewer than were delivered a second ago
            marketRing[(int)(frames % marketRing.Length)] = marketDeliveries;
            int secondAgo = frames >= marketRing.Length ? marketRing[(int)((frames + 1) % marketRing.Length)] : 0;
            if (w.Stall.Received > marketDeliveries || w.Stall.Received < secondAgo) marketBad++;
            // ---- cottages ----
            for (int p = 0; p < plots; p++)
            {
                if (t.Plan.Plots[p].Kind != PlotKind.Cottage || !Main.Buildings.PlotDrawn(p)) continue;
                if (w.Doors.Wanted(p)) cottageNear[p] += dt; else cottageNear[p] = 0;
                if (cottageNear[p] > CottageDoors.DoorSeconds + 2 * dt && w.Doors.OpenOf(p) < 0.999f) cottageBad++;
                if (w.Doors.OpenOf(p) > 0.999f && cottageNear[p] > 0 && cottageNear[p] <= CottageDoors.DoorSeconds + 2 * dt + dt) cottageOpens++;
            }
            worstCottage = Math.Max(worstCottage, w.Doors.LastLargestChange);
        }
        if (!_short) Check(T.HasCharter, $"the first age reached the charter in {frames} frames (level {T.Level})");
        Notes.Add($"fps={_fps} frames={frames} after-charter={slowFrames} level={T.Level} tools={T.ToolsLevel}");
        Notes.Add($"field-gold-min={minGold:0.00} drawn={minDrawnGold:0.00} front-moves={frontMoves} units={unitsSeen} front-bad={frontBad}");
        Check(minGold >= 1f / 3f - 1e-4f, $"a field showed less than a third gold ({minGold:0.000})");
        Check(minDrawnGold >= 1f / 3f - 1e-4f, $"a field was drawn with less than a third gold ({minDrawnGold:0.000})");
        Check(frontBad == 0, $"fronts moved without work finished there ({frontBad} frames)");
        Check(frontMoves > 0, "no field front moved");
        Notes.Add($"door-bad={doorBad}/{doorFrames}");
        if (!_short || doorFrames > 0) Check(doorFrames > 0 || _short, "the mill was never drawn");
        Check(doorBad == 0, $"the mill's door was not open exactly while it should be ({doorBad} frames)");
        string Sail(int k) => sailN[k] > 0 ? (sailSum[k] / sailN[k]).ToString("0.000") : "-";
        Notes.Add($"sails={Sail(0)}/{Sail(1)}/{Sail(2)} frames={sailN[0]}/{sailN[1]}/{sailN[2]} raw={rawBusy[0]}/{rawBusy[1]}/{rawBusy[2]}");
        if (!_short)
        {
            Check(sailN[0] > 0 && sailN[1] > 0 && sailN[2] > 0, $"sails not seen with 0, 1 and 2 at work ({sailN[0]}, {sailN[1]}, {sailN[2]} frames)");
            Check(sailSum[2] / sailN[2] > sailSum[1] / sailN[1] && sailSum[1] / sailN[1] > sailSum[0] / sailN[0], $"sails not faster with more at work: {Sail(0)} / {Sail(1)} / {Sail(2)}");
        }
        Notes.Add($"market={marketDeliveries} table={w.Stall.Shown} missed={w.Stall.Missed} late={marketBad}");
        Check(marketBad == 0 && w.Stall.Shown == w.Stall.Received && w.Stall.Missed == 0,
            $"deliveries to the market not all on the table: {marketDeliveries} delivered, {w.Stall.Received} received, {w.Stall.Shown} shown, {w.Stall.Missed} missed, {marketBad} frames late");
        if (!_short) Check(marketDeliveries > 0, "no delivery to the market");
        Notes.Add($"cottage-open-frames={cottageOpens} cottage-bad={cottageBad} chimney-puffs={w.Doors.Puffs}");
        Check(cottageBad == 0, $"a cottage's door stayed shut with its people at it ({cottageBad} frames)");
        Check(cottageOpens > 0, "no cottage door opened for its people");
        Notes.Add($"one-frame: piece={worstPiece:0.000} bucket={worstBucket:0.000} door={worstDoor:0.000} cottage={worstCottage:0.000} bound={ease:0.000}");
        Check(worstPiece <= ease && worstBucket <= ease && worstDoor <= ease && worstCottage <= ease, "something changed faster than a 0.15 s ease allows");
        Notes.Add($"alloc-frames={allocFrames} alloc-max={allocMax}B hoist-at-tools={hoistSeenAt} rope-at-tools={ropeSeenAt}");
        Check(allocFrames <= frames / 100 + 12, $"the work places allocated in {allocFrames} of {frames} frames (max {allocMax} B)");
        if (!_short)
        {
            Check(hoistSeenAt >= MillWorks.HoistTools, $"the hoist showed at Tools {hoistSeenAt} (from {MillWorks.HoistTools})");
            Check(ropeSeenAt >= WellWorks.SecondRopeTools, $"the second rope showed at Tools {ropeSeenAt} (from {WellWorks.SecondRopeTools})");
        }
        // ---- a loaded town: fields lived in at once ----
        Main.Driver.SetPaused(true);   // held still, so what is meant and what is drawn are compared on the same moment
        Main.Driver.Adopt(T);
        await Frames(2);
        for (int p = 0; p < plots; p++)
        {
            if (T.Plan.Plots[p].Kind != PlotKind.Field || !Main.Buildings.PlotDrawn(p)) continue;
            float g = w.Fields.GoldShare(p), d = w.Fields.DrawnGoldShare(p);
            Notes.Add($"loaded-field{p}={g:0.00}");
            Check(g < 0.999f && g >= 1f / 3f, $"a loaded field does not look lived in ({g:0.00} gold)");
            Check(MathF.Abs(g - d) < 1e-3f, $"a loaded field grows in instead of standing ({d:0.00} drawn, {g:0.00} meant)");
        }
    }
}
