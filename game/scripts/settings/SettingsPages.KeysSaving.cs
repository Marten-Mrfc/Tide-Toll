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
        int half = (GameInput.Actions.Length + 1) / 2;
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
                    CustomMinimumSize = new Vector2(0, 40 * s),
                };
                if (key == Key.None && !waiting) b.AddThemeColorOverride("font_color", UiKit.FadedInk);
                if (waiting) b.AddThemeStyleboxOverride("normal", SettingsLook.Slip(s, UiKit.Pressed, 2.5f));
                b.Pressed += () => StartCapture(a.Name, sl);
                _controls[$"keys.{a.Name}.{sl}"] = b;
                row.AddChild(b);
            }
            Row(col, a.Label, row, s, null, 330);
        }
        var note = UiKit.Text(_capture != null ? "Press the new key. Esc cancels." : "Esc always goes back from these pages.", s, 16, null, UiKit.FadedInk);
        note.Name = "KeysNote";
        note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        right.AddChild(note);
        var restore = PageButton("keys.restore", "Restore defaults", s, () => { CancelCapture(); Store.RestoreDefaultKeys(); Build(); FocusName("keys.restore"); },
            "Put every key back as it was at first");
        restore.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        right.AddChild(restore);
    }

    public void StartCapture(string action, int slot)
    {
        _capture = (action, slot);
        Build();
        FocusName($"keys.{action}.{slot}");
    }

    public void CancelCapture()
    {
        if (_capture == null) return;
        var c = _capture.Value;
        _capture = null;
        if (_frame != null && Section == "Keys") { Build(); FocusName($"keys.{c.action}.{c.slot}"); }
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
        Build();
        FocusName($"keys.{action}.{slot}");
    }

    private void BuildSaving(VBoxContainer left, VBoxContainer right, float s)
    {
        Heading(left, "Writing it down", s);
        var auto = new ChoiceButton(AutosaveNames, Math.Max(0, Array.IndexOf(GameSettings.AutosaveChoices, Store.AutosaveMinutes)), s)
        { TooltipText = "How often the game saves by itself" };
        auto.Chosen += i => Store.SetAutosaveMinutes(GameSettings.AutosaveChoices[i]);
        _controls["saving.autosave"] = auto;
        Row(left, "Autosave", auto, s, null, 250);

        bool have = Saves != null;
        var save = PageButton("saving.save", "Save now", s, () =>
        {
            _savingNote = Saves!.SaveNow() ? "Saved." : "The game could not be saved.";
            Build(); FocusName("saving.save");
        }, "Write the town down now");
        save.Disabled = !have;
        Row(left, LastSaveText(), save, s, null, 220);

        var load = PageButton("saving.load", "Load the last save", s, () =>
        {
            _savingNote = Saves!.LoadLast() ? "The last save is loaded." : "There is no save to load.";
            Build(); FocusName("saving.load");
        }, "Go back to the town as it was last saved");
        load.Disabled = !have || !Saves!.HasSave;
        Row(left, "Go back to the last save", load, s, null, 220);
        if (_savingNote.Length > 0)
        {
            var n = UiKit.Text(_savingNote, s, 17, UiKit.UiBold, UiKit.Ink);
            n.Name = "SavingNote";
            left.AddChild(n);
        }

        Heading(right, "The save file", s);
        var del = PageButton("saving.delete", "Delete the save", s, () => OpenSlip("delete"), "Remove the save from this computer");
        del.Disabled = !have || !Saves!.HasSave;
        Row(right, "Start again from nothing", del, s, "You are asked before anything is removed.", 220);
        var where = UiKit.Text("Where it lives", s, 19);
        right.AddChild(where);
        var path = UiKit.Text(have ? Saves!.FolderPath : "Saving is not ready yet.", s, 16, null, UiKit.FadedInk);
        path.Name = "SavePath";
        path.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
        right.AddChild(path);
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
        Build();
        FocusName("slip.0");
    }

    private void BuildSlip(float s)
    {
        var blocker = new ColorRect { Name = "Blocker", Color = new Color(UiKit.Ink, 0.35f), MouseFilter = MouseFilterEnum.Stop };
        blocker.SetAnchorsPreset(LayoutPreset.FullRect);
        _frame!.AddChild(blocker);
        var card = new PanelContainer { Name = "Slip" };
        var cardBox = UiKit.Card(s, UiKit.Paper, 2f);
        cardBox.SetContentMarginAll(UiKit.Px(s, 26));
        card.AddThemeStyleboxOverride("panel", cardBox);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", UiKit.Px(s, 12));
        card.AddChild(box);
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
        box.AddChild(UiKit.Text(title, s, 30, UiKit.Serif));
        var body = UiKit.Text(text, s, 18);
        body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.CustomMinimumSize = new Vector2(Mathf.Min(460 * s, _rightPage.Size.X - 60 * s), 0);
        box.AddChild(body);
        if (_slip == "keep")
        {
            _countdown = UiKit.Text(KeepText(), s, 17, UiKit.UiBold, UiKit.FadedInk);
            _countdown.Name = "Countdown";
            box.AddChild(_countdown);
        }
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", UiKit.Px(s, 14));
        box.AddChild(row);
        var bYes = PageButton("slip.0", yes, s, SlipYes);
        var bNo = PageButton("slip.1", no, s, SlipCancel);
        bYes.CustomMinimumSize = bNo.CustomMinimumSize = new Vector2(150 * s, 44 * s);
        row.AddChild(bYes);
        row.AddChild(bNo);
        _frame.AddChild(card);
        // Keep the keyboard inside the slip.
        foreach (var (b, o) in new[] { (bYes, bNo), (bNo, bYes) })
        {
            var p = o.GetPath();
            b.FocusNeighborLeft = b.FocusNeighborRight = b.FocusNext = b.FocusPrevious = p;
            b.FocusNeighborTop = b.FocusNeighborBottom = b.GetPath();
        }
        // Over the right page, centred.
        card.ResetSize();
        var size = card.GetCombinedMinimumSize();
        card.Position = (_rightPage.GetCenter() - size / 2).Round();
    }

    private void SlipYes()
    {
        switch (_slip)
        {
            case "keep": KeepDisplay(); break;
            case "delete":
                Saves?.DeleteSave();
                _slip = ""; _savingNote = "The save is deleted.";
                Build(); FocusName("saving.save"); break;
            case "clash":
                _slip = "";
                Store.SwapKey(_clash.action, _clash.slot, _clash.other, _clash.otherSlot);
                // The swap gave this slot the other's key, which is the key just pressed.
                Build(); FocusName($"keys.{_clash.action}.{_clash.slot}"); break;
        }
    }

    private void SlipCancel()
    {
        switch (_slip)
        {
            case "keep": RevertDisplay(); break;
            case "delete": _slip = ""; Build(); FocusName("saving.delete"); break;
            case "clash": _slip = ""; Build(); FocusName($"keys.{_clash.action}.{_clash.slot}"); break;
        }
    }
}
