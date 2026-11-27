using System;
using System.Collections.Generic;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// The shop card on the right (layout sheet v2 section 3, sheet v3 docs/plans/n4/shop-sheet-v3.md), as tall as its
/// rows. A header ("Shop", and "3 to buy"), then the display groups, each heading (name and a hairline, nothing else)
/// only while it has rows, and a foot line ("Next: Basket at level 3") while anything is still locked (finished and
/// locked purchases are not rows). Rows are 56 units when everything fits, else all 40 (one line), else the card
/// scrolls (the wheel scrolls it only then). A row or heading fades in or out over 0.2 s and what is below slides over
/// 0.2 s, the card's bottom edge and the foot with the same ease, but not while the pointer is over the shop (until it
/// leaves, or 1.5 s), and a row that moved in the last 0.3 s takes no click. A row that has just become affordable
/// pulses once, one at a time, at most two a second.
/// </summary>
public partial class TownHud
{
    public const float FootUnits = 30;
    /// <summary>Follow-up 3b: tall rows come back only when they would fit with this many rows to spare.</summary>
    public const int TallSpareRows = 2;
    private float _shopK = -1, _kFrom, _kTo, _kT = -1, _tallContent, _footA;
    private bool _footWant;
    private int _rowsBelow;
    /// <summary>Frames in which the foot lay over a row (follow-up 1; tests: always 0), frames of the shop, frames of a
    /// tall/compact switch.</summary>
    public int FootOverRowFrames { get; private set; }
    public int ShopFrames { get; private set; }
    public int SwitchFrames { get; private set; }
    /// <summary>Tall rows' content height and the card's limit (tests: the switch is sticky).</summary>
    public float ShopTallContent => _tallContent;
    public float ShopLimit => Zones.ShopMaxHeight;
    public float TallPitch => UiKit.U(_s, ShopRow.TallUnits) + UiKit.U(_s, 4);
    public bool ShopSwitching => _kT >= 0;
    public float ShopTallness => _shopK;
    public const float RowFade = 0.2f, RowSlide = 0.2f, RowHold = 1.5f, RowClickCalm = 0.3f, PulseSeconds = 0.4f, PulseGap = 0.5f;
    private Panel _shop = null!;
    private Control _shopBody = null!;
    private Label _shopTitle = null!, _shopAfford = null!;
    private readonly List<ShopRow> _rows = new();       // every row node, leaving ones included
    private readonly List<ShopRow> _rowOrder = new();   // the rows shown, in order (parallel to _shown and _marks)
    private readonly Control[] _groupHead = new Control[5];
    private readonly Label[] _groupName = new Label[5];
    private readonly ShopSlide[] _headAnim = new ShopSlide[5];
    private readonly ShopSlide _card = new();
    private Control _foot = null!;
    private Label _footText = null!;
    private int _nameSize;
    private float _scroll, _holdAge = -1f, _pulseGap;
    private bool _compactShop, _shopScrolls;
    private readonly System.Collections.Generic.List<float> _shopBounds = new();
    private float _shopVisEnd = float.MaxValue;
    private int _affordSig = int.MinValue;
    private readonly Queue<int> _pulseQueue = new();

    /// <summary>The pill's width in sheet units: 96 on the 320 rail (104 when "99,999" would not fit at body size), 88 on
    /// the 272 rail (decided when the shop is built, <see cref="DecidePillShort"/>).</summary>
    private float _pillUnits = 96;
    private float PillUnits => _pillUnits;

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
            float nameW = TextWidth(_groupName[g]) + 2;
            _groupName[g].Size = new Vector2(nameW, head.Size.Y);
            // The hairline: ink at 16 percent from the name to the right edge (sheet v3), and nothing else on the line.
            var rule = new ColorRect { Name = "Rule", Color = UiKit.Ruling, MouseFilter = Control.MouseFilterEnum.Ignore };
            float rx = UiKit.U(_s, 6) + nameW + UiKit.U(_s, 8);
            rule.Position = new Vector2(rx, MathF.Round(head.Size.Y / 2f + UiKit.U(_s, 2)));
            rule.Size = new Vector2(head.Size.X - rx - UiKit.U(_s, 6), Math.Max(1, UiKit.U(_s, 1)));
            head.AddChild(_groupName[g]);
            head.AddChild(rule);
            _shopBody.AddChild(head);
            _groupHead[g] = head;
            _headAnim[g] = new ShopSlide();
        }
        // The foot: one caption line under a hairline, at the card's bottom (it moves with the bottom edge).
        _foot = new Control { Name = "Foot", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _foot.Size = new Vector2(w - 2 * pad, UiKit.U(_s, FootUnits));
        var footRule = new ColorRect { Name = "Rule", Color = UiKit.Ruling, MouseFilter = Control.MouseFilterEnum.Ignore };
        footRule.Position = new Vector2(UiKit.U(_s, 6), 0);
        footRule.Size = new Vector2(_foot.Size.X - UiKit.U(_s, 12), Math.Max(1, UiKit.U(_s, 1)));
        _foot.AddChild(footRule);
        _footText = UiKit.Fixed(_s, UiKit.Caption, UiKit.UiFont, UiKit.MutedNumber);
        _footText.Name = "Text";
        _footText.Position = new Vector2(UiKit.U(_s, 6), UiKit.U(_s, 4));
        _footText.Size = new Vector2(_foot.Size.X - UiKit.U(_s, 12), _foot.Size.Y - UiKit.U(_s, 4));
        _foot.AddChild(_footText);
        _shop.AddChild(_foot);
        _footSig = int.MinValue;
        _card.Reset();
        _shopK = -1; _kT = -1; _footA = 0; _footWant = false;
        DecidePillShort();
        _nameSize = NameSizeFor(w - 2 * pad);
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
                row.Layout(Zones.Rail - UiKit.U(_s, 16), !_compactShop, PillUnits, _nameSize);
                if (_shopK >= 0) row.Blend(_shopK);
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
        // Follow-up 1: the foot takes its room while it shows or is wanted (it fades in only once the edge has arrived).
        float footH = _footWant || _footA > 0 ? UiKit.U(_s, FootUnits) : 0;
        float tallH = UiKit.U(_s, ShopRow.TallUnits), compactH = UiKit.U(_s, 40), tallGap = gap, compactGap = UiKit.U(_s, 2);
        float Content(float rowH, float rowGap) => top + groups * headH + _rowOrder.Count * (rowH + rowGap) + footH + pad;
        float max = z.ShopMaxHeight;
        // Follow-up 3b: sticky. Compact when tall rows do not fit; tall again only when they fit with two rows to spare.
        _tallContent = Content(tallH, tallGap);
        bool compact = CompactForce ?? (_compactShop ? _tallContent > max - TallSpareRows * (tallH + tallGap) : _tallContent > max);
        if (_shopK < 0) { _compactShop = compact; _shopK = compact ? 0 : 1; _kFrom = _kTo = _shopK; _kT = -1; foreach (var r in _rows) r.Blend(_shopK); }
        if (compact != _compactShop)
        {
            _compactShop = compact;
            _kFrom = _shopK; _kTo = compact ? 0 : 1; _kT = 0;
        }
        // Follow-up 3a: the switch eases over the rows' slide: heights, places, line 2 and the key caps.
        bool switching = _kT >= 0;
        if (switching)
        {
            _kT += dt;
            float u = Math.Clamp(_kT / RowSlide, 0, 1);
            _shopK = _kFrom + (_kTo - _kFrom) * BookMotion.Smoother(u);
            if (u >= 1) { _shopK = _kTo; _kT = -1; }
            foreach (var r in _rows) r.Blend(_shopK);
        }
        float rowH = MathF.Round(compactH + (tallH - compactH) * _shopK), rowGap = compactGap + (tallGap - compactGap) * _shopK;
        float Gap(float h) => rowGap;
        float content = Content(rowH, rowGap);
        _shopScrolls = compact && Content(compactH, compactGap) > max;
        // The card follows its rows: as tall as where they are now (a row that is still sliding up or fading out keeps
        // its place in the card), at most H - 104.
        // Sheet v3: the bottom edge (and the foot on it) eases with the rows' slide; it never cuts a row still showing.
        if (switching) { _card.Y = _card.To = content; _card.T = -1; }
        else _card.Aim(content, PointerOverShop() && _holdAge >= 0 && _holdAge <= RowHold);
        _card.Step(dt);
        float extent = _card.Y;
        foreach (var r in _rows) if (r.Y >= 0 && r.Alpha > 0.01f) extent = Math.Max(extent, top + r.Y + rowH + footH + pad);
        for (int g = 0; g < 5; g++) if (_headAnim[g].Alpha > 0.01f) extent = Math.Max(extent, top + _headAnim[g].Y + headH + footH + pad);
        var rect = z.Shop(extent);
        _shop.Position = rect.Position;
        _shop.Size = rect.Size;
        _scroll = _shopScrolls ? Math.Clamp(_scroll, 0, content - rect.Size.Y) : 0;
        _shopBody.Position = new Vector2(pad, top - _scroll);
        // Targets in order: group heads and rows (first pass: where each row wants to be).
        float y = 0;
        int k = 0;
        bool pending = false;
        _shopBounds.Clear();
        for (int g = 0; g < 5; g++) _headAnim[g].Wanted = false;
        for (int g = 0; g < 5; g++)
        {
            if (!GroupHasAny(g)) continue;
            var ha = _headAnim[g];
            ha.Wanted = true;
            ha.Want = y;
            if (ha.Y >= -0.5f && Math.Abs(y - ha.To) > 0.5f) pending = true;
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
            float bodyH = rect.Size.Y - top - pad - footH, end = 0;
            foreach (float b in _shopBounds) if (b - _scroll <= bodyH + 0.5f) end = Math.Max(end, b - _scroll);
            if (end > 0) _shop.Size = new Vector2(rect.Size.X, top + end + footH + pad);
            _shopVisEnd = _scroll + end;
            _shopBody.Position = new Vector2(pad, top - _scroll);
        }
        // Follow-up 6: rows out of sight below (said in the foot's place).
        int below = 0;
        if (_shopScrolls) foreach (var r in _rowOrder) if (r.Want + rowH > _shopVisEnd + 0.5f) below++;
        _rowsBelow = below;
        // The pointer's hold: while it is over the shop, a change waits until it leaves or 1.5 s have passed since the
        // change, so the row under the pointer is the row the player aimed at.
        bool over = PointerOverShop();
        if (!over || !pending) _holdAge = -1f;
        else if (_holdAge < 0) _holdAge = 0f;
        else _holdAge += dt;
        bool hold = over && pending && _holdAge <= RowHold;
        for (int g = 0; g < 5; g++)
        {
            var ha = _headAnim[g];
            if (!ha.Wanted) continue;
            if (ha.Y < -0.5f || ha.Alpha <= 0.01f || switching) { ha.Y = ha.Want; ha.To = ha.Want; ha.T = -1; }
            else if (!hold && Math.Abs(ha.Want - ha.To) > 0.5f) { ha.From = ha.Y; ha.To = ha.Want; ha.T = 0; }
        }
        for (int g = 0; g < 5; g++)
        {
            var ha = _headAnim[g];
            ha.Slide(dt, RowSlide);
            // A new heading waits (unseen) while the pointer holds the rows, as a new row does.
            bool show = ha.Wanted && (!hold || ha.Alpha > 0);
            ha.Alpha = show ? Math.Min(1, ha.Alpha + dt / RowFade) : Math.Max(0, ha.Alpha - dt / RowFade);
            var head = _groupHead[g];
            head.Position = new Vector2(0, ha.Y);
            head.Modulate = new Color(1, 1, 1, ha.Alpha);
            head.Visible = ha.Alpha > 0.01f && (!_shopScrolls || (ha.Y >= _scroll - 0.5f && ha.Y + headH <= _shopVisEnd + 0.5f));
        }
        _foot.Position = new Vector2(pad, _shop.Size.Y - pad - footH);
        // The foot fades in only once the card's edge and every row are at rest (so it never lies over a row), and
        // fades out before its room is given back.
        bool settled = !switching && _card.T < 0;
        foreach (var r in _rows) if (r.SlideT >= 0 || (r.Leaving && r.Alpha > 0)) settled = false;
        _footA = _footWant && (settled || _footA > 0) ? Math.Min(1, _footA + dt / RowFade) : Math.Max(0, _footA - dt / RowFade);
        _foot.Modulate = new Color(1, 1, 1, _footA);
        _foot.Visible = _footA > 0.01f;
        foreach (var r in _rowOrder)
        {
            float to = r.Want;
            if (r.Waiting) { r.Y = to; r.To = to; r.SlideT = -1; if (!hold) r.Waiting = false; }
            else if (r.Y < -0.5f || switching) { r.Y = to; r.To = to; r.SlideT = -1; }
            else if (!hold && Math.Abs(to - r.To) > 0.5f) { r.From = r.Y; r.To = to; r.SlideT = 0; }
        }
        // Animate: slides and fades (Position and Modulate only).
        bool footOver = false;
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
            if (_foot.Visible && r.Button.Visible && r.Button.GetGlobalRect().Grow(-0.5f).Intersects(_foot.GetGlobalRect())) footOver = true;
            if (r.Leaving && r.Alpha <= 0f)
            {
                _rows.RemoveAt(i);
                r.Button.GetParent()?.RemoveChild(r.Button);
                r.Button.QueueFree();
            }
        }
        if (footOver) FootOverRowFrames++;
        ShopFrames++;
        if (switching) SwitchFrames++;
    }

    private bool PointerOverShop() => _shop.IsVisibleInTree() && _shop.GetGlobalRect().HasPoint(_shop.GetGlobalMousePosition());

    /// <summary>Sheet v3: a group has a heading only while it has a row (one that is not leaving).</summary>
    private bool GroupHasAny(int g)
    {
        foreach (var r in _rowOrder) if ((int)GroupOf(r.Item.Kind) == g) return true;
        return false;
    }

    /// <summary>The one size of every row name in this layout: body when every name of the catalogue fits its box at
    /// body, else caption (sheet v3: decided once from the whole catalogue, never row by row).</summary>
    private int NameSizeFor(float rowWidth)
    {
        int body = UiKit.Type(_s, UiKit.Body), cap = UiKit.Type(_s, UiKit.Caption);
        float box = ShopRow.NameBox(rowWidth, PillUnits, _s, true);
        bool Fits(int size, bool shortNames)
        {
            foreach (var n in RowNameCatalogue)
            {
                string t = shortNames && ShortRowNames.TryGetValue(n, out var sh) ? sh : n;
                if (UiKit.UiFont.GetStringSize(t, HorizontalAlignment.Left, -1, size).X > box) return false;
            }
            return true;
        }
        _shortNames = false;
        if (Fits(body, false)) return body;
        if (Fits(cap, false)) return cap;
        _shortNames = true;
        return cap;
    }

    /// <summary>"3 to buy", with the down mark when one of them is scrolled out of sight.</summary>
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
        _shopAfford.Text = n == 0 ? "" : hidden ? $"{n} to buy ↓" : $"{n} to buy";
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
                // Addendum B: a short lightening of the fill, never a change of size.
                r.Light = u >= 1 ? 0f : 0.18f * MathF.Sin(MathF.PI * u);
                r.Tone();
                if (u >= 1) r.Pulse = -1;
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
    /// <summary>The group headings showing (their node, and whether any row of the group is shown; tests).</summary>
    public bool GroupHeadShown(int g) => _groupHead[g].Visible && _groupHead[g].Modulate.A > 0.5f;
    public Control GroupHead(int g) => _groupHead[g];
    public bool GroupHasRows(int g) => GroupHasAny(g);
    /// <summary>The foot line's text ("" when it does not show; tests).</summary>
    public string FootText => _foot.Visible ? _footText.Text : "";
    /// <summary>The one name size of this layout and the row width it was decided for (tests).</summary>
    public int RowNameSize => _nameSize;
    public float RowWidth => Zones.Rail - UiKit.U(_s, 16);
    public float PillWidthUnits => PillUnits;
    public ShopRow RowNode(int i) => _rowOrder[i];
    /// <summary>The InputMap action of row i ("" none; tests: the keys ascend down the list).</summary>
    public string RowAction(int i)
    {
        int ord = 0;
        for (int k = 0; k < i; k++) if (_shown[k].Kind == ShopItemKind.Building) ord++;
        return ActionFor(_shown[i], _shown[i].Kind == ShopItemKind.Building ? ord : 0);
    }
}
