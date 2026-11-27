using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// game-trades (n7 trades, docs/plans/n7/trades-build-sheet.md "Tests and pictures"), run at 30, 60 and 120 frames a
/// second by tools/trades-check.sh. A real town (the payback bot to the mill), then frame by frame at 1x:
/// <list type="bullet">
/// <item>the figures: every villager wears its trade's head, a Helper hair only (never a hat or a hood), a miller its
/// apron; a change of trade changes the head by a shrink and grow, never in one frame (TradesTest.cs);</item>
/// <item>the cards: the Trades card and the villager's card in one zone by a crossfade, never both; plus and minus
/// move a count and a villager's head; the pills' hit areas; the hover rings of a trade's members; the switch; the
/// villager's Trade caps (TradesTest.Cards.cs);</item>
/// <item>the Training row buys; nothing changes in one frame; bytes per frame.</item>
/// </list>
/// </summary>
public partial class TradesTest : TownTestBase
{
    protected override string Marker => "TRADES PASS";
    protected override Vector2I WindowSize => new(1920, 1080);
    private int _fps = 60;
    private long _frames, _helperHatFrames;
    private float _worstHeadStep, _worstAlphaStep;
    private float _tradesA0 = -1, _personA0 = -1, _ringA0 = -1;
    private readonly List<string> _hatFails = new();

    protected override async Task Run()
    {
        foreach (var a in OS.GetCmdlineUserArgs()) if (a.StartsWith("--fps=")) _fps = int.Parse(a.Substring(6));
        var bot = new TownBot(BotKind.Payback, 2);
        Main.Driver.SetPaused(true);
        Main.Driver.RunBot(bot, t => t.VillagerCount >= 12 && t.TradeOpen(Trade.Flour), 20 * 60 * 40);
        Check(T.VillagerCount >= 12 && T.TradeOpen(Trade.Flour) && T.TradesShown, $"a town with the mill ({T.VillagerCount} villagers)");
        Main.Driver.Advance(20);
        Main.Driver.SetSpeed(1);
        Main.Driver.SetPaused(false);
        // The counts set through the calls (as the town would fill them), then handed back to the town: nobody is set
        // one by one, so the stepper can move anyone.
        T.SetTradeCount(Trade.Water, 3); T.SetTradeCount(Trade.Grain, 4); T.SetTradeCount(Trade.Flour, 2);
        T.SetTownFillsTrades(false); T.SetTownFillsTrades(true);
        await Frames(_fps);
        await Cards();
        await Figures();
        Check(_helperHatFrames == 0, $"no Helper wears a hat or a hood ({_helperHatFrames} villager-frames; {string.Join("; ", _hatFails)})");
        Check(_worstHeadStep <= 0.5f, $"a head changes over several frames, never in one (largest scale step {_worstHeadStep:0.00})");
        Check(_worstAlphaStep <= 0.5f, $"cards and rings fade, never switch in one frame (largest step {_worstAlphaStep:0.00})");
        Notes.Add($"tags={Main.Hud.AutoTagsGiven} fps={_fps} villagers={T.VillagerCount} headstep={_worstHeadStep:0.00} alphastep={_worstAlphaStep:0.00} frames={_frames}");
    }

    /// <summary>Waits n frames, sampling each.</summary>
    private async Task Run(int n)
    {
        for (int k = 0; k < n; k++) { await Frames(1); Sample(); }
    }

    private async Task Figures()
    {
        var vil = Main.Villagers;
        int n = Math.Min(T.VillagerCount, TownVillagers.Capacity);
        // Every villager set to a trade by number (and every fourth a Helper), then read back from the drawing.
        for (int i = 0; i < n; i++) T.SetVillagerTrade(i, (Trade)(i % 4));
        await Run((int)(_fps * 0.6f));
        int wrong = 0, aprons = 0, millers = 0;
        for (int i = 0; i < n; i++)
        {
            var tr = T.Villager(i).Trade;
            if (vil.HeadShown(i) != TownVillagers.HeadOf(i, tr) || vil.HeadScale(i) < 0.999f) wrong++;
            if (tr == Trade.Flour) millers++;
        }
        for (int k = 0; k < TownVillagers.HeadKinds; k++) if (k == TownVillagers.MillerHead) aprons = vil.ApronsDrawn;
        Check(wrong == 0, $"every villager wears its trade's head ({wrong} of {n} not)");
        Check(aprons == millers && millers > 0, $"every miller wears the apron ({aprons} aprons, {millers} millers)");
        int heads = 0;
        for (int k = 0; k < TownVillagers.HeadKinds; k++) heads += vil.HeadsDrawn(k);
        Check(heads == vil.Drawn, $"one head per figure ({heads} heads, {vil.Drawn} figures)");

        // A change of trade: the head shrinks into the neck and the new one grows, over HeadSwapSeconds.
        int who = 1;
        var before = T.Villager(who).Trade;
        var to = before == Trade.Grain ? Trade.Flour : Trade.Grain;
        T.SetVillagerTrade(who, to);
        int frames = 0, shownOld = 0;
        for (int k = 0; k < _fps; k++)
        {
            await Frames(1); Sample();
            if (vil.HeadScale(who) < 0.999f) frames++;
            if (vil.HeadShown(who) == TownVillagers.HeadOf(who, before)) shownOld++;
        }
        Check(vil.HeadShown(who) == TownVillagers.HeadOf(who, to) && vil.HeadScale(who) > 0.999f, $"villager {who} wears the new head ({vil.HeadShown(who)})");
        Check(frames >= Math.Max(2, (int)(0.2f * _fps)), $"the change of head lasts ({frames} frames at {_fps} fps)");
        Check(shownOld >= 1, "the old head shrinks before the new one grows");
    }

    private ulong _sampledAt;
    private void Sample()
    {
        _frames++;
        // A step is measured between two frames in a row only (Click and other waits run frames unsampled).
        ulong f = Engine.GetProcessFrames();
        if (f != _sampledAt + 1) { _tradesA0 = _personA0 = _ringA0 = -1; Array.Fill(_autoA0, -1f); }
        _sampledAt = f;
        var vil = Main.Villagers;
        var t = T;
        _worstHeadStep = Math.Max(_worstHeadStep, vil.MaxHeadStep);
        int n = Math.Min(t.VillagerCount, TownVillagers.Capacity);
        for (int i = 0; i < n; i++)
        {
            // A Helper at rest (no change under way) wears hair only.
            if (t.Villager(i).Trade != Trade.None || vil.HeadScale(i) < 0.999f) continue;
            if (!TownVillagers.IsHelperHead(vil.HeadShown(i)) || vil.HeadShown(i) >= TownVillagers.HeadKinds)
            {
                _helperHatFrames++;
                if (_hatFails.Count < 3) _hatFails.Add($"villager {i} head {vil.HeadShown(i)}");
            }
        }
        SampleCards();
    }

    private void Step(ref float last, float now, ref float worst, string what)
    {
        if (last >= 0 && Math.Abs(now - last) > worst)
        {
            worst = Math.Abs(now - last);
            if (worst > 0.3f) GD.Print($"TRADES step: {what} {last:0.00} -> {now:0.00} at frame {_frames}");
        }
        last = now;
    }
}
