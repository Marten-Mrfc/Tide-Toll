using System;
using System.Collections.Generic;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Round 5, rules v3 in the interface: the choice of two boons (a card at the top centre with both boons side by side;
/// the town runs on behind it; a click or the keys tt_choose_a / tt_choose_b pick), the charter drive (its own card
/// under the request card, a bar that fills in parts), milestones (a note with a small flourish) and the extra hands
/// (a marker over the workplace with an offer). The card never covers more than a strip at the top of the map.
/// </summary>
public partial class TownHud
{
    private PanelContainer _choice = null!;
    private Label _choiceHead = null!;
    private readonly Button[] _boonBtn = new Button[2];
    private readonly Label[] _boonName = new Label[2], _boonText = new Label[2], _boonWorth = new Label[2];
    private readonly TextureRect[] _boonIcon = new TextureRect[2];
    private int _choiceSig = int.MinValue;
    private PanelContainer _drive = null!;
    private Label _driveName = null!, _driveAsk = null!, _driveDone = null!;
    private Control _driveBarBack = null!;
    private ColorRect _driveBar = null!;
    private readonly ColorRect[] _driveTicks = new ColorRect[DriveParts - 1];
    private int _driveSig = int.MinValue;
    private Control _handLayer = null!;
    private readonly List<Label> _handMarks = new();
    private float _flourish;
    private static readonly StringName HandMeta = "hand_workplace";
    /// <summary>The bar of the charter drive is drawn in this many parts.</summary>
    public const int DriveParts = 10;

    // Tests.
    public bool ChoiceCardVisible => _choice.Visible;
    public Button BoonButton(int side) => _boonBtn[side];
    public string ChoiceCardText => _choice.Visible ? $"{_choiceHead.Text} | {_boonName[0].Text}: {_boonText[0].Text} | {_boonName[1].Text}: {_boonText[1].Text}" : "";
    public bool DriveCardVisible => _drive.Visible;
    public string DriveCardText => _drive.Visible ? $"{_driveName.Text} | {_driveAsk.Text} | {_driveDone.Text}" : "";
    public float DriveBar => _driveBar.Size.X / Math.Max(1f, _driveBarBack.Size.X - 2f);
    public int HandMarkersShown { get { int n = 0; foreach (var l in _handMarks) if (l.Visible) n++; return n; } }
    public int Milestones { get; private set; }
    public int BoonMoments { get; private set; }
    public string LastMilestone { get; private set; } = "";

    public static string BoonIcon(Boon b) => b switch
    {
        Boon.WiderLanes => "up-pave", Boon.StrongerBacks => "up-pace", Boon.DeeperWell => "good-water", Boon.LongerRows => "good-grain",
        Boon.ThirdRoom => "ui-extend", Boon.FasterHands => "ui-tools", Boon.MarketRight => "ui-value", Boon.MillersGuild => "good-flour",
        _ => "ui-achievement",
    };

    private void BuildChoiceParts()
    {
        _choice = new PanelContainer { Name = "Choice", MouseFilter = Control.MouseFilterEnum.Stop, Visible = false };
        var box = UiKit.Card(_s, new Color(UiKit.Paper, 0.97f), 2f);
        box.SetContentMarginAll(UiKit.Px(_s, 10));
        _choice.AddThemeStyleboxOverride("panel", box);
        _root.AddChild(_choice);
        var col = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", UiKit.Px(_s, 6));
        _choice.AddChild(col);
        _choiceHead = UiKit.Text("", _s, 20, UiKit.Serif);
        _choiceHead.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(_choiceHead);
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", UiKit.Px(_s, 10));
        col.AddChild(row);
        for (int side = 0; side < 2; side++)
        {
            int k = side;
            var b = new Button { Name = side == 0 ? "BoonA" : "BoonB", FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(UiKit.Px(_s, 270), UiKit.Px(_s, 112)) };
            foreach (var (state, bg, w) in new[] { ("normal", UiKit.Paper, 1.5f), ("hover", UiKit.PaperHover, 2.5f), ("pressed", UiKit.Pressed, 2.5f), ("hover_pressed", UiKit.Pressed, 2.5f) })
            {
                var sb = UiKit.Card(_s, bg, w);
                sb.SetContentMarginAll(UiKit.Px(_s, 8));
                b.AddThemeStyleboxOverride(state, sb);
            }
            _boonBtn[side] = b;
            b.Pressed += () => PickBoon(k);
            b.MouseEntered += () => _main.Audio.UiHover();
            row.AddChild(b);
            var inner = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            inner.AddThemeConstantOverride("separation", UiKit.Px(_s, 8));
            inner.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.Minsize, UiKit.Px(_s, 8));
            b.AddChild(inner);
            _boonIcon[side] = new TextureRect
            {
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = Vector2.One * UiKit.Px(_s, 44), TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
                MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            };
            inner.AddChild(_boonIcon[side]);
            var words = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            words.AddThemeConstantOverride("separation", UiKit.Px(_s, 2));
            inner.AddChild(words);
            _boonName[side] = UiKit.Text("", _s, 18, UiKit.UiBold);
            _boonText[side] = UiKit.Text("", _s, 15, UiKit.UiFont);
            _boonText[side].AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _boonText[side].CustomMinimumSize = new Vector2(UiKit.Px(_s, 196), 0);
            _boonWorth[side] = UiKit.Text("", _s, 14, UiKit.UiFont, UiKit.FadedInk);
            words.AddChild(_boonName[side]);
            words.AddChild(_boonText[side]);
            words.AddChild(_boonWorth[side]);
        }
        _choiceSig = int.MinValue;

        _drive = new PanelContainer { Name = "Drive", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        var dbox = UiKit.Card(_s, new Color(UiKit.Pressed.Lerp(UiKit.Paper, 0.5f), 0.97f), 2f);
        dbox.SetContentMarginAll(UiKit.Px(_s, 9));
        _drive.AddThemeStyleboxOverride("panel", dbox);
        _drive.CustomMinimumSize = new Vector2(UiKit.Px(_s, 330), 0);
        _root.AddChild(_drive);
        var dcol = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        dcol.AddThemeConstantOverride("separation", UiKit.Px(_s, 3));
        _drive.AddChild(dcol);
        _driveName = UiKit.Text("", _s, 19, UiKit.Serif);
        _driveAsk = UiKit.Text("", _s, 17, UiKit.UiBold);
        _driveAsk.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _driveAsk.CustomMinimumSize = new Vector2(UiKit.Px(_s, 310), 0);
        _driveDone = UiKit.Text("", _s, 15, UiKit.UiFont);
        dcol.AddChild(_driveName);
        dcol.AddChild(_driveAsk);
        _driveBarBack = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(0, UiKit.Px(_s, 12)) };
        var back = UiKit.Card(_s, UiKit.PaperDark, 1f);
        back.SetContentMarginAll(0);
        _driveBarBack.AddThemeStyleboxOverride("panel", back);
        dcol.AddChild(_driveBarBack);
        _driveBar = new ColorRect { Color = Palette.Thatch, MouseFilter = Control.MouseFilterEnum.Ignore };
        _driveBarBack.AddChild(_driveBar);
        for (int i = 0; i < _driveTicks.Length; i++)
        {
            _driveTicks[i] = new ColorRect { Color = new Color(UiKit.Ink, 0.55f), MouseFilter = Control.MouseFilterEnum.Ignore };
            _driveBarBack.AddChild(_driveTicks[i]);
        }
        dcol.AddChild(_driveDone);
        _driveSig = int.MinValue;

        _handLayer = new Control { Name = "HandMarks", MouseFilter = Control.MouseFilterEnum.Ignore };
        _handLayer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(_handLayer);
        // Under every card and page: markers belong to the map.
        _root.MoveChild(_handLayer, _floatLayer.GetIndex() + 1);
        _handMarks.Clear();
    }

    /// <summary>Takes one side of the open choice (a click on its half of the card or its key).</summary>
    public bool PickBoon(int side)
    {
        var t = _drv.Town;
        if (!t.ChoiceOpen) return false;
        bool ok = t.ChooseSide(side);
        if (ok) _main.Audio.UiPress();
        return ok;
    }

    private bool ChoiceKey(InputEvent e)
    {
        if (!_drv.Town.ChoiceOpen) return false;
        if (Is(e, "tt_choose_a")) { PickBoon(0); return true; }
        if (Is(e, "tt_choose_b")) { PickBoon(1); return true; }
        return false;
    }

    private void ChoiceFrame(float dt)
    {
        var t = _drv.Town;
        // Shown over everything, the achievements page too (it starts below the card): a choice is never missed.
        bool show = t.ChoiceOpen;
        _choice.Visible = show;
        if (show)
        {
            var c = t.Choice;
            int sig = HashCode.Combine(c.Choice, WorthTenths(c.AWorth), WorthTenths(c.BWorth), Settings.GameInput.Version);
            if (sig != _choiceSig)
            {
                _choiceSig = sig;
                _choiceHead.Text = $"Level {c.Level}: choose one boon, for good. The town runs on.";
                for (int side = 0; side < 2; side++)
                {
                    var b = side == 0 ? c.A : c.B;
                    _boonName[side].Text = (side == 0 ? c.AName : c.BName) + Settings.GameInput.Tag(side == 0 ? "tt_choose_a" : "tt_choose_b", "   ");
                    _boonText[side].Text = side == 0 ? c.AText : c.BText;
                    int w = WorthTenths(side == 0 ? c.AWorth : c.BWorth);
                    _boonWorth[side].Text = w > 0 ? WorthText(w) + " now" : "";
                    _boonIcon[side].Texture = IconOr(BoonIcon(b), "ui-achievement");
                    _boonBtn[side].TooltipText = Town.BoonText(b);
                }
            }
            // Top centre, between the ledger and the bookmarks; a gentle pulse of the frame so it is not missed.
            var vp = _built;
            var size = _choice.GetCombinedMinimumSize();
            _choice.Size = size;
            _choice.Position = new Vector2((vp.X - size.X) / 2f, UiKit.Px(_s, 14));
            float pulse = 0.5f + 0.5f * MathF.Sin((float)_drv.AnimTime * 3f);
            _choice.Modulate = new Color(1, 1, 1, 0.94f + 0.06f * pulse);
        }
        DriveFrame();
        HandFrame();
        if (_flourish > 0) _flourish = Math.Max(0, _flourish - dt);
    }

    private void DriveFrame()
    {
        var t = _drv.Town;
        var d = t.Drive;
        bool show = d.Active && _fullA > 0.01f;
        _drive.Visible = show;
        if (!show) return;
        float y = (_ledger != null ? _ledger.GetRect().End.Y : 180) + UiKit.Px(_s, 8);
        if (_req.Visible) y = _req.GetRect().End.Y + UiKit.Px(_s, 8);
        _drive.Position = new Vector2(UiKit.Px(_s, 16), y);
        _drive.Modulate = new Color(1, 1, 1, _fullA);
        int sig = HashCode.Combine(d.Good, d.Amount, d.Delivered, (long)d.RenownGiven);
        if (sig != _driveSig)
        {
            _driveSig = sig;
            _driveName.Text = d.Name;
            _driveAsk.Text = $"Send {Money(d.Amount)} {GoodName(d.Good)} for the charter";
            _driveDone.Text = $"Sent {Money(Math.Min(d.Delivered, d.Amount))} of {Money(d.Amount)}. Every load counts.";
        }
        float k = d.Amount > 0 ? Math.Clamp((float)d.Delivered / d.Amount, 0f, 1f) : 0f;
        var sz = _driveBarBack.Size;
        // Fills in parts: whole tenths, so each part lands as a small step.
        float parts = MathF.Floor(k * DriveParts) / DriveParts;
        _driveBar.Position = new Vector2(1, 1);
        _driveBar.Size = new Vector2(Math.Max(0, (sz.X - 2) * parts), Math.Max(0, sz.Y - 2));
        for (int i = 0; i < _driveTicks.Length; i++)
        {
            _driveTicks[i].Position = new Vector2(1 + (sz.X - 2) * (i + 1) / DriveParts, 1);
            _driveTicks[i].Size = new Vector2(Math.Max(1, UiKit.Px(_s, 1.5f)), Math.Max(0, sz.Y - 2));
        }
    }

    private void HandFrame()
    {
        var t = _drv.Town;
        int n = t.WorkplaceCount, used = 0;
        bool show = _fullA > 0.01f && !AchievementsOpen && !AgePageOpen;
        for (int w = 0; w < n && show; w++)
        {
            if (!t.HandOffered(w)) continue;
            var wp = t.Workplace(w);
            if (wp.Plot < 0) continue;
            var pl = t.Plan.Plots[wp.Plot];
            var at = _main.Rig.LocalToScreen(new Vector3((pl.X + pl.W / 2f) * TownGround.Cell, 6f, (pl.Y + pl.H / 2f) * TownGround.Cell));
            if (used == _handMarks.Count)
            {
                var l = UiKit.Text("+ hand", _s, 15, UiKit.UiBold, Color.FromHtml("#3A2A10"));
                var sb = UiKit.Card(_s, new Color(UiKit.Pressed, 0.95f), 1.5f);
                sb.SetContentMarginAll(UiKit.Px(_s, 4));
                l.AddThemeStyleboxOverride("normal", sb);
                l.MouseFilter = Control.MouseFilterEnum.Ignore;
                _handLayer.AddChild(l);
                _handMarks.Add(l);
            }
            var m = _handMarks[used++];
            m.Visible = true;
            if (m.GetMeta(HandMeta, -1).AsInt32() != w) { m.SetMeta(HandMeta, w); m.TooltipText = t.HandName(w); }
            m.ResetSize();
            m.Position = at - new Vector2(m.Size.X / 2f, m.Size.Y + UiKit.Px(_s, 6));
        }
        for (int i = used; i < _handMarks.Count; i++) _handMarks[i].Visible = false;
    }

    /// <summary>Rules v3 events: the choice offered and taken, milestones, the drive, a hand on offer, a touch.</summary>
    private bool ChoiceEvent(in TownEvent e)
    {
        switch (e.Kind)
        {
            case TownEventKind.ChoiceOffered:
                Note("A choice of two boons is open at the top.", true);
                _main.Audio.Moment("request-asked");
                return true;
            case TownEventKind.BoonChosen:
                BoonMoments++;
                Banner("A boon for good", $"{e.Text}. {Town.BoonText((Boon)e.Index)}");
                _main.Audio.Moment("level-reached");
                return true;
            case TownEventKind.Milestone:
                Milestones++;
                LastMilestone = e.Text ?? "";
                _flourish = 1.2f;
                Note($"Milestone! {LastMilestone}. A little extra on top.", true);
                _main.Audio.Moment("gift-arrives");
                return true;
            case TownEventKind.DriveAsked:
                Banner(Town.DriveName, $"One last push: send {Money(e.Amount)} {GoodName(e.Good)} for the charter. Every load counts.");
                _main.Audio.Moment("request-asked");
                return true;
            case TownEventKind.DriveProgress: return true;
            case TownEventKind.DriveMet:
                Note("The charter drive is met.", true);
                _main.Audio.Moment("request-met");
                return true;
            case TownEventKind.TouchAdded:
                _touchesSeen++;
                // A small note now and then, never a banner.
                if (_touchesSeen % 4 == 1) Note(TouchNote((TouchKind)e.Level));
                return true;
        }
        return false;
    }

    private int _touchesSeen;

    public static string TouchNote(TouchKind k) => k switch
    {
        TouchKind.Woodpile => "Someone has stacked a woodpile.",
        TouchKind.WashingLine => "Washing hangs out to dry.",
        TouchKind.FlowerBed => "A flower bed is planted.",
        TouchKind.HenCoop => "Hens have a new coop.",
        TouchKind.Bench => "A bench to sit on.",
        TouchKind.Beehives => "Bees have come to the hives.",
        TouchKind.Haystack => "A haystack is built.",
        TouchKind.AppleTree => "An apple tree is planted.",
        TouchKind.Handcart => "A handcart stands ready.",
        TouchKind.Signpost => "A signpost points the way.",
        _ => "The town grows a little homelier.",
    };
}
