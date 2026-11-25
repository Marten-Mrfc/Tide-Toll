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
/// - river-crossings: ferry, drawbridge and footbridge in one view at a middle zoom, a rowboat and a barge among them;
/// - river-boats: a rowboat moored at the landing, close (a barge too when one is there);
/// - river-ferry: the ferry's raft on its rope, with riders when the timing allows;
/// - river-blocked: a barge held below the footbridge (crossed-out mast) and, river-turnback, the moment it turns back
///   (vermillion diamond, "Turned back", the margin note);
/// - river-ledger: the whole book with the chapter page's ledger and chronicle;
/// - river-flood: the play view during a spring flood (World.StartFlood): the wash over the flood band, the closed ford's
///   bar and its "Ford closed" tag, the ledger's "Spring flood, day N" and the margin note;
/// - river-ice and river-ice-close: the river frozen (World.Freeze) after a day and a half of walkers crossing on the
///   ice: pale ice with ink cracks, boats held in it, the ice paths, the ledger's "Frozen since day N".
/// Each scene is checked in the simulation before it is photographed (the bridge really is up, the barge really waits).
/// </summary>
public partial class Screenshot
{
    private async Task RiverShots()
    {
        var rules = _main.River ?? RiverRules.Default; // every crossing affordable, whatever the prices are
        _main.River = rules with { StartMoney = rules.FootbridgePrice + rules.FerryPrice + rules.DrawbridgePrice + rules.KeeperPrice + 870, KeeperUnlockRaises = 0 }; // keeper hired at once; the unlock has its own test
        _main.SwitchChapter(2); // Chapter II, The River: the book is built again with the river rules on
        await Frames(2);
        var sim = _main.Sim;
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
        await Look(drawRow, 56f);
        Check(await Snap("river-waiting", true), "river-waiting");
        // Raised by hand with the L key, as a player would; walkers bunch on both banks.
        _main.Tools.Select(draw.Id);
        Key(Godot.Key.L);
        await Frames(1);
        for (int i = 0; i < 40 && Find(draw.Id)!.State != BridgeState.Raised; i++) sim.StepTicks(1);
        sim.StepTicks(4);
        Need(Find(draw.Id)!.State == BridgeState.Raised, "the drawbridge did not rise");
        await Look(drawRow, 56f);
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
        // All three crossings at a middle zoom, a rowboat and a barge on the move between them, then the whole river.
        _main.Tools.Select(-1);
        // Clear water first (the turned-back barges sail off), then a rowboat and, 25 ticks behind it, a barge: boats
        // sail a row a tick, so when the rowboat is between the drawbridge and the landing the barge has just reached
        // the footbridge and waits below it.
        for (int i = 0; i < 300 && Moving() > 0; i++) sim.StepTicks(1);
        w.SpawnBoat(BoatKind.Rowboat);
        sim.StepTicks(25);
        w.SpawnBoat(BoatKind.Barge);
        sim.StepTicks(Math.Max(10, (int)(128.5 - (drawRow + land) / 2) - 25));
        await Look((ferryRow + footRow) / 2, (footRow - ferryRow) * 2f + 40f);
        Check(await Snap("river-crossings", true), "river-crossings");
        // The rowboat moored at the landing, close: a short hull with no mast against a barge's length and sail bar.
        for (int i = 0; i < 120 && !RowboatMoored(); i++) sim.StepTicks(1);
        if (!RowboatMoored()) GD.Print("SCREENSHOT river-boats: the rowboat did not moor in time");
        GD.Print("SCREENSHOT river-boats: " + BoatList());
        _main.Ground.SnapNext();
        _main.Ground.Upload();
        _main.Rig.LookAt(_main.Boats.LandingAt + new Vector3(0, 0, 4f), 48f);
        await Frames(4);
        Check(await Snap("river-boats", true), "river-boats");
        _main.Rig.FramePlay(instant: true);
        await Frames(4);
        Check(await Snap("river-map", true), "river-map");
        // A slice of the day with frames between the ticks, so the ledger's "today" figures have something to count.
        for (int i = 0; i < 16; i++) { sim.StepTicks(6); await Frames(1); }
        _main.Rig.FrameBook(instant: true);
        await Frames(6);
        GD.Print($"SCREENSHOT river-ledger: coin {r.Money:0}c, rep {r.Reputation:0}, notes {_main.Feed.NoteCount}: '{_main.Feed.Note(0)}'");
        Check(await Snap("river-ledger", true), "river-ledger");
        await SeasonShots(sim, r);
        await RiverRulesShots(); // the book is built again: the third pass's rules with their own purse
        _main.Sim.SetPaused(false);
    }

    /// <summary>A spring flood at the play view, then (the flood gone) the river frozen with paths worn on the ice.</summary>
    private async Task SeasonShots(SimDriver sim, RiverView r)
    {
        var w = sim.World;
        for (int i = 0; i < 4 * _main.Params.TicksPerDay && r.Flooded; i++) sim.StepTicks(1); // any flood of its own first
        var flood = w.StartFlood(2);
        Need(flood.Ok && r.Flooded && !r.FordOpen, $"the flood did not start: {flood}");
        sim.StepTicks(_main.Params.TicksPerDay / 3);
        _main.Feed.Drain();
        _main.Rig.FramePlay(instant: true);
        await SnapSettled(() => _main.Ground.Flood >= 1f);
        Need(_main.Crossings.FordClosedShown, "no closed-ford mark during the flood");
        GD.Print($"SCREENSHOT river-flood: wash {_main.Ground.Flood:0.00}, ford tag {_main.Ui.Tags.Tag(MapTags.FordClosed).Visible}, note '{_main.Feed.Note(0)}'");
        Check(await Snap("river-flood", true), "river-flood");
        for (int i = 0; i < 3 * _main.Params.TicksPerDay && r.Flooded; i++) sim.StepTicks(1);
        Need(!r.Flooded, "the flood never went down");
        var ice = w.Freeze();
        Need(ice.Ok && r.Frozen, $"the river did not freeze: {ice}");
        sim.StepTicks(_main.Params.TicksPerDay * 3 / 2);
        _main.Feed.Drain();
        float most = 0;
        for (int i = 0; i < r.IceWear.Length; i++) most = Math.Max(most, r.IceWear[i]);
        _main.Ground.SnapNext();
        _main.Ground.Upload();
        _main.Rig.FramePlay(instant: true);
        await SnapSettled(() => _main.Ground.Ice >= 1f);
        GD.Print($"SCREENSHOT river-ice: ice {_main.Ground.Ice:0.00}, ice crossings {r.Stats.IceCrossings}, most ice wear {most:0.0}, ice uploads {_main.Ground.IceUploads}, note '{_main.Feed.Note(0)}'");
        Need(r.Stats.IceCrossings > 0, "nobody crossed on the ice");
        Check(await Snap("river-ice", true), "river-ice");
        _main.Rig.LookAt(new Vector3(w.RiverX[r.LandingRow] * 2f, 0, (r.LandingRow + 6) * 2f), 110f);
        await Frames(4);
        Check(await Snap("river-ice-close", true), "river-ice-close");
        w.Thaw();
        sim.StepTicks(1);
        _main.Feed.Drain();
    }

    /// <summary>Real frames until an eased look has arrived (the flood's wash, the ice), at most about ten seconds.</summary>
    private async Task SnapSettled(Func<bool> done)
    {
        ulong end = Time.GetTicksMsec() + 10000;
        await Frames(2);
        while (!done() && Time.GetTicksMsec() < end) await Frames(1);
        await Frames(2);
    }

    private string BoatList()
    {
        var sb = new System.Text.StringBuilder();
        foreach (ref readonly var b in _main.Sim.World.River!.Boats)
        {
            _main.Boats.BoatPosition(b.Id, out var at);
            sb.Append($"{b.Kind} {b.State} row {b.Y:0.0} berth {b.Berth} drawn at {at.X:0},{at.Z:0}; ");
        }
        return sb.ToString();
    }

    private bool RowboatMoored()
    {
        foreach (ref readonly var b in _main.Sim.World.River!.Boats) if (b.State == BoatState.Mooring && b.Kind == BoatKind.Rowboat) return true;
        return false;
    }

    /// <summary>Boats on the river that are not moored at the landing.</summary>
    private int Moving()
    {
        int k = 0;
        foreach (ref readonly var b in _main.Sim.World.River!.Boats) if (b.State != BoatState.Mooring) k++;
        return k;
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
