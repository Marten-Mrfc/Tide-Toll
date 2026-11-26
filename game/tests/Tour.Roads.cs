using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>The tour's first age, later: the lane worn in and paved, a crowded road, hands, touches, the shop, the
/// mill, cobbles, the charter drive and the charter.</summary>
public partial class Tour
{
    private async Task RoadView()
    {
        Main.Rig.UserMoved = true;
        Main.Rig.LookAt(_roadPivot, _roadSize, instant: true);
        await Frames(2);
    }

    private async Task RoadsAndCrowds()
    {
        // d02: the same lane worn in (same view as d01).
        if (await Fast(t => t.RoadInfo(_road).WornIn, 30))
        {
            await RoadView();
            await Settle(1500);
            await Calm();
            await Shot("d02-trail-worn");
        }
        else NotMade("d02-trail-worn", "the lane did not wear in within 30 minutes");

        // d03 and d04: Pave mode by its bookmark, the lane pointed at, then clicked: gravel (same view).
        if (await Idle(t => t.CanPaveNow(_road), 10) || await Fast(t => t.CanPaveNow(_road), 10))
        {
            await RoadView();
            await Settle(800);
            await Calm();
            var pm = Main.Hud.Mark(ShopItemKind.Pave);
            Check(pm != null, "a Pave bookmark");
            await Click(TestInput.Centre(pm!));
            Check(Main.Hud.PaveMode, "d03: the Pave bookmark started Pave mode");
            var at = Main.Hud.RoadScreen(_road);
            TestInput.Motion(at);
            await Frames(4);
            await Wait(600);
            if (Main.Hud.HoverRoad != _road) Wrong("d03-pave-mode", $"the pointer is on the lane but the tip names road {Main.Hud.HoverRoad}.");
            await Shot("d03-pave-mode");
            await Click(at);
            if (Main.Hud.PaveMode) await Key(Godot.Key.Escape);
            TestInput.Motion(new Vector2(960, 1070));
            Check(T.RoadInfo(_road).Surface == Surface.Gravel, "d04: the click paved the lane with gravel");
            await Settle(1500);
            await Calm();
            await Shot("d04-gravel");
        }
        else { NotMade("d03-pave-mode", "the lane could not be paved within 20 minutes"); NotMade("d04-gravel", "not paved"); }

        // d06: a crowded unpaved road: the bot buys villagers and never paves until some track road crowds.
        int crowd = -1;
        if (await Fast(t => (crowd = Crowded(t)) >= 0, 40))
        {
            var info = T.RoadInfo(crowd);
            await LookRoad(crowd, 34f);
            await Settle(1500);
            await Calm(8000);
            crowd = Crowded(T) >= 0 ? Crowded(T) : crowd;
            await Shot("d06-crowded");
            Notes.Add($"crowded road {crowd} crowding {info.Crowding:0.00}");
        }
        else NotMade("d06-crowded", "no unpaved road crowded within 40 minutes of play without paving");

        // d05: the lane in cobbles (level 8) by clicks, another worn road in gravel, earth tracks still near (the bot
        // has not paved yet).
        if (await Fast(t => t.Level >= 8 && t.CanPaveNow(_road), 60))
        {
            await RoadView();
            await PaveByClick(_road);
            int other = -1; double best = 1e9;
            var mid = T.Plan.Roads[_road];
            for (int r = 0; r < T.RoadCount; r++)
            {
                if (r == _road || !T.RoadOpen(r) || T.RoadInfo(r).Surface != Surface.Track || !T.CanPave(r, out _)) continue;
                var rr = T.Plan.Roads[r];
                double dx = rr.Xs[rr.Xs.Length / 2] - mid.Xs[mid.Xs.Length / 2], dy = rr.Ys[rr.Ys.Length / 2] - mid.Ys[mid.Ys.Length / 2];
                if (dx * dx + dy * dy < best) { best = dx * dx + dy * dy; other = r; }
            }
            if (other >= 0) { await Idle(t => t.CanPaveNow(other), 5); await PaveByClick(other); }
            Main.Rig.UserMoved = true;
            Main.Rig.LookAt(_roadPivot, 52f, instant: true);
            await Settle(1500);
            await Calm();
            if (T.RoadInfo(_road).Surface != Surface.Cobbles) Wrong("d05-cobbles", "The lane did not become cobbles.");
            if (other < 0 || T.RoadInfo(other).Surface != Surface.Gravel) Wrong("d05-cobbles", "No second road could be laid with gravel beside it.");
            await Shot("d05-cobbles");
        }
        else NotMade("d05-cobbles", "the lane could not be cobbled");
        _bot.NoPave = false;
    }

    /// <summary>Pave mode by its bookmark, the road pointed at and clicked, Pave mode left with Esc.</summary>
    private async Task PaveByClick(int r)
    {
        await Frames(2);
        if (!Main.Hud.PaveMode) await Click(TestInput.Centre(Main.Hud.Mark(ShopItemKind.Pave)!));
        var at = Main.Hud.RoadScreen(r);
        TestInput.Motion(at);
        await Frames(3);
        if (Main.Hud.HoverRoad == r) await Click(at);
        else Wrong("d05-cobbles", $"Pointing at road {r} the tip named road {Main.Hud.HoverRoad}; not clicked.");
        if (Main.Hud.PaveMode) await Key(Godot.Key.Escape);
        TestInput.Motion(new Vector2(960, 1070));
        await Frames(2);
    }

    private static int Crowded(Town t)
    {
        int best = -1; double most = 1.15;
        for (int r = 0; r < t.RoadCount; r++)
        {
            if (!t.RoadOpen(r)) continue;
            var i = t.RoadInfo(r);
            if (i.Surface == Surface.Track && i.Crowding > most && i.Walkers >= 3) { most = i.Crowding; best = r; }
        }
        return best;
    }

    private async Task Later()
    {
        // c06: a small touch beside a cottage (the simulation places it; is it drawn?).
        if (await Fast(t => t.TouchCount > 0, 20))
        {
            var touch = T.Touch(0);
            string kind = touch.Kind.ToString();
            if (Drawn3D(kind))
            {
                await Look(touch.X, touch.Y, 22f);
                await Settle(1000);
                await Calm();
                await Shot("c06-touch");
            }
            else NotMade("c06-touch", $"the simulation placed a {kind} at ({touch.X:0.0}, {touch.Y:0.0}) but the game draws no touches in this build");
        }
        else NotMade("c06-touch", "no touch placed by level " + T.Level);

        // b08: an extra hand offered at a queued workplace.
        int w = -1;
        if (await Fast(t => (w = Handed(t)) >= 0, 30))
        {
            bool listed = false;
            foreach (var it in Main.Hud.ShownItems) if (it.Kind == ShopItemKind.Hand) listed = true;
            if (listed)
            {
                await LookPlot(T.Workplace(w).Plot, 40f);
                await Settle(1000);
                await Shot("b08-hand");
            }
            else NotMade("b08-hand", $"the simulation offers {T.HandName(w)} but the game's shop does not list it in this build");
        }
        else NotMade("b08-hand", "no hand offered by level " + T.Level);

        // b03: the shop with a line to buy, one short of coin, one locked and one finished.
        await Fast(t => ShopMix(t) == 4, 30);
        await FollowView();
        await Settle(800);
        await Calm();
        int mix = ShopMix(T);
        bool worth = false;
        var rx = new Regex(@"\d\s*(/\s*min|a minute|per minute|/m\b)", RegexOptions.IgnoreCase);
        foreach (var s in ShownTexts()) if (rx.IsMatch(s)) worth = true;
        if (mix < 4) Wrong("b03-shop-lines", $"Only {mix} of the four kinds of line (buyable, short of coin, locked, finished) were on the shop at once.");
        if (!worth) Wrong("b03-shop-lines", "No line shows what it is worth in coin a minute (the simulation gives ShopItem.Worth; the game does not draw it in this build).");
        await Shot("b03-shop-lines");

        // c07: the mill working, Flour carried to the market.
        int mill = -1;
        for (int p = 0; p < T.Plan.Plots.Length; p++) if (T.Plan.Plots[p].Kind == PlotKind.Mill) mill = p;
        if (mill >= 0 && await Fast(t => t.PlotWorking(mill) && Carrier(t, Good.Flour) >= 0, 60))
        {
            await LookPlot(mill, 46f);
            await Settle(2000);
            await Calm();
            await Shot("c07-mill");
        }
        else NotMade("c07-mill", "the mill was not working with Flour carried");

        // e01: the charter drive (level 15).
        if (await Fast(t => t.Drive.Active && t.Drive.Delivered > 0, 600))
        {
            await FollowView();
            await Settle(800);
            await Calm(8000);
            if (ShowsText("charter drive")) await Shot("e01-charter-drive");
            else NotMade("e01-charter-drive", $"the simulation runs the drive ({T.DriveText}) but the game draws nothing for it in this build");
        }
        else NotMade("e01-charter-drive", "no charter drive ran by level " + T.Level);

        // e03, e02, e04: the charter granted, the whole town, the achievements by Tab.
        Check(await Fast(t => t.HasCharter, 600), "the bot reached the charter");
        await FollowView();
        await Settle(300);
        await Shot("e03-charter");
        if (!await Calm(60000)) Wrong("e02-town", "The charter banner was still up after a minute.");
        var ext = Main.TownExtent();
        Main.Rig.UserMoved = true;
        await Look(ext.GetCenter().X / TownGround.Cell, ext.GetCenter().Y / TownGround.Cell, Math.Max(ext.Size.Y * 1.3f, ext.Size.X * 0.75f)); // TownExtent is in metres
        await Settle(1500);
        await Shot("e02-town");
        await Key(Godot.Key.Tab);
        await Wait(600);
        Check(Main.Hud.AchievementsOpen, "Tab opens the achievements");
        await Shot("e04-achievements");
        await Key(Godot.Key.Tab);
        await Frames(3);
    }

    /// <summary>Kinds of line on the shop now: affordable, short of coin, locked, finished (count of kinds present).</summary>
    private int ShopMix(Town t)
    {
        bool buy = false, coin = false, locked = false, done = false;
        t.ShopItems(_items);
        foreach (var it in _items)
        {
            if (TownHud.RiverHidden(it)) continue;
            if (it.Affordable) buy = true;
            else if (TownHud.IsLocked(it)) locked = true;
            else if (TownHud.IsFinished(it)) done = true;
            else if (it.Reason == "Not enough coin") coin = true;
        }
        return (buy ? 1 : 0) + (coin ? 1 : 0) + (locked ? 1 : 0) + (done ? 1 : 0);
    }

    private readonly System.Collections.Generic.List<ShopItem> _items = new();

    private static int Handed(Town t)
    {
        for (int i = 0; i < t.WorkplaceCount; i++) if (t.HandOffered(i)) return i;
        return -1;
    }

    /// <summary>Whether any 3D node of the map is named after (or loaded from a file named after) the thing.</summary>
    private bool Drawn3D(string kind)
    {
        string k = kind.ToLowerInvariant();
        bool Walk(Node n)
        {
            string name = n.Name.ToString().ToLowerInvariant().Replace("-", "").Replace("_", "");
            string file = n.SceneFilePath.ToLowerInvariant().Replace("-", "").Replace("_", "");
            if (name.Contains(k) || file.Contains(k)) return true;
            foreach (var c in n.GetChildren()) if (Walk(c)) return true;
            return false;
        }
        return Walk(Main.MapOrigin);
    }
}
