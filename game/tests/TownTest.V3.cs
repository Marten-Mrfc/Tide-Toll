using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// Round 5, rules v3 through real input: the choice of two by a click (level 4, in the main loop) and by its key
/// (level 7), with the card, the moment and the town running on behind it; a milestone announced; touches arriving
/// and drawn at their spots; an extra hand offered (shop bookmark and marker on the workplace) and bought by a click;
/// the charter drive shown, filling in parts, and met.
/// </summary>
public partial class TownTest
{
    private bool _boonByClick;

    private async Task BoonByClick()
    {
        await Frames(3);
        var c = T.Choice;
        Check(Main.Hud.ChoiceCardVisible, "a choice open shows its card");
        string text = Main.Hud.ChoiceCardText;
        Check(text.Contains(c.AName) && text.Contains(c.BName) && text.Contains(c.AText) && text.Contains(c.BText), $"the card names both boons: {text}");
        var r = Main.Hud.GetNode<Control>("Root/Choice").GetGlobalRect();
        var vp = Main.GetViewport().GetVisibleRect().Size;
        Check(r.Size.Y < vp.Y * 0.3f && r.Size.X < vp.X * 0.6f, $"the card leaves the map in view ({r.Size})");
        double t0 = T.Time;
        await Frames(20);
        Check(T.Time > t0 && T.ChoiceOpen, "the town runs on behind the open choice");
        int moments = Main.Hud.BoonMoments;
        await Click(Main.Hud.BoonButton(0).GetGlobalRect().GetCenter());
        await Frames(3);
        Check(!T.ChoiceOpen && T.ChoicePicked(c.Choice) == (int)c.A, $"a click on the left half takes {c.AName} (picked {T.ChoicePicked(c.Choice)})");
        Check(Main.Hud.BoonMoments == moments + 1 && Main.Hud.BannerText.Contains(c.AName), $"the boon's moment shows ('{Main.Hud.BannerText}')");
        Check(!Main.Hud.ChoiceCardVisible, "the card goes once picked");
        _boonByClick = true;
        Notes.Add($"boonclick={c.AName}");
    }

    private async Task RulesV3()
    {
        Check(_boonByClick, "the level-4 choice was picked by a click");
        var bot = new TownBot(BotKind.Payback, 1);
        Main.Driver.SetSpeed(1);

        // The next choice by its key (X: the right-hand boon).
        int touches0 = T.TouchCount;
        Main.Driver.RunBotUntil(bot, t => t.ChoiceOpen, 20 * 60 * 60 * 10);
        Check(T.ChoiceOpen, "the bot played on to the next choice");
        await Frames(3);
        var c = T.Choice;
        Check(Main.Hud.ChoiceCardVisible && Main.Hud.ChoiceCardText.Contains(c.BName),
            $"the second choice's card (open {T.ChoiceOpen}, choice {c.Choice}, achievements {Main.Hud.AchievementsOpen}, age page {Main.Hud.AgePageOpen}, text '{Main.Hud.ChoiceCardText}')");
        await Key(Godot.Key.X);
        await Frames(2);
        Check(T.ChoicePicked(c.Choice) == (int)c.B, $"key X takes the right-hand boon {c.BName}");
        Check(Main.Hud.BannerText.Contains(c.BName), $"the key's pick has its moment ('{Main.Hud.BannerText}')");

        // Milestones: Pace and the rest passed fifth steps by now; each was announced by name.
        Check(Main.Hud.Milestones >= 1 && Main.Hud.LastMilestone.Length > 0,
            $"a milestone announced ({Main.Hud.Milestones}, '{Main.Hud.LastMilestone}')");

        // Touches arrived while the town grew, and every one is drawn at its spot.
        await Frames(60);
        // (Whether one arrives between here and the next choice depends on the play; the arrival itself is checked below.)
        Check(T.TouchCount > 0 && Main.Touches.Shown == T.TouchCount, $"touches are drawn ({touches0} then {T.TouchCount}, drawn {Main.Touches.Shown})");
        for (int i = 0; i < T.TouchCount; i++)
        {
            var v = T.Touch(i);
            var n = Main.Touches.Item(i)!;
            Check(Math.Abs(n.Position.X - v.X * TownGround.Cell) < 0.01f && Math.Abs(n.Position.Z - v.Y * TownGround.Cell) < 0.01f, $"touch {i} stands at its spot");
        }
        int before = T.TouchCount;
        Main.Driver.RunBot(bot, t => t.TouchCount > before, 20 * 60 * 60 * 2);
        await Frames(2);
        Check(Main.Touches.Popping > 0, "a new touch pops in");
        await Until(() => Main.Touches.Popping == 0, 300, "the touch to settle");
        Check(!Main.Hud.BannerLog.Exists(l => l.Contains(TownHud.TouchNote(T.Touch(before).Kind))), "a touch never has a banner");

        // An extra hand: offered in the shop and over its workplace, bought by a click.
        int w = -1;
        Main.Driver.RunBotUntil(bot, t => { for (int i = 0; i < t.WorkplaceCount; i++) if (t.HandOffered(i)) return true; return false; }, 20 * 60 * 60 * 4);
        for (int i = 0; i < T.WorkplaceCount && w < 0; i++) if (T.HandOffered(i)) w = i;
        Check(w >= 0, "a hand is offered at a queued workplace");
        for (int k = 0; k < 200 && T.Coin < T.HandPrice(w); k++) Main.Driver.Advance(10);
        // Markers belong to the map and hide under an open page: close the achievements page first if the play left it open.
        if (Main.Hud.AchievementsOpen) await Key(Godot.Key.Tab);
        Main.Rig.UserMoved = false;
        Main.Follow(instant: true);
        await Frames(10);
        Check(!Main.Hud.AchievementsOpen && Main.Hud.HandMarkersShown >= 1, "the offer shows a marker on its workplace");
        var mark = Main.Hud.Mark(ShopItemKind.Hand, w);
        Check(mark != null && mark.Text.Contains(T.HandName(w)), "the offer has a bookmark in the shop");
        int hands = T.HandsBought(w);
        var markRect = mark!.GetGlobalRect(); bool markOff = mark.Disabled;
        await Click(markRect.GetCenter());
        Check(T.HandsBought(w) == hands + 1, $"a click on the bookmark buys the hand ({T.HandName(w)}; coin {T.Coin:0} price {T.HandPrice(w):0}, mark at {markRect} disabled {markOff}, screen {Main.GetViewport().GetVisibleRect().Size}, shown {Main.Hud.Marks.Count})");
        _bought++; _kinds[ShopItemKind.Hand] = Got(ShopItemKind.Hand) + 1;

        // The charter drive: its own card, a bar that fills in parts, met.
        Main.Driver.RunBotUntil(bot, t => t.Drive.Active, 20 * 60 * 60 * 10);
        Check(T.Drive.Active, "the charter drive starts at level 15");
        await Frames(3);
        var d = T.Drive;
        Check(Main.Hud.DriveCardVisible && Main.Hud.DriveCardText.Contains(d.Name) && Main.Hud.DriveCardText.Contains(TownHud.Money(d.Amount)),
            $"the drive's card names it and its amount ({Main.Hud.DriveCardText})");
        bool partSeen = false;
        for (int k = 0; k < 400 && T.Drive.Active; k++)
        {
            Main.Driver.RunBotUntil(bot, t => !t.Drive.Active || t.Drive.Delivered * 10 / Math.Max(1, t.Drive.Amount) > d.Delivered * 10 / Math.Max(1, d.Amount), 20 * 60 * 10);
            d = T.Drive;
            await Frames(2);
            if (!d.Active) break;
            float want = MathF.Floor(10f * d.Delivered / d.Amount) / 10f;
            Check(Math.Abs(Main.Hud.DriveBar - want) < 0.02f, $"the bar shows whole parts ({Main.Hud.DriveBar:0.00} for {d.Delivered} of {d.Amount})");
            if (want > 0) partSeen = true;
        }
        Check(T.Drive.Done && partSeen, "the drive filled in parts and was met");
        await Frames(3);
        Check(!Main.Hud.DriveCardVisible && Main.Hud.NoteLog.Exists(l => l.Contains("charter drive is met")), "the met drive leaves a note and its card goes");
        Notes.Add($"boonkey={c.BName} milestones={Main.Hud.Milestones} touches={T.TouchCount} hand={T.HandName(w)} drive=met");
    }
}
