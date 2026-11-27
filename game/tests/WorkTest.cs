using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// game-work (n5, work you can watch: the villagers' checks of docs/plans/n5/work-build-sheet.md, "How it is judged").
/// A real town: the payback bot plays to the middle of the first age (the mill and the market open), then the town runs
/// on frame by frame with nobody buying, at 1x and then at 4x, with the bell rung; later the bot plays on to a handcart
/// and the scythe and the town runs again. Every frame (run with --fixed-fps 30, 60 and 120 by tools/work-check.sh):
/// <list type="bullet">
/// <item>the line: two neighbours in a line who have reached their places stand 1.6 to 2.4 m apart, in queue order,
/// each facing the workplace (the one ahead of them), none inside a building's or a field's footprint;</item>
/// <item>no load on a villager whose work is not finished (nothing in the arms while waiting or working, the mill's
/// grain aside), no load appearing or leaving in one frame (<see cref="TownVillagers.Pops"/>), and every delivery
/// lands on its table or door (<see cref="WorkSites.DeliveredCount"/>);</item>
/// <item>nothing changes in one frame: no pose changes faster than <see cref="MaxPoseRate"/>, no villager jumps;</item>
/// <item>D: reapers hold the sickle, the scythe from the Tools milestone; a handcart stands parked by its owner at work;
/// the bell lifts heads and quickens the rhythm;</item>
/// <item>allocation: the villagers' node allocates nothing per frame; the whole frame stays inside game-perf's 1x gate.</item>
/// </list>
/// </summary>
public partial class WorkTest : TownTestBase
{
    protected override string Marker => "WORK PASS";
    /// <summary>The fastest a pose may change (radians per second, lean + swing + crouch): an eased change of the
    /// largest pose (0.6 rad) over 0.15 s is 4 rad/s, the scythe's swing at the bell's tempo about 8; a change in one
    /// frame is 0.3 rad or more, 9 rad/s even at 30 fps and 36 at 120.</summary>
    public const float MaxPoseRate = 12f;
    private int _fps = 60;
    private long _pairs, _faced, _outside, _frames, _loadsChecked, _reapers, _parked;
    private int _deliveries;
    private float _worstGap = 0, _bestGap = 99, _worstRate, _worstStep;

    private partial class AllocProbe : Node
    {
        public long Mark, Sum, Frames;
        public AllocProbe? Begin;
        public override void _Process(double delta)
        {
            long now = GC.GetAllocatedBytesForCurrentThread();
            if (Begin == null) Mark = now;
            else { Sum += now - Begin.Mark; Frames++; }
        }
    }

    protected override async Task Run()
    {
        foreach (var a in OS.GetCmdlineUserArgs()) if (a.StartsWith("--fps=")) _fps = int.Parse(a.Substring(6));
        var bot = new TownBot(BotKind.Payback, 1);
        Main.Driver.SetPaused(true);
        Main.Driver.RunBot(bot, t => Open(t, PlotKind.Mill) && Open(t, PlotKind.Market) && t.VillagerCount >= 10, 20 * 60 * 40);
        Check(Open(T, PlotKind.Mill) && Open(T, PlotKind.Market), "the town reached the mill and the market");
        Main.Driver.Advance(20);
        Drain();
        // Probes round the villagers' node: what it allocates per frame.
        var vil = Main.Villagers;
        var parent = vil.GetParent();
        var begin = new AllocProbe { Name = "WorkAllocBegin" };
        var end = new AllocProbe { Name = "WorkAllocEnd" };
        end.Begin = begin;
        parent.AddChild(begin); parent.MoveChild(begin, vil.GetIndex());
        parent.AddChild(end); parent.MoveChild(end, vil.GetIndex() + 1);
        Main.Driver.SetSpeed(1);
        Main.Driver.SetPaused(false);
        await Frames(10);
        long d0 = WorkSites.DeliveredCount;
        int inFlight0 = 0;
        for (int i = 0; i < Math.Min(T.VillagerCount, TownVillagers.Capacity); i++) if (vil.Dropping(i)) inFlight0++;
        int pops0 = vil.Pops;
        begin.Sum = end.Sum = 0; end.Frames = 0;
        long g0 = GC.GetAllocatedBytesForCurrentThread(); long f0 = _frames;
        await Watch(45);
        long perFrame = (GC.GetAllocatedBytesForCurrentThread() - g0) / Math.Max(1, _frames - f0);
        long villagerBytes = end.Sum / Math.Max(1, end.Frames);
        // The bell: heads up for a moment, a quicker rhythm while it rings.
        if (!T.BellReady) { Main.Driver.SetPaused(true); Main.Driver.RunBot(new TownBot(BotKind.Payback, 1) { NoBell = true }, t => t.BellReady, 20 * 60 * 5); Drain(); Main.Driver.SetPaused(false); }
        Check(T.RingBell(), "the bell rang");
        float heads = 0, tempo = 0;
        for (int f = 0; f < _fps * 2; f++) { await Frames(1); heads = Math.Max(heads, vil.HeadsUp); if (T.BellRinging) tempo = Math.Max(tempo, vil.Tempo); Sample(); }
        Check(heads > 0.9f, $"the bell lifts the workers' heads (most {heads:0.00})");
        Check(tempo >= (float)TownRules.BellHurry - 1e-3f, $"the bell quickens the rhythm (tempo {tempo:0.00})");
        Main.Driver.SetSpeed(4);
        await Watch(30);
        Main.Driver.SetSpeed(1);
        // D: a handcart and the scythe (the bot plays on and buys them), then the town runs again at 1x.
        Main.Driver.SetPaused(true);
        Main.Driver.RunBot(bot, t => (TownVillagers.CartAt(t.LoadLevel) && TownVillagers.ScytheAt(t.ToolsLevel)) || t.HasCharter, 20 * 60 * 40);
        Main.Driver.Advance(10);
        Drain();
        Main.Driver.SetPaused(false);
        await Watch(25);
        // Every load that left the arms has landed (give the last ones their arc).
        for (int f = 0; f < _fps; f++) { await Frames(1); }
        Main.Driver.SetPaused(true);
        for (int f = 0; f < _fps; f++) await Frames(1);
        long landed = WorkSites.DeliveredCount - d0;
        Check(_deliveries > 10, $"deliveries seen ({_deliveries})");
        Check(landed >= _deliveries && landed <= _deliveries + inFlight0, $"every delivery lands on its table or door ({landed} landed of {_deliveries}, {inFlight0} on the way at the start)");
        Check(vil.Pops - pops0 == 0, $"no load appears or vanishes in one frame ({vil.Pops - pops0})");
        Check(_pairs > 40, $"enough neighbours in lines checked ({_pairs})");
        Check(_outside == 0, $"nobody in a line stands inside a building or a field ({_outside})");
        Check(_reapers > 0, $"reapers seen with their tool ({_reapers})");
        Check(villagerBytes <= 16, $"the villagers allocate nothing per frame ({villagerBytes} B)");
        Check(perFrame < Perf.AllocGate1x, $"a frame allocates under game-perf's 1x gate ({perFrame} B, gate {Perf.AllocGate1x})");
        Notes.Add($"fps={_fps} frames={_frames} pairs={_pairs} gap={_bestGap:0.00}-{_worstGap:0.00}m faced={_faced} loads={_loadsChecked} deliveries={_deliveries} reapers={_reapers} parked={_parked} " +
                  $"poserate={_worstRate:0.0} step={_worstStep:0.000} bytes={perFrame} villagerbytes={villagerBytes} tools={T.ToolsLevel} load={T.LoadLevel}");
    }

    /// <summary>Events of a stretch played without frames (the bot) are dropped: no frame drew those villagers.</summary>
    private readonly System.Collections.Generic.List<TownEvent> _scratch = new(4096);
    private void Drain() { _scratch.Clear(); T.DrainEvents(_scratch); _scratch.Clear(); }

    private static bool Open(Town t, PlotKind k)
    {
        for (int i = 0; i < t.WorkplaceCount; i++) { var w = t.Workplace(i); if (w.Open && w.Kind == k) return true; }
        if (k == PlotKind.Market) foreach (var p in t.Plan.Plots) if (p.Kind == k && t.PlotWorking(p.Id)) return true;
        return false;
    }

    private async Task Watch(double gameSeconds)
    {
        double until = T.Time + gameSeconds;
        for (int f = 0; f < 200000 && T.Time < until; f++) { await Frames(1); Sample(); }
    }

    private void Sample()
    {
        _frames++;
        var vil = Main.Villagers;
        var t = T;
        var ev = Main.Driver.Events;
        for (int e = 0; e < ev.Count; e++) if (ev[e].Kind == TownEventKind.Delivery && ev[e].Villager < TownVillagers.Capacity) _deliveries++;
        _worstRate = Math.Max(_worstRate, vil.MaxPoseRate);
        Check(vil.MaxPoseRate <= MaxPoseRate, $"a pose changed in one frame: {vil.MaxPoseRate:0.0} rad/s at {_fps} fps (bound {MaxPoseRate})");
        if (Main.Driver.Speed <= 1)
        {
            _worstStep = Math.Max(_worstStep, vil.MaxStepCells);
            Check(vil.MaxStepCells < 0.5f, $"a villager jumped {vil.MaxStepCells:0.00} cells in one frame");
        }
        int n = Math.Min(t.VillagerCount, TownVillagers.Capacity);
        for (int i = 0; i < n; i++)
        {
            var v = t.Villager(i);
            var shown = vil.LoadShown(i);
            _loadsChecked++;
            // No load before the work is finished: nothing in the arms while waiting or working but what was brought.
            if (v.Phase is VillagerPhase.Queued or VillagerPhase.Working && v.Workplace >= 0)
                Check(shown == Good.None || shown == v.Carry, $"villager {i} drawn with {shown} while {v.Phase} (carrying {v.Carry})");
            if (v.Phase == VillagerPhase.Working && v.Workplace >= 0)
            {
                var wp = t.Workplace(v.Workplace);
                Check(wp.Kind == PlotKind.Mill || shown == Good.None, $"villager {i} at work at the {wp.Kind} drawn with {shown}");
                if (wp.Kind == PlotKind.Field && vil.PoseWeight(i) > 0.9f)
                {
                    Check(vil.ToolOf(i) == (TownVillagers.ScytheAt(t.ToolsLevel) ? 2 : 1), $"reaper {i} holds the {(TownVillagers.ScytheAt(t.ToolsLevel) ? "scythe" : "sickle")} (tool {vil.ToolOf(i)}, Tools {t.ToolsLevel})");
                    _reapers++;
                }
                if (TownVillagers.CartAt(t.LoadLevel) && vil.PoseWeight(i) > 0.99f) { Check(vil.Parked(i) > 0.9f, $"worker {i}'s handcart stands parked ({vil.Parked(i):0.00})"); _parked++; }
            }
        }
        if (Main.Driver.Speed > 1) return;
        // The line.
        var plots = t.Plan.Plots;
        for (int w = 0; w < t.WorkplaceCount; w++)
        {
            var wp = t.Workplace(w);
            if (wp.Kind is not (PlotKind.Well or PlotKind.Field or PlotKind.Mill)) continue;
            for (int p = 0; p < wp.QueueLength; p++)
            {
                int me = t.QueuedVillager(w, p);
                if (me >= TownVillagers.Capacity || !vil.LineSettled(me)) continue;
                var at = vil.DrawnAt(me);
                for (int k = 0; k < plots.Length; k++)
                {
                    var q = plots[k];
                    if (!t.PlotGiven(q.Id) || q.Kind is PlotKind.BellGreen) continue;
                    if (at.X > q.X + 0.1f && at.X < q.X + q.W - 0.1f && at.Y > q.Y + 0.1f && at.Y < q.Y + q.H - 0.1f)
                    { _outside++; Check(false, $"villager {me}, place {p} at the {wp.Kind}, stands inside plot {q.Id} ({q.Kind}) at {at}"); }
                }
                Vector2 toward;
                if (p == 0) { WorkSites.LineHead(t, wp.Plot, out float hx, out float hy); toward = new Vector2(hx, hy) - at; }
                else
                {
                    int ahead = t.QueuedVillager(w, p - 1);
                    if (ahead >= TownVillagers.Capacity || !vil.LineSettled(ahead)) continue;
                    var aat = vil.DrawnAt(ahead);
                    toward = aat - at;
                    float gap = toward.Length() * TownGround.Cell;
                    _pairs++;
                    _worstGap = Math.Max(_worstGap, gap); _bestGap = Math.Min(_bestGap, gap);
                    Check(gap >= 1.6f && gap <= 2.4f, $"the line at the {wp.Kind}: places {p - 1} and {p} stand {gap:0.00} m apart (1.6 to 2.4)");
                    Check(vil.LinePlace(ahead) < vil.LinePlace(me), $"the line at the {wp.Kind} in queue order (place {p - 1} at {vil.LinePlace(ahead):0.00}, {p} at {vil.LinePlace(me):0.00})");
                }
                float yaw = vil.DrawnYaw(me);
                var facing = new Vector2(MathF.Sin(yaw), MathF.Cos(yaw));
                if (toward.Length() < 1e-3f) continue;
                float ang = MathF.Acos(Math.Clamp(facing.Dot(toward.Normalized()), -1f, 1f));
                Check(ang < 1.05f, $"villager {me}, place {p} at the {wp.Kind}, faces {ang:0.00} rad away from the workplace");
                _faced++;
            }
        }
    }
}
