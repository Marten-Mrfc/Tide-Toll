using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>game-trades, the cards (TownHud.Trades*.cs, TownHud.PeopleTrade.cs), by real pointer input.</summary>
public partial class TradesTest
{
    private async Task Cards()
    {
        var hud = Main.Hud; var vil = Main.Villagers;
        float s = hud.UiScale;
        hud.Select(-1);
        await Run(_fps / 2);
        Check(hud.TradesCardAlpha > 0.99f && hud.PersonCardAlpha < 0.01f, $"the Trades card shows, alone ({hud.TradesCardAlpha:0.00}, villager card {hud.PersonCardAlpha:0.00})");
        var z = new HudZones(GetViewport().GetVisibleRect().Size, s);
        var tr = hud.TradesCardRect;
        Check(Near(tr.Position, z.Person.Position) && Math.Abs(tr.Size.X - z.Person.Size.X) <= 1 && Math.Abs(tr.Size.Y - UiKit.U(s, TownHud.TradesCardH)) <= 1,
            $"the Trades card in the Person zone, 156 high ({tr} vs {z.Person})");

        // The hit areas: 40 x 28 units around a pill drawn 32 x 24.
        foreach (var t in new[] { Trade.Water, Trade.Grain, Trade.Flour })
        {
            var mh = hud.TradeMinusHit(t); var ph = hud.TradePlusHit(t);
            var mp = hud.TradeMinusPill(t); var pp = hud.TradePlusPill(t);
            Check(Size(mh, 40, 28, s) && Size(ph, 40, 28, s), $"{t}: the pills' hit areas are 40 x 28 units ({mh.Size}, {ph.Size})");
            Check(Size(mp, 32, 24, s) && Size(pp, 32, 24, s), $"{t}: the pills are drawn 32 x 24 units ({mp.Size}, {pp.Size})");
            Check(mh.Encloses(mp) && ph.Encloses(pp) && !mh.Intersects(ph), $"{t}: each hit area holds its pill; the two do not overlap");
            Check(tr.Encloses(mh) && tr.Encloses(ph), $"{t}: the hit areas inside the card");
        }

        // The hover rings: a line hovered rings its members on the map.
        var line = hud.TradeLineRect(Trade.Grain);
        TestInput.Motion(line.Position + new Vector2(line.Size.X * 0.3f, line.Size.Y / 2));
        Input.WarpMouse(line.Position + new Vector2(line.Size.X * 0.3f, line.Size.Y / 2));
        await Run(_fps / 2);
        int reapers = T.TradeInfo(Trade.Grain).Members;
        Check(hud.TradeHovered == (int)Trade.Grain && vil.TradeRingsFor == (int)Trade.Grain && vil.TradeRingAlpha > 0.99f, $"hovering the Reapers' line rings them (hovered {hud.TradeHovered}, rings for {vil.TradeRingsFor}, {vil.TradeRingAlpha:0.00})");
        Check(vil.TradeRingsDrawn == Math.Min(reapers, vil.Drawn), $"every reaper ringed ({vil.TradeRingsDrawn} of {reapers})");
        await Run((int)(_fps * 0.5f));
        Check(hud.TipText.StartsWith("Reapers bring in the grain.") && hud.TipText.Contains("Members " + reapers), $"the line's tooltip ('{hud.TipText}')");

        // Plus: one more carrier, by a click near the hit area's corner (outside the drawn pill); a villager's head changes.
        var before = new Trade[T.VillagerCount];
        for (int i = 0; i < before.Length; i++) before[i] = T.Villager(i).Trade;
        int water = T.TradeInfo(Trade.Water).Members;
        var hit = hud.TradePlusHit(Trade.Water);
        var corner = hit.Position + new Vector2(2, 2);
        Check(!hud.TradePlusPill(Trade.Water).HasPoint(corner), "the corner point is outside the drawn pill");
        Check(hud.TradePlusEnabled(Trade.Water), "plus can take someone");
        await Click(corner);
        await Run(2);
        Check(T.TradeInfo(Trade.Water).Members == water + 1 && T.TradeInfo(Trade.Water).ByPlayer, $"plus: one more carrier, the count yours ({T.TradeInfo(Trade.Water).Members}, was {water})");
        int moved = -1;
        for (int i = 0; i < before.Length; i++) if (T.Villager(i).Trade != before[i]) moved = i;
        Check(moved >= 0, "a villager changed trade");
        await Run(_fps / 2);
        Check(hud.TradeCountText(Trade.Water) == (water + 1).ToString() && hud.TradeCountBold(Trade.Water), $"the count shows {water + 1} in ink and bold ('{hud.TradeCountText(Trade.Water)}')");
        Check(vil.HeadShown(moved) == TownVillagers.HeadOf(moved, Trade.Water), $"villager {moved} wears the kerchief ({vil.HeadShown(moved)})");
        // Minus: one fewer, by a click in the middle of the hit area.
        await Click(hud.TradeMinusHit(Trade.Water).GetCenter());
        await Run(_fps / 2);
        Check(T.TradeInfo(Trade.Water).Members == water, $"minus: one carrier fewer ({T.TradeInfo(Trade.Water).Members})");
        // Minus at the last member is disabled and its tooltip says why.
        int flour = T.TradeInfo(Trade.Flour).Members;
        if (flour > 1) { T.SetTradeCount(Trade.Flour, 1); await Run(_fps / 2); }
        Check(!hud.TradeMinusEnabled(Trade.Flour), "minus at the last miller is disabled");
        await Click(hud.TradeMinusHit(Trade.Flour).GetCenter());
        await Run(2);
        Check(T.TradeInfo(Trade.Flour).Members == 1, "a click on a disabled minus does nothing");
        TestInput.Motion(hud.TradeMinusHit(Trade.Flour).GetCenter()); Input.WarpMouse(hud.TradeMinusHit(Trade.Flour).GetCenter());
        await Run((int)(_fps * 0.6f));
        Check(hud.TipText.Contains("keep one member"), $"the disabled pill's tooltip says why ('{hud.TipText}')");

        // The switch: off, then on again hands every count back to the town.
        await Click(hud.TradesSwitchRect.GetCenter());
        await Run(_fps / 2);
        Check(!T.TownFillsTrades && !hud.TradesSwitchOn, "the switch turns the town's filling off");
        await Click(hud.TradesSwitchRect.GetCenter());
        await Run(_fps / 2);
        Check(T.TownFillsTrades && hud.TradesSwitchOn && !T.TradeInfo(Trade.Water).ByPlayer && !hud.TradeCountBold(Trade.Water), "on again: every count is the town's (muted, regular)");

        // The crossfade: the villager's card takes the zone; at rest never both.
        int who = 2;
        hud.Select(who);
        float worstBoth = 0f; int frames = 0;
        for (int k = 0; k < _fps; k++)
        {
            await Frames(1); Sample();
            if (hud.PersonCardAlpha < 0.99f || hud.TradesCardAlpha > 0.01f) frames++;
            worstBoth = Math.Max(worstBoth, Math.Min(hud.PersonCardAlpha, hud.TradesCardAlpha));
        }
        Check(hud.PersonCardAlpha > 0.99f && hud.TradesCardAlpha < 0.01f, $"the villager's card alone at rest ({hud.PersonCardAlpha:0.00}, {hud.TradesCardAlpha:0.00})");
        Check(frames >= Math.Max(2, (int)(0.15f * _fps)), $"a crossfade, not a switch ({frames} frames)");
        Check(worstBoth <= 0.55f, $"the two cards cross, never both shown ({worstBoth:0.00})");
        Check(Near(hud.PersonCardRect.Position, tr.Position) && Math.Abs(hud.PersonCardRect.Size.Y - tr.Size.Y) <= 1, $"the same rectangle ({hud.PersonCardRect} vs {tr})");
        var v = T.Villager(who);
        string title = T.VillagerName(who) + " · ";
        Check(hud.PersonCardText.StartsWith(title), $"the card's title names the trade ('{hud.PersonCardText}')");
        // A cap: the reaper's (or the carrier's for a reaper) sets the trade.
        var to = v.Trade == Trade.Grain ? Trade.Water : Trade.Grain;
        Check(hud.TradeCapCurrent(v.Trade), "the current trade's cap is marked");
        await Click(hud.TradeCapRect(to).GetCenter());
        await Run(_fps / 2);
        Check(T.Villager(who).Trade == to && T.Villager(who).TradeByPlayer && hud.TradeCapCurrent(to), $"a click on a cap sets the trade ({T.Villager(who).Trade})");
        Check(hud.PersonCardText.StartsWith(title + (to == Trade.Water ? "Carrier" : "Reaper")), $"the title follows ('{hud.PersonCardText}')");
        var likes = T.Villager(who).Likes;
        if (likes != Trade.None) Check(hud.TradeCapLiked(likes), "the liked cap carries the fold");
        hud.Select(-1);
        await Run(_fps / 2);

        // The Training row buys (a real click on its row).
        await TrainingBuys();

        // Bytes per frame with the card shown and a line hovered.
        line = hud.TradeLineRect(Trade.Water);
        TestInput.Motion(line.GetCenter()); Input.WarpMouse(line.GetCenter());
        await Run(_fps);
        long g0 = GC.GetAllocatedBytesForCurrentThread(); long f0 = _frames;
        await Run(_fps * 5);
        long perFrame = (GC.GetAllocatedBytesForCurrentThread() - g0) / Math.Max(1, _frames - f0);
        Check(perFrame < Perf.AllocGate1x, $"a frame allocates under game-perf's 1x gate ({perFrame} B, gate {Perf.AllocGate1x})");
        Notes.Add($"bytes={perFrame}");
    }

    private async Task TrainingBuys()
    {
        var hud = Main.Hud;
        int row = -1;
        for (int k = 0; k < 400 && row < 0; k++)
        {
            for (int i = 0; i < 40 && row < 0; i++) if (hud.RowText(i).StartsWith("Training")) row = i;
            if (row < 0) await Run(1);
        }
        Check(row >= 0, "the Training row is listed");
        Main.Driver.SetPaused(true);
        for (int k = 0; k < 400 && T.Coin < T.TrainingPrice; k++) Main.Driver.Advance(10);
        Main.Driver.SetPaused(false);
        int before = T.Training;
        Check(T.TrainingLockReason.Length == 0, $"Training can be bought ('{T.TrainingLockReason}')");
        for (int k = 0; k < _fps * 2 && !hud.RowCalm(row); k++) await Run(1);
        await Click(hud.RowNode(row).Button.GetGlobalRect().GetCenter());
        await Run(_fps / 2);
        Check(T.Training == before + 1, $"a click on the Training row buys a step ({T.Training}, was {before}; row '{hud.RowText(row)}')");
    }

    private void SampleCards()
    {
        var hud = Main.Hud; var vil = Main.Villagers;
        Step(ref _tradesA0, hud.TradesCardAlpha, ref _worstAlphaStep, "trades card");
        Step(ref _personA0, hud.PersonCardAlpha, ref _worstAlphaStep, "villager card");
        Step(ref _ringA0, vil.TradeRingAlpha, ref _worstAlphaStep, "trade rings");
    }

    private static bool Size(Rect2 r, float w, float h, float s) => Math.Abs(r.Size.X - UiKit.U(s, w)) <= 1 && Math.Abs(r.Size.Y - UiKit.U(s, h)) <= 1;
    private static bool Near(Vector2 a, Vector2 b) => a.DistanceTo(b) <= 1.5f;
}
