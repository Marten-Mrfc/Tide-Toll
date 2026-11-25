using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim;

namespace TideToll.Game.Tests;

/// <summary>
/// Headless test of the two chapters (the lead's decision A), through real input:
/// - the game opens on Chapter I, The Hamlet: the river rules off, rain and growth on, no ferry or drawbridge tabs,
///   no ledger, gravel and gate tabs present, the chapter page titled "Chapter I";
/// - the chapter bookmark (a click) turns the page: the page-turn moment runs (the leaf covers the book, the book is
///   built again under it) and Chapter II, The River, opens with the river rules on, the ferry and drawbridge tabs and
///   the ledger, a fresh town from the same seed (day one again), the walker count and speed kept;
/// - Reset and New map stay inside the chapter; the C key turns back to Chapter I with the new map's seed;
/// - keys pressed while the page turns change nothing.
/// Prints CHAPTER PASS or CHAPTER FAIL and quits with 0 or 1.
/// </summary>
public partial class ChapterTest : Node
{
    private Main _main = null!;
    private readonly List<string> _notes = new();

    public override void _Ready()
    {
        GetWindow().Size = new Vector2I(1280, 720);
        Input.UseAccumulatedInput = false;
        _main = GD.Load<PackedScene>("res://main.tscn").Instantiate<Main>();
        _main.StartSeed = 1;
        AddChild(_main);
        Run();
    }

    private async void Run()
    {
        try
        {
            await TestInput.Frames(this, 3);
            await Opening();
            await TurnByBookmark();
            await ResetAndNewMap();
            await TurnBackByKey();
            GD.Print("CHAPTER PASS " + string.Join(" ", _notes));
            GetTree().Quit(0);
        }
        catch (Exception e)
        {
            GD.PrintErr("CHAPTER FAIL: " + e);
            GetTree().Quit(1);
        }
    }

    private static void Check(bool ok, string what) { if (!ok) throw new Exception(what); }

    private string ChapterTitle() => _main.Ui.Chapter.GetNode("Column/Title").GetChild<Label>(0).Text;

    private void CheckChapter(int ch)
    {
        var w = _main.Sim.World;
        Check(_main.Chapter == ch, $"chapter is {_main.Chapter}, expected {ch}");
        Check(ChapterTitle() == "Chapter " + Main.ChapterNumerals[ch], $"chapter page title '{ChapterTitle()}'");
        var c = _main.Ui.Controls;
        Check(c.ContainsKey("Gravel") && c.ContainsKey("Gate") && c.ContainsKey("Fence") && c.ContainsKey("Footbridge") && c.ContainsKey("Erase"),
            "a path tool tab is missing");
        Check(c.ContainsKey("Chapter"), "no chapter bookmark");
        bool ledger = _main.Ui.Chapter.GetNodeOrNull("Column/Coin") != null;
        if (ch == 1)
        {
            Check(w.River == null && _main.Params.River == null, "Chapter I has the river rules on");
            Check(w.RainOn && w.GrowthOn, "Chapter I should rain and grow houses");
            Check(!c.ContainsKey("Ferry") && !c.ContainsKey("Drawbridge"), "Chapter I shows crossing tabs");
            Check(!ledger, "Chapter I shows the ledger");
        }
        else
        {
            Check(w.River != null, "Chapter II has the river rules off");
            Check(!w.RainOn && !w.GrowthOn, "rain or growth on with the river rules");
            Check(c.ContainsKey("Ferry") && c.ContainsKey("Drawbridge"), "Chapter II lacks the crossing tabs");
            Check(ledger, "Chapter II has no ledger");
        }
    }

    private Task Opening()
    {
        CheckChapter(1);
        Check(_main.ChapterBuilds == 1, "the book was built more than once at start");
        _notes.Add("opens=I");
        return Task.CompletedTask;
    }

    private async Task TurnByBookmark()
    {
        var sim = _main.Sim;
        sim.SetWalkers(150);
        sim.SetSpeed(4);
        sim.StepTicks(3 * _main.Params.TicksPerDay);
        Check(sim.World.Time > 0, "no time passed");
        var oldSim = sim;
        var mark = _main.Ui.Controls["Chapter"];
        await TestInput.Frames(this, 2);
        var at = TestInput.Centre(mark);
        TestInput.Motion(at);
        TestInput.Button(at, MouseButton.Left, true);
        TestInput.Button(at, MouseButton.Left, false);
        await TestInput.Frames(this, 2);
        Check(_main.Turn.Busy, "the bookmark did not start a page turn");
        // A key during the turn changes nothing (the fence tool stays down, the speed stays).
        TestInput.Key(Key.F);
        TestInput.Key(Key.Key1);
        float most = 0;
        int frames = 0;
        while (_main.Turn.Busy && frames < 400) { most = Math.Max(most, _main.Turn.Cover); await TestInput.Frames(this, 1); frames++; }
        Check(!_main.Turn.Busy, "the page turn never ended");
        Check(most >= 0.999f, $"the leaf never covered the book ({most:F2})");
        Check(_main.Turn.Turns == 1, "page turns not counted");
        CheckChapter(2);
        Check(_main.Sim != oldSim && !IsInstanceValid(oldSim) || _main.Sim != oldSim, "the simulation was not replaced");
        var w = _main.Sim.World;
        Check(w.Seed == 1 && w.Ticks == 0, $"Chapter II did not start a fresh town from seed 1 (seed {w.Seed}, tick {w.Ticks})");
        Check(_main.Sim.WalkerCount == 150 && _main.Sim.Speed == 4, $"walkers {_main.Sim.WalkerCount} or speed {_main.Sim.Speed} not kept");
        Check(_main.Tools.Tool == Tool.None, "a key pressed during the turn picked up a tool");
        Check(_main.ChapterBuilds == 2, $"book built {_main.ChapterBuilds} times");
        Check(_main.Ui.Chapter.GetNodeOrNull("Column/Coin/Value") is Label { } coin, "no coin entry");
        _notes.Add($"turn-frames={frames}");
    }

    private async Task ResetAndNewMap()
    {
        var sim = _main.Sim;
        sim.StepTicks(200);
        TestInput.Key(Key.R);
        await TestInput.Frames(this, 2);
        Check(_main.Chapter == 2 && _main.Sim.World.River != null && _main.Sim.World.Ticks < 10, "reset left the chapter or did not start over");
        TestInput.Key(Key.N);
        await TestInput.Frames(this, 2);
        Check(_main.Chapter == 2 && _main.Sim.World.River != null, "new map left Chapter II");
        Check(_main.Sim.Seed == SimDriver.NextSeed(1), $"new map seed {_main.Sim.Seed}");
        _notes.Add($"newseed={_main.Sim.Seed}");
    }

    private async Task TurnBackByKey()
    {
        int seed = _main.Sim.Seed;
        TestInput.Key(Key.C);
        await TestInput.Frames(this, 2);
        Check(_main.Turn.Busy, "C did not turn the page");
        int frames = 0;
        while (_main.Turn.Busy && frames < 400) { await TestInput.Frames(this, 1); frames++; }
        CheckChapter(1);
        Check(_main.Sim.Seed == seed && _main.Sim.World.Ticks < 20, "Chapter I did not start fresh from the current seed");
        _main.Sim.Reset();
        CheckChapter(1);
        _notes.Add("back=I");
    }
}
