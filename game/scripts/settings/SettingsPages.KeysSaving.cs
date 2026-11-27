using System;
using Godot;

namespace TideToll.Game.Settings;

/// <summary>The Keys and Saving sections, and the slips (small dialogs): keep the display, delete the save, a key clash.</summary>
public partial class SettingsPages
{
    private static readonly string[] AutosaveNames = { "Off", "Every minute", "Every 2 minutes", "Every 5 minutes" };

    private (string action, int slot)? _capture;
    private (string action, int slot, Key key, string other, int otherSlot) _clash;
    private string _savingNote = "";

    /// <summary>The action and slot waiting for a new key, if any.</summary>
    public (string action, int slot)? Capturing => _capture;

    private void BuildKeys(VBoxContainer left, VBoxContainer right, float s)
    {
        // Round 4: rows closer together and the left column one row longer than the right (which also holds the note
        // and Restore defaults), so the 25 actions fit at 100 percent without scrolling; where a column must still
        // scroll, its window shows whole rows only (FitWholeRows). Round 5: 27 actions (the two boon keys), so rows of
        // 36 px 5 px apart: 14 rows take less height than round 4's 13 rows of 40 px 6 px apart.
        int half = (GameInput.Actions.Length + 1) / 2;
        float rowPx = 36;
        int sep = UiKit.Px(s, 5);
        left.AddThemeConstantOverride("separation", sep);
        right.AddThemeConstantOverride("separation", sep);
        for (int i = 0; i < GameInput.Actions.Length; i++)
        {
            var a = GameInput.Actions[i];
            var col = i < half ? left : right;
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", UiKit.Px(s, 8));
            for (int slot = 0; slot < GameInput.Slots; slot++)
            {
                int sl = slot;
                bool waiting = _capture is { } c && c.action == a.Name && c.slot == sl;
                var key = Store.KeyFor(a.Name, sl);
                var b = new Button
                {
                    Text = waiting ? "Press a key" : key == Key.None ? "None" : GameInput.KeyName(key),
                    FocusMode = FocusModeEnum.All, SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true,
                    TooltipText = $"{a.Label}: choose, then press the new key. Esc cancels.",
                    CustomMinimumSize = new Vector2(0, rowPx * s),
                };
                if (key == Key.None && !waiting) b.AddThemeColorOverride("font_color", UiKit.FadedInk);
                if (waiting) b.AddThemeStyleboxOverride("normal", SettingsLook.Slip(s, UiKit.Pressed, 2.5f));
                b.Pressed += () => StartCapture(a.Name, sl);
                Target[$"keys.{a.Name}.{sl}"] = b;
                row.AddChild(b);
            }
            Row(col, a.Label, row, s, null, 330);
            row.CustomMinimumSize = new Vector2(row.CustomMinimumSize.X, rowPx * s);
        }
        var note = UiKit.Text(_capture != null ? "Press the new key. Esc cancels." : "Esc always goes back from these pages.", s, 16, null, UiKit.FadedInk);
        note.Name = "KeysNote";
        note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        right.AddChild(note);
        var restore = PageButton("keys.restore", "Restore defaults", s, () => { CancelCapture(); Store.RestoreDefaultKeys(); Refresh(KeysAt); FocusName("keys.restore"); },
            "Put every key back as it was at first");
        restore.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        right.AddChild(restore);
        FitWholeRows(left, rowPx * s + sep, sep);
        FitWholeRows(right, rowPx * s + sep, sep);
    }

    /// <summary>
    /// Once the column's scroll window has its size: if the rows do not all fit, the window is cut to a whole number
    /// of rows (the space under it stays empty), so no row is cut in half at the bottom.
    /// </summary>
    private static void FitWholeRows(VBoxContainer col, float pitch, int sep)
    {
        if (col.GetParent()?.GetParent() is not ScrollContainer scroll || scroll.GetParent() is not VBoxContainer box) return;
        bool done = false, queued = false;
        void Fit()
        {
            queued = false;
            if (done || !IsInstanceValid(scroll) || !scroll.IsInsideTree() || scroll.Size.Y < 1) return;
            float pad = scroll.GetChild<MarginContainer>(0).GetThemeConstant("margin_top") * 2f;
            if (col.Size.Y + pad <= scroll.Size.Y + 0.5f) return; // everything fits: no scrolling
            done = true;
            // Whole rows by their laid-out heights (a row stacks its name over its keys in a narrow column).
            float h = 0;
            foreach (var n in col.GetChildren())
            {
                if (n is not Control c || !c.Visible) continue;
                float end = c.Position.Y + c.Size.Y;
                if (end + pad > scroll.Size.Y) break;
                h = end;
            }
            h = Math.Max(h, Math.Min(pitch, scroll.Size.Y - pad)) + pad;
            int at = scroll.GetIndex();
            scroll.SizeFlagsVertical = SizeFlags.ShrinkBegin;
            scroll.CustomMinimumSize = new Vector2(0, h);
            var spacer = new Control { Name = "UnderRows", SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
            box.AddChild(spacer);
            box.MoveChild(spacer, at + 1);
        }
        // Measured a frame after the column is laid out (wrapped labels know their height only then).
        void Queue()
        {
            if (done || queued || !scroll.IsInsideTree()) return;
            queued = true;
            scroll.GetTree().Connect(SceneTree.SignalName.ProcessFrame, Callable.From(Fit), (uint)ConnectFlags.OneShot);
        }
        scroll.Resized += Queue;
        col.Resized += Queue;
    }

    public void StartCapture(string action, int slot)
    {
        _capture = (action, slot);
        Refresh(KeysAt);
        FocusName($"keys.{action}.{slot}");
    }

    public void CancelCapture()
    {
        if (_capture == null) return;
        var c = _capture.Value;
        _capture = null;
        if (_frame != null) { Refresh(KeysAt); FocusName($"keys.{c.action}.{c.slot}"); }
    }

    private void Captured(InputEventKey e)
    {
        var (action, slot) = _capture!.Value;
        if (e.IsAction("ui_cancel", true) || GameInput.KeyOf(e) == Key.Escape) { CancelCapture(); return; }
        var key = GameInput.KeyOf(e);
        _capture = null;
        var other = Store.FindKey(key, action, slot);
        if (other is { } o && !(o.action == action))
        {
            _clash = (action, slot, key, o.action, o.slot);
            OpenSlip("clash");
            return;
        }
        if (other is { } same)   // the same action's other slot: swap within the action
            Store.SwapKey(action, slot, same.action, same.slot);
        else
            Store.SetKey(action, slot, key);
        Refresh(KeysAt);
        FocusName($"keys.{action}.{slot}");
    }

    private void BuildSaving(VBoxContainer left, VBoxContainer right, float s)
    {
        Heading(left, "Writing it down", s);
        var auto = new ChoiceButton(AutosaveNames, Math.Max(0, Array.IndexOf(GameSettings.AutosaveChoices, Store.AutosaveMinutes)), s)
        { TooltipText = "How often the game saves by itself" };
        auto.Chosen += i => Store.SetAutosaveMinutes(GameSettings.AutosaveChoices[i]);
        Target["saving.autosave"] = auto;
        Row(left, "Autosave", auto, s, null, 250);

        bool have = Saves != null;
        var save = PageButton("saving.save", "Save now", s, () =>
        {
            _savingNote = Saves!.SaveNow() ? "Saved." : "The game could not be saved.";
            Refresh(SavingAt); FocusName("saving.save");
        }, "Write the town down now");
        save.Disabled = !have;
        var saveRow = Row(left, LastSaveText(), save, s, null, 220);
        _lastSaveLabel = saveRow.GetChild(0).GetChild<Label>(0);

        var load = PageButton("saving.load", "Load the last save", s, () =>
        {
            _savingNote = Saves!.LoadLast() ? "The last save is loaded." : "There is no save to load.";
            Refresh(SavingAt); FocusName("saving.load");
        }, "Go back to the town as it was last saved");
        load.Disabled = !have || !Saves!.HasSave;
        _loadButton = load;
        Row(left, "Go back to the last save", load, s, null, 220);
        // Always made, shown only with a note in it (the page is kept between visits: RefreshSaving updates it).
        var n = UiKit.Text(_savingNote, s, 17, UiKit.UiBold, UiKit.Ink);
        n.Name = "SavingNote";
        n.Visible = _savingNote.Length > 0;
        left.AddChild(n);
        _savingNoteLabel = n;

        Heading(right, "The save file", s);
        var del = PageButton("saving.delete", "Delete the save", s, () => OpenSlip("delete"), "Remove the save from this computer");
        del.Disabled = !have || !Saves!.HasSave;
        _deleteButton = del;
        Row(right, "Start again from nothing", del, s, "You are asked before anything is removed.", 220);
        var where = UiKit.Text("Where it lives", s, 19);
        right.AddChild(where);
        var path = UiKit.Text(have ? Saves!.FolderPath : "Saving is not ready yet.", s, 16, null, UiKit.FadedInk);
        path.Name = "SavePath";
        path.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
        right.AddChild(path);
    }

    private Label? _lastSaveLabel, _savingNoteLabel;
    private Button? _loadButton, _deleteButton;

    /// <summary>
    /// The Saving page as the disk is now, without building it again (the pages are kept while put away, and the save
    /// changes meanwhile): the last save's time, Load and Delete offered only with a save, the note.
    /// </summary>
    private void RefreshSaving()
    {
        if (_lastSaveLabel == null || !IsInstanceValid(_lastSaveLabel)) return;
        bool have = Saves != null, any = have && Saves!.HasSave;
        string last = LastSaveText();
        if (_lastSaveLabel.Text != last) _lastSaveLabel.Text = last;
        if (_loadButton != null && IsInstanceValid(_loadButton)) _loadButton.Disabled = !any;
        if (_deleteButton != null && IsInstanceValid(_deleteButton)) _deleteButton.Disabled = !any;
        if (_savingNoteLabel != null && IsInstanceValid(_savingNoteLabel))
        {
            if (_savingNoteLabel.Text != _savingNote) _savingNoteLabel.Text = _savingNote;
            _savingNoteLabel.Visible = _savingNote.Length > 0;
        }
    }

    private string LastSaveText()
    {
        var t = Saves?.LastSaveTime;
        return t == null ? "Not saved yet" : $"Last saved at {t.Value:HH:mm}, {t.Value:d MMM}";
    }

    // ---------------------------------------------------------------- slips

    private void OpenSlip(string kind)
    {
        _slip = kind;
        Refresh();
        FocusName("slip.0");
    }

    private void BuildSlip(float s)
    {
        var blocker = new ColorRect { Name = "Blocker", Color = new Color(UiKit.Ink, 0.35f), MouseFilter = MouseFilterEnum.Stop };
        blocker.SetAnchorsPreset(LayoutPreset.FullRect);
        _frame!.AddChild(blocker);
        _slipBlocker = blocker;
        string title, text, yes, no;
        switch (_slip)
        {
            case "keep":
                title = "Keep this?"; text = "The window has changed. Keep it like this?"; yes = "Keep"; no = "Go back"; break;
            case "delete":
                title = "Delete the save?"; text = "The town on disk is removed. This cannot be undone."; yes = "Delete"; no = "Keep the save"; break;
            default:
                var other = GameInput.Actions[GameInput.IndexOf(_clash.other)].Label;
                var oldKey = Store.KeyFor(_clash.action, _clash.slot);
                title = "That key is taken";
                text = oldKey == Godot.Key.None
                    ? $"{GameInput.KeyName(_clash.key)} is already used for {other}. Move it here, so {other} has no key there?"
                    : $"{GameInput.KeyName(_clash.key)} is already used for {other}. Swap the keys, so {other} gets {GameInput.KeyName(oldKey)} instead?";
                yes = "Swap keys"; no = "Cancel"; break;
        }
        // Round L3: the one modal card (ModalCard), centred on the window, never over the rows it is about.
        var card = ModalCard.Build(s, title, text, out var body, out var row, "Slip");
        if (_slip == "keep")
        {
            _countdown = UiKit.Text(KeepText(), s, 17, UiKit.UiBold, UiKit.FadedInk);
            _countdown.Name = "Countdown";
            body.GetParent().AddChild(_countdown);
            body.GetParent().MoveChild(_countdown, body.GetIndex() + 1);
        }
        var bYes = PageButton("slip.0", yes, s, SlipYes);
        var bNo = PageButton("slip.1", no, s, SlipCancel);
        row.AddChild(ModalCard.Button(bYes, s, primary: false));
        row.AddChild(ModalCard.Button(bNo, s, primary: true));
        _frame.AddChild(card);
        _slipCard = card;
        // Keep the keyboard inside the slip.
        foreach (var (b, o) in new[] { (bYes, bNo), (bNo, bYes) })
        {
            var p = o.GetPath();
            b.FocusNeighborLeft = b.FocusNeighborRight = b.FocusNext = b.FocusPrevious = p;
            b.FocusNeighborTop = b.FocusNeighborBottom = b.GetPath();
        }
        // Centred on the window (the canvas).
        ModalCard.Centre(card, _canvas);
        // Round 6: the clash slip keeps clear of the rows it names (it lay over them); placed again once the rows have
        // their places (the page is laid out after this call).
        if (_slip == "clash")
        {
            PlaceClashSlip(card);
            GetTree().CreateTimer(0.02, processAlways: true).Timeout += () => { if (IsInstanceValid(card) && card.IsInsideTree()) PlaceClashSlip(card); };
        }
    }

    /// <summary>The rows (whole page width) of the two actions a clash names, in the frame's units (tests).</summary>
    public System.Collections.Generic.List<Rect2> ClashRows()
    {
        var rows = new System.Collections.Generic.List<Rect2>();
        if (_frame == null) return rows;
        var off = _frame.GetGlobalRect().Position;
        float pw = _rightPage.Size.X;
        var leftPage = new Rect2(_rightPage.Position.X - pw, _rightPage.Position.Y, pw, _rightPage.Size.Y);
        foreach (var a in new[] { _clash.action, _clash.other })
            for (int sl = 0; sl < 2; sl++)
                if (_controls.TryGetValue($"keys.{a}.{sl}", out var b) && b.IsInsideTree())
                {
                    var r = b.GetGlobalRect(); r.Position -= off;
                    var page = r.GetCenter().X >= _rightPage.Position.X ? _rightPage : leftPage;
                    rows.Add(new Rect2(page.Position.X, r.Position.Y - 4, page.Size.X, r.Size.Y + 8));
                }
        return rows;
    }

    /// <summary>The slip's rectangle in the frame's units while it shows (tests).</summary>
    public Rect2 SlipRect => _frame?.GetNodeOrNull<Control>("Slip") is { } c ? c.GetRect() : new Rect2();

    private void PlaceClashSlip(Control card)
    {
        var size = card.GetCombinedMinimumSize();
        var rows = ClashRows();
        // Round L3: nor over the page's own buttons (it lay on "Restore defaults").
        var off = _frame!.GetGlobalRect().Position;
        foreach (var key in new[] { "keys.restore", "back" })
            if (_controls.TryGetValue(key, out var ctl) && ctl.IsInsideTree()) { var r0 = ctl.GetGlobalRect(); r0.Position -= off; rows.Add(r0.Grow(8)); }
        // Round L3 review: one rule at every window size. The card stands centred on the window, as every modal card
        // does; only when that would lie on a row it names (or on the page's own buttons) does it move straight up or
        // down from the centre, to the nearest place that is clear and inside the window.
        var c0 = (_canvas - size) / 2f;
        float step = 8f, reach = Math.Max(c0.Y, 0f);
        for (float d = 0; d <= reach + step; d += step)
            foreach (float dy in d == 0 ? new[] { 0f } : new[] { -d, d })
            {
                var at = new Vector2(c0.X, Math.Clamp(c0.Y + dy, 0f, Math.Max(0f, _canvas.Y - size.Y)));
                var r = new Rect2(at, size);
                bool clear = true;
                foreach (var row in rows) if (row.Intersects(r)) { clear = false; break; }
                if (clear) { card.Position = at.Round(); return; }
            }
        card.Position = c0.Round();
    }

    private void SlipYes()
    {
        switch (_slip)
        {
            case "keep": KeepDisplay(); break;
            case "delete":
                Saves?.DeleteSave();
                _slip = ""; _savingNote = "The save is deleted.";
                Refresh(SavingAt); FocusName("saving.save"); break;
            case "clash":
                _slip = "";
                Store.SwapKey(_clash.action, _clash.slot, _clash.other, _clash.otherSlot);
                // The swap gave this slot the other's key, which is the key just pressed.
                Refresh(KeysAt); FocusName($"keys.{_clash.action}.{_clash.slot}"); break;
        }
    }

    private void SlipCancel()
    {
        switch (_slip)
        {
            case "keep": RevertDisplay(); break;
            case "delete": _slip = ""; Refresh(SavingAt); FocusName("saving.delete"); break;
            case "clash": _slip = ""; Refresh(KeysAt); FocusName($"keys.{_clash.action}.{_clash.slot}"); break;
        }
    }
}
