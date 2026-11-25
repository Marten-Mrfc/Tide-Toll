using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim;

namespace TideToll.Game.Tests;

/// <summary>
/// Headless test of the river in the game, through real input (keys, mouse motion, presses and releases at screen
/// positions, clicks on the bridge badge's buttons), checking the simulation's state and the views afterwards:
/// - the Ferry (Y) and Drawbridge (D) tools: off the river they place nothing and say why; on the river the ghost shows
///   the crossing's cells and the flow preview answers with the boats it would hold; press and release builds it and
///   charges its price; the crossing layer draws it;
/// - a purchase the purse cannot pay places nothing, says "Not enough coin" and greys the tab's price;
/// - the drawbridge: a click on it selects it, a second click raises it (the drawn leaf rises to 80 degrees and the
///   signal stands up), L lowers it, the badge's lever raises it, Ctrl+1 lowers it; the badge's keeper button hires the
///   keeper (coin paid, the hut appears);
/// - Erase removes the ferry and the drawbridge (and their views and badge), charging half their price;
/// - several in-game days with boats: river events arrive, the chronicle fills, the ledger shows the purse, coins
///   float up by the landing, and the river's sound cues are counted (nothing plays headless).
/// - the river's seasons (RiverTest.Seasons.cs): a flood and a freeze driven through the simulation's hooks, with their
///   marks, tags, notes and ledger lines appearing and going, the ford tool reason while the ford is closed, the river
///   loop quieter on the ice; and the badge and the place tags kept apart at 1280 x 720 and 1920 x 1080.
/// Starts with the ferry's, the drawbridge's and the keeper's prices (read from the rules) plus 20c, so a second
/// drawbridge is refused for coin.
/// Prints RIVER PASS or RIVER FAIL and quits with 0 or 1.
/// </summary>
public partial class RiverTest : Node
{
    private Main _main = null!;
    private readonly List<string> _notes = new();

    public override void _Ready()
    {
        GetWindow().Size = new Vector2I(1280, 720);
        Input.UseAccumulatedInput = false;
        _main = GD.Load<PackedScene>("res://main.tscn").Instantiate<Main>();
        _main.StartSeed = 1;
        var rules = RiverRules.Default;
        _main.River = rules with { StartMoney = rules.FerryPrice + rules.DrawbridgePrice + rules.KeeperPrice + 20 };
        AddChild(_main);
        Run();
    }

    private async void Run()
    {
        try
        {
            await TestInput.Frames(this, 3);
            Check(W.River != null, "the river rules are off in the game");
            _main.Sim.SetPaused(true);
            _main.Sim.StepTicks(2 * _main.Params.TicksPerDay);
            await TestInput.Frames(this, 2);
            int ferry = await Build(Godot.Key.Y, Tool.Ferry, CrossingType.Ferry, LandingRow() - 8, -1);
            int draw = await Build(Godot.Key.D, Tool.Drawbridge, CrossingType.Drawbridge, LandingRow() + 8, +1);
            await Refused();
            await Lever(draw);
            await Keeper(draw);
            await Overlaps(draw);
            await Days();
            await Seasons();
            await EraseBoth(ferry, draw);
            GD.Print("RIVER PASS " + string.Join(" ", _notes));
            GetTree().Quit(0);
        }
        catch (Exception e)
        {
            GD.PrintErr("RIVER FAIL: " + e);
            GetTree().Quit(1);
        }
    }

    private World W => _main.Sim.World;
    private RiverView R => W.River!;
    private int LandingRow() => R.LandingRow;

    private Crossing? Find(int id)
    {
        var xs = R.Crossings;
        for (int i = 0; i < xs.Count; i++) if (xs[i].Id == id) return xs[i];
        return null;
    }

    private int FreeRow(CrossingType t, int from, int step)
    {
        for (int y = from; y > 6 && y < W.Grid.Width - 6; y += step)
            if (W.CrossingWhatIf(t, W.RiverX[y], y + 0.5) != null) return y;
        throw new InvalidOperationException($"no free row for a {t} from {from}");
    }

    /// <summary>Picks the tool by its key, checks the refusal off the river, then builds on the river by press and release.</summary>
    private async Task<int> Build(Key key, Tool tool, CrossingType type, int from, int step)
    {
        int row = FreeRow(type, from, step);
        // Frame the river there, as a player would before building.
        _main.Rig.LookAt(new Vector3(W.RiverX[row] * 2f, 0, row * 2f), 90f);
        await TestInput.Frames(this, 3);
        TestInput.Key(key);
        await TestInput.Frames(this, 2);
        Check(_main.Tools.Tool == tool, $"{key} did not pick up the {tool} tool");
        // Off the river: a cross, a reason in words, nothing built on a click.
        int count = R.Crossings.Count;
        var off = TestInput.CellScreen(_main, (int)W.RiverX[row] + 12, row);
        await ClickAt(off);
        Check(R.Crossings.Count == count, $"{tool}: a click off the river built something");
        Check(_main.Tools.LastMessage.Contains("river"), $"{tool}: no reason off the river: '{_main.Tools.LastMessage}'");
        // On the river: the ghost shows the crossing's cells, the preview speaks of boats for a drawbridge.
        var cells = W.CrossingWhatIf(type, W.RiverX[row], row + 0.5)!.Value.Crossing!.Value.Cells;
        var at = TestInput.CellScreen(_main, (int)W.RiverX[row], row);
        TestInput.Motion(at);
        await TestInput.Frames(this, 2);
        Check(_main.Tools.Ghost.Pieces == cells.Length, $"{tool}: ghost shows {_main.Tools.Ghost.Pieces} cells, expected {cells.Length}");
        for (int i = 0; i < 90 && !_main.Tools.Previewer.Settled; i++) await TestInput.Frames(this, 1);
        string preview = _main.Tools.Previewer.Text;
        if (type == CrossingType.Drawbridge) Check(preview.Contains("raised"), $"drawbridge preview says nothing of boats: '{preview}'");
        double money = R.Money;
        int places = _main.Audio.Places;
        TestInput.Button(at, MouseButton.Left, true);
        await TestInput.Frames(this, 1);
        Check(R.Crossings.Count == count, $"{tool}: built on press");
        TestInput.Button(at, MouseButton.Left, false);
        await TestInput.Frames(this, 2);
        Check(R.Crossings.Count == count + 1, $"{tool}: not built on release ('{_main.Tools.LastMessage}')");
        var x = R.Crossings[R.Crossings.Count - 1];
        Check(x.Type == type && x.Row == row && x.Cells[0] == cells[0], $"{tool}: built {x.Type} at row {x.Row}, expected {type} at {row}");
        Check(Math.Abs(money - R.Money - R.Rules.Price(type)) < 1e-6, $"{tool}: paid {money - R.Money}c, price {R.Rules.Price(type)}c");
        Check(_main.Crossings.NodeFor(x.Id) != null, $"{tool}: not drawn");
        Check(_main.Audio.Places == places + 1, $"{tool}: no place sound cue");
        if (type == CrossingType.Drawbridge) Check(_main.Ui.Badges.Count == 1, "no badge for the drawbridge");
        TestInput.Key(Godot.Key.Escape);
        await TestInput.Frames(this, 1);
        _notes.Add($"{type}@{row}");
        return x.Id;
    }

    private async Task ClickAt(Vector2 p)
    {
        TestInput.Motion(p);
        await TestInput.Frames(this, 1);
        TestInput.Button(p, MouseButton.Left, true);
        await TestInput.Frames(this, 1);
        TestInput.Button(p, MouseButton.Left, false);
        await TestInput.Frames(this, 2);
    }

    /// <summary>A second drawbridge with 50c left: nothing built, the reason said, the tab's price greyed.</summary>
    private async Task Refused()
    {
        _main.Sim.StepTicks(1);
        Check(R.Money < R.Rules.DrawbridgePrice, $"test setup: {R.Money}c left");
        await WaitFor(() => !_main.Ui.Tabs.PriceAffordable("Drawbridge"), 3); // the interface refreshes twice a second
        Check(!_main.Ui.Tabs.PriceAffordable("Drawbridge"), "the drawbridge's price is not greyed when the purse is short");
        Check(_main.Ui.Tabs.PriceAffordable("Ferry"), "the ferry's price is greyed though it is affordable");
        int row = FreeRow(CrossingType.Drawbridge, LandingRow() + 20, +1);
        _main.Rig.LookAt(new Vector3(W.RiverX[row] * 2f, 0, row * 2f), 90f);
        await TestInput.Frames(this, 3);
        TestInput.Key(Godot.Key.D);
        await TestInput.Frames(this, 2);
        int count = R.Crossings.Count, refusals = _main.Audio.Refusals;
        var at = TestInput.CellScreen(_main, (int)W.RiverX[row], row);
        TestInput.Motion(at);
        await TestInput.Frames(this, 2);
        Check(_main.Tools.LastMessage.StartsWith("Not enough coin"), $"hover gives no coin reason: '{_main.Tools.LastMessage}'");
        await ClickAt(at);
        Check(R.Crossings.Count == count, "a drawbridge was built without the coin");
        Check(_main.Tools.LastMessage.StartsWith($"Not enough coin: the drawbridge costs {RiverFeed.Coin(R.Rules.DrawbridgePrice)}"), $"no reason in words: '{_main.Tools.LastMessage}'");
        Check(_main.Audio.Refusals > refusals, "no refusal sound cue");
        TestInput.Key(Godot.Key.Escape);
        await TestInput.Frames(this, 1);
        _notes.Add("refused=ok");
    }

    private async Task StepUntil(Func<bool> done, int max, string what)
    {
        for (int i = 0; i < max && !done(); i++) { _main.Sim.StepTicks(1); await TestInput.Frames(this, 1); }
        Check(done(), what);
    }

    /// <summary>Select by a click, raise by a second click, lower by L, raise by the badge's lever, lower by Ctrl+1.</summary>
    private async Task Lever(int id)
    {
        var x = Find(id)!;
        _main.Rig.LookAt(new Vector3(W.RiverX[x.Row] * 2f, 0, x.Row * 2f), 70f);
        await TestInput.Frames(this, 3);
        Check(_main.Tools.Tool == Tool.None, "a tool is still in hand");
        var deck = TestInput.CellScreen(_main, x.Cells[1] % W.Grid.Width, x.Row);
        await ClickAt(deck);
        Check(_main.Tools.SelectedBridge == id, "a click did not select the drawbridge");
        Check(x.State == BridgeState.Closed, "selecting raised the bridge");
        await ClickAt(deck);
        Check(x.State == BridgeState.Raising, $"a second click did not raise it ({x.State})");
        await StepUntil(() => x.State == BridgeState.Raised, 60, "the bridge never rose");
        await TestInput.Frames(this, 2);
        float leaf = _main.Crossings.LeafDegrees(id);
        Check(Math.Abs(leaf - CrossingLayer.MaxLeafDeg) < 1f, $"raised leaf drawn at {leaf} degrees");
        Check(_main.Ui.Badges.StateText(id).StartsWith("Up"), $"badge says '{_main.Ui.Badges.StateText(id)}' while raised");
        TestInput.Key(Godot.Key.L);
        await TestInput.Frames(this, 1);
        Check(x.State == BridgeState.Lowering, $"L did not lower it ({x.State})");
        await StepUntil(() => x.State == BridgeState.Closed, 60, "the bridge never came down");
        await TestInput.Frames(this, 2);
        Check(Math.Abs(_main.Crossings.LeafDegrees(id)) < 1f, $"lowered leaf drawn at {_main.Crossings.LeafDegrees(id)} degrees");
        var lever = _main.Ui.Badges.LeverButton(id)!;
        Check(lever.IsVisibleInTree(), "the badge's lever is hidden");
        await ClickAt(TestInput.Centre(lever));
        Check(x.State == BridgeState.Raising, $"the badge's lever did not raise it ({x.State})");
        Input.ParseInputEvent(new InputEventKey { Keycode = Godot.Key.Key1, PhysicalKeycode = Godot.Key.Key1, Pressed = true, CtrlPressed = true });
        Input.ParseInputEvent(new InputEventKey { Keycode = Godot.Key.Key1, PhysicalKeycode = Godot.Key.Key1, Pressed = false, CtrlPressed = true });
        await TestInput.Frames(this, 1);
        Check(x.State == BridgeState.Lowering, $"Ctrl+1 did not lower drawbridge 1 ({x.State})");
        Check(_main.Sim.Speed == 1, "Ctrl+1 changed the speed too");
        await StepUntil(() => x.State == BridgeState.Closed, 60, "the bridge never came down after Ctrl+1");
        _notes.Add("lever=ok");
    }

    private async Task Keeper(int id)
    {
        var x = Find(id)!;
        double money = R.Money;
        var button = _main.Ui.Badges.KeeperButton(id)!;
        await ClickAt(TestInput.Centre(button));
        Check(x.Keeper, $"the badge's keeper button did not hire one ('{_main.Tools.LastMessage}')");
        Check(Math.Abs(money - R.Money - R.Rules.KeeperPrice) < 1e-6, $"the keeper cost {money - R.Money}c");
        await TestInput.Frames(this, 2);
        Check(_main.Crossings.KeeperShown(id), "no keeper's hut drawn");
        Check(button.Disabled, "the keeper button still offers a hire");
        // The keeper raises for a barge by itself.
        W.SpawnBoat(BoatKind.Barge);
        await StepUntil(() => x.State != BridgeState.Closed, 400, "the keeper never raised the bridge for a barge");
        _notes.Add("keeper=ok");
    }

    /// <summary>Several in-game days with boats: events, chronicle, ledger, coins, sound cues.</summary>
    private async Task Days()
    {
        var feed = _main.Feed;
        int drained = feed.Drained, notes = feed.NotesWritten, coins = _main.Ui.Floats.Coins, calls = _main.Audio.Calls, coinCues = _main.Audio.CoinCues;
        W.SpawnBoat(BoatKind.Rowboat);
        int days = 4;
        for (int i = 0; i < days * _main.Params.TicksPerDay / 15; i++)
        {
            _main.Sim.StepTicks(15);
            await TestInput.Frames(this, 1);
        }
        await WaitFor(() => _main.Ui.Chapter.GetNode<Label>("Column/Coin/Value").Text == $"{(int)Math.Floor(R.Money + 1e-9)}c", 3);
        int sold = R.Stats.Served[0] + R.Stats.Served[1];
        Check(feed.Drained > drained + 20, $"only {feed.Drained - drained} river events in {days} days");
        Check(feed.NotesWritten > notes, "no chronicle notes");
        Check(sold > 0, "no boat was served in four days");
        Check(_main.Ui.Floats.Coins > coins, "no coins floated up by the landing");
        Check(_main.Audio.CoinCues > coinCues, "no coin sound cue");
        Check(_main.Audio.Calls > calls || _main.Audio.BridgeCues > 0, "no boat or bridge sound cues");
        Check(_main.Ui.Floats.Overlaps == 0, $"{_main.Ui.Floats.Overlaps} floating notes lie over a boat, a crossing or another note");
        var coin = _main.Ui.Chapter.GetNode<Label>("Column/Coin/Value").Text;
        Check(coin == $"{(int)Math.Floor(R.Money + 1e-9)}c", $"the ledger shows {coin}, the purse holds {R.Money:0.##}c");
        var note0 = _main.Ui.Chapter.GetNode<Label>("Column/Chronicle/Note0").Text;
        Check(note0 == feed.Note(0) && note0.StartsWith("Day "), $"the chronicle shows '{note0}'");
        _notes.Add($"days={days} events={feed.Drained - drained} served={sold} coins={_main.Ui.Floats.Coins - coins} note='{feed.Note(0)}'");
    }

    private async Task EraseBoth(int ferry, int draw)
    {
        TestInput.Key(Godot.Key.E);
        await TestInput.Frames(this, 2);
        Check(_main.Tools.Tool == Tool.Erase, "E did not pick up the eraser");
        foreach (int id in new[] { ferry, draw })
        {
            var x = Find(id)!;
            // Wait for a quiet moment: nothing on the crossing (a drawbridge down), so the click is plain.
            await StepUntil(() => x.State == BridgeState.Closed, 400, "the bridge stayed up");
            _main.Rig.LookAt(new Vector3(W.RiverX[x.Row] * 2f, 0, x.Row * 2f), 70f);
            await TestInput.Frames(this, 3);
            double money = R.Money;
            var at = TestInput.CellScreen(_main, x.Cells[1] % W.Grid.Width, x.Row);
            TestInput.Motion(at);
            await TestInput.Frames(this, 2);
            Check(_main.Tools.LastMessage.Contains("costs"), $"erase hover does not give the cost: '{_main.Tools.LastMessage}'");
            await ClickAt(at);
            Check(Find(id) == null, $"the {x.Type} was not erased");
            Check(_main.Crossings.NodeFor(id) == null, $"the {x.Type} is still drawn");
            double cost = R.Rules.EraseCostShare * R.Rules.Price(x.Type);
            Check(Math.Abs(money - R.Money - cost) < 1e-6, $"erasing the {x.Type} cost {money - R.Money}c, expected {cost}c");
        }
        Check(_main.Ui.Badges.Count == 0, "a badge outlived its drawbridge");
        Check(W.Bridges.Count == 0, "the drawbridge's bridge is still in the world");
        _notes.Add("erase=ok");
    }

    /// <summary>Waits real time (the interface refreshes on a clock, and headless frames are short) until done or timeout.</summary>
    private async Task WaitFor(Func<bool> done, double seconds)
    {
        ulong end = Time.GetTicksMsec() + (ulong)(seconds * 1000);
        while (!done() && Time.GetTicksMsec() < end) await TestInput.Frames(this, 1);
    }

    private static void Check(bool ok, string why)
    {
        if (!ok) throw new InvalidOperationException(why);
    }
}
