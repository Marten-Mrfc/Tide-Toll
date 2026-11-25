using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim;

namespace TideToll.Game.Tests;

/// <summary>
/// Headless test of the river's third-pass rules as the game shows them (Chapter II, seed 1), through the simulation's
/// debug hooks (StepTicks, SpawnBoat) and real input (keys, clicks on the badge):
/// - the starting purse (RiverRules.StartMoney, the default rules): the price tabs say at once what it can buy
///   ("can buy") and what it cannot yet ("not yet", a gauge of the purse against the price, the coin to save in the tooltip; never "short"), the ledger's coin is the purse; the flood band's
///   dotted edge is marked on the dry map; the Ford tag says what wading costs;
/// - wet grain at the ford: a carrier with a sack is drawn with it, a wet one wet; a "wet grain: -Nc" note floats at
///   the landing; the ledger's ford line counts the coin lost today and in all;
/// - the ledger right after a fast-forward: "Grain today" counts from the last dawn inside the jump (the same town run
///   to that dawn gives the reference);
/// - the Ford tag never lies over a boat at the play view (it hides while one passes);
/// - the keeper unlocked by use (default KeeperUnlockRaises): the badge says the raises still to go, the keeper button is
///   locked with that reason and K is refused in words; ten raises by hand (L) count down; the unlock writes a margin
///   note, floats a note, rings the bell and makes the button glow; then the badge's button hires the keeper;
/// - barges held below a footbridge: queued boats are never drawn over one another, and boats turning back together
///   leave one "Turned back ×N" float and one chronicle line with a count.
/// Prints RIVER RULES PASS or RIVER RULES FAIL and quits with 0 or 1.
/// </summary>
public partial class RiverRulesTest : Node
{
    private Main _main = null!;
    private readonly List<string> _notes = new();

    public override void _Ready()
    {
        GetWindow().Size = new Vector2I(1280, 720);
        Input.UseAccumulatedInput = false;
        _main = GD.Load<PackedScene>("res://main.tscn").Instantiate<Main>();
        _main.StartSeed = 1;
        _main.StartChapter = 2;
        _main.River = RiverRules.Default; // the rules as a player meets them
        AddChild(_main);
        Run();
    }

    private async void Run()
    {
        try
        {
            await TestInput.Frames(this, 3);
            Check(W.River != null, "the river rules are off in Chapter II");
            _main.Sim.SetPaused(true);
            await Purse();
            await WetGrain();
            await FastForward();
            await FordTag();
            // A fuller purse for the keeper's part (a drawbridge, a footbridge and the keeper), the unlock as the rules say.
            var rules = RiverRules.Default;
            _main.River = rules with { StartMoney = rules.DrawbridgePrice + rules.FootbridgePrice + rules.KeeperPrice + 10 };
            _main.SwitchChapter(2);
            await TestInput.Frames(this, 3);
            _main.Sim.SetPaused(true);
            await KeeperUnlock();
            await QueueAndTurnBack();
            GD.Print("RIVER RULES PASS " + string.Join(" ", _notes));
            GetTree().Quit(0);
        }
        catch (Exception e)
        {
            GD.PrintErr("RIVER RULES FAIL: " + e);
            GetTree().Quit(1);
        }
    }

    private World W => _main.Sim.World;
    private RiverView R => W.River!;

    /// <summary>What the opening purse can buy, said at once; the band and the ford's cost marked from the first frame.</summary>
    private async Task Purse()
    {
        var rr = R.Rules;
        Check(Math.Abs(R.Money - rr.StartMoney) < 1e-9, $"the purse starts at {R.Money}c, the rules say {rr.StartMoney}c");
        await WaitFor(() => _main.Ui.Chapter.GetNode<Label>("Column/Coin/Value").Text == $"{rr.StartMoney:0}c", 3);
        Check(_main.Ui.Chapter.GetNode<Label>("Column/Coin/Value").Text == $"{rr.StartMoney:0}c", "the ledger does not show the starting purse");
        foreach (var (name, price) in new[] { ("Footbridge", rr.FootbridgePrice), ("Ferry", rr.FerryPrice), ("Drawbridge", rr.DrawbridgePrice) })
        {
            var tab = (Button)_main.Ui.Controls[name];
            var line = tab.GetNode<Label>("Row/Lines/Price").Text;
            bool can = rr.StartMoney >= price;
            Check(_main.Ui.Tabs.PriceAffordable(name) == can, $"{name}: '{line}' with {rr.StartMoney}c in the purse");
            string want = can ? $"{price:0}c · can buy" : $"{price:0}c · not yet";
            Check(line == want, $"{name} price line '{line}', expected '{want}'");
            float fill = _main.Ui.Tabs.PurseFill(name), wantFill = (float)Math.Min(1, rr.StartMoney / price);
            Check(Math.Abs(fill - wantFill) < 1e-4, $"{name}: purse gauge at {fill}, expected {wantFill}");
            if (!can) Check(tab.TooltipText.Contains($"{Math.Ceiling(price - rr.StartMoney):0}c to save"), $"{name}: the tooltip does not say what is to save: '{tab.TooltipText}'");
            Check(!line.Contains("short") && !tab.Disabled, $"{name} reads as broken while the purse is short: '{line}'");
            _notes.Add($"{name}='{line}'");
        }
        Check(rr.StartMoney >= Math.Min(rr.FerryPrice, rr.FootbridgePrice), "the starting purse buys neither a ferry nor a footbridge");
        Check(_main.Ground.BandMarked, "the flood band's edge is not marked on the dry map");
        string hint = _main.Ui.Tags.FordHint;
        Check(hint == MapTags.FordHintText(rr) && hint.Contains("wet grain"), $"the Ford tag does not say what wading costs: '{hint}'");
        var hintLabel = _main.Ui.Tags.Tag(3).GetNodeOrNull<Label>("Lines/Hint");
        Check(hintLabel != null && hintLabel.Text == hint, "the Ford tag's second line is missing");
        var how = _main.Ui.Chapter.GetNode<Label>("Column/HowTo/HowToText").Text;
        Check(how.Contains("wet") && how.Contains($"{rr.KeeperPrice:0}c") && how.Contains($"{rr.StartMoney:0}c"), $"Chapter II's page does not explain the river: '{how}'");
        _notes.Add($"hint='{hint}'");
    }

    /// <summary>Ford only: grain carried through the ford is drawn wet, sells for less at the landing, and the ledger counts it.</summary>
    private async Task WetGrain()
    {
        var st = R.Stats;
        int maxSacks = 0, maxWet = 0;
        for (int i = 0; i < 6 * _main.Params.TicksPerDay / 10 && (st.GrainDeliveredWet < 3 || maxWet == 0); i++)
        {
            _main.Sim.StepTicks(10);
            await TestInput.Frames(this, 1);
            maxSacks = Math.Max(maxSacks, _main.Walkers.SacksDrawn);
            maxWet = Math.Max(maxWet, _main.Walkers.WetSacksDrawn);
        }
        Check(st.GrainDeliveredWet > 0, $"no grain came through the ford wet in six days ({st.GrainDelivered} delivered)");
        Check(maxSacks > 0, "no carrier was drawn with a sack");
        Check(maxWet > 0, "no carrier was drawn with a wet sack");
        // The count drawn is the count carrying in the world (one frame's sync).
        var (carrying, wet) = Carried();
        Check(_main.Walkers.SacksDrawn == carrying && _main.Walkers.WetSacksDrawn == wet, $"sacks drawn {_main.Walkers.SacksDrawn}/{_main.Walkers.WetSacksDrawn}, carried {carrying}/{wet}");
        await WaitFor(() => _main.Ui.Floats.WetNotes > 0, RiverFloats.WetEvery + 2);
        Check(_main.Ui.Floats.WetNotes > 0, $"no 'wet grain' note at the landing ({st.WetValueLost:0.##}c lost)");
        await WaitFor(() => _main.Ui.Chapter.FordText.Contains($"{(int)Math.Floor(st.WetValueLost + 1e-9)}c in all"), 3);
        string ford = _main.Ui.Chapter.FordText;
        Check(ford.Contains($"{(int)Math.Floor(st.WetValueLost + 1e-9)}c in all"), $"the ledger's ford line '{ford}' does not say {st.WetValueLost:0.##}c lost in all");
        _notes.Add($"wet={st.GrainDeliveredWet}/{st.GrainDelivered} lost={st.WetValueLost:0.#}c sacks={maxSacks} wetSacks={maxWet} ford='{ford}'");
    }

    /// <summary>A jump over a dawn: the ledger's "today" starts at that dawn, not at the frame after the jump.</summary>
    private async Task FastForward()
    {
        var sim = _main.Sim;
        int tpd = _main.Params.TicksPerDay;
        sim.Reset();
        await TestInput.Frames(this, 2);
        // The reference: the same town run to a dawn, its grain total there, and how far into that day grain arrives.
        int toDawn = 3 * tpd, into = -1;
        sim.StepTicks(toDawn);
        int grainAtDawn = R.Stats.GrainDelivered;
        for (int t = 1; t < tpd - 10 && into < 0; t++)
        {
            sim.StepTicks(1);
            if (R.Stats.GrainDelivered > grainAtDawn) into = t + 5;
        }
        Check(into > 0, $"test setup: no grain on day 4 ({R.Stats.GrainDelivered} in all)");
        sim.Reset();
        await TestInput.Frames(this, 2);
        // One jump from the start into that day, past its first sack.
        sim.StepTicks(toDawn + into);
        int want = R.Stats.GrainDelivered - grainAtDawn;
        Check(want > 0, "test setup: the replay differs from the reference");
        string expect = (want == 1 ? "1 sack" : $"{want} sacks") + " to the landing";
        var grain = _main.Ui.Chapter.GetNode<Label>("Column/Graintoday/Value");
        await WaitFor(() => grain.Text.StartsWith(expect), 2);
        Check(grain.Text.StartsWith(expect), $"after a fast-forward the ledger says '{grain.Text}', expected '{expect}'");
        _notes.Add($"fastforward='{grain.Text}'");
    }

    /// <summary>At the play view, boats pass the ford: the Ford tag hides while one is under it and never lies over one.</summary>
    private async Task FordTag()
    {
        _main.Rig.FramePlay();
        await TestInput.Frames(this, 3);
        W.SpawnBoat(BoatKind.Barge);
        _main.Sim.StepTicks(20);
        W.SpawnBoat(BoatKind.Rowboat);
        int hidden = 0, frames = 0, shownFrames = 0;
        var tags = _main.Ui.Tags;
        for (int i = 0; i < 500; i++)
        {
            _main.Sim.StepTicks(1);
            await TestInput.Frames(this, 1);
            frames++;
            if (tags.FordHiddenByBoat) hidden++;
            if (tags.VisibleRect(3, out var r))
            {
                shownFrames++;
                Check(!OverBoat(r), $"frame {i}: the Ford tag lies over a boat");
            }
        }
        Check(shownFrames > 0, "the Ford tag never showed at the play view");
        Check(hidden > 0, "no boat passed under the Ford tag in the run (the test saw nothing to check)");
        _notes.Add($"fordtag hidden={hidden}/{frames}");
    }

    private bool OverBoat(Rect2 tag)
    {
        var rig = _main.Rig;
        var boats = _main.Boats;
        float mpp = rig.Camera.Size / GetViewport().GetVisibleRect().Size.Y;
        for (int i = 0; i < boats.Slots; i++)
        {
            if (!boats.BoatBox(i, out var at, out float half)) continue;
            var c = rig.LocalToScreen(at);
            float r = half / mpp;
            if (tag.Intersects(new Rect2(c.X - r * 0.4f, c.Y - r * 0.9f, r * 0.8f, r * 1.8f))) return true;
        }
        return false;
    }

    private int FreeRow(CrossingType t, int from, int step)
    {
        for (int y = from; y > 6 && y < W.Grid.Width - 6; y += step)
            if (W.CrossingWhatIf(t, W.RiverX[y], y + 0.5) != null) return y;
        throw new InvalidOperationException($"no free row for a {t} from {from}");
    }

    private Crossing? Find(int id)
    {
        var xs = R.Crossings;
        for (int i = 0; i < xs.Count; i++) if (xs[i].Id == id) return xs[i];
        return null;
    }

    /// <summary>The keeper is earned by raising the bridge by hand: the count to go, the lock, the moment, then the hire.</summary>
    private async Task KeeperUnlock()
    {
        var rr = R.Rules;
        int need = rr.KeeperUnlockRaises;
        Check(need > 0, "test setup: the keeper needs no raises");
        int row = FreeRow(CrossingType.Drawbridge, R.LandingRow + 9, +1);
        var built = _main.Sim.PlaceCrossing(CrossingType.Drawbridge, (int)W.RiverX[row], row);
        Check(built.Ok, $"drawbridge: {built.Reason}");
        int id = built.Id;
        var x = Find(id)!;
        _main.Rig.LookAt(new Vector3(W.RiverX[row] * 2f, 0, row * 2f), 70f);
        await TestInput.Frames(this, 4);
        var badges = _main.Ui.Badges;
        var keeper = badges.KeeperButton(id)!;
        Check(badges.UnlockText(id) == BridgeBadges.RaiseText(need), $"the badge says '{badges.UnlockText(id)}' before any raise");
        Check(keeper.Disabled && keeper.Text.Contains($"{need} to go") && keeper.Text.Contains($"{rr.KeeperPrice:0}c"), $"the keeper button reads '{keeper.Text}' (disabled {keeper.Disabled})");
        Check(keeper.TooltipText.Contains("No keeper will take this bridge on yet"), $"the locked button gives no reason: '{keeper.TooltipText}'");
        // K while locked: refused in words, nothing hired, nothing paid.
        _main.Tools.Select(id);
        double money = R.Money;
        TestInput.Key(Godot.Key.K);
        await TestInput.Frames(this, 2);
        Check(!x.Keeper && R.Money == money, "a keeper was hired before the unlock");
        Check(_main.Tools.LastMessage.Contains("raise it by hand"), $"K before the unlock says '{_main.Tools.LastMessage}'");
        int unlockNotes = 0, cues = _main.Audio.UnlockCues;
        for (int k = 1; k <= need; k++)
        {
            TestInput.Key(Godot.Key.L); // raise
            await TestInput.Frames(this, 1);
            Check(x.State == BridgeState.Raising, $"raise {k}: L did not raise the bridge ({x.State}, '{_main.Tools.LastMessage}')");
            await StepUntil(() => x.State == BridgeState.Raised, 60, $"raise {k}: the bridge never rose");
            TestInput.Key(Godot.Key.L); // lower
            await TestInput.Frames(this, 1);
            await StepUntil(() => x.State == BridgeState.Closed, 160, $"raise {k}: the bridge never came down");
            await TestInput.Frames(this, 2);
            Check(x.HandRaises == k, $"raise {k}: the bridge counts {x.HandRaises}");
            if (k < need) Check(badges.UnlockText(id) == BridgeBadges.RaiseText(need - k), $"after {k} raises the badge says '{badges.UnlockText(id)}'");
        }
        for (int i = 0; i < RiverFeed.ChronicleLines; i++) if (_main.Feed.Note(i).Contains("a keeper will take the drawbridge on")) unlockNotes++;
        Check(x.KeeperUnlocked, "the keeper is still locked after the raises");
        Check(badges.UnlockText(id) == "", $"the badge still says '{badges.UnlockText(id)}'");
        Check(!keeper.Disabled && keeper.Text.Contains($"{rr.KeeperPrice:0}c"), $"the keeper button reads '{keeper.Text}' after the unlock");
        Check(unlockNotes == 1, "no margin note for the unlock");
        Check(_main.Audio.UnlockCues == cues + 1, "no bell for the unlock");
        Check(badges.Glowing(id), "the keeper button does not glow at the unlock");
        Check(_main.Ui.Floats.UnlockNotes == 1, $"{_main.Ui.Floats.UnlockNotes} floats for the unlock");
        money = R.Money;
        TestInput.Motion(TestInput.Centre(keeper));
        await TestInput.Frames(this, 1);
        TestInput.Button(TestInput.Centre(keeper), MouseButton.Left, true);
        await TestInput.Frames(this, 1);
        TestInput.Button(TestInput.Centre(keeper), MouseButton.Left, false);
        await TestInput.Frames(this, 2);
        Check(x.Keeper, $"the badge's keeper button did not hire after the unlock ('{_main.Tools.LastMessage}')");
        Check(Math.Abs(money - R.Money - rr.KeeperPrice) < 1e-6, $"the keeper cost {money - R.Money}c, the rules say {rr.KeeperPrice}c");
        _notes.Add($"unlock={need} raises, note='{_main.Feed.Note(1)}'");
    }

    /// <summary>Barges held below a footbridge: never drawn over one another; turning back together, one float and one line.</summary>
    private async Task QueueAndTurnBack()
    {
        int row = FreeRow(CrossingType.Footbridge, Math.Min(W.Grid.Width - 12, R.LandingRow + 30), +1);
        var foot = _main.Sim.PlaceCrossing(CrossingType.Footbridge, (int)W.RiverX[row], row);
        Check(foot.Ok, $"footbridge: {foot.Reason}");
        _main.Rig.LookAt(new Vector3(W.RiverX[row] * 2f, 0, (row + 10) * 2f), 110f);
        await TestInput.Frames(this, 3);
        for (int k = 0; k < 4; k++) { W.SpawnBoat(BoatKind.Barge); _main.Sim.StepTicks(8); }
        int turned0 = R.Stats.TurnedBack[(int)BoatKind.Barge], maxWaiting = 0, maxTurned = 0;
        _turnTimes.Clear();
        _sameDayRepeat = false;
        _lastNote = "";
        _main.Feed.Event += OnTurnBack;
        for (int i = 0; i < 400 && R.Stats.TurnedBack[(int)BoatKind.Barge] < turned0 + 2; i++)
        {
            // Several turn-backs in one drain: steps of a few ticks, as at 8x.
            _main.Sim.StepTicks(6);
            await TestInput.Frames(this, 1);
            int waiting = Waiting();
            maxWaiting = Math.Max(maxWaiting, waiting);
            Check(_main.Boats.QueueOverlaps == 0, $"{_main.Boats.QueueOverlaps} queued boats drawn over one another ({waiting} waiting)");
            maxTurned = Math.Max(maxTurned, _main.Ui.Floats.TurnedBackCount);
        }
        _main.Feed.Event -= OnTurnBack;
        int turned = R.Stats.TurnedBack[(int)BoatKind.Barge] - turned0;
        Check(maxWaiting >= 2, $"test setup: only {maxWaiting} barges queued");
        Check(turned >= 2, $"only {turned} barges turned back");
        await TestInput.Frames(this, 2);
        // Turn-backs within one float's life make one float with a count, never a second slip.
        bool together = _turnTimes.Count >= 2 && _turnTimes[_turnTimes.Count - 1] - _turnTimes[0] < RiverFloats.Seconds * 1000;
        if (together) Check(maxTurned >= _turnTimes.Count, $"{_turnTimes.Count} boats turned back together but the float counted {maxTurned}");
        else Check(maxTurned >= 1, "no 'Turned back' float");
        // The chronicle never repeats a note on consecutive lines: the same note again that day is one line with a count.
        for (int i = 0; i + 1 < RiverFeed.ChronicleLines; i++)
        {
            string a = _main.Feed.Note(i), b = _main.Feed.Note(i + 1);
            Check(a.Length == 0 || a != b, $"the chronicle repeats '{a}'");
        }
        bool counted = false;
        for (int i = 0; i < RiverFeed.ChronicleLines; i++) counted |= _main.Feed.Note(i).Contains("(×");
        if (_sameDayRepeat) Check(counted, $"two identical turn-back notes in a row that day, but no line with a count: '{_main.Feed.Note(0)}'");
        _notes.Add($"queue waiting={maxWaiting} turned={turned} float×{maxTurned} note='{_main.Feed.Note(0)}'");
    }

    private readonly List<ulong> _turnTimes = new();
    private bool _sameDayRepeat;
    private string _lastNote = "";

    /// <summary>Real time of each turn-back, and whether one wrote the same chronicle note as the note just before it.</summary>
    private void OnTurnBack(RiverEvent e)
    {
        if (e.Kind != RiverEventKind.BoatTurnedBack) return;
        _turnTimes.Add(Time.GetTicksMsec());
        string body = _main.Feed.Note(0);
        int at = body.IndexOf(" (×", StringComparison.Ordinal);
        if (at >= 0) body = body.Substring(0, at) + ".";
        if (body == _lastNote) _sameDayRepeat = true;
        _lastNote = body;
    }

    private (int carrying, int wet) Carried()
    {
        int carrying = 0, wet = 0;
        foreach (ref readonly var k in W.Walkers) if (k.Visible && k.Cargo != 0) { carrying++; if (k.CargoWet) wet++; }
        return (carrying, wet);
    }

    private int Waiting()
    {
        int waiting = 0;
        foreach (ref readonly var b in R.Boats) if (b.State == BoatState.Waiting) waiting++;
        return waiting;
    }

    private async Task StepUntil(Func<bool> done, int max, string what)
    {
        for (int i = 0; i < max && !done(); i++) { _main.Sim.StepTicks(1); await TestInput.Frames(this, 1); }
        Check(done(), what);
    }

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
