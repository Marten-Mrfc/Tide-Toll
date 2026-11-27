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
    private Control _drive = null!;
    private Label _driveName = null!, _driveAsk = null!, _driveDone = null!;
    private readonly ColorRect[] _drivePips = new ColorRect[DriveParts];
    private readonly Label[] _boonKey = new Label[2];
    private int _drivePipsLit;
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
    public float DriveBar => _drivePipsLit / (float)DriveParts;
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

    /// <summary>The icon track's boon names (assets-r3-ic, "For the lead" 2); <see cref="BoonIcon"/> is the stand-in.</summary>
    public static string BoonIconNew(Boon b) => b switch
    {
        Boon.WiderLanes => "boon-wider-lanes", Boon.StrongerBacks => "boon-stronger-backs", Boon.DeeperWell => "boon-deeper-well",
        Boon.LongerRows => "boon-longer-rows", Boon.ThirdRoom => "boon-third-room", Boon.FasterHands => "boon-faster-hands",
        Boon.MarketRight => "boon-market-right", Boon.MillersGuild => "boon-millers-guild", _ => "ui-achievement",
    };

    private void BuildChoiceParts()
    {
        // Night 3: the choice of two in the Message zone (card style, at most 200 high): per option the icon, the name
        // and its key (Z or X), what it does, "+n a minute now".
        var z = Zones;
        var rect = z.Message(HudZones.ChoiceMaxH);
        int pad = UiKit.U(_s, 12), gap = UiKit.U(_s, 8);
        _choice = new PanelContainer { Name = "Choice", MouseFilter = Control.MouseFilterEnum.Stop, Visible = false, Position = rect.Position, Size = rect.Size };
        _choice.AddThemeStyleboxOverride("panel", UiKit.CardBox(_s, UiKit.CardKind.Card, 0));
        _root.AddChild(_choice);
        var holder = new Control { Name = "Box", MouseFilter = Control.MouseFilterEnum.Ignore };
        _choice.AddChild(holder);
        _choiceHead = UiKit.Fixed(_s, UiKit.Body, UiKit.Serif, UiKit.Ink, HorizontalAlignment.Center);
        _choiceHead.Name = "Head";
        _choiceHead.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _choiceHead.MaxLinesVisible = 2;
        _choiceHead.Position = new Vector2(pad, UiKit.U(_s, 6));
        _choiceHead.Size = new Vector2(rect.Size.X - 2 * pad, UiKit.U(_s, 46));
        holder.AddChild(_choiceHead);
        float hw = (rect.Size.X - 2 * pad - gap) / 2f, top = UiKit.U(_s, 56), hh = rect.Size.Y - top - pad;
        for (int side = 0; side < 2; side++)
        {
            int k = side;
            var b = new Button { Name = side == 0 ? "BoonA" : "BoonB", Text = "", ClipContents = true, Position = new Vector2(pad + side * (hw + gap), top), Size = new Vector2(hw, hh) };
            UiKit.StyleButton(b, _s, 0);
            _boonBtn[side] = b;
            b.Pressed += () => PickBoon(k);
            b.MouseEntered += () => _main.Audio.UiHover();
            holder.AddChild(b);
            _boonIcon[side] = UiKit.IconRect(_s, null);
            _boonIcon[side].Position = new Vector2(UiKit.U(_s, 8), UiKit.U(_s, 6));
            b.AddChild(_boonIcon[side]);
            _boonName[side] = UiKit.Fixed(_s, UiKit.Body, UiKit.UiBold);
            _boonName[side].Name = "Name";
            _boonName[side].Position = new Vector2(UiKit.U(_s, 38), UiKit.U(_s, 4));
            _boonName[side].Size = new Vector2(hw - UiKit.U(_s, 72), UiKit.U(_s, 26));
            b.AddChild(_boonName[side]);
            _boonKey[side] = UiKit.KeyCap(_s);
            _boonKey[side].Position = new Vector2(hw - UiKit.U(_s, 30), UiKit.U(_s, 7));
            _boonKey[side].Size = new Vector2(UiKit.U(_s, 22), UiKit.U(_s, 20));
            b.AddChild(_boonKey[side]);
            _boonText[side] = UiKit.Fixed(_s, UiKit.Caption);
            _boonText[side].Name = "Text";
            _boonText[side].AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _boonText[side].MaxLinesVisible = 3;
            _boonText[side].VerticalAlignment = VerticalAlignment.Top;
            _boonText[side].Position = new Vector2(UiKit.U(_s, 8), UiKit.U(_s, 32));
            _boonText[side].Size = new Vector2(hw - UiKit.U(_s, 16), hh - UiKit.U(_s, 56));
            b.AddChild(_boonText[side]);
            _boonWorth[side] = UiKit.Fixed(_s, UiKit.Caption, UiKit.UiFont, UiKit.MutedNumber);
            _boonWorth[side].Name = "Worth";
            _boonWorth[side].Position = new Vector2(UiKit.U(_s, 8), hh - UiKit.U(_s, 24));
            _boonWorth[side].Size = new Vector2(hw - UiKit.U(_s, 16), UiKit.U(_s, 20));
            b.AddChild(_boonWorth[side]);
        }
        _choiceSig = int.MinValue;

        // The charter drive: one line in the ledger's strip ("Charter", ten pips, "40 of 100").
        int sp = UiKit.U(_s, 12), sw = (int)_stripBox.Size.X - 2 * sp;
        _drive = new Control { Name = "Drive", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false, Size = _stripBox.Size };
        _stripBox.AddChild(_drive);
        _driveName = UiKit.Fixed(_s, UiKit.Caption, UiKit.UiBold);
        _driveName.Name = "Name";
        _driveName.Position = new Vector2(sp, 0);
        _driveName.Size = new Vector2(UiKit.U(_s, 70), _stripBox.Size.Y);
        _drive.AddChild(_driveName);
        float pip = UiKit.U(_s, 8), pg = UiKit.U(_s, 3), px0 = sp + UiKit.U(_s, 74);
        for (int i = 0; i < DriveParts; i++)
        {
            _drivePips[i] = new ColorRect { Name = "Pip" + i, Color = UiKit.PaperDarker, MouseFilter = Control.MouseFilterEnum.Ignore,
                Position = new Vector2(px0 + i * (pip + pg), (_stripBox.Size.Y - pip) / 2f), Size = new Vector2(pip, pip) };
            _drive.AddChild(_drivePips[i]);
        }
        _driveDone = UiKit.Fixed(_s, UiKit.Caption, UiKit.UiFont, UiKit.MutedNumber, HorizontalAlignment.Right);
        _driveDone.Name = "Count";
        float dx = px0 + DriveParts * (pip + pg) + UiKit.U(_s, 4);
        _driveDone.Position = new Vector2(dx, 0);
        _driveDone.Size = new Vector2(sp + sw - dx, _stripBox.Size.Y);
        _drive.AddChild(_driveDone);
        _driveAsk = new Label { Name = "Ask", Visible = false };
        _drive.AddChild(_driveAsk);
        _driveSig = int.MinValue;

        _handLayer = new Control { Name = "HandMarks", MouseFilter = Control.MouseFilterEnum.Ignore };
        _handLayer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(_handLayer);
        // Under every card and page: markers belong to the map.
        _root.MoveChild(_handLayer, _floatLayer.GetIndex() + 1);
        _handMarks.Clear(); _handA.Clear();
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
        // Night 3, L2: it arrives and leaves like every large message (0.22 s, fade and 12 down from above).
        // One large message at a time: a banner showing leaves first (it waits again), then the choice comes.
        _choiceF.Want(show && (!_banner.Visible || _choiceF.On));
        _choiceF.Step(dt);
        bool vis = _choiceF.Value > 0f;
        if (_choice.Visible != vis) _choice.Visible = vis;
        if (vis) _choice.Modulate = new Color(1, 1, 1, _choiceF.Value);
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
                    _boonName[side].Text = side == 0 ? c.AName : c.BName;
                    _boonKey[side].Text = Settings.GameInput.Bound(side == 0 ? "tt_choose_a" : "tt_choose_b");
                    _boonText[side].Text = side == 0 ? c.AText : c.BText;
                    int w = WorthTenths(side == 0 ? c.AWorth : c.BWorth);
                    _boonWorth[side].Text = w > 0 ? WorthLine(w) + " now" : "";
                    _boonIcon[side].Texture = IconOr(BoonIconNew(b), BoonIcon(b)) ?? IconOr(BoonIcon(b), "ui-achievement");
                }
            }
            // Night 3: in the Message zone (TownHud.Zones.cs); no pulse of its own (the next round's motion).
        }
        DriveFrame();
        HandFrame();
        if (_flourish > 0) _flourish = Math.Max(0, _flourish - dt);
    }

    private void DriveFrame()
    {
        var t = _drv.Town;
        var d = t.Drive;
        // The strip: the Ring in the next age button comes first; the drive takes the line otherwise.
        bool flash = _driveMetFlash > 0f;
        bool show = (d.Active || flash) && !_nextAgeF.On;
        _driveF.Want(show && _fullA > 0.01f);
        _driveF.Step(_msgDt);
        _driveMetFlash = Math.Max(0f, _driveMetFlash - MessageQueue.Clock(_msgDt));
        bool vis = _driveF.Value > 0f;
        if (_drive.Visible != vis) _drive.Visible = vis;
        if (vis) _drive.Modulate = new Color(1, 1, 1, _driveF.Value);
        if (flash && !_driveDone.Text.StartsWith("Done"))
        {
            // Met (message-system section 2): the strip says it in its own place, in moss, for 1.6 s.
            _driveDone.Text = "Done";
            for (int i = 0; i < DriveParts; i++) _drivePips[i].Color = UiKit.Moss;
            _driveSig = 0;
        }
        if (!show || flash) return;
        int sig = HashCode.Combine(d.Good, d.Amount, d.Delivered, (long)d.RenownGiven);
        if (sig != _driveSig)
        {
            _driveSig = sig;
            // Sheet 3: the strip says the short name "Charter"; the whole name is in the ask (tooltip, tests).
            _driveName.Text = "Charter";
            _driveAsk.Text = $"{d.Name}: send {Money(d.Amount)} {GoodName(d.Good)} for the charter";
            float k = d.Amount > 0 ? Math.Clamp((float)d.Delivered / d.Amount, 0f, 1f) : 0f;
            // Fills in parts: whole tenths, so each part lands as a small step ("4 of 10"; the amounts are in the tooltip).
            _drivePipsLit = (int)MathF.Floor(k * DriveParts);
            _driveDone.Text = $"{_drivePipsLit} of {DriveParts}";
            for (int i = 0; i < DriveParts; i++) _drivePips[i].Color = i < _drivePipsLit ? UiKit.RiverBlue : UiKit.PaperDarker;
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
                var l = UiKit.Text("+ hand", _s, UiKit.Caption, UiKit.UiBold, UiKit.Ink);
                l.AddThemeFontSizeOverride("font_size", UiKit.Type(_s, UiKit.Caption));
                var sb = UiKit.CardBox(_s, UiKit.CardKind.Quiet, 4);
                l.AddThemeStyleboxOverride("normal", sb);
                l.MouseFilter = Control.MouseFilterEnum.Ignore;
                _handLayer.AddChild(l);
                _handMarks.Add(l);
                _handA.Add(0f);
            }
            int mi = used++;
            var m = _handMarks[mi];
            // Night 3, L2: a tag arrives and leaves by an eased 0.15 s fade (never in one frame).
            _handA[mi] = Math.Min(1f, _handA[mi] + _msgDt / TipTagSeconds);
            float ha = BookMotion.Smoother(_handA[mi]);
            if (!m.Visible) m.Visible = true;
            m.Modulate = new Color(1, 1, 1, ha);
            if (m.GetMeta(HandMeta, -1).AsInt32() != w) m.SetMeta(HandMeta, w);
            m.ResetSize();
            m.Position = at - new Vector2(m.Size.X / 2f, m.Size.Y + UiKit.Px(_s, 6));
        }
        for (int i = used; i < _handMarks.Count; i++)
        {
            _handA[i] = Math.Max(0f, _handA[i] - _msgDt / TipTagSeconds);
            float ha = BookMotion.Smoother(_handA[i]);
            if (ha <= 0f) { if (_handMarks[i].Visible) _handMarks[i].Visible = false; }
            else _handMarks[i].Modulate = new Color(1, 1, 1, ha);
        }
    }

    /// <summary>Rules v3 events: the choice offered and taken, milestones, the drive, a hand on offer, a touch.</summary>
    private bool ChoiceEvent(in TownEvent e)
    {
        switch (e.Kind)
        {
            case TownEventKind.ChoiceOffered:
                // The choice card is the message (its level is in its heading); its sound plays as it opens.
                _main.Audio.Large("level-reached");
                return true;
            case TownEventKind.BoonChosen:
                BoonMoments++;
                Note($"A boon for good: {e.Text}. {Town.BoonText((Boon)e.Index)}", true, NoteBuilding, "");
                return true;
            case TownEventKind.Milestone:
                Milestones++;
                LastMilestone = e.Text ?? "";
                _flourish = 1.2f;
                Note($"Milestone! {LastMilestone}. A little extra on top.", true, NoteBuilding, "gift-arrives");
                return true;
            case TownEventKind.DriveAsked:
                Large(Town.DriveName, $"One last push: send {Money(e.Amount)} {GoodName(e.Good)} for the charter. Every load counts.", PrioDrive, BannerStay, "request-asked");
                return true;
            case TownEventKind.DriveProgress: return true;
            case TownEventKind.DriveMet:
                // The strip says it (message-system section 2): no note.
                DriveMetFlash();
                _main.Audio.Large("request-met");
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
    private readonly System.Collections.Generic.List<float> _handA = new();
    private readonly MessageFader _choiceF = new(LargeSeconds);
    /// <summary>The choice card's eased opacity (tests).</summary>
    public float ChoiceAlpha => _choiceF.Value;
    private float _driveMetFlash;
    private readonly MessageFader _driveF = new(LargeSeconds);
    private void DriveMetFlash() => _driveMetFlash = 1.6f;

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
