using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim;

namespace TideToll.Game.Tests;

/// <summary>
/// Headless test of Chapter I's living town (steps 3 and 4 of the game round):
/// - a house arriving: when the library builds a cottage during a tick, the town draws it (a node for every building,
///   rising, then standing), its yard and shadow are added, the mark rings it, the chapter page says "Houses: N" and
///   "newest on day N", the walker count follows and every walker out is drawn;
/// - a forced shower (World.StartRain): the rain layer shows over the map page, the ground wets, trails turn to mud
///   where the library says, the weather line reads "Rain, day N", then "Mud until day N", and the mud dries to "Dry";
///   footsteps follow the wet ground;
/// - the growth room used up (a small reserve: HamletTarget 12, so room for 5 grown houses, the walker count lowered by
///   hand after each): the page says so once under Houses ("no room for more families") the moment World.GrowthRoomFull
///   turns true, and not before.
/// Prints HAMLET PASS or HAMLET FAIL and quits with 0 or 1.
/// </summary>
public partial class HamletTest : Node
{
    private Main _main = null!;
    private readonly List<string> _notes = new();

    public override void _Ready()
    {
        GetWindow().Size = new Vector2I(1280, 720);
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
            Check(_main.Chapter == 1 && _main.Sim.World.GrowthOn && _main.Sim.World.RainOn, "not Chapter I with growth and rain");
            await HouseArrives();
            await Shower();
            await RoomFull();
            GD.Print("HAMLET PASS " + string.Join(" ", _notes));
            GetTree().Quit(0);
        }
        catch (Exception e)
        {
            GD.PrintErr("HAMLET FAIL: " + e);
            GetTree().Quit(1);
        }
    }

    private static void Check(bool ok, string what) { if (!ok) throw new Exception(what); }

    private async Task HouseArrives()
    {
        var sim = _main.Sim;
        sim.SetPaused(true);
        var w = sim.World;
        int houses = w.Houses.Length, buildings = w.Buildings.Length, walkers = w.WalkerCount, shadows = _main.Marks.Shadows;
        // Tick by tick until the library builds the first cottage (the frame loop is paused, so this is the only clock).
        int ticks = 0;
        while (w.Grown.Length == 0 && ticks < 60 * _main.Params.TicksPerDay) { sim.StepTicks(1); ticks++; }
        Check(w.Grown.Length == 1, $"no house grew in {ticks} ticks");
        var g = w.Grown[0];
        Check(w.Buildings.Length == buildings + 1 && w.Houses.Length == houses + 1, "the library did not add a building");
        Check(_main.Town.Grown == 1 && _main.Town.GetChildCount() == w.Buildings.Length, $"town draws {_main.Town.GetChildCount()} of {w.Buildings.Length} buildings");
        var b = w.Buildings[g.Building];
        var node = _main.Town.GetNodeOrNull<Node3D>($"House{b.Id}");
        Check(node != null, "no node for the new cottage");
        Check(_main.Town.Rising == 1 && node!.Scale.Y < 0.5f, "the new cottage is not rising");
        Check(_main.Marks.Shadows == shadows + 1, $"yard marks {_main.Marks.Shadows}, expected {shadows + 1}");
        Check(w.WalkerCount == walkers + _main.Params.HouseholdSize, $"walkers {w.WalkerCount}, expected {walkers + _main.Params.HouseholdSize}");
        await TestInput.Frames(this, 2);
        Check(_main.Ui.Houses.Showing >= 1, "no mark on the new house");
        // It stands up within the raising time, and the page counts it.
        double t0 = _main.Town.Now;
        while (_main.Town.Rising > 0 && _main.Town.Now - t0 < TownLayer.RaiseSec + 1) await TestInput.Frames(this, 1);
        Check(_main.Town.Rising == 0 && Mathf.IsEqualApprox(node!.Scale.Y, 1f), "the cottage never finished rising");
        await TestInput.Frames(this, 40);
        var page = _main.Ui.Chapter;
        string count = page.GetNode<Label>("Column/Houses/Value").Text, note = page.GetNode<Label>("Column/HousesNote").Text;
        Check(count == w.Houses.Length.ToString(), $"page says {count} houses, the town has {w.Houses.Length}");
        Check(note == $"newest on day {g.Day}", $"houses note '{note}'");
        // Every walker out is drawn, the new household included.
        sim.StepTicks(200);
        _main.Walkers.Sync();
        Check(_main.Walkers.Drawn == w.WalkersOut(), $"drew {_main.Walkers.Drawn} of {w.WalkersOut()} walkers out");
        _notes.Add($"house@tick{ticks}(day {g.Day}) walkers={w.WalkerCount}");
    }

    private async Task Seconds(double s)
    {
        ulong until = Time.GetTicksMsec() + (ulong)(s * 1000);
        while (Time.GetTicksMsec() < until) await TestInput.Frames(this, 1);
    }

    /// <summary>A town that has used the room kept for its houses says so once, under Houses, instead of going quiet.</summary>
    private async Task RoomFull()
    {
        var sim = _main.Sim;
        sim.SetPaused(true);
        // A small reserve: World pre-sizes room for min(HamletTarget, MaxWalkers) / HouseholdSize + 1 grown houses.
        sim.Params = _main.Params with { HamletTarget = 12 };
        sim.Reset();
        var w = sim.World;
        int room = sim.Params.MaxGrownHouses;
        Check(w.GrowthOn && w.GrowthRoomLeft == room && room <= 6, $"test setup: room for {w.GrowthRoomLeft} houses");
        sim.StepTicks(3 * _main.Params.TicksPerDay); // trails worn at the full walker count first (no growth above the target)
        var note = _main.Ui.Chapter.GetNode<Label>("Column/HousesNote");
        int ticks = 0, said = 0;
        bool wasSaid = false;
        while (!w.GrowthRoomFull && ticks < 40 * _main.Params.TicksPerDay)
        {
            sim.SetWalkers(0); // lowered by hand, the only way the room fills
            sim.StepTicks(20);
            ticks += 20;
            if (ticks % 200 == 0)
            {
                await Seconds(0.6); // the page refreshes about twice a second
                Check(w.GrowthRoomFull || !note.Text.Contains(ChapterPage.RoomFullText), $"the page says the room is full with {w.GrowthRoomLeft} left: '{note.Text}'");
            }
        }
        Check(w.GrowthRoomFull, $"the growth room never filled ({w.Grown.Length} of {room} grown in {ticks} ticks)");
        for (int i = 0; i < 4; i++)
        {
            sim.StepTicks(50);
            await Seconds(0.6);
            bool shown = note.Text.Contains(ChapterPage.RoomFullText);
            if (shown && !wasSaid) said++;
            wasSaid = shown;
            Check(shown, $"after the room filled the houses note says '{note.Text}'");
            int at = note.Text.IndexOf(ChapterPage.RoomFullText, StringComparison.Ordinal);
            Check(note.Text.IndexOf(ChapterPage.RoomFullText, at + 1, StringComparison.Ordinal) < 0, $"said twice: '{note.Text}'");
        }
        Check(said == 1, $"the full room was announced {said} times");
        _notes.Add($"room-full after {w.Grown.Length} houses: '{note.Text}'");
    }

    private async Task Shower()
    {
        var sim = _main.Sim;
        var w = sim.World;
        var p = _main.Params;
        sim.StepTicks(4 * p.TicksPerDay);   // trails worn enough to turn to mud
        if (w.Weather.Raining || w.Weather.Wetness > 0) { w.StopRain(dry: true); sim.StepTicks(2); }
        await TestInput.Frames(this, 40);
        Check(_main.Ui.Chapter.WeatherText == "Dry", $"weather line '{_main.Ui.Chapter.WeatherText}' before the shower");
        Check(_main.Ui.Rain.Level == 0 && _main.Ground.MudShownMax == 0, "rain or mud drawn on dry ground");
        // A forced shower of one day.
        w.StartRain(p.DaySimSec);
        sim.StepTicks((int)(p.RainRampSimSec * p.TickHz) + 5);
        Check(w.Weather.Raining && w.MudCells > 0, $"the library has no shower or no mud (mud cells {w.MudCells})");
        int rainDay = (int)Math.Floor(w.Weather.RainStart / p.DaySimSec) + 1;
        sim.SetPaused(false);
        sim.SetSpeed(1);
        int steps0 = _main.Audio.MudSteps;
        for (int i = 0; i < 400 && (_main.Ground.Wet < 0.5f || _main.Audio.MudSteps == steps0); i++) await TestInput.Frames(this, 1);
        sim.SetPaused(true);
        _main.Ground.Upload();
        await TestInput.Frames(this, 40);
        Check(_main.Ui.Rain.Level > 0.1f && _main.Ui.Rain.Visible, $"no rain over the map (level {_main.Ui.Rain.Level})");
        Check(_main.Ground.Wet > 0.5f, $"the ground is not drawn wet ({_main.Ground.Wet:F2})");
        Check(_main.Ground.MudShownMax > 0.9f, $"mud drawn at most {_main.Ground.MudShownMax:F2} while it rains");
        Check(_main.Ui.Chapter.WeatherText == $"Rain, day {rainDay}", $"weather line '{_main.Ui.Chapter.WeatherText}' in the rain");
        Check(_main.Audio.MudSteps > steps0, "no wet footsteps on the mud");
        Check(_main.Audio.RainDb > -70, $"no rain bed ({_main.Audio.RainDb:F0} dB)");
        int mudCells = w.MudCells;
        // The shower stops: the rain goes, the mud stays its days, then dries from the least worn cells first.
        w.StopRain();
        sim.StepTicks(5 * (int)p.TickHz);
        await TestInput.Frames(this, 40);
        int mudDay = (int)Math.Floor(w.Weather.MudUntil / p.DaySimSec) + 1;
        Check(!w.Weather.Raining && w.MudCells > 0, "the mud went with the rain");
        Check(_main.Ui.Rain.Level == 0 && !_main.Ui.Rain.Visible, "rain still drawn after it stopped");
        Check(_main.Ui.Chapter.WeatherText == $"Mud until day {mudDay}", $"weather line '{_main.Ui.Chapter.WeatherText}' after the rain");
        // Half way through the drying window: some mud cells drawn dry, none wetter than the ground's wetness.
        double half = w.Weather.MudUntil - p.DrySimSec * 0.5;
        while (w.Time < half) sim.StepTicks(10);
        _main.Ground.Upload();
        double wet = w.Weather.Wetness;
        Check(wet > 0.2 && wet < 0.8, $"wetness {wet:F2} half way through the drying");
        Check(_main.Ground.MudShownMax <= wet + 0.01 && _main.Ground.MudShownMax > 0, $"mud drawn at {_main.Ground.MudShownMax:F2} with wetness {wet:F2}");
        // Dry: no mud, the line says so, the ground is no longer wet.
        while (w.Time < w.Weather.MudUntil + 1) sim.StepTicks(10);
        _main.Ground.Upload();
        for (int i = 0; i < 300 && _main.Ground.Wet > 0; i++) await TestInput.Frames(this, 1);
        await TestInput.Frames(this, 40);
        Check(w.MudCells == 0 && _main.Ground.MudShownMax == 0, "mud left after the ground dried");
        Check(_main.Ground.Wet == 0, $"ground still drawn wet ({_main.Ground.Wet:F2})");
        Check(_main.Ui.Chapter.WeatherText == "Dry", $"weather line '{_main.Ui.Chapter.WeatherText}' once dry");
        _notes.Add($"shower=day{rainDay} mud={mudCells} until-day{mudDay} wetsteps={_main.Audio.MudSteps - steps0}");
    }
}
