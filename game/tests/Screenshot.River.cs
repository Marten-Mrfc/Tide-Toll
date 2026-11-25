using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim;

namespace TideToll.Game.Tests;

/// <summary>
/// The river's screenshots (both window sizes, as NAME-WxH.png): seed 1 with a fuller purse (so every crossing can be
/// bought), and boats sent in by hand so each scene happens at once:
/// - river-map: the play view with the landing, a ferry north of it, a drawbridge and a footbridge south of it, and
///   boats on the river;
/// - river-waiting: a barge waiting below the lowered drawbridge, its patience ring running down, walkers crossing;
/// - river-raised: the same bridge raised by hand (L), walkers bunched at the barriers, the barge passing;
/// - river-ferry: the ferry's raft on its rope, with riders when the timing allows;
/// - river-blocked: a barge held below the footbridge (crossed-out mast) and, river-turnback, the moment it turns back
///   (vermillion diamond, "Turned back", the margin note);
/// - river-ledger: the whole book with the chapter page's ledger and chronicle.
/// Each scene is checked in the simulation before it is photographed (the bridge really is up, the barge really waits).
/// </summary>
public partial class Screenshot
{
    private async Task RiverShots()
    {
        var sim = _main.Sim;
        sim.Params = sim.Params with { River = (sim.Params.River ?? RiverRules.Default) with { StartMoney = 1200 } };
        sim.Reset(1);
        sim.SetSpeed(1);
        sim.SetPaused(true);
        sim.StepTicks(3 * _main.Params.TicksPerDay);
        var w = sim.World;
        var r = w.River ?? throw new InvalidOperationException("river rules are off");
        int land = r.LandingRow, n = w.Grid.Width;
        int drawRow = FreeRow(land + 9, +1), footRow = FreeRow(Math.Min(n - 8, drawRow + 16), +1), ferryRow = FreeRow(land - 9, -1);
        var ferry = sim.PlaceCrossing(CrossingType.Ferry, (int)w.RiverX[ferryRow], ferryRow);
        var draw = sim.PlaceCrossing(CrossingType.Drawbridge, (int)w.RiverX[drawRow], drawRow);
        Need(ferry.Ok && draw.Ok, $"river shots: ferry {ferry}, drawbridge {draw}");
        w.SpawnBoat(BoatKind.Rowboat);
        sim.StepTicks(12);
        w.SpawnBoat(BoatKind.Barge);
        // The barge sails up to the lowered drawbridge and waits there, patience running.
        for (int i = 0; i < 300 && !BoatWaitsAt(draw.Id); i++) sim.StepTicks(1);
        Need(BoatWaitsAt(draw.Id), "no barge waiting at the drawbridge");
        sim.StepTicks(30);
        await Look(drawRow, 70f);
        Check(await Snap("river-waiting", true), "river-waiting");
        // Raised by hand with the L key, as a player would; walkers bunch on both banks.
        _main.Tools.Select(draw.Id);
        Key(Godot.Key.L);
        await Frames(1);
        for (int i = 0; i < 40 && Find(draw.Id)!.State != BridgeState.Raised; i++) sim.StepTicks(1);
        sim.StepTicks(4);
        Need(Find(draw.Id)!.State == BridgeState.Raised, "the drawbridge did not rise");
        await Look(drawRow, 70f);
        Check(await Snap("river-raised", true), "river-raised");
        for (int i = 0; i < 200 && Find(draw.Id)!.State != BridgeState.Closed; i++) sim.StepTicks(1);
        // The ferry: wait for the raft under way with riders (or at least for a crossing).
        for (int i = 0; i < 600 && !(Find(ferry.Id)!.FerrySide == 0 && Find(ferry.Id)!.Passengers > 0); i++) sim.StepTicks(1);
        await Look(ferryRow, 60f);
        GD.Print($"SCREENSHOT river-ferry: side {Find(ferry.Id)!.FerrySide} riders {Find(ferry.Id)!.Passengers}");
        Check(await Snap("river-ferry", true), "river-ferry");
        // A footbridge south of everything: the next barge stops below it and, out of patience, turns back.
        var foot = sim.PlaceCrossing(CrossingType.Footbridge, (int)w.RiverX[footRow], footRow);
        Need(foot.Ok, $"footbridge: {foot}");
        w.SpawnBoat(BoatKind.Barge);
        for (int i = 0; i < 300 && !BoatWaitsAt(foot.Id); i++) sim.StepTicks(1);
        Need(BoatWaitsAt(foot.Id), "no barge waiting below the footbridge");
        sim.StepTicks(40);
        await Look(footRow, 70f);
        Check(await Snap("river-blocked", true), "river-blocked");
        int turned = r.Stats.TurnedBack[(int)BoatKind.Barge];
        for (int i = 0; i < 300 && r.Stats.TurnedBack[(int)BoatKind.Barge] == turned; i++) sim.StepTicks(1);
        Need(r.Stats.TurnedBack[(int)BoatKind.Barge] > turned, "the barge did not turn back");
        sim.StepTicks(8);
        await Look(footRow + 2, 70f);
        Check(await Snap("river-turnback", true), "river-turnback");
        // The whole river in the play view: boats on the move between the crossings.
        w.SpawnBoat(BoatKind.Rowboat);
        sim.StepTicks(25);
        w.SpawnBoat(BoatKind.Barge);
        sim.StepTicks(6);
        _main.Rig.FramePlay(instant: true);
        await Frames(4);
        Check(await Snap("river-map", true), "river-map");
        _main.Rig.FrameBook(instant: true);
        await Frames(6);
        GD.Print($"SCREENSHOT river-ledger: coin {r.Money:0}c, rep {r.Reputation:0}, notes {_main.Feed.NoteCount}: '{_main.Feed.Note(0)}'");
        Check(await Snap("river-ledger", true), "river-ledger");
        sim.SetPaused(false);
    }

    private int FreeRow(int from, int step)
    {
        var w = _main.Sim.World;
        for (int y = from; y > 4 && y < w.Grid.Width - 4; y += step)
            if (w.CrossingWhatIf(CrossingType.Drawbridge, w.RiverX[y], y + 0.5) != null) return y;
        throw new InvalidOperationException($"no free river row from {from}");
    }

    private async Task Look(int row, float size)
    {
        var w = _main.Sim.World;
        _main.Ground.SnapNext();
        _main.Ground.Upload();
        _main.Rig.LookAt(new Vector3(w.RiverX[row] * 2f, 0, row * 2f + 6f), size);
        await Frames(4);
    }

    private bool BoatWaitsAt(int crossing)
    {
        foreach (ref readonly var b in _main.Sim.World.River!.Boats)
            if (b.State == BoatState.Waiting && b.WaitingAt == crossing && b.Kind == BoatKind.Barge) return true;
        return false;
    }

    private Crossing? Find(int id)
    {
        var xs = _main.Sim.World.River!.Crossings;
        for (int i = 0; i < xs.Count; i++) if (xs[i].Id == id) return xs[i];
        return null;
    }

    private void Need(bool ok, string why)
    {
        if (!ok) throw new InvalidOperationException(why);
    }
}
