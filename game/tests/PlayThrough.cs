using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// game-playthrough: the whole first age played through injected clicks and keys only, at 1920 x 1080, like a sensible
/// person: about once a game second they look at the bookmarks and buy what is lit (a building first, then a villager
/// when there is room, an extension when every cottage is full, then Load, Tools, a price, Pace, the bell track),
/// pave the suggested road when the Pave bookmark is lit (P, point at the road until the tip names it, click), extend
/// the suggested cottage when Extend is lit (its bookmark, point, click), ring the bell when it is ready, and at the
/// charter ring in the next age and read the page. Based on the play-tester's round-1 scene (build/pt/r1).
/// Fails on: any error line (tools/godot-test.sh), a Pave or Extend click that does not act on what its tip named,
/// a bookmark click on a lit item that buys nothing, any whole minute after the second with no purchase possible,
/// no charter within the time cap. Prints purchases and decisions a minute for each ten minutes and the charter's minute.
/// Args: --seed=N --speed=N --cap=MINUTES.
/// </summary>
public partial class PlayThrough : TownTestBase
{
    protected override string Marker => "PLAY PASS";
    protected override Vector2I WindowSize => new(1920, 1080);

    private int _seed = 1;
    private double _speed = 16, _capMin = 75;
    private readonly int[] _buyMin = new int[200], _decMin = new int[200];
    private readonly bool[] _possible = new bool[200], _looked = new bool[200];
    private int _buys, _paves, _extends, _bells, _keysUsed, _clicks, _turn, _misses;
    private double _paveBackoff, _extendBackoff;

    private void P(string s) => GD.Print($"PT [{T.Time,7:0.0}s] {s}");
    private int Min => Math.Min(199, (int)(T.Time / 60));

    protected override async Task Run()
    {
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            if (a.StartsWith("--seed=")) _seed = int.Parse(a.Substring(7));
            if (a.StartsWith("--speed=")) _speed = double.Parse(a.Substring(8), CultureInfo.InvariantCulture);
            if (a.StartsWith("--cap=")) _capMin = double.Parse(a.Substring(6), CultureInfo.InvariantCulture);
        }
        if (_seed != Main.Driver.Seed) { Main.Driver.Reset(_seed); await Frames(3); }
        Main.Driver.SetSpeed(_speed);
        P($"start seed {_seed} speed {_speed}");
        double next = 0;
        int level = 1;
        while (!T.HasCharter && T.Time < _capMin * 60)
        {
            await Frames(1);
            if (T.Level > level) { level = T.Level; P($"LEVEL {level}"); }
            if (T.Time < next) continue;
            next = T.Time + 1.0;
            Main.Driver.SetPaused(true);
            await Act();
            Main.Driver.SetPaused(false);
        }
        Check(T.HasCharter, $"the charter within {_capMin} game minutes (level {T.Level})");
        double charter = T.Time / 60.0;
        Main.Driver.SetPaused(true);
        await Frames(5);

        // The end of the age, as a player: the Ring bookmark (its key as bound now), the page, Esc.
        Check(Main.Hud.NextAgeButton.IsVisibleInTree(), "the Ring in the next age bookmark shows");
        await Key(BoundKey("tt_next_age"));
        Check(T.NextAgeRung, "the next age is rung in by its key");
        Main.Driver.SetPaused(false);
        await Until(() => Main.Hud.AgePageOpen, 60 * 20, "the second age's page");
        // Round 4: the ring now opens the second age; its page says what is new on the river.
        Check(Main.Hud.AgePageText.Contains("The second age: The River") && T.InRiverAge, "the page opens the second age on the river");
        Check(Main.Audio.Music.Log.Contains(TownMusic.RisingId), "the rising theme plays");
        await Click(TestInput.Centre(Main.Hud.AgePageClose));
        Check(!Main.Hud.AgePageOpen && !Main.Menu.IsPaused, "Keep playing closes the page");

        Report(charter);
        Check(_misses == 0, $"{_misses} clicks or keys did not do what the interface said (see the MISS lines)");
        Notes.Add($"charter={charter:0.0}min buys={_buys} paves={_paves} extends={_extends} bells={_bells} clicks={_clicks} keys={_keysUsed}");
    }

    private void Report(double charter)
    {
        int last = (int)charter;
        var dead = new List<int>();
        for (int m = 2; m < last; m++) if (_looked[m] && !_possible[m]) dead.Add(m);
        for (int m0 = 0; m0 <= last; m0 += 10)
        {
            int m1 = Math.Min(last + 1, m0 + 10), b = 0, d = 0;
            for (int m = m0; m < m1; m++) { b += _buyMin[m]; d += _decMin[m]; }
            double mins = Math.Max(1, Math.Min(charter, m1) - m0);
            GD.Print($"PLAY minutes {m0,2}-{m1 - 1,2}: purchases a minute {b / mins:0.0}  decisions a minute {d / mins:0.0}");
        }
        GD.Print($"PLAY charter at minute {charter:0.0} (seed {_seed}): {_buys} purchases, {_paves} paved, {_extends} extended, {_bells} bells");
        Check(dead.Count == 0, $"minutes with no purchase possible: {string.Join(",", dead)}");
    }

    private static Key BoundKey(string action)
    {
        foreach (var ev in InputMap.ActionGetEvents(action))
            if (ev is InputEventKey k) return Settings.GameInput.KeyOf(k);
        throw new Exception("no key bound to " + action);
    }

    private bool Lit(int i) => i < Main.Hud.Marks.Count && !Main.Hud.Marks[i].Disabled && Main.Hud.Marks[i].IsVisibleInTree();

    private static int Rank(in ShopItem it, Town t) => it.Kind switch
    {
        ShopItemKind.Building => 0,
        ShopItemKind.Extend => t.VillagerCount >= t.Housing ? 1 : 6,
        ShopItemKind.Villager => 2,
        ShopItemKind.Load => 3,
        ShopItemKind.Pave => 4,
        ShopItemKind.Tools => 5,
        ShopItemKind.Value => 5,
        ShopItemKind.Pace => 6,
        _ => 7,
    };

    private async Task Act()
    {
        for (int round = 0; round < 8; round++)
        {
            await Frames(1);
            var shown = Main.Hud.ShownItems;
            int pick = -1, best = 99, lit = 0;
            var kinds = new HashSet<ShopItemKind>();
            for (int i = 0; i < shown.Count; i++)
            {
                var it = shown[i];
                if (!Lit(i) || !it.Affordable) continue;
                if (it.Kind == ShopItemKind.Pave && T.Time < _paveBackoff) continue;
                if (it.Kind == ShopItemKind.Extend && T.Time < _extendBackoff) continue;
                lit++; kinds.Add(it.Kind);
                int r = Rank(it, T);
                if (r < best || (r == best && it.Price < shown[pick].Price)) { best = r; pick = i; }
            }
            _looked[Min] = true;
            if (lit > 0) _possible[Min] = true;
            if (round == 0 && kinds.Count >= 2) _decMin[Min]++;
            bool bell = T.BellReady && Main.Hud.BellButton.IsVisibleInTree() && !Main.Hud.BellButton.Disabled;
            if (pick < 0) { if (bell) await Ring(); return; }
            var item = shown[pick];
            if (item.Kind == ShopItemKind.Pave) { if (!await Pave()) _paveBackoff = T.Time + 20; continue; }
            if (item.Kind == ShopItemKind.Extend) { if (!await Extend(pick)) _extendBackoff = T.Time + 20; continue; }
            await Buy(pick, item);
        }
    }

    private async Task Buy(int i, ShopItem it)
    {
        long p0 = T.Purchases;
        string action = TownHud.ActionFor(it, OfferOrdinal(i));
        bool byKey = _turn++ % 3 == 2 && action.Length > 0;
        if (byKey) { await Key(BoundKey(action)); _keysUsed++; }
        else
        {
            var at = TestInput.Centre(Main.Hud.Marks[i]);
            Check(GetViewport().GetVisibleRect().HasPoint(at), $"bookmark {it.Name} is on screen");
            await Click(at); _clicks++;
        }
        if (T.Purchases > p0) { _buys++; _buyMin[Min]++; }
        else { _misses++; P($"MISS: {(byKey ? "key" : "click")} on lit '{it.Name}' bought nothing ({Main.Hud.LastNote})"); }
    }

    private int OfferOrdinal(int i)
    {
        int n = 0;
        for (int k = 0; k < i; k++) if (Main.Hud.ShownItems[k].Kind == ShopItemKind.Building) n++;
        return n;
    }

    private async Task Ring()
    {
        int b = T.BellRings;
        if (_bells % 2 == 0) { await Key(BoundKey("tt_bell")); _keysUsed++; }
        else { await Click(TestInput.Centre(Main.Hud.BellButton)); _clicks++; }
        if (T.BellRings > b) _bells++;
        else { _misses++; P("MISS: the bell did not ring though it was ready"); }
    }

    /// <summary>True when a road was paved; false when no point of the suggested road could be pointed at (no click made).</summary>
    private async Task<bool> Pave()
    {
        await Key(BoundKey("tt_pave")); _keysUsed++;
        if (!Main.Hud.PaveMode) { P($"note: Pave mode did not open ({Main.Hud.LastNote})"); return false; }
        int r = T.SuggestedPave();
        await Steady(() => Main.Hud.RoadScreen(r));
        var vis = GetViewport().GetVisibleRect().Grow(-8);
        for (int k = 0; k <= 10; k++)
        {
            var at = Main.Hud.RoadScreenAt(r, 0.5 + (k % 2 == 0 ? 1 : -1) * 0.04 * k);
            if (!vis.HasPoint(at)) continue;
            TestInput.Motion(at);
            await Frames(2);
            int tip = Main.Hud.HoverRoad;
            if (tip < 0 || !Main.Hud.HoverTipText.Contains("Click to pave")) continue;
            var before = T.RoadInfo(tip).Surface;
            int paved0 = T.RoadsPaved;
            TestInput.Button(at, MouseButton.Left, true);
            await Frames(1);
            TestInput.Button(at, MouseButton.Left, false);
            await Frames(2);
            _clicks++;
            if (Main.Hud.LastPaveClickRoad != tip || T.RoadInfo(tip).Surface == before || T.RoadsPaved != paved0 + 1)
                throw new Exception($"a Pave click did not do what its tip said: tip road {tip}, acted on {Main.Hud.LastPaveClickRoad}, surface {before} -> {T.RoadInfo(tip).Surface}");
            _paves++; _buys++; _buyMin[Min]++;
            if (Main.Hud.PaveMode) await Key(Godot.Key.Escape);
            return true;
        }
        P($"note: no point of suggested road {r} could be pointed at");
        await Key(Godot.Key.Escape);
        return false;
    }

    private async Task<bool> Extend(int i)
    {
        await Click(TestInput.Centre(Main.Hud.Marks[i])); _clicks++;
        if (!Main.Hud.ExtendMode) { P($"note: Extend mode did not open ({Main.Hud.LastNote})"); return false; }
        int p = T.SuggestedExtend();
        if (p < 0) { await Key(Godot.Key.Escape); return false; }
        await Steady(() => Main.Hud.PlotScreen(p));
        var at = Main.Hud.PlotScreen(p);
        TestInput.Motion(at);
        await Frames(2);
        int tip = Main.Hud.HoverCottage;
        if (tip < 0 || !Main.Hud.HoverTipText.Contains("Extend this cottage")) { P($"note: the suggested cottage {p} could not be pointed at"); await Key(Godot.Key.Escape); return false; }
        int e0 = T.Extensions(tip);
        await Click(at);
        if (Main.Hud.LastExtendClickPlot != tip || T.Extensions(tip) != e0 + 1)
            throw new Exception($"an Extend click did not do what its tip said: tip cottage {tip}, acted on {Main.Hud.LastExtendClickPlot}, extensions {e0} -> {T.Extensions(tip)}");
        _extends++; _buys++; _buyMin[Min]++;
        if (Main.Hud.ExtendMode) await Key(Godot.Key.Escape);
        return true;
    }

    /// <summary>Waits (as a person does) until a point on screen stops moving: the view may glide to bring it into sight.</summary>
    private async Task Steady(Func<Vector2> at)
    {
        var last = at();
        for (int w = 0; w < 240; w++)
        {
            await Frames(1);
            var now = at();
            if (now.DistanceTo(last) < 0.3f && w > 3) return;
            last = now;
        }
    }
}
