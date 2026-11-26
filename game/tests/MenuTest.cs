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

    /// <summary>
    /// Round 4 (r4-review A1, A2): game keys do nothing under the closed cover, on the settings pages or while the book
    /// closes; Extend mode ends when the game pauses; a click on the table beside the closed book does nothing; after
    /// Resume no mode is on and the next Esc pauses.
    /// </summary>
    private async Task KeysUnderTheCover()
    {
        // Extend mode on by its key (the bot plays until a cottage can be extended and paid for).
        Main.Driver.RunBot(new TideToll.Sim.Town.TownBot(TideToll.Sim.Town.BotKind.Payback, 1),
            t => t.SuggestedExtend() >= 0 && t.Coin >= t.ExtendPrice, 60 * 60 * 30);
        Check(T.SuggestedExtend() >= 0 && T.Coin >= T.ExtendPrice, $"the bot found no cottage to extend (level {T.Level}, '{T.ExtendLockReason}')");
        Main.Driver.SetPaused(true); // the town stands still while the test looks (Resume below unpauses it)
        await Frames(5);
        await Key(Godot.Key.E);
        Check(Main.Hud.ExtendMode, $"E did not switch Extend mode on ('{Main.Hud.LastNote}')");
        await ClickC(Main.Hud.PauseMark);
        await Until(() => M.CoverShown, 600, "the cover after the pause control in Extend mode");
        Check(!Main.Hud.ExtendMode, "Extend mode survived the pause");

        // Clicks on the table beside the closed book, and on a cottage there, do nothing.
        double coin = T.Coin; int level = T.Level, people = T.VillagerCount;
        var cover = M.CoverRect();
        int plot = T.SuggestedExtend();
        foreach (var p in new[] { cover.Position + new Vector2(-80, cover.Size.Y / 2), cover.End + new Vector2(60, -40), new Vector2(40, 1040), Main.Hud.PlotScreen(plot) })
        {
            if (cover.HasPoint(p)) continue;
            await Click(p);
        }
        // Game keys under the cover: Pave, Extend, a purchase, the bell, a building, achievements.
        foreach (var k in new[] { Godot.Key.P, Godot.Key.E, Godot.Key.Key4, Godot.Key.Key5, Godot.Key.Key2, Godot.Key.Key1, Godot.Key.Space, Godot.Key.F, Godot.Key.Tab })
        {
            if (k == Godot.Key.Space) continue; // Space presses the focused cover item (Resume), as a button should
            await Key(k);
        }
        Check(!Main.Hud.PaveMode && !Main.Hud.ExtendMode && !Main.Hud.AchievementsOpen, "a game key switched a mode or page on under the closed cover");
        Check(T.Coin == coin && T.Level == level && T.VillagerCount == people, "a key or click under the closed cover bought something");
        Check(M.CoverShown, "the cover went away");
        // The same on the settings pages.
        M.SettingsButton.GrabFocus();
        await Key(Godot.Key.Enter);
        await Frames(3);
        Check(M.Pages != null, "the settings pages did not open");
        foreach (var k in new[] { Godot.Key.P, Godot.Key.E, Godot.Key.Key4, Godot.Key.Key2 }) await Key(k);
        Check(!Main.Hud.PaveMode && !Main.Hud.ExtendMode && T.Coin == coin, "a game key acted on the settings pages");
        await Key(Godot.Key.Escape);
        await Frames(3);
        Check(M.CoverShown, "Esc did not leave the settings pages");
        // Resume: no mode on, and the next Esc pauses at once.
        await Key(Godot.Key.Escape);
        // A key while the book opens does nothing either.
        await Key(Godot.Key.P);
        Check(M.Now != PauseMenu.State.Playing || !Main.Hud.PaveMode, "P acted while the book was opening");
        await Resumed();
        Check(!Main.Hud.PaveMode && !Main.Hud.ExtendMode, "a mode was on after Resume");
        await Key(Godot.Key.Escape);
        // While the book closes, P does nothing.
        await Key(Godot.Key.P);
        await Until(() => M.CoverShown, 600, "Esc after Resume did not pause");
        Check(!Main.Hud.PaveMode, "P acted while the book was closing");
        await Key(Godot.Key.Escape);
        await Resumed();
        Check(!Main.Hud.PaveMode && !M.IsPaused, "after the cover, a mode was on or the game stayed paused");
        Notes.Add("cover-keys=ok");
    }

    protected override async Task Run()
    {
        Main.Driver.Advance(30);
        // Move the camera by hand so Resume has something to return to.
        Main.Rig.UserMoved = true;
        Main.Rig.LookAt(Main.Rig.PivotLocal + new Vector3(8, 0, 6), Main.Rig.Camera.Size * 1.3f, instant: true);
        await Frames(10);

        // Esc in Pave mode leaves Pave mode first and does not pause (the bot plays until a road can be paved and paid for: round 3, Pave mode opens only then).
        Main.Driver.RunBot(new TideToll.Sim.Town.TownBot(TideToll.Sim.Town.BotKind.Payback, 1), t => t.SuggestedPave() >= 0 && t.Coin >= t.PavePrice(t.SuggestedPave()), 10 * 60 * 30);
        await Frames(3);
        Main.Hud.SetPaveMode(true);
        await Frames(2);
        Check(Main.Hud.PaveMode, "Pave mode did not open (no worn-in road)");
        await Key(Godot.Key.Escape);
        Check(!Main.Hud.PaveMode && !M.IsPaused && !Main.Driver.Paused, "Esc in Pave mode should only leave Pave mode");
        // Round 3: Pave mode may bring the suggested road into view, so the hand-moved view to return to is set after it.
        Main.Rig.UserMoved = true;
        Main.Rig.LookAt(Main.Rig.PivotLocal + new Vector3(8, 0, 6), Main.Rig.Camera.Size * 1.3f, instant: true);
        await Frames(10);
        var pivot0 = Main.Rig.PivotLocal; float size0 = Main.Rig.Camera.Size;

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

        await KeysUnderTheCover();

        // The pause control in the interface.
        await ClickC(Main.Hud.PauseMark);
        await Until(() => M.CoverShown, 600, "the cover after the pause control");

        // New town: asks once; keeping the town changes nothing; then a new seed at level 1.
        Main.Driver.Advance(600);
        int seed = Main.Driver.Seed;
        M.NewTownButton.GrabFocus();
        await Key(Godot.Key.Enter);
        Check(M.QuestionShown && M.QuestionText == PauseMenu.Question, "New town did not ask");
        // Round 4 (A13): both choices look like buttons before the pointer is on them (a frame in their normal state).
        foreach (var b in new[] { M.YesButton, M.NoButton })
            Check(b.GetThemeStylebox("normal") is StyleBoxFlat f && f.BorderColor.A > 0.3f && f.BorderWidthTop > 0, $"'{b.Text}' has no frame of its own");
        await Key(Godot.Key.Escape);
        Check(M.CoverShown && Main.Driver.Seed == seed, "Esc on the question did not keep the town");
        M.NewTownButton.GrabFocus();
        await Key(Godot.Key.Enter);
        Check(GetViewport().GuiGetFocusOwner() == M.NoButton, "the question should start on Keep this town");
        // Round 4 (A8): the new town starts clean: no perk silenced, no hint marker or request flash from the old town.
        bool clean = false; string was = $"perks {Main.Hud.PerksNoted} hint '{Main.Hud.HintText}' marker {Main.Hud.HintMarkerVisible} flash {Main.Hud.RequestFlashing}";
        void Replaced() => clean = Main.Hud.PerksNoted == 0 && Main.Hud.HintText == "" && !Main.Hud.HintMarkerVisible && !Main.Hud.RequestFlashing;
        Main.Driver.Replaced += Replaced;
        await Key(Godot.Key.Up);
        await Key(Godot.Key.Enter);
        Main.Driver.Replaced -= Replaced;
        Check(clean, $"New town kept the old town's perks, hint or request flash (before: {was})");
        Notes.Add("before-new: " + was);
        await Resumed();
        Check(Main.Driver.Seed != seed && T.Level == 1, $"New town gave seed {Main.Driver.Seed} level {T.Level}");
        Check(store.KeyFor("tt_pause", 0) == Godot.Key.K && store.Volume("Music") == 40, "New town touched the settings");
        Notes.Add($"seed {seed}->{Main.Driver.Seed}");
        store.RestoreDefaultKeys();
    }
}
