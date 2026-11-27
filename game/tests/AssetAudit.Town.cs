using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// The asset audit's second part: what the game builds in code, pictured where it stands in a played town (seed 1,
/// the best-payback bot, played at once to the borough seal): roads in each surface, cottages at each extension,
/// the field and bell green in place, the river at high and low water, the landing with its boats, each crossing
/// (the drawbridge down and lifted), and the book (open on the table, closed, paper, leather and table close up).
/// Same two pictures per subject as the models (normal: play zoom, yaw 0; close: yaw 35 degrees).
/// </summary>
public partial class AssetAudit
{
    private float _play;

    private bool Want(string name) => _only == null || _only.Contains(name);

    /// <summary>A map-local ground point (map cells, continuous) to the rig's local metres.</summary>
    private static Vector3 Cells(double x, double y) => new((float)x * TownGround.Cell, 0, (float)y * TownGround.Cell);

    private async Task ShootAt(string name, string file, Vector3 local, float close, float h = 0f, float? normal = null)
    {
        if (!Want(name)) return;
        _lines.Add(new JsonObject { ["name"] = name, ["file"] = "code: " + file, ["triangles"] = null, ["size_m"] = null }.ToJsonString());
        Main.Rig.Yaw.Rotation = Vector3.Zero;
        float n = normal ?? _play;
        Main.Rig.Place(Centre(local, h, n, 0f), n);
        await Snap(name + "-normal");
        float yaw = Mathf.DegToRad(35f);
        Main.Rig.Yaw.Rotation = new Vector3(0, yaw, 0);
        Main.Rig.Place(Centre(local, h, close, yaw), close);
        await Snap(name + "-close");
        Main.Rig.Yaw.Rotation = Vector3.Zero;
    }

    private async Task TownShots(float playSize)
    {
        _play = playSize;
        if (Main.MapOrigin.GetNodeOrNull<Node3D>("Trees") is { } trees) trees.Visible = true;
        const string roads = "game/scripts/town/TownGround.cs";
        // The first trail, as the town opens it.
        for (int r = 0; r < T.RoadCount; r++)
            if (T.RoadOpen(r))
            {
                await Frames(30);
                await LookRoad("road-new-trail", r, roads);
                break;
            }
        var bot = new TownBot(BotKind.Payback, 1);
        // Three stops in the first age (early, middle, the charter), so each road surface and cottage size is seen
        // somewhere; what one stop has pictured is not taken again.
        foreach (int level in new[] { 5, 8, 9, 99 })
        {
            Main.Driver.RunBot(bot, t => t.Level >= level || t.CanRingNextAge, 300 * 60 * TownRules.TickHz);
            await Frames(60); // roads drawn in, buildings popped in
            await Unblock();
            Notes.Add($"stop level={T.Level}");
            await FirstAgeShots();
            // Night 3: the town at this stop as a whole (the play zoom and a close-in view over the town's middle);
            // each view is taken once (a stop the bot passes at once is not pictured twice).
            var pl = T.Plan;
            string view = level == 99 ? "town-view-charter" : $"town-view-level-{level}";
            if (_taken.Add(view))
                await ShootAt(view, "game/scripts/town (the whole first-age town)",
                    Cells(pl.WindowX + pl.WindowW / 2.0, pl.WindowY + pl.WindowH / 2.0), 45f);
        }
        bot.PlayOn = true; // the bot rings in the second age itself
        Main.Driver.RunBot(bot, t => t.HasSeal, 600 * 60 * TownRules.TickHz);
        await Frames(30);
        await Unblock();
        await Frames(60);
        Notes.Add($"town level={T.Level} seal={T.HasSeal}");

        // The river.
        var rp = T.RiverPlan;
        const string river = "game/scripts/town/RiverLayer.cs";
        if (rp == null) { Notes.Add("no river plan"); return; }
        var landing = Cells(rp.Px[RiverPoint.Landing], rp.Py[RiverPoint.Landing]);
        await Tide(true);
        await ShootAt("river-high-water", river, landing, 40f);
        await ShootAt("landing-with-boats", "game/scripts/town/RiverBoats.cs", landing, 18f, 1f);
        await Tide(false);
        await ShootAt("river-low-water", river, landing, 40f);
        await ShootAt("landing-low-water", river, landing, 18f, 1f);
        foreach (var k in new[] { RiverLinkKind.Ford, RiverLinkKind.Ferry, RiverLinkKind.Footbridge, RiverLinkKind.Drawbridge })
        {
            if (!T.CrossingExists(k)) { Notes.Add($"no {k}"); continue; }
            var l = rp.Links[rp.LinkOf(k)];
            var mid = Cells((l.Px[0] + l.Px[^1]) / 2, (l.Py[0] + l.Py[^1]) / 2);
            string nm = "crossing-" + k.ToString().ToLowerInvariant();
            if (k == RiverLinkKind.Drawbridge)
            {
                await Until(() => T.DrawbridgeAngle < 0.05, 20000, true);
                await ShootAt(nm + "-down", river, mid, 26f, 1f);
                await Until(() => T.DrawbridgeAngle > 1.0, 60000, true);
                await ShootAt(nm + "-lifted", river, mid, 26f, 2f);
            }
            else await ShootAt(nm, river, mid, 26f, 1f);
        }

        // Night 3, track RV: the whole river town at the seal (the play zoom over the landing's reach of river), and
        // the ferry close in with its raft at a bank.
        await Tide(true);
        await ShootAt("town-view-river", river + " (the whole second-age town at the seal)", landing, 70f);
        if (T.CrossingExists(RiverLinkKind.Ferry))
        {
            var fl = rp.Links[rp.LinkOf(RiverLinkKind.Ferry)];
            await ShootAt("town-view-ferry", river, Cells(fl.Px[0], fl.Py[0]), 40f);
        }

        // The book.
        await BookShots();
    }

    private readonly System.Collections.Generic.HashSet<string> _taken = new();

    /// <summary>Pages the town opened by itself (the age page at the ring and the seal) hold the book view: closed.</summary>
    private async Task Unblock()
    {
        for (int i = 0; i < 5; i++)
        {
            if (Main.Hud.AgePageOpen) Main.Hud.CloseAgePage(quiet: true);
            if (Main.Menu.IsPaused) Main.Menu.Resume();
            await Frames(20);
            if (!Main.Hud.AgePageOpen && !Main.Menu.IsPaused) break;
        }
        Main.Rig.UserMoved = true;
        Notes.Add($"unblock agepage={Main.Hud.AgePageOpen} paused={Main.Menu.IsPaused}");
    }

    private async Task FirstAgeShots()
    {
        const string roads = "game/scripts/town/TownGround.cs";
        for (int r = 0; r < T.RoadCount; r++)
        {
            if (!T.RoadOpen(r)) continue;
            var v = T.RoadInfo(r);
            string name = v.Surface switch
            {
                Surface.Gravel => "road-gravel",
                Surface.Cobbles => "road-cobbles",
                _ => v.WornIn ? "road-worn" : null!,
            };
            if (name == null || !_taken.Add(name)) continue;
            await LookRoad(name, r, roads);
        }
        const string bld = "game/scripts/town/TownBuildings.cs";
        foreach (var p in T.Plan.Plots)
        {
            if (!T.PlotGiven(p.Id)) continue;
            string name = p.Kind switch
            {
                PlotKind.Cottage => $"town-cottage-extension-{T.Extensions(p.Id)}",
                PlotKind.Field => "town-field",
                PlotKind.Well => "town-well",
                PlotKind.Market => "town-market",
                PlotKind.Mill => "town-mill",
                _ => "town-bell-green",
            };
            if (!_taken.Add(name)) continue;
            await ShootAt(name, bld, Cells(p.X + p.W / 2.0, p.Y + p.H / 2.0), Math.Max(p.W, p.H) * TownGround.Cell * 1.5f, 1.5f);
        }
    }

    private async Task LookRoad(string name, int r, string file)
    {
        var road = T.Plan.Roads[r];
        int k = road.Xs.Length / 2;
        await ShootAt(name, file, Cells(road.Xs[k] + 0.5, road.Ys[k] + 0.5), 14f);
    }

    /// <summary>Steps the town (no frames) until high or low water, then lets the drawing catch up.</summary>
    private async Task Tide(bool high)
    {
        int n = 0, max = 30 * 60 * TownRules.TickHz;
        while (n < max && (high ? !T.HighWater : T.TideLevel > 0.08)) { T.Step(); n++; }
        await Frames(20);
    }

    private async Task Until(Func<bool> cond, int maxTicks, bool step)
    {
        int n = 0;
        while (n < maxTicks && !cond()) { T.Step(); n++; }
        await Frames(10);
    }

    private async Task BookShots()
    {
        const string book = "game/scripts/BookBuilder.cs";
        var inv = Main.MapOrigin.GlobalTransform.AffineInverse();
        var bc = Main.Rig.BookCentre;
        var bs = Main.Rig.BookSize;
        if (Want("book-open"))
        {
            Main.Rig.Yaw.Rotation = Vector3.Zero;
            Main.Rig.FrameBook(instant: true);
            _lines.Add(new JsonObject { ["name"] = "book-open", ["file"] = "code: " + book, ["triangles"] = null, ["size_m"] = null }.ToJsonString());
            await Snap("book-open-normal");
            float yaw = Mathf.DegToRad(35f);
            Main.Rig.Yaw.Rotation = new Vector3(0, yaw, 0);
            Main.Rig.Place(inv * bc, bs.X * 0.8f);
            await Snap("book-open-close");
            Main.Rig.Yaw.Rotation = Vector3.Zero;
        }
        var left = Main.GetNodeOrNull<Node3D>("Book/LeftPage");
        if (left != null) await ShootAt("book-paper", book, inv * (left.GlobalPosition + new Vector3(bs.X * 0.12f, 0, bs.Y * 0.3f)), 8f, 0f, 60f);
        await ShootAt("book-table", "game/scripts/BookBuilder.cs", inv * (bc + new Vector3(bs.X * 0.3f, 0, bs.Y / 2f + 30f)), 16f, 0f, 60f);
        if (Want("book-closed"))
        {
            Main.Cover.Close(instant: true);
            await Frames(10);
            var (centre, size) = Main.ClosedBookView();
            _lines.Add(new JsonObject { ["name"] = "book-closed", ["file"] = "code: game/scripts/town/BookCover.cs", ["triangles"] = null, ["size_m"] = null }.ToJsonString());
            Main.Rig.SetDeep(true);
            Main.Rig.Place(centre, size);
            await Snap("book-closed-normal");
            Main.Rig.Yaw.Rotation = new Vector3(0, Mathf.DegToRad(35f), 0);
            Main.Rig.Place(centre, size * 0.9f);
            await Snap("book-closed-close");
            // The leather close up: a corner of the closed cover.
            var cs = BookCover.ClosedSize;
            _lines.Add(new JsonObject { ["name"] = "book-leather", ["file"] = "code: game/scripts/town/BookCover.cs", ["triangles"] = null, ["size_m"] = null }.ToJsonString());
            Main.Rig.Yaw.Rotation = Vector3.Zero;
            Main.Rig.Place(centre + new Vector3(cs.X * 0.4f, 0, cs.Y * 0.4f), 60f);
            await Snap("book-leather-normal");
            Main.Rig.Yaw.Rotation = new Vector3(0, Mathf.DegToRad(35f), 0);
            Main.Rig.Place(centre + new Vector3(cs.X * 0.45f, 0, cs.Y * 0.45f), 12f);
            await Snap("book-leather-close");
            Main.Rig.Yaw.Rotation = Vector3.Zero;
            Main.Rig.SetDeep(false);
            Main.Cover.Open(instant: true);
        }
    }
}
