using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim;

namespace TideToll.Game.Tests;

/// <summary>
/// The river's third-pass rules as the player meets them (both window sizes), after the other river shots:
/// - river-start: Chapter II on its first morning with the rules' own starting purse: the price tabs saying what the
///   purse can buy ("can buy") and what is still to save, the flood band's dotted edge on the dry map, the Ford tag
///   with what wading costs;
/// - river-wet: close on the ford with villagers wading it with grain sacks, the wet ones dark and running with water;
/// - river-wet-note: the landing as the "wet grain: -Nc" note floats up, and the chapter page's ford line (whole book);
/// - river-keeper-locked: a new drawbridge's badge with the hand raises still to go and the keeper locked;
/// - river-keeper-unlocked: the moment the last hand raise earns the keeper (the button glowing, the note floating).
/// </summary>
public partial class Screenshot
{
    private const string RiverRulesNames = "river-start river-wet river-wet-note river-keeper-locked river-keeper-unlocked";

    private async Task RiverRulesShots()
    {
        var rules = RiverRules.Default;
        _main.River = rules; // the purse a player starts with
        _main.SwitchChapter(2);
        await Frames(2);
        var sim = _main.Sim;
        sim.Reset(1);
        sim.SetSpeed(1);
        sim.SetPaused(true);
        sim.StepTicks(_main.Params.TicksPerDay / 3);
        var w = sim.World;
        var r = w.River ?? throw new InvalidOperationException("river rules are off");
        _main.Ground.SnapNext();
        _main.Ground.Upload();
        _main.Rig.FramePlay(instant: true);
        await Frames(8); // the interface's prices refresh on a half-second clock
        Need(_main.Ground.BandMarked, "the flood band is not marked");
        GD.Print($"SCREENSHOT river-start: purse {r.Money:0}c, ford tag '{_main.Ui.Tags.FordHint}', ferry affordable {_main.Ui.Tabs.PriceAffordable("Ferry")}, drawbridge {_main.Ui.Tabs.PriceAffordable("Drawbridge")}");
        Check(await Snap("river-start", true), "river-start");

        // Wet grain: run until several carriers wade the ford with sacks, then look close.
        int fy = w.Map.FordY;
        for (int i = 0; i < 6 * _main.Params.TicksPerDay && WadingWithSacks(fy) < 3; i++) sim.StepTicks(1);
        GD.Print($"SCREENSHOT river-wet: wading with sacks {WadingWithSacks(fy)}, wet delivered {r.Stats.GrainDeliveredWet}, lost {r.Stats.WetValueLost:0.#}c");
        _main.Ground.SnapNext();
        _main.Ground.Upload();
        _main.Rig.LookAt(new Vector3(w.RiverX[fy] * 2f, 0, fy * 2f + 1f), 40f);
        await Frames(6);
        Check(await Snap("river-wet", true), "river-wet");
        // The landing's note: run a little in real frames until one floats, then the whole book for the ledger's ford line.
        _main.Rig.LookAt(_main.Boats.LandingAt + new Vector3(0, 0, 6f), 70f);
        int notes = _main.Ui.Floats.WetNotes;
        ulong end = Time.GetTicksMsec() + 30000;
        while (_main.Ui.Floats.WetNotes == notes && Time.GetTicksMsec() < end) { sim.StepTicks(3); await Frames(1); }
        // The ledger refreshes on a half-second clock: wait until its ford line has the wet grain too.
        ulong led = Time.GetTicksMsec() + 3000;
        while (_main.Ui.Chapter.FordText.StartsWith("no grain") && Time.GetTicksMsec() < led) await Frames(1);
        await Frames(2);
        GD.Print($"SCREENSHOT river-wet-note: '{_main.Ui.Floats.LastText}', ledger ford '{_main.Ui.Chapter.FordText}'");
        Check(await Snap("river-wet-note", true), "river-wet-note");

        // The keeper earned by use: a drawbridge built with a purse that can pay for it and the keeper.
        _main.River = rules with { StartMoney = rules.DrawbridgePrice + rules.KeeperPrice + 30 };
        _main.SwitchChapter(2);
        await Frames(2);
        sim = _main.Sim;
        sim.Reset(1);
        sim.SetPaused(true);
        sim.StepTicks(_main.Params.TicksPerDay);
        w = sim.World;
        r = w.River!;
        int row = FreeRow(r.LandingRow + 9, +1);
        var draw = sim.PlaceCrossing(CrossingType.Drawbridge, (int)w.RiverX[row], row);
        Need(draw.Ok, $"drawbridge: {draw}");
        _main.Tools.Select(draw.Id);
        sim.StepTicks(4);
        await Look(row, 56f);
        await Frames(4);
        GD.Print($"SCREENSHOT river-keeper-locked: '{_main.Ui.Badges.UnlockText(draw.Id)}'");
        Check(await Snap("river-keeper-locked", true), "river-keeper-locked");
        var x = Find(draw.Id)!;
        for (int k = 0; k < rules.KeeperUnlockRaises; k++)
        {
            sim.SetBridge(draw.Id, true);
            for (int i = 0; i < 40 && x.State != BridgeState.Raised; i++) sim.StepTicks(1);
            sim.SetBridge(draw.Id, false);
            for (int i = 0; i < 200 && x.State != BridgeState.Closed; i++) sim.StepTicks(1);
            if (k == rules.KeeperUnlockRaises - 2) await Frames(2); // the badge sees the count before the last raise
        }
        Need(x.KeeperUnlocked, "the keeper did not unlock");
        await Frames(3);
        GD.Print($"SCREENSHOT river-keeper-unlocked: glowing {_main.Ui.Badges.Glowing(draw.Id)}, float '{_main.Ui.Floats.LastText}', note '{_main.Feed.Note(0)}'");
        Check(await Snap("river-keeper-unlocked", true), "river-keeper-unlocked");
    }

    private int WadingWithSacks(int fordRow)
    {
        var w = _main.Sim.World;
        int n = w.Grid.Width, k = 0;
        foreach (ref readonly var a in w.Walkers)
            if (a.Visible && a.Cargo != 0 && a.CargoWet && Math.Abs(a.Cell / n - fordRow) <= 3) k++;
        return k;
    }
}
