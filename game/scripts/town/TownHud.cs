using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// The town's interface in the book's style (UiKit: paper cards with an ink edge, Atkinson Hyperlegible, Alegreya for
/// headings): the ledger card (coin, coin a minute, the level bar with renown and the next gift), margin notes for
/// events, a floating "+N" at every delivery, and (TownHud.Shop.cs) the upgrade bookmarks, the building offers, the
/// Pave mode and the bell. Every control shows its key. Built for the window's height (UiKit.ScaleFor), so it reads
/// at 1280 x 720 and 1920 x 1080; rebuilt when the window changes size.
/// </summary>
public partial class TownHud : CanvasLayer
{
    public const int MaxNotes = 5, MaxFloaters = 32;
    public const float NoteSeconds = 7f, FloatSeconds = 1.6f;

    private Main _main = null!;
    private TownDriver _drv = null!;
    private float _s = 1f;
    private Vector2 _built;
    private Control _root = null!;
    private Label _coin = null!, _rate = null!, _level = null!, _renown = null!, _next = null!;
    private ColorRect _barFill = null!;
    private Control _barBack = null!;
    private VBoxContainer _notes = null!;
    private readonly List<(Label label, float age)> _noteList = new();
    private Control _floatLayer = null!;
    private readonly Label[] _floaters = new Label[MaxFloaters];
    private readonly Vector3[] _floatAt = new Vector3[MaxFloaters];
    private readonly float[] _floatAge = new float[MaxFloaters];
    private int _floatNext;

    /// <summary>Notes shown so far (tests), the text of the newest, floaters started.</summary>
    public int NotesShown { get; private set; }
    public string LastNote { get; private set; } = "";
    public readonly List<string> NoteLog = new();
    public int FloatersStarted { get; private set; }
    public string CoinText => _coin.Text;
    public string NextText => _next.Text;
    public float Scale => _s;

    public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    public static string Money(double c) => Math.Floor(c).ToString("N0", Inv);

    public void Init(Main main)
    {
        _main = main;
        _drv = main.Driver;
        Name = "Hud";
        Layer = 5;
        Build();
    }

    public static Texture2D? IconOr(string name, string standIn)
    {
        foreach (var n in new[] { name, standIn })
        {
            string p = $"res://assets/icons/{n}.svg";
            if (ResourceLoader.Exists(p)) return ModelLib.MipTexture(p);
        }
        return null;
    }

    private void Build()
    {
        foreach (var c in GetChildren()) { RemoveChild(c); c.QueueFree(); }
        var vp = GetViewport().GetVisibleRect().Size;
        _built = vp;
        _s = UiKit.ScaleFor(vp.Y);
        _root = new Control { Name = "Root", MouseFilter = Control.MouseFilterEnum.Ignore, Theme = UiKit.MakeTheme(_s) };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        _floatLayer = new Control { Name = "Floaters", MouseFilter = Control.MouseFilterEnum.Ignore };
        _floatLayer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(_floatLayer);
        for (int i = 0; i < MaxFloaters; i++)
        {
            var l = UiKit.Text("", _s, 20, UiKit.UiBold, UiKit.Ink);
            l.AddThemeColorOverride("font_outline_color", UiKit.Paper);
            l.AddThemeConstantOverride("outline_size", UiKit.Px(_s, 5));
            l.Visible = false;
            _floatLayer.AddChild(l);
            _floaters[i] = l;
        }

        BuildLedger();
        _notes = new VBoxContainer { Name = "Notes", MouseFilter = Control.MouseFilterEnum.Ignore };
        _notes.AddThemeConstantOverride("separation", UiKit.Px(_s, 6));
        _notes.Position = new Vector2(UiKit.Px(_s, 16), UiKit.Px(_s, 200));
        _notes.Size = new Vector2(UiKit.Px(_s, 330), 0);
        _root.AddChild(_notes);
        _noteList.Clear();
        BuildShop();
        BuildPave();
        Refresh();
    }

    private void BuildLedger()
    {
        var card = new PanelContainer { Name = "Ledger", MouseFilter = Control.MouseFilterEnum.Ignore };
        var box = UiKit.Card(_s, new Color(UiKit.Paper, 0.96f), 1.5f);
        box.SetContentMarginAll(UiKit.Px(_s, 10));
        card.AddThemeStyleboxOverride("panel", box);
        card.Position = new Vector2(UiKit.Px(_s, 16), UiKit.Px(_s, 14));
        card.CustomMinimumSize = new Vector2(UiKit.Px(_s, 330), 0);
        _root.AddChild(card);
        var col = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", UiKit.Px(_s, 3));
        card.AddChild(col);
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", UiKit.Px(_s, 8));
        col.AddChild(row);
        var coinIcon = IconOr("ui-coin", "ui-coin");
        if (coinIcon != null)
            row.AddChild(new TextureRect
            {
                Texture = coinIcon, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = Vector2.One * UiKit.Px(_s, 30), TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });
        _coin = UiKit.Text("0 coin", _s, 26, UiKit.UiBold);
        row.AddChild(_coin);
        _rate = UiKit.Text("", _s, 16, UiKit.UiFont, UiKit.FadedInk);
        _rate.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
        row.AddChild(_rate);
        col.AddChild(UiKit.Rule(_s, 0.5f));
        _level = UiKit.Text("Level 1", _s, 19, UiKit.Serif);
        _level.AddThemeConstantOverride("line_spacing", 0);
        col.AddChild(_level);
        _barBack = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(0, UiKit.Px(_s, 12)) };
        var back = UiKit.Card(_s, UiKit.PaperDark, 1f);
        back.SetContentMarginAll(0);
        _barBack.AddThemeStyleboxOverride("panel", back);
        col.AddChild(_barBack);
        _barFill = new ColorRect { Color = Palette.Thatch, MouseFilter = Control.MouseFilterEnum.Ignore };
        _barBack.AddChild(_barFill);
        _renown = UiKit.Text("", _s, 15, UiKit.UiFont, UiKit.FadedInk);
        col.AddChild(_renown);
        _next = UiKit.Text("", _s, 17, UiKit.UiFont);
        _next.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _next.CustomMinimumSize = new Vector2(UiKit.Px(_s, 310), 0);
        col.AddChild(_next);
    }

    public override void _Process(double delta)
    {
        var vp = GetViewport().GetVisibleRect().Size;
        if (vp != _built && vp.Y > 1) Build();
        foreach (var e in _drv.Events) OnEvent(e);
        Refresh();
        StepNotes((float)delta);
        StepFloaters((float)delta);
        ShopFrame((float)delta);
        PaveFrame();
    }

    private void Refresh()
    {
        var t = _drv.Town;
        _coin.Text = Money(t.Coin) + " coin";
        _rate.Text = Money(t.CoinPerMinute) + " a minute";
        _level.Text = t.Level >= t.MaxLevel ? $"Level {t.Level}, the last" : $"Level {t.Level} of {t.MaxLevel}";
        float bar = (float)Math.Clamp(t.LevelBar, 0, 1);
        var sz = _barBack.Size;
        _barFill.Position = new Vector2(1, 1);
        _barFill.Size = new Vector2(Math.Max(0, (sz.X - 2) * bar), Math.Max(0, sz.Y - 2));
        _renown.Text = t.Level >= t.MaxLevel ? $"Renown {Money(t.Renown)}" : $"Renown {Money(t.Renown)} of {Money(t.NextLevelRenown)}";
        _next.Text = t.NextGift;
    }

    // ------------------------------------------------------------------ notes

    /// <summary>A margin note: a slip of paper on the left that fades after a few seconds.</summary>
    public void Note(string text, bool strong = false)
    {
        var l = UiKit.Text(text, _s, strong ? 18 : 16, strong ? UiKit.UiBold : UiKit.UiFont);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.CustomMinimumSize = new Vector2(UiKit.Px(_s, 320), 0);
        var slip = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        var box = UiKit.Card(_s, new Color(strong ? UiKit.Pressed : UiKit.Paper, 0.95f), strong ? 2f : 1f);
        box.SetContentMarginAll(UiKit.Px(_s, 7));
        slip.AddThemeStyleboxOverride("panel", box);
        slip.AddChild(l);
        _notes.AddChild(slip);
        _notes.MoveChild(slip, 0);
        _noteList.Add((l, 0f));
        while (_notes.GetChildCount() > MaxNotes)
        {
            var old = _notes.GetChild(_notes.GetChildCount() - 1);
            _notes.RemoveChild(old);
            old.QueueFree();
        }
        NotesShown++;
        LastNote = text;
        NoteLog.Add(text);
        if (NoteLog.Count > 200) NoteLog.RemoveAt(0);
    }

    private void StepNotes(float dt)
    {
        _notes.Position = new Vector2(UiKit.Px(_s, 16), (_root.GetNodeOrNull<Control>("Ledger")?.GetRect().End.Y ?? 180) + UiKit.Px(_s, 10));
        for (int i = _noteList.Count - 1; i >= 0; i--)
        {
            var (l, age) = _noteList[i];
            age += dt;
            if (!IsInstanceValid(l) || age > NoteSeconds)
            {
                if (IsInstanceValid(l) && l.GetParent() is Control slip && IsInstanceValid(slip)) { slip.GetParent()?.RemoveChild(slip); slip.QueueFree(); }
                _noteList.RemoveAt(i);
                continue;
            }
            _noteList[i] = (l, age);
            if (l.GetParent() is Control p) p.Modulate = new Color(1, 1, 1, Math.Clamp((NoteSeconds - age) / 1.2f, 0f, 1f));
        }
    }

    private void OnEvent(in TownEvent e)
    {
        var t = _drv.Town;
        switch (e.Kind)
        {
            case TownEventKind.Delivery: Float(e.X, e.Y, e.Coin); break;
            case TownEventKind.LevelReached:
                if (e.Level > 1) Note($"Level {e.Level}! The town gives {Lower(e.Text)}.", true);
                break;
            case TownEventKind.BuildingOpened: Note((e.Text ?? "A new building") + " is open.", true); break;
            case TownEventKind.OfferAvailable: Note("New to buy: " + (e.Text ?? "a building") + "."); break;
            case TownEventKind.RoadWornIn: Note("A road has worn in. It can be paved now."); break;
            case TownEventKind.RoadPaved: Note(e.Surface == Surface.Cobbles ? "A road is cobbled." : "A road is laid with gravel."); break;
            case TownEventKind.PerkEarned: Note("Perk: " + (e.Text ?? "") + ".", true); break;
            case TownEventKind.AchievementUnlocked: Note("Achievement: " + (e.Text ?? "") + "."); break;
            case TownEventKind.VillagerArrived: if (t.VillagerCount > 1) Note("A new villager moves in."); break;
            case TownEventKind.Charter: Note("The town has its charter. Ring in the next age when you are ready.", true); break;
            case TownEventKind.NextAge: Note("The next age is coming. It is not built yet; the town keeps going.", true); break;
        }
    }

    private static string Lower(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "something new";
        return char.ToLowerInvariant(s[0]) + s.Substring(1);
    }

    // ------------------------------------------------------------------ floating coin

    private void Float(double x, double y, double coin)
    {
        int i = _floatNext;
        _floatNext = (_floatNext + 1) % MaxFloaters;
        _floatAt[i] = new Vector3((float)x * TownGround.Cell, 3f, (float)y * TownGround.Cell);
        _floatAge[i] = 0.0001f;
        _floaters[i].Text = "+" + Money(Math.Max(1, coin));
        _floaters[i].Visible = true;
        FloatersStarted++;
    }

    private void StepFloaters(float dt)
    {
        for (int i = 0; i < MaxFloaters; i++)
        {
            if (_floatAge[i] <= 0) continue;
            _floatAge[i] += dt;
            var l = _floaters[i];
            if (_floatAge[i] > FloatSeconds) { _floatAge[i] = 0; l.Visible = false; continue; }
            var at = _main.Rig.LocalToScreen(_floatAt[i]);
            float k = _floatAge[i] / FloatSeconds;
            l.Position = at - new Vector2(l.Size.X / 2f, UiKit.Px(_s, 24) + k * UiKit.Px(_s, 36));
            l.Modulate = new Color(1, 1, 1, Math.Clamp((1f - k) * 2f, 0f, 1f));
        }
    }
}
