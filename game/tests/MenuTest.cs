using System;
using System.Threading.Tasks;
using Godot;
using TideToll.Game.Settings;

namespace TideToll.Game.Tests;

/// <summary>
/// The pause menu at 1920 x 1080 with real input: Esc closes the book (the simulation's tick stands still over 60
/// frames) and the cover menu shows; with the keyboard alone down to Settings, Enter opens the settings pages, Esc
/// (Back) returns to the cover, and the Back control does too; Resume continues the simulation and puts the camera
/// back; settings changed on the pages take effect in the game (interface size, a volume, coin numbers off, a rebound
/// pause key); New town asks once, then gives a different seed at level 1; the pause control in the interface.
/// </summary>
public partial class MenuTest : TownTestBase
{
    protected override string Marker => "MENU PASS";
    protected override Vector2I WindowSize => new(1920, 1080);
    private PauseMenu M => Main.Menu;

    private Vector2 Screen(Control c) => GetViewport().GetFinalTransform() * c.GetGlobalRect().GetCenter();

    private async Task ClickC(Control c)
    {
        var p = Screen(c);
        TestInput.Motion(p);
        await Frames(1);
        TestInput.Button(p, MouseButton.Left, true);
        await Frames(1);
        TestInput.Button(p, MouseButton.Left, false);
        await Frames(3);
    }

    private async Task PauseByKey(Key k)
    {
        await Key(k);
        await Until(() => M.CoverShown, 600, "the cover menu after pausing");
    }

    private async Task Resumed()
    {
        await Until(() => M.Now == PauseMenu.State.Playing, 600, "the book open again");
        await Until(() => MathF.Abs(Main.Rig.Camera.Size - Main.Rig.TargetSize) < 0.01f, 600, "the camera settled");
        await Frames(30);
    }

    protected override async Task Run()
    {
        Main.Driver.Advance(30);
        // Move the camera by hand so Resume has something to return to.
        Main.Rig.UserMoved = true;
        Main.Rig.LookAt(Main.Rig.PivotLocal + new Vector3(8, 0, 6), Main.Rig.Camera.Size * 1.3f, instant: true);
        await Frames(10);
        var pivot0 = Main.Rig.PivotLocal; float size0 = Main.Rig.Camera.Size;

        // Esc in Pave mode leaves Pave mode first and does not pause.
        Main.Hud.SetPaveMode(true);
        await Frames(2);
        await Key(Godot.Key.Escape);
        Check(!Main.Hud.PaveMode && !M.IsPaused && !Main.Driver.Paused, "Esc in Pave mode should only leave Pave mode");

        // Esc pauses: the book closes, the tick stands still, the cover menu shows with Resume focused.
        int books0 = Main.Audio.BookSounds;
        await PauseByKey(Godot.Key.Escape);
        Check(Main.Driver.Paused, "Esc did not stop the simulation");
        Check(Main.Cover.Closed, "the cover is not closed");
        Check(!Main.Hud.Visible, "the interface still shows over the closed book");
        Check(Main.Audio.PauseMusic, "the pause music is not on");
        Check(Main.Audio.BookSounds > books0, "no book-close sound");
        Check(Main.Saver.HasSave, "pausing did not save");
        long tick = T.Tick;
        await Frames(60);
        Check(T.Tick == tick, $"the tick advanced while paused ({tick} -> {T.Tick})");
        Check(GetViewport().GuiGetFocusOwner() == M.ResumeButton, "Resume does not have the focus");

        // The keyboard alone: down to Settings, Enter, the pages; Esc (Back) to the cover.
        int ui0 = Main.Audio.UiSounds;
        await Key(Godot.Key.Down);
        Check(GetViewport().GuiGetFocusOwner() == M.SettingsButton, "Down did not move to Settings");
        await Key(Godot.Key.Enter);
        await Frames(3);
        Check(M.Pages != null && M.Pages.IsVisibleInTree(), "Enter on Settings did not open the settings pages");
        Check(Main.Audio.UiSounds > ui0, "no interface sound on the menu");
        await Key(Godot.Key.Escape);
        await Frames(3);
        Check(M.Pages == null && M.CoverShown, "Esc on the settings pages did not return to the cover");
        Check(T.Tick == tick, "the settings pages let the town run");
        // The same with the Back control.
        await Key(Godot.Key.Enter);
        await Frames(3);
        Check(M.Pages != null, "Enter did not reopen the settings");
        await ClickC((Control)M.Pages!.Controls["back"]);
        Check(M.Pages == null && M.CoverShown, "Back did not return to the cover");

        // Settings changed on the pages take effect in the game.
        await Key(Godot.Key.Enter);
        await Frames(3);
        var pages = M.Pages!;
        var store = GameSettings.Current!;
        ((HSlider)pages.Controls["sound.Music"]).Value = 0;
        await Frames(2);
        int music = AudioServer.GetBusIndex("Music");
        Check(store.Volume("Music") == 0 && AudioServer.IsBusMute(music), "Music at 0 did not mute the Music bus");
        ((HSlider)pages.Controls["sound.Music"]).Value = 40;
        await Frames(2);
        Check(!AudioServer.IsBusMute(music) && MathF.Abs(AudioServer.GetBusVolumeDb(music) - SettingsApplier.VolumeDb(40)) < 0.01f, "Music at 40 not applied");
        pages.ShowSection("Display");
        await Frames(3);
        await ClickC(pages.Controls["display.coins"]);
        Check(!store.ShowCoinNumbers, "the coin numbers switch did not turn them off");
        pages.ShowSection("Keys");
        await Frames(3);
        await ClickC(pages.Controls["keys.tt_pause.0"]);
        await Key(Godot.Key.K);
        Check(store.KeyFor("tt_pause", 0) == Godot.Key.K, "K was not bound to Pause");
        pages.ShowSection("Display");
        await Frames(3);
        float markPx0 = M.GetViewport().GetFinalTransform().Scale.Y * Main.Hud.PauseMark.Size.Y;
        ((ChoiceButton)pages.Controls["display.interface"]).SetIndex((150 - GameSettings.MinInterface) / 10, notify: true);
        await Frames(5);
        Check(MathF.Abs(GetWindow().ContentScaleFactor - 1.5f) < 1e-3f, "interface 150 % did not set the content scale");
        await ClickC((Control)pages.Controls["back"]);
        Check(M.CoverShown, "Back did not return to the cover after the changes");

        // Resume (Enter on Resume): the book opens, the town runs, the camera is back where it was.
        M.ResumeButton.GrabFocus();
        await Key(Godot.Key.Enter);
        await Resumed();
        Check(!Main.Driver.Paused && T.Tick > tick, "Resume did not continue the simulation");
        Check(Main.Rig.PivotLocal.DistanceTo(pivot0) < 0.5f && MathF.Abs(Main.Rig.Camera.Size - size0) < 0.5f,
            $"the camera did not return: {Main.Rig.PivotLocal} size {Main.Rig.Camera.Size} (was {pivot0} size {size0})");
        Check(Main.Hud.Visible, "the interface did not come back");
        Check(!Main.Audio.PauseMusic, "the pause music did not stop");

        // In the game: the interface is 1.5 times as large on screen (counted once), coin numbers are off.
        await Frames(5);
        float markPx1 = GetViewport().GetFinalTransform().Scale.Y * Main.Hud.PauseMark.Size.Y;
        Notes.Add($"pause-mark {markPx0:0}px->{markPx1:0}px");
        Check(markPx1 > markPx0 * 1.4f && markPx1 < markPx0 * 1.6f, $"the interface did not grow 1.5 times ({markPx0} -> {markPx1} px)");
        long trips = T.Trips; int floats = Main.Hud.FloatersStarted;
        Main.Driver.SetSpeed(16);
        await Until(() => T.Trips >= trips + 3, 60 * 60, "three deliveries");
        Main.Driver.SetSpeed(1);
        Check(Main.Hud.FloatersStarted == floats, "coin numbers still float up with the setting off");
        store.SetInterfaceSize(100);
        await Frames(5);

        // The rebound key pauses; Esc on the cover resumes.
        await PauseByKey(Godot.Key.K);
        await Key(Godot.Key.Escape);
        await Resumed();
        Check(!Main.Driver.Paused, "Esc on the cover did not resume");

        // The pause control in the interface.
        await ClickC(Main.Hud.PauseMark);
        await Until(() => M.CoverShown, 600, "the cover after the pause control");

        // New town: asks once; keeping the town changes nothing; then a new seed at level 1.
        Main.Driver.Advance(600);
        int seed = Main.Driver.Seed;
        M.NewTownButton.GrabFocus();
        await Key(Godot.Key.Enter);
        Check(M.QuestionShown && M.QuestionText == PauseMenu.Question, "New town did not ask");
        await Key(Godot.Key.Escape);
        Check(M.CoverShown && Main.Driver.Seed == seed, "Esc on the question did not keep the town");
        M.NewTownButton.GrabFocus();
        await Key(Godot.Key.Enter);
        Check(GetViewport().GuiGetFocusOwner() == M.NoButton, "the question should start on Keep this town");
        await Key(Godot.Key.Up);
        await Key(Godot.Key.Enter);
        await Resumed();
        Check(Main.Driver.Seed != seed && T.Level == 1, $"New town gave seed {Main.Driver.Seed} level {T.Level}");
        Check(store.KeyFor("tt_pause", 0) == Godot.Key.K && store.Volume("Music") == 40, "New town touched the settings");
        Notes.Add($"seed {seed}->{Main.Driver.Seed}");
        store.RestoreDefaultKeys();
    }
}
