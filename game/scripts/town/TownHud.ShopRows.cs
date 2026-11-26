using System;
using System.Collections.Generic;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Night 3 (layout sheet v2, section 3): the shop card on the right, as tall as its rows. A heading line ("Shop", and
/// "3 to buy now"), then the groups in the simulation's order with what is folded away on each heading line (finished
/// and locked purchases are not rows). Rows are 56 units when everything fits, else all 40 (one line), else the card
/// scrolls (the wheel scrolls it only then). A row fades in or out over 0.2 s and the rows below slide over 0.2 s, but
/// not while the pointer is over the shop (until it leaves, or 1.5 s), and a row that moved in the last 0.3 s takes no
/// click. A row that has just become affordable pulses once, one at a time, at most two a second.
/// </summary>
public partial class TownHud
{
    public const float RowFade = 0.2f, RowSlide = 0.2f, RowHold = 1.5f, RowClickCalm = 0.3f, PulseSeconds = 0.4f, PulseGap = 0.5f;
    private Panel _shop = null!;
    private Control _shopBody = null!;
    private Label _shopTitle = null!, _shopAfford = null!;
    private readonly List<ShopRow> _rows = new();       // every row node, leaving ones included
    private readonly List<ShopRow> _rowOrder = new();   // the rows shown, in order (parallel to _shown and _marks)
    private readonly Control[] _groupHead = new Control[5];
    private readonly Label[] _groupName = new Label[5], _groupFold = new Label[5];
    private readonly int[] _groupFoldSig = new int[5];
    private readonly List<ShopItem> _groupDone = new();
    private float _scroll, _holdAge = -1f, _pulseGap;
    private bool _compactShop, _shopScrolls;
    private readonly System.Collections.Generic.List<float> _shopBounds = new();
    private float _shopVisEnd = float.MaxValue;
    private int _affordSig = int.MinValue;
    private readonly Queue<int> _pulseQueue = new();

    private float PillUnits => Zones.RailUnits >= 320 ? 96 : 88;

    private void BuildShopCard()
    {
        _shop = new Panel { Name = "Shop", MouseFilter = Control.MouseFilterEnum.Stop, ClipContents = true };
        _shop.AddThemeStyleboxOverride("panel", UiKit.CardBox(_s, UiKit.CardKind.Card, 0));
        _shop.GuiInput += ShopGui;
        _root.AddChild(_shop);
        float w = Zones.Rail, pad = UiKit.U(_s, 8);
        _shopTitle = UiKit.Fixed(_s, UiKit.Title, UiKit.Serif);
        _shopTitle.Name = "Title";
        _shopTitle.Text = "Shop";
        _shopTitle.Position = new Vector2(UiKit.U(_s, 14), pad);
        _shopTitle.Size = new Vector2(w / 2f, UiKit.U(_s, 32));
        _shop.AddChild(_shopTitle);
        _shopAfford = UiKit.Fixed(_s, UiKit.Caption, UiKit.UiFont, UiKit.MutedNumber, HorizontalAlignment.Right);
        _shopAfford.Name = "ToBuy";
        _shopAfford.Position = new Vector2(w / 2f - UiKit.U(_s, 14), pad);
        _shopAfford.Size = new Vector2(w / 2f, UiKit.U(_s, 32));
        _shop.AddChild(_shopAfford);
        _shopBody = new Control { Name = "Body", MouseFilter = Control.MouseFilterEnum.Ignore, Position = new Vector2(0, pad + UiKit.U(_s, 36)) };
        _shop.AddChild(_shopBody);
        for (int g = 0; g < 5; g++)
        {
            var head = new Control { Name = "Group" + GroupNames[g].Replace(" ", ""), MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
            head.Size = new Vector2(w - 2 * pad, UiKit.U(_s, 28));
            _groupName[g] = UiKit.Fixed(_s, UiKit.Body, UiKit.Serif);
            _groupName[g].Name = "Name";
            _groupName[g].Text = GroupNames[g];
            _groupName[g].Position = new Vector2(UiKit.U(_s, 6), 0);
            _groupName[g].Size = new Vector2(UiKit.U(_s, 90), head.Size.Y);
            _groupFold[g] = UiKit.Fixed(_s, UiKit.Caption, UiKit.UiFont, UiKit.Muted, HorizontalAlignment.Right);
            _groupFold[g].Name = "Folded";
            _groupFold[g].Position = new Vector2(UiKit.U(_s, 96), 0);
            _groupFold[g].Size = new Vector2(head.Size.X - UiKit.U(_s, 102), head.Size.Y);
            head.AddChild(_groupName[g]);
            head.AddChild(_groupFold[g]);
            _shopBody.AddChild(head);
            _groupHead[g] = head;
            _groupFoldSig[g] = int.MinValue;
        }
        _rows.Clear(); _rowOrder.Clear(); _shown.Clear(); _marks.Clear(); _pulseQueue.Clear();
        _scroll = 0; _holdAge = -1f; _compactShop = false; _shopScrolls = false; _affordSig = int.MinValue;
    }

    /// <summary>A row's identity: its kind, and for offers, goods and hands which one (a track's step is not part of it,
    /// so buying a step keeps the row in place).</summary>
    private static int RowId(in ShopItem it) => (int)it.Kind * 1000
        + (it.Kind is ShopItemKind.Building or ShopItemKind.Value or ShopItemKind.RiverValue or ShopItemKind.Hand ? it.Index + 1 : 0);

    /// <summary>A row the player sees: not locked and not finished (at the last level a "later level" step is finished).</summary>
    private bool IsRowItem(in ShopItem it, Town t)
    {
        bool lastLevel = it.Reason == "Unlocks at a later level" && t.Level >= t.MaxLevel;
        return !(IsLocked(it) || IsFinished(it) || lastLevel);
    }

    private readonly List<ShopItem> _rowWant = new();

    /// <summary>Brings the rows to the wanted set: new rows wait (unseen) while the pointer holds the layout, leaving rows fade.</summary>
    private void SyncRows(Town t, bool hold)
    {
        _rowWant.Clear();
        foreach (var it in _items) if (IsRowItem(it, t)) _rowWant.Add(it);
        bool same = _rowWant.Count == _rowOrder.Count;
        for (int i = 0; same && i < _rowWant.Count; i++) same = RowId(_rowWant[i]) == _rowOrder[i].Id;
        if (same) { for (int i = 0; i < _rowWant.Count; i++) { _rowOrder[i].Item = _rowWant[i]; _shown[i] = _rowWant[i]; } return; }
        foreach (var r in _rowOrder) r.Leaving = true;
        _rowOrder.Clear(); _shown.Clear(); _marks.Clear();
        foreach (var it in _rowWant)
        {
            int id = RowId(it);
            ShopRow? row = null;
            foreach (var r in _rows) if (r.Id == id && r.Leaving) { row = r; break; }
            if (row == null)
            {
                row = new ShopRow(_s) { Id = id, Alpha = 0f, Waiting = hold };
                row.Button.Name = "Row" + id;
                var made = row;
                row.Button.Pressed += () => RowPressed(made);
                row.Button.MouseEntered += () => _main.Audio.UiHover();
                row.Button.GuiInput += ShopGui;
                row.Layout(Zones.Rail - UiKit.U(_s, 16), !_compactShop, PillUnits);
                row.Button.Modulate = new Color(1, 1, 1, 0);
                _shopBody.AddChild(row.Button);
                _rows.Add(row);
            }
            row.Leaving = false;
            row.Item = it;
            _rowOrder.Add(row);
            _shown.Add(it);
            _marks.Add(row.Button);
        }
    }

    private void RowPressed(ShopRow row)
    {
        // The row under the pointer is always the row the player aimed at: a row that just moved takes no click.
        if (row.SinceMove < RowClickCalm || row.Waiting || row.Leaving) return;
        int i = _rowOrder.IndexOf(row);
        if (i >= 0) Activate(i);
    }

    /// <summary>Lays the rows out (sizes by fit, targets, slides and fades) and the card's own rectangle.</summary>
    private void LayoutShop(float dt, Town t)
    {
        var z = Zones;
        float pad = UiKit.U(_s, 8), headH = UiKit.U(_s, 28), gap = UiKit.U(_s, 4), top = pad + UiKit.U(_s, 36);
        int groups = 0;
        for (int g = 0; g < 5; g++) if (GroupHasAny(g)) groups++;
        float Gap(float rowH) => rowH < UiKit.U(_s, 50) ? UiKit.U(_s, 2) : gap;
        float Content(float rowH) => top + groups * headH + _rowOrder.Count * (rowH + Gap(rowH)) + pad;
        float max = z.ShopMaxHeight;
        bool compact = Content(UiKit.U(_s, 56)) > max;
        _shopScrolls = compact && Content(UiKit.U(_s, 40)) > max;
        if (compact != _compactShop)
        {
            _compactShop = compact;
            foreach (var r in _rows) r.Layout(z.Rail - UiKit.U(_s, 16), !compact, PillUnits);
        }
        float rowH = UiKit.U(_s, compact ? 40 : 56);
        float content = Content(rowH);
        // The card follows its rows: as tall as where they are now (a row that is still sliding up or fading out keeps
        // its place in the card), at most H - 104.
        float extent = content;
        foreach (var r in _rows) if (r.Y >= 0 && r.Alpha > 0.01f) extent = Math.Max(extent, top + r.Y + rowH + pad);
        var rect = z.Shop(extent);
        _shop.Position = rect.Position;
        _shop.Size = rect.Size;
        _scroll = _shopScrolls ? Math.Clamp(_scroll, 0, content - rect.Size.Y) : 0;
        _shopBody.Position = new Vector2(pad, top - _scroll);
        // Targets in order: group heads and rows (first pass: where each row wants to be).
        float y = 0;
        int k = 0;
        bool pending = false;
        for (int g = 0; g < 5; g++) _groupHead[g].Visible = false;
        _shopBounds.Clear();
        for (int g = 0; g < 5; g++)
        {
            if (!GroupHasAny(g)) continue;
            var head = _groupHead[g];
            head.Visible = !_shopScrolls || (y >= _scroll - 0.5f && y + headH <= _shopVisEnd + 0.5f);
            head.Position = new Vector2(0, y);
            _shopBounds.Add(y);
            y += headH;
            for (; k < _rowOrder.Count && (int)GroupOf(_rowOrder[k].Item.Kind) == g; k++)
            {
                var r = _rowOrder[k];
                r.Want = y;
                if (r.Waiting || (r.Y >= -0.5f && Math.Abs(y - r.To) > 0.5f)) pending = true;
                y += rowH + Gap(rowH);
                _shopBounds.Add(y);
            }
        }
        // L1 fix: when the rows scroll (1280 x 720 at 150), the card shows whole rows only: the scroll stops at a
        // heading or a row's top and the card ends under the last whole row (u06 and u33 showed a row cut in half).
        if (_shopScrolls && _shopBounds.Count > 0)
        {
            float best = 0, maxScroll = content - rect.Size.Y;
            bool atEnd = _scroll >= maxScroll - 0.5f;
            foreach (float b in _shopBounds)
                if (atEnd ? b >= maxScroll - 0.5f && (best < maxScroll - 0.5f || b < best) : Math.Abs(b - _scroll) < Math.Abs(best - _scroll)) best = b;
            _scroll = best;
            float bodyH = rect.Size.Y - top - pad, end = 0;
            foreach (float b in _shopBounds) if (b - _scroll <= bodyH + 0.5f) end = Math.Max(end, b - _scroll);
            if (end > 0) _shop.Size = new Vector2(rect.Size.X, top + end + pad);
            _shopVisEnd = _scroll + end;
            _shopBody.Position = new Vector2(pad, top - _scroll);
        }
        // The pointer's hold: while it is over the shop, a change waits until it leaves or 1.5 s have passed since the
        // change, so the row under the pointer is the row the player aimed at.
        bool over = PointerOverShop();
        if (!over || !pending) _holdAge = -1f;
        else if (_holdAge < 0) _holdAge = 0f;
        else _holdAge += dt;
        bool hold = over && pending && _holdAge <= RowHold;
        foreach (var r in _rowOrder)
        {
            float to = r.Want;
            if (r.Waiting) { r.Y = to; r.To = to; r.SlideT = -1; if (!hold) r.Waiting = false; }
            else if (r.Y < -0.5f) { r.Y = to; r.To = to; r.SlideT = -1; }
            else if (!hold && Math.Abs(to - r.To) > 0.5f) { r.From = r.Y; r.To = to; r.SlideT = 0; }
        }
        // Animate: slides and fades (Position and Modulate only).
        for (int i = _rows.Count - 1; i >= 0; i--)
        {
            var r = _rows[i];
            r.SinceMove += dt;
            if (r.SlideT >= 0)
            {
                r.SlideT += dt;
                float u = Math.Clamp(r.SlideT / RowSlide, 0, 1);
                r.Y = r.From + (r.To - r.From) * BookMotion.Smoother(u);
                r.SinceMove = 0;
                if (u >= 1) r.SlideT = -1;
            }
            r.Button.Position = new Vector2(0, r.Y);
            float want = r.Leaving || r.Waiting ? 0f : 1f;
            r.Alpha = want > r.Alpha ? Math.Min(want, r.Alpha + dt / RowFade) : Math.Max(want, r.Alpha - dt / RowFade);
            r.Button.Modulate = new Color(1, 1, 1, r.Alpha);
            // A scrolled shop shows whole rows only: one that is not wholly inside the card is not drawn.
            r.Button.Visible = r.Alpha > 0.01f && (!_shopScrolls || (r.Y >= _scroll - 0.5f && r.Y + rowH <= _shopVisEnd + 0.5f));
            if (r.Leaving && r.Alpha <= 0f)
            {
                _rows.RemoveAt(i);
                r.Button.GetParent()?.RemoveChild(r.Button);
                r.Button.QueueFree();
            }
        }
    }

    private bool PointerOverShop() => _shop.IsVisibleInTree() && _shop.GetGlobalRect().HasPoint(_shop.GetGlobalMousePosition());

    private bool GroupHasAny(int g)
    {
        // Night 3 L1 fix: a group whose purchases are all finished (or not yet unlocked) keeps its heading with the
        // count of done (sheet 3: the same thing stays in the same place), so it reads the simulation's whole list.
        foreach (var it in _raw) if ((int)GroupOf(it.Kind) == g) return true;
        return false;
    }

    /// <summary>The heading lines: what is folded away ("1 done · Yoke at level 7 · +1 more").</summary>
    private void FoldFrame(Town t)
    {
        for (int g = 0; g < 5; g++)
        {
            int done = 0, locked = 0, firstLocked = -1;
            for (int i = 0; i < _raw.Count; i++)
            {
                var it = _raw[i];
                if ((int)GroupOf(it.Kind) != g || IsRowItem(it, t)) continue;
                if (IsLocked(it) && !(it.Reason == "Unlocks at a later level" && t.Level >= t.MaxLevel)) { locked++; if (firstLocked < 0) firstLocked = i; }
                else done++;
            }
            int sig = HashCode.Combine(done, locked, firstLocked >= 0 ? _raw[firstLocked].Name : "", firstLocked >= 0 ? _raw[firstLocked].Reason : "");
            if (sig == _groupFoldSig[g]) continue;
            _groupFoldSig[g] = sig;
            var f = _groupFold[g];
            string d = done > 0 ? $"{done} done" : "";
            string l = firstLocked >= 0 ? $"{RowName(_raw[firstLocked])} {Lower(FoldLock(_raw[firstLocked].Reason))}" : "";
            string more = locked > 1 ? $"+{locked - 1} more" : "";
            f.Text = Join(d, l, more);
            // L1 fix: one grammar for the headings (sheet 3): "1 done · +2 more", never "2 later".
            if (TextWidth(f) > f.Size.X) f.Text = Join(d, locked > 0 ? $"+{locked} more" : "", "");
            if (TextWidth(f) > f.Size.X) f.Text = d;
        }
    }

    private static string Join(string a, string b, string c)
    {
        string r = a;
        if (b.Length > 0) r = r.Length > 0 ? r + " · " + b : b;
        if (c.Length > 0) r = r.Length > 0 ? r + " · " + c : c;
        return r;
    }

    /// <summary>"3 to buy now", with a note when one of them is scrolled out of sight.</summary>
    private void AffordFrame()
    {
        int n = 0; bool hidden = false;
        float view = _shop.Size.Y;
        for (int i = 0; i < _rowOrder.Count; i++)
        {
            if (!_shown[i].Affordable) continue;
            n++;
            float y = _shopBody.Position.Y + _rowOrder[i].Y;
            if (y + _rowOrder[i].Button.Size.Y > view || y < _shopBody.Position.Y - 1) hidden = true;
        }
        int sig = n * 2 + (hidden ? 1 : 0);
        if (sig == _affordSig) return;
        _affordSig = sig;
        _shopAfford.Text = n == 0 ? "" : hidden ? $"{n} to buy now ↓" : $"{n} to buy now";
    }

    /// <summary>The wheel over the shop: it scrolls only when the rows overflow (and is swallowed at the ends);
    /// otherwise the map zooms as everywhere else.</summary>
    private void ShopGui(InputEvent e)
    {
        if (e is not InputEventMouseButton { Pressed: true } mb || mb.ButtonIndex is not (MouseButton.WheelUp or MouseButton.WheelDown)) return;
        if (_shopScrolls)
        {
            _scroll += (mb.ButtonIndex == MouseButton.WheelUp ? -1 : 1) * UiKit.U(_s, 42); // one row; snapped to a row
            _shop.AcceptEvent();
            return;
        }
        if (mb.ShiftPressed) return;
        _main.Rig.ZoomAt(_shop.GetViewport().GetMousePosition(), mb.ButtonIndex == MouseButton.WheelUp ? 0.85f : 1f / 0.85f);
        _main.Rig.UserMoved = true;
        _shop.AcceptEvent();
    }

    /// <summary>One pulse at a time, at most two a second: a row that has just become affordable.</summary>
    private void PulseFrame(float dt)
    {
        _pulseGap = Math.Max(0, _pulseGap - dt);
        foreach (var r in _rowOrder)
        {
            bool aff = r.Item.Affordable;
            if (aff && !r.WasAffordable && r.Alpha > 0.5f && !_pulseQueue.Contains(r.Id)) _pulseQueue.Enqueue(r.Id);
            r.WasAffordable = aff;
            if (r.Pulse >= 0)
            {
                r.Pulse += dt;
                float u = Math.Clamp(r.Pulse / PulseSeconds, 0, 1);
                float k = 1f + 0.08f * MathF.Sin(MathF.PI * u);
                r.Pill.Scale = new Vector2(k, k);
                if (u >= 1) { r.Pulse = -1; r.Pill.Scale = Vector2.One; }
            }
        }
        if (_pulseGap > 0 || _pulseQueue.Count == 0) return;
        int id = _pulseQueue.Dequeue();
        foreach (var r in _rowOrder) if (r.Id == id && r.Item.Affordable) { r.Pulse = 0; _pulseGap = PulseGap; PulsesStarted++; break; }
    }

    /// <summary>Pulses started (tests: one at a time, at most two a second).</summary>
    public int PulsesStarted { get; private set; }
    /// <summary>Rows are 40 units with one line (tests).</summary>
    public bool CompactShop => _compactShop;
    /// <summary>The rows overflow and the shop scrolls (tests).</summary>
    public bool ShopScrolls => _shopScrolls;
    public Rect2 ShopRect => _shop.GetGlobalRect();
    /// <summary>The key printed on row i ("" none), and its text as one line (tests).</summary>
    public string RowKey(int i) => i >= 0 && i < _rowOrder.Count ? _rowOrder[i].Key.Text : "";
    public string RowText(int i) => i >= 0 && i < _rowOrder.Count ? $"{_rowOrder[i].Name.Text} {_rowOrder[i].Step.Text} | {_rowOrder[i].Line2.Text} | {_rowOrder[i].Price.Text}" : "";
    /// <summary>A row's text by its button ("" when it is not a row; tests).</summary>
    public string MarkText(Button? b) { for (int i = 0; i < _rowOrder.Count; i++) if (_rowOrder[i].Button == b) return RowText(i); return ""; }
    public ShopRow.PillState RowPill(int i) => _rowOrder[i].State;
    /// <summary>Row i lies whole inside the shop's visible rectangle (it may be scrolled out of sight; tests).</summary>
    public bool RowInView(int i) => i >= 0 && i < _rowOrder.Count && _shop.GetGlobalRect().Grow(1).Encloses(_rowOrder[i].Button.GetGlobalRect());
    public string RowDebug(int i) { if (i < 0 || i >= _rowOrder.Count) return "-"; var r = _rowOrder[i]; return $"Y {r.Y:0.0} To {r.To:0.0} Want {r.Want:0.0} slide {r.SlideT:0.00} since {r.SinceMove:0.00} alpha {r.Alpha:0.00} waiting {r.Waiting} leaving {r.Leaving} hold {_holdAge:0.00} over {PointerOverShop()} compact {_compactShop} rows {_rowOrder.Count}/{_rows.Count}"; }
    public bool RowCalm(int i) => i >= 0 && i < _rowOrder.Count && _rowOrder[i].SinceMove >= RowClickCalm && _rowOrder[i].Alpha >= 1f;
    public string GroupFoldText(ShopGroup g) => _groupHead[(int)g].Visible ? _groupFold[(int)g].Text : "";
}
