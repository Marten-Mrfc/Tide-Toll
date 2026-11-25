using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// game-town, the Pave bug (r3-review A1): the town at the end of the age, reached by the simulation's bot without
/// paving (so many worn-in roads lie close together), and 30 Pave clicks at points along lit roads, 15 tilted close in
/// and 15 from straight above. Before each click the price tip names a road; the click must act on exactly that road
/// (and change no other), whatever road the pointer was meant for.
/// </summary>
public partial class TownTest
{
    private async Task PaveAtTheEndOfTheAge()
    {
        int seed = Main.Driver.Seed, towns = 1;
        Main.Driver.Adopt(EndOfAge(seed));
        Main.Driver.SetPaused(true);
        await Frames(10);
        var rng = new Random(7);
        int intended = 0, shifted = 0, tilted = 0, flat = 0;
        for (int i = 0; i < 30; i++)
        {
            bool flatView = i >= 15;
            for (int k = 0; k < 40 && !PavableNow(); k++) Main.Driver.Advance(15);
            if (!PavableNow())
            {
                // Every worn-in road of this town is cobbled: the next seed's town at the end of its age.
                if (Main.Hud.PaveMode) await Key(Godot.Key.Escape);
                Main.Driver.Adopt(EndOfAge(++seed));
                towns++;
                await Frames(10);
            }
            Check(PavableNow(), $"click {i}: some road can be paved");
            // A random road that can be paved now, at a random point along its curve.
            int r;
            do r = rng.Next(T.RoadCount); while (!Main.Hud.Pavable(r));
            var road = T.Plan.Roads[r];
            road.At(road.Length * (0.15 + 0.7 * rng.NextDouble()), out double x, out double y, out _);
            var local = new Vector3((float)x * TownGround.Cell, 0, (float)y * TownGround.Cell);
            float size = flatView ? Main.Rig.TiltFlatSize : Main.Rig.MinSize + 24f;
            // Pave mode first (it may bring the suggested road into view), then the view on the point meant.
            if (!Main.Hud.PaveMode) await Key(Godot.Key.P);
            Check(Main.Hud.PaveMode, $"click {i}: P switches Pave mode on");
            Main.Rig.UserMoved = true;
            Main.Rig.LookAt(local, size, instant: true);
            await Frames(3);
            // Zoomed out the view keeps the page in frame and may glide a little: wait until the point stands still.
            var last = Main.Rig.LocalToScreen(local);
            for (int w = 0; w < 600; w++)
            {
                await Frames(1);
                var now = Main.Rig.LocalToScreen(local);
                if (now.DistanceTo(last) < 0.05f) break;
                last = now;
            }
            if (flatView) { Check(Main.Rig.PitchNow > 88f, $"click {i}: flat view (pitch {Main.Rig.PitchNow:0.0})"); flat++; }
            else { Check(Main.Rig.PitchNow < 70f, $"click {i}: tilted view (pitch {Main.Rig.PitchNow:0.0})"); tilted++; }
            var at = Main.Rig.LocalToScreen(local);
            TestInput.Motion(at);
            await Frames(2);
            int tip = Main.Hud.HoverRoad;
            string tipText = Main.Hud.HoverTipText;
            Check(tip >= 0 && tipText.Contains("Click to pave"), $"click {i}: the pointer on a lit road names a road (tip {tip}, '{tipText}', road {r} pavable {Main.Hud.Pavable(r)}, direct {Main.Hud.PaveTarget(at)}, at {at}, now {Main.Rig.LocalToScreen(local)}, size {Main.Rig.Camera.Size:0} target {Main.Rig.TargetSize:0}, gui {GetViewport().GuiGetHoveredControl()?.Name})");
            var before = new Surface[T.RoadCount];
            for (int q = 0; q < before.Length; q++) before[q] = T.RoadInfo(q).Surface;
            bool shift = i % 3 == 1;
            if (shift) shifted++;
            await ClickWith(at, shift);
            Check(Main.Hud.LastPaveClickRoad == tip, $"click {i}: the click acted on road {Main.Hud.LastPaveClickRoad}, the tip named {tip}");
            int changed = 0;
            for (int q = 0; q < before.Length; q++)
                if (T.RoadInfo(q).Surface != before[q]) { changed++; Check(q == tip, $"click {i}: road {q} was paved, the tip named {tip}"); }
            Check(changed == 1, $"click {i}: exactly one road paved ({changed})");
            if (shift) Check(Main.Hud.PaveMode || !PavableNow(), $"click {i}: Shift keeps Pave mode on");
            else Check(!Main.Hud.PaveMode, $"click {i}: Pave mode ends after a pave");
            if (tip == r) intended++;
        }
        if (Main.Hud.PaveMode) await Key(Godot.Key.Escape);
        Check(intended >= 20, $"most clicks were meant for the road they paved ({intended} of 30)");
        Main.Rig.UserMoved = false;
        Main.Driver.SetPaused(false);
        Notes.Add($"endpave=30 towns={towns} tilted={tilted} flat={flat} meant={intended} shift={shifted} paved={T.RoadsPaved}");
    }

    /// <summary>A town played to its charter by the simulation's best-payback bot, never paving (many worn-in roads).</summary>
    private static Town EndOfAge(int seed)
    {
        var town = new Town(seed);
        var bot = new TownBot(BotKind.Payback, 1) { NoPave = true };
        var events = new System.Collections.Generic.List<TownEvent>();
        int ticks = 0;
        while (!town.HasCharter && ticks < 20 * 60 * 60 * 10) { town.Step(); bot.Act(town); ticks++; if (ticks % 600 == 0) town.DrainEvents(events); }
        town.DrainEvents(events);
        Check(town.HasCharter, $"the bot reached the end of the age (seed {seed})");
        return town;
    }

    private async Task ClickWith(Vector2 at, bool shift)
    {
        TestInput.Motion(at);
        await Frames(1);
        Input.ParseInputEvent(new InputEventMouseButton { Position = at, GlobalPosition = at, ButtonIndex = MouseButton.Left, Pressed = true, ShiftPressed = shift, ButtonMask = MouseButtonMask.Left });
        await Frames(1);
        Input.ParseInputEvent(new InputEventMouseButton { Position = at, GlobalPosition = at, ButtonIndex = MouseButton.Left, Pressed = false, ShiftPressed = shift });
        await Frames(2);
    }
}
