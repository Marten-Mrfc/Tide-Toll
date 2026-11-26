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
        await CardsStopClicks();
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

    /// <summary>
    /// Round 4 (r4-review A3): a road lying under the ledger card can be neither hovered, paved nor dragged through the
    /// card with real input; a note slip and the request card count as cards too; Pave mode brings a suggested road
    /// lying under a card out from under it.
    /// </summary>
    private async Task CardsStopClicks()
    {
        var ledger = Main.Hud.GetNode<Control>("Root/Ledger");
        Check(ledger.IsVisibleInTree(), "the ledger card shows");
        for (int k = 0; k < 40 && !PavableNow(); k++) Main.Driver.Advance(15);
        await Frames(2);
        if (!Main.Hud.PaveMode) await Key(Godot.Key.P);
        Check(Main.Hud.PaveMode, $"cards: P switches Pave mode on ('{Main.Hud.LastNote}')");
        int r = T.SuggestedPave();
        if (r < 0 || !Main.Hud.Pavable(r)) for (r = 0; r < T.RoadCount && !Main.Hud.Pavable(r); r++) { }
        var at = await PlaceUnder(ledger, r);
        Check(Main.Rig.PickLocal(at, out _) && Main.Hud.OverInterface(at), "cards: the road's middle lies under the ledger");
        TestInput.Motion(at);
        await Frames(2);
        Check(Main.Hud.HoverRoad < 0 && Main.Hud.PaveTarget(at) < 0, $"cards: a road under the ledger is named by the tip (road {Main.Hud.HoverRoad})");
        int paved = T.RoadsPaved; double coin = T.Coin;
        await Click(at);
        Check(T.RoadsPaved == paved && T.Coin == coin, "cards: a click on the ledger paved the road under it");
        // A drag that starts on the card does not move the map.
        var pivot = Main.Rig.PivotLocal;
        TestInput.Button(at, MouseButton.Left, true);
        await Frames(1);
        for (int k = 1; k <= 6; k++) { TestInput.Motion(at + new Vector2(40 * k, 25 * k), new Vector2(40, 25)); await Frames(1); }
        TestInput.Button(at + new Vector2(240, 150), MouseButton.Left, false);
        await Frames(3);
        Check(Main.Rig.PivotLocal.DistanceTo(pivot) < 0.01f, $"cards: a drag from the ledger moved the map ({Main.Rig.PivotLocal.DistanceTo(pivot):0.00} m)");
        Check(T.RoadsPaved == paved, "cards: the drag paved a road");
        // A note slip is a card too.
        Main.Hud.Note("A note to click on.");
        await Frames(2);
        var notes = Main.Hud.GetNode<Control>("Root/Notes");
        var slip = notes.GetChild<Control>(notes.GetChildCount() - 1);
        Check(Main.Hud.OverInterface(slip.GetGlobalRect().GetCenter()), "cards: a note slip does not count as a card");
        // Leave Pave mode, put the suggested road under the ledger, and choose Pave again: the road comes out from under it.
        await Key(Godot.Key.Escape);
        Check(!Main.Hud.PaveMode && !Main.Menu.IsPaused, "cards: Esc leaves Pave mode");
        for (int k = 0; k < 40 && !(T.SuggestedPave() >= 0 && Main.Hud.Pavable(T.SuggestedPave())); k++) Main.Driver.Advance(15);
        int sug = T.SuggestedPave();
        if (sug >= 0 && Main.Hud.Pavable(sug))
        {
            Main.Rig.UserMoved = true;
            await PlaceUnder(ledger, sug);
            await Key(Godot.Key.P);
            Check(Main.Hud.PaveMode, "cards: P switches Pave mode on again");
            await Frames(90);
            var now = Main.Hud.RoadScreen(sug);
            Check(!Main.Hud.OverInterface(now), $"cards: Pave mode left the suggested road under a card (at {now})");
            await Key(Godot.Key.Escape);
            Notes.Add("cards=ok bring=ok");
        }
        else Notes.Add("cards=ok bring=skipped");
        Main.Rig.UserMoved = true;
    }

    /// <summary>Moves the view (close in, tilted) so the middle of road r lies at the centre of a card; returns that screen point.</summary>
    private async Task<Vector2> PlaceUnder(Control card, int r)
    {
        var road = T.Plan.Roads[r];
        road.At(road.Length / 2, out double x, out double y, out _);
        var local = new Vector3((float)x * TownGround.Cell, 0, (float)y * TownGround.Cell);
        var on = card.GetGlobalRect().GetCenter();
        float size = Main.Rig.MinSize + 24f;
        Main.Rig.UserMoved = true; // a still view: the follow view would glide away
        Main.Rig.LookAt(local, size, instant: true);
        await Frames(3);
        for (int k = 0; k < 4; k++)
        {
            Check(Main.Rig.PickLocal(on, out var under), "the card's centre is over the ground");
            Main.Rig.LookAt(Main.Rig.PivotLocal + (local - under), size, instant: true);
            await Frames(3);
        }
        var at = Main.Rig.LocalToScreen(local);
        Check(at.DistanceTo(on) < 12f, $"road {r} could not be put under the card (at {at}, card centre {on})");
        return at;
    }

    /// <summary>A town played to its charter by the simulation's best-payback bot, never paving (many worn-in roads).</summary>
    private static Town EndOfAge(int seed)
    {
        var town = new Town(seed);
        var bot = new TownBot(BotKind.Payback, 1) { NoPave = true, NoHands = true };
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
