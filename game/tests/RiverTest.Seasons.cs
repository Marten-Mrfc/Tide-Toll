using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim;

namespace TideToll.Game.Tests;

/// <summary>
/// The river test's second half: the seasons and the overlap checks.
/// - Overlaps: at 1280 x 720 and 1920 x 1080, in the play view with the drawbridge selected (its full badge showing), the
///   badge lies on screen and clear of the bridge itself and of every place tag showing (Well, Market, Field, Ford).
/// - Seasons: World.StartFlood(1) puts the band under water: the ground's flood wash fills, the closed-ford bar and the
///   "Ford closed" tag show (the plain Ford tag gives way), a margin note and the ledger's "Spring flood, day N" appear,
///   and the footbridge tool by the ford says the ford is closed by the flood; when the water goes down they all go and
///   the ledger says mud. World.Freeze(): the ice look eases in, boats stand still, walkers cross on the ice and the
///   ice-wear layer is uploaded, the note and "Frozen since day N" appear, the river loop drops; World.Thaw() clears it.
/// </summary>
public partial class RiverTest
{
    private async Task Overlaps(int draw)
    {
        foreach (var size in new[] { new Vector2I(1280, 720), new Vector2I(1920, 1080) })
        {
            GetWindow().Size = size;
            await TestInput.Frames(this, 4);
            _main.Rig.FramePlay(instant: true);
            _main.Tools.Select(draw);
            await TestInput.Frames(this, 4);
            var card = _main.Ui.Badges.Card(draw)!;
            Check(card.Visible, $"{size}: the selected drawbridge's badge is hidden in the play view");
            var r = card.GetGlobalRect();
            var vp = GetViewport().GetVisibleRect().Size;
            Check(r.Position.X >= 0 && r.Position.Y >= 0 && r.End.X <= vp.X && r.End.Y <= vp.Y, $"{size}: badge {r} off screen");
            var bridge = _main.Ui.Badges.BridgeRect(draw);
            Check(!r.Intersects(bridge), $"{size}: the badge {r} covers its bridge {bridge}");
            int tags = 0;
            for (int i = 0; i < _main.Ui.Tags.Count; i++)
            {
                if (!_main.Ui.Tags.VisibleRect(i, out var t)) continue;
                tags++;
                Check(!r.Intersects(t), $"{size}: the badge {r} covers the tag '{_main.Ui.Tags.Tag(i).Name}' {t}");
            }
            Check(tags >= 3, $"{size}: only {tags} place tags show in the play view");
            _notes.Add($"badge@{size.X}=clear({tags} tags)");
        }
        _main.Tools.Select(-1);
        GetWindow().Size = new Vector2I(1280, 720);
        await TestInput.Frames(this, 4);
    }

    private string RiverLine => _main.Ui.Chapter.GetNode<Label>("Column/Theriver/Value").Text;

    private async Task Seasons()
    {
        var w = W;
        var r = R;
        for (int i = 0; i < 4 * _main.Params.TicksPerDay && r.Flooded; i++) _main.Sim.StepTicks(1); // a flood of its own first
        _main.Rig.FramePlay(instant: true);
        await TestInput.Frames(this, 3);
        Check(!_main.Crossings.FordClosedShown && !_main.Ui.Tags.Tag(MapTags.FordClosed).Visible, "the ford is marked closed before any flood");
        // The flood.
        var flood = w.StartFlood(1);
        Check(flood.Ok && r.Flooded && !r.FordOpen, $"the flood did not start: {flood}");
        await WaitFor(() => _main.Ground.Flood >= 1f, 5);
        Check(_main.Ground.Flood >= 1f, $"the flood wash did not fill ({_main.Ground.Flood:0.00})");
        Check(_main.Crossings.FordClosedShown, "no closed-ford bar");
        Check(_main.Ui.Tags.Tag(MapTags.FordClosed).Visible, "no 'Ford closed' tag in the play view");
        Check(!_main.Ui.Tags.Tag(3).Visible, "the plain Ford tag still shows beside 'Ford closed'");
        Check(FeedHas("Spring flood"), $"no margin note for the flood: '{_main.Feed.Note(0)}'");
        await WaitFor(() => RiverLine.StartsWith("Spring flood, day"), 3);
        Check(RiverLine.StartsWith($"Spring flood, day {_main.Feed.FloodDay}"), $"the ledger's river line says '{RiverLine}'");
        // Up close the bar stays (every zoom) and the tag too.
        _main.Rig.LookAt(new Vector3(w.RiverX[w.Map.FordY] * 2f, 0, w.Map.FordY * 2f), 60f);
        await TestInput.Frames(this, 3);
        Check(_main.Crossings.FordClosedShown && _main.Ui.Tags.Tag(MapTags.FordClosed).Visible, "the ford's closed marks go when zoomed in");
        // The footbridge tool by the ford gives the flood as part of its reason.
        TestInput.Key(Godot.Key.B);
        await TestInput.Frames(this, 2);
        TestInput.Motion(TestInput.CellScreen(_main, (int)w.RiverX[w.Map.FordY + 1], w.Map.FordY + 1));
        await TestInput.Frames(this, 2);
        string why = _main.Tools.LastMessage;
        Check(why.Contains("ford") && why.Contains("closed by the spring flood"), $"the tool's reason by the closed ford: '{why}'");
        TestInput.Key(Godot.Key.Escape);
        await TestInput.Frames(this, 1);
        // The water goes down.
        for (int i = 0; i < 2 * _main.Params.TicksPerDay && r.Flooded; i++) _main.Sim.StepTicks(1);
        Check(!r.Flooded && r.FordOpen, "the flood never went down");
        _main.Rig.FramePlay(instant: true);
        await WaitFor(() => _main.Ground.Flood <= 0f, 6);
        Check(_main.Ground.Flood <= 0f, $"the flood wash stayed ({_main.Ground.Flood:0.00})");
        Check(!_main.Crossings.FordClosedShown && !_main.Ui.Tags.Tag(MapTags.FordClosed).Visible, "the ford still looks closed after the flood");
        Check(FeedHas("flood went down"), "no margin note when the flood went down");
        await WaitFor(() => RiverLine.StartsWith("Mud"), 3);
        Check(RiverLine.StartsWith("Mud") == r.Muddy, $"after the flood the river line says '{RiverLine}' (muddy {r.Muddy})");
        _notes.Add($"flood=ok(day {_main.Feed.FloodDay})");

        // The ice.
        _main.Rig.LookAt(new Vector3(w.RiverX[r.LandingRow] * 2f, 0, r.LandingRow * 2f), 150f);
        await TestInput.Frames(this, 3);
        await WaitFor(() => false, 1.2); // the river loop settles at this view
        float db0 = _main.Audio.RiverDb;
        int uploads = _main.Ground.IceUploads;
        var ice = w.Freeze();
        Check(ice.Ok && r.Frozen, $"the river did not freeze: {ice}");
        Check(!w.Freeze().Ok, "a second freeze was not refused");
        int boat = r.Boats.Length > 0 ? r.Boats[0].Id : -1;
        double by = BoatY(boat);
        int crossings = r.Stats.IceCrossings;
        for (int i = 0; i < _main.Params.TicksPerDay / 10; i++) { _main.Sim.StepTicks(10); await TestInput.Frames(this, 1); }
        if (boat >= 0) Check(BoatY(boat) == by, $"a boat moved on the ice ({by} to {BoatY(boat)})");
        Check(r.Stats.IceCrossings > crossings, "nobody crossed on the ice in a day");
        await WaitFor(() => _main.Ground.Ice >= 1f && _main.Audio.RiverDb < db0 - 10f, 4);
        Check(_main.Ground.Ice >= 1f, $"the ice look did not ease in ({_main.Ground.Ice:0.00})");
        Check(_main.Ground.IceUploads > uploads, "the ice-wear layer was never uploaded");
        Check(_main.Audio.RiverDb < db0 - 10f, $"the river loop did not quieten on the ice ({db0:0.0} to {_main.Audio.RiverDb:0.0} dB)");
        Check(FeedHas("river froze"), "no margin note for the freeze");
        await WaitFor(() => RiverLine.StartsWith("Frozen since day"), 3);
        Check(RiverLine.StartsWith($"Frozen since day {_main.Feed.FrozenDay}"), $"the ledger's river line says '{RiverLine}'");
        // The thaw: the ice paths go with it.
        int u2 = _main.Ground.IceUploads;
        Check(w.Thaw().Ok && !r.Frozen, "the thaw was refused");
        _main.Sim.StepTicks(1);
        await WaitFor(() => _main.Ground.Ice <= 0f && _main.Ground.IceUploads > u2, 5);
        Check(_main.Ground.Ice <= 0f && _main.Ground.IceUploads > u2, $"the ice look stayed after the thaw ({_main.Ground.Ice:0.00}, uploads {_main.Ground.IceUploads - u2})");
        float most = 0;
        for (int i = 0; i < r.IceWear.Length; i++) most = Math.Max(most, r.IceWear[i]);
        Check(most == 0f, "the ice-wear layer outlived the thaw");
        Check(FeedHas("ice broke up"), "no margin note for the thaw");
        await WaitFor(() => !RiverLine.StartsWith("Frozen"), 3);
        Check(!RiverLine.StartsWith("Frozen"), $"the river line still says '{RiverLine}' after the thaw");
        _notes.Add($"ice=ok(crossings {r.Stats.IceCrossings - crossings}, river {db0:0}->frozen)");
    }

    private double BoatY(int id)
    {
        var bs = R.Boats;
        for (int i = 0; i < bs.Length; i++) if (bs[i].Id == id) return bs[i].Y;
        return double.NaN;
    }

    private bool FeedHas(string words)
    {
        for (int i = 0; i < _main.Feed.NoteCount; i++) if (_main.Feed.Note(i).Contains(words)) return true;
        return false;
    }
}
