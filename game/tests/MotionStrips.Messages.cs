using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// Night 3, L2: frame strips of the message system (tools/motion-strips.sh --messages-only, or after the book's). The
/// interface's clocks are held (<see cref="TownHud.StripStep"/>) and stepped by a fixed amount per saved frame, so the
/// twelve frames show the eased arrival, the stay and the leave whatever the renderer's frame rate:
/// msg-banner, msg-choice, msg-notes, msg-request, msg-line, msg-coin.
/// </summary>
public partial class MotionStrips
{
    /// <summary>One frame of the strip: the interface's clocks step <paramref name="step"/> s, then the frame is saved.</summary>
    private async Task MsgFrame(string name, int k, float step, Action? before = null)
    {
        before?.Invoke();
        // (A test's frame wait resumes before the nodes' own frame: the step is taken once, by the interface's next frame.)
        TownHud.StripStep = 0f;
        TownHud.StripNext = step;
        await Frames(2);
        await Save(name, k);
    }

    /// <summary>The interface's clocks run on (unsaved) for this many seconds, in steps of 0.05 s.</summary>
    private async Task Run(float seconds)
    {
        for (float t = 0; t < seconds; t += 0.05f) { TownHud.StripStep = 0f; TownHud.StripNext = 0.05f; await Frames(1); }
        TownHud.StripStep = 0f;
    }

    private async Task MessageStrips(TownBot bot)
    {
        var hud = Main.Hud;
        var T = Main.Driver.Town;
        if (T.ChoiceOpen) { TestInput.Key(Godot.Key.Z); await Wait(600); }
        Main.Driver.SetPaused(true);
        // (At one frame a second the queues would take many minutes to empty: the strips start from empty zones.)
        hud.ClearMessagesNow();
        await Frames(2);
        float a = TownHud.LargeSeconds / 4f;
        // A banner: frames 1-5 arrive (0.055 s apart), 6 at rest, 7 clicked away, 8-12 leaving.
        TownHud.StripStep = 0f;
        hud.Large("Level 9", "A field of barley at the mill road.", TownHud.PrioLevel, TownHud.BannerStay, "");
        GD.Print($"STRIPS banner: zone free {hud.MessageZoneFree}, interface {hud.FullAlpha:0.00}, choice {T.ChoiceOpen}, waiting {hud.LargeQueue.Waiting}");
        for (int k = 0; k < 6; k++) await MsgFrame("msg-banner", k, k == 0 ? 0.001f : a);
        GD.Print($"STRIPS banner after arrival: phase {hud.LargeQueue.Now}, alpha {hud.LargeQueue.Alpha:0.00}, shown {hud.BannerShown}");
        hud.LargeQueue.Dismiss();
        for (int k = 6; k < 12; k++) await MsgFrame("msg-banner", k, k == 6 ? 0.001f : a);
        hud.ClearMessagesNow();
        // Two notes in a queue: the first arrives, is clicked away, the gap, the second arrives.
        hud.Note("Achievement: First steps.", false, TownHud.NoteAchievement, "");
        hud.Note("Perk: Cheaper paving.", false, TownHud.NoteOther, "");
        float n = TownHud.NoteInSeconds / 4f;
        for (int k = 0; k < 5; k++) await MsgFrame("msg-notes", k, k == 0 ? 0.001f : n);
        hud.NoteQueue.Dismiss();
        for (int k = 5; k < 12; k++) await MsgFrame("msg-notes", k, 0.09f); // leave 0.2, gap 0.15, the next arriving
        hud.ClearMessagesNow();
        // The line changing text: a refusal arrives, a second cross-fades over it.
        hud.Refuse("30 short.");
        float l = TownHud.LineSeconds / 4f;
        for (int k = 0; k < 6; k++) await MsgFrame("msg-line", k, k == 0 ? 0.001f : l);
        hud.Refuse("The bell rests 31 s.");
        for (int k = 6; k < 12; k++) await MsgFrame("msg-line", k, k == 6 ? 0.001f : l);
        TownHud.StripStep = -1f;
        Main.Driver.SetPaused(false);

        // The choice opening (and leaving after a pick).
        Main.Driver.RunBotUntil(bot, t => t.ChoiceOpen, 20 * 60 * 60 * 10);
        if (T.ChoiceOpen)
        {
            Main.Driver.SetPaused(true);
            TownHud.StripStep = 0f;
            await Frames(2);
            hud.ClearMessagesNow();
            for (int k = 0; k < 6; k++) await MsgFrame("msg-choice", k, k == 0 ? 0.001f : TownHud.LargeSeconds / 4f);
            TestInput.Key(Godot.Key.Z);
            for (int k = 6; k < 12; k++) await MsgFrame("msg-choice", k, k == 6 ? 0.001f : TownHud.LargeSeconds / 4f);
            Main.Driver.SetPaused(false);
        }
        else Check(false, "the strips: no choice came");
        // A request arriving, being met and leaving (the bot plays until a request runs, then until it ends).
        Main.Driver.RunBotUntil(bot, t => t.Request.Active, 20 * 60 * 60 * 2);
        if (T.Request.Active)
        {
            Main.Driver.SetPaused(true);
            TownHud.StripStep = 0f;
            await Frames(2);
            hud.ClearMessagesNow();
            for (int k = 0; k < 5; k++) await MsgFrame("msg-request", k, k == 0 ? 0.001f : TownHud.LargeSeconds / 4f);
            Main.Driver.SetPaused(false);
            Main.Driver.RunBotUntil(bot, t => !t.Request.Active, 20 * 60 * 60);
            Main.Driver.SetPaused(true);
            for (int k = 5; k < 8; k++) await MsgFrame("msg-request", k, 0.5f); // "Done" or "Too late", 1.6 s in place
            for (int k = 8; k < 12; k++) await MsgFrame("msg-request", k, TownHud.LargeSeconds / 4f);
            Main.Driver.SetPaused(false);
        }
        else Check(false, "the strips: no request came");

        // Coin numbers: the town runs at its own pace; the interface's clock steps 0.12 s a frame.
        Main.Driver.SetSpeed(1);
        for (int k = 0; k < 12; k++) { TownHud.StripStep = -1f; await Frames(1); await MsgFrame("msg-coin", k, 0.12f); }
        TownHud.StripStep = -1f;

        TownHud.StripStep = -1f;
    }
}
