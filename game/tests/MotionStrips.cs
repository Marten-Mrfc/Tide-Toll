using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Game.Settings;
using TideToll.Sim.Town;

namespace TideToll.Game.Tests;

/// <summary>
/// Frame strips of the book's transitions for human eyes (tools/motion-strips.sh: rendered under xvfb with the Mobile
/// renderer at 1920 x 1080). Each transition is started by real input, its clock is then held at twelve evenly spaced
/// moments from its start to its end (BookMotion.Hold, SettingsPages.HoldTurnAt, TownHud.HoldPageTurnAt) and each moment
/// is saved at 640 x 360 as --out-dir/frames/NAME-NN.png; the script joins them into one contact sheet per transition.
/// </summary>
public partial class MotionStrips : TownTestBase
{
    protected override string Marker => "STRIPS PASS";
    protected override Vector2I WindowSize => GetWindow().Size;
    public const int Count = 12;
    private string _dir = "";
    private PauseMenu M => Main.Menu;
    private SettingsPages P => Main.Menu.SettingsSpread;

    protected override async Task Run()
    {
        _dir = ProjectSettings.GlobalizePath("res://../build/motion");
        foreach (var a in OS.GetCmdlineUserArgs()) if (a.StartsWith("--out-dir=")) _dir = a.Substring(10);
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(_dir, "frames"));
        bool ageOnly = Array.IndexOf(OS.GetCmdlineUserArgs(), "--age-only") >= 0;
        var bot = new TownBot(BotKind.Payback, 1);
        if (!ageOnly)
        {
            Main.Driver.RunBot(bot, t => t.Level >= 5, 20 * 60 * 30);
            await Frames(30);
            // The view moved by hand, tilted over the town, as a player leaves it.
            var ext = Main.TownExtent().GetCenter();
            Main.Rig.UserMoved = true;
            Main.Rig.LookAt(new Vector3(ext.X + 6, 0, ext.Y + 4), 80f, instant: true);
            await Wait(1500);

            await Strip("pause", () => TestInput.Key(Godot.Key.Escape));
            await Until(() => M.CoverShown, 3000, "the cover");
            M.SettingsButton.GrabFocus();
            await Strip("settings-open", () => TestInput.Key(Godot.Key.Enter));
            await Until(() => M.Now == PauseMenu.State.Settings && !M.Motion.Running, 3000, "the settings");
            await Wait(400);
            await TurnStrip("tab-forward", "ribbon.Display");
            await TurnStrip("tab-back", "ribbon.Sound");
            await Strip("settings-close", () => TestInput.Key(Godot.Key.Escape));
            await Until(() => M.CoverShown, 3000, "the cover");
            M.NewTownButton.GrabFocus();
            await Strip("question", () => TestInput.Key(Godot.Key.Enter), half: 0);
            await Until(() => !M.Motion.Running, 3000, "the question");
            await Strip("question", () => TestInput.Key(Godot.Key.Escape), half: 1);
            await Until(() => M.CoverShown, 3000, "the cover");
            M.ResumeButton.GrabFocus();
            await Strip("resume", () => TestInput.Key(Godot.Key.Enter));
            await Until(() => M.Now == PauseMenu.State.Playing, 3000, "the town");
        }
        // The age-change page: a town at its charter, the next age rung in by its key.
        Main.Driver.RunBot(bot, t => t.HasCharter, 20 * 60 * 60);
        Check(Main.Driver.Town.HasCharter, "the bot reached the charter");
        Main.Rig.UserMoved = false;
        Main.Follow(instant: true);
        await Wait(1500);
        Main.Hud.HoldPageTurnAt = 0f;
        TestInput.Key(Godot.Key.N);
        await Frames(2);
        Check(Main.Hud.PageTurning, "the page turns at the next age");
        for (int k = 0; k < Count; k++)
        {
            Main.Hud.HoldPageTurnAt = TownHud.WipeSeconds * k / (Count - 1);
            await Frames(3);
            await Save("age-page", k);
        }
        Main.Hud.HoldPageTurnAt = -1f;
        Notes.Add("dir=" + _dir);
    }

    private async Task Wait(int ms)
    {
        ulong t0 = Time.GetTicksMsec();
        while (Time.GetTicksMsec() - t0 < (ulong)ms) await Frames(1);
    }

    /// <summary>
    /// Starts a book transition and holds its clock at Count even moments from its start to its end (half 0 or 1: only
    /// the first or second half of the sheet's frames, for a sheet that shows two transitions).
    /// </summary>
    private async Task Strip(string name, Action input, int half = -1)
    {
        M.Motion.Hold(0f);
        input();
        await Frames(2);
        Check(M.Motion.Running, $"{name}: no transition started");
        int n = half < 0 ? Count : Count / 2, off = half == 1 ? Count / 2 : 0;
        for (int k = 0; k < n; k++)
        {
            M.Motion.Hold(M.Motion.Length * k / (n - 1));
            await Frames(3);
            await Save(name, off + k);
        }
        M.Motion.Release();
        await Until(() => !M.Motion.Running, 3000, name + " to end");
        await Frames(5);
    }

    private async Task TurnStrip(string name, string ribbon)
    {
        P.HoldTurnAt = 0f;
        var c = P.Controls[ribbon];
        var at = GetViewport().GetFinalTransform() * c.GetGlobalRect().GetCenter();
        TestInput.Motion(at);
        TestInput.Button(at, MouseButton.Left, true);
        TestInput.Button(at, MouseButton.Left, false);
        await Frames(2);
        Check(P.Turning, $"{name}: no page turned");
        for (int k = 0; k < Count; k++)
        {
            P.HoldTurnAt = k / (float)(Count - 1);
            await Frames(3);
            await Save(name, k);
        }
        P.HoldTurnAt = -1f;
        await Until(() => !P.Turning, 3000, name + " to end");
        // Move the pointer off the ribbons so the next sheet does not show a hover.
        TestInput.Motion(new Vector2(960, 1060));
        await Frames(5);
    }

    private async Task Save(string name, int k)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var img = GetViewport().GetTexture().GetImage();
        img.Resize(640, 360, Image.Interpolation.Lanczos);
        img.SavePng(System.IO.Path.Combine(_dir, "frames", $"{name}-{k:00}.png"));
    }
}
