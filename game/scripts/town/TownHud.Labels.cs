using System;
using System.Collections.Generic;
using Godot;

namespace TideToll.Game;

/// <summary>
/// The map's tags (layout sheet sections 4 and 5; night 3, L1 fix). The plots' "What" and the boats' "Wants" words are
/// still kept by their owners as Label3Ds (text and place), but those are never drawn: each one is drawn as a tag on
/// screen instead, one style for all (a quiet paper pill, caption size at every zoom), at the label's place. A tag that
/// would touch a visible card or a tag already kept fades out and stays out for at least half a second; every tag
/// fades with the full interface and is out while a page is open. Boats' tags come first (what a boat wants now).
/// The words are read twice a second (no strings per frame); boats say "Grain 21 of 656", not "... loads".
/// </summary>
public partial class TownHud
{
    private sealed class MapTag
    {
        public Label3D Source = null!;
        public Label Pill = null!;
        public bool Boat;
        public string Text = "";
        public float A, Out; // opacity now; seconds it must stay out still
    }

    private readonly List<MapTag> _tags = new();
    private readonly List<Label> _tagPool = new();
    private Control? _tagLayer;
    private Control?[] _labelCards = Array.Empty<Control?>();
    private Node?[] _labelOwners = Array.Empty<Node?>();
    private readonly List<Rect2> _keptRects = new();
    private readonly List<Rect2> _cardRects = new();
    private int _labelScan;
    /// <summary>Map tags faded out this frame because a card or another tag is there (tests).</summary>
    public int LabelsHidden { get; private set; }
    /// <summary>Map tags shown this frame, and their rectangles on screen (tests: none overlap a card or each other).</summary>
    public IReadOnlyList<Rect2> LabelsShownRects => _keptRects;
    /// <summary>The words of the tags shown now (tests and the audit).</summary>
    public int TagCount => _tags.Count;

    public const float TagFade = 0.2f, TagStayOut = 0.5f;

    private void LabelsFrame(float dt)
    {
        if (_tagLayer == null || !IsInstanceValid(_tagLayer))
        {
            _tagLayer = new Control { Name = "Tags", MouseFilter = Control.MouseFilterEnum.Ignore };
            _root.AddChild(_tagLayer);
            _tagLayer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _root.MoveChild(_tagLayer, 0); // under every card
            _tags.Clear(); _tagPool.Clear();
            _labelCards = Array.Empty<Control?>(); _labelScan = 0; // the interface was built anew
        }
        if (--_labelScan <= 0) { _labelScan = 30; ScanTags(); }
        _cardRects.Clear();
        if (_labelCards.Length == 0) _labelCards = new Control?[] { _ledger, _shop, _bell, _banner, _achMark, _agePageMark, PauseMark, _choice, _tide, _slim, _lineCard, _home, _hoverTip, _suggest };
        foreach (var c in _labelCards)
            if (c != null && IsInstanceValid(c) && c.IsVisibleInTree() && c.Modulate.A > 0.05f) _cardRects.Add(c.GetGlobalRect().Grow(UiKit.Px(_s, 6)));
        bool page = AchievementsOpen || AgePageOpen;
        _keptRects.Clear();
        LabelsHidden = 0;
        var cam = _main.Rig.Camera;
        float gap = UiKit.Px(_s, 4);
        for (int pass = 0; pass < 2; pass++)
            foreach (var g in _tags)
            {
                if (g.Boat != (pass == 0)) continue;
                var l = g.Source;
                bool live = IsInstanceValid(l) && l.IsInsideTree() && l.GetParent() is Node3D owner && owner.IsVisibleInTree()
                            && g.Text.Length > 0 && !cam.IsPositionBehind(l.GlobalPosition);
                bool want = live && !page && _fullA > 0.01f;
                Rect2 r = default;
                if (want)
                {
                    var at = cam.UnprojectPosition(l.GlobalPosition);
                    var sz = g.Pill.Size;
                    // Plots' words hang under their place; a boat's sits above its hull.
                    r = new Rect2(new Vector2(at.X - sz.X / 2f, g.Boat ? at.Y - sz.Y : at.Y), sz);
                    g.Pill.Position = r.Position.Round();
                    bool touch = !_tagLayer.GetGlobalRect().Encloses(r); // never cut by the window's edge
                    foreach (var c in _cardRects) if (c.Intersects(r)) { touch = true; break; }
                    if (!touch) foreach (var k in _keptRects) if (k.Intersects(r.Grow(gap))) { touch = true; break; }
                    if (touch) { g.Out = TagStayOut; LabelsHidden++; }
                    else if (g.Out > 0) { g.Out = Math.Max(0, g.Out - dt); LabelsHidden++; }
                    want = !touch && g.Out <= 0;
                }
                g.A = want ? Math.Min(1f, g.A + dt / TagFade) : Math.Max(0f, g.A - dt / TagFade);
                float a = g.A * _fullA;
                if (g.Pill.Modulate.A != a) g.Pill.Modulate = new Color(1, 1, 1, a);
                bool vis = a > 0.01f;
                if (g.Pill.Visible != vis) g.Pill.Visible = vis;
                if (want) _keptRects.Add(r);
                if (IsInstanceValid(l) && l.Visible) l.Visible = false; // the 3D words are never drawn
            }
    }

    /// <summary>Finds the map's words (they come and go with plots and boats) and reads their text.</summary>
    private void ScanTags()
    {
        if (_labelOwners.Length == 0 || Array.IndexOf(_labelOwners, null) >= 0) _labelOwners = new Node?[] { _main.Boats, _main.Buildings, _main.River };
        int used = 0;
        foreach (Node? n in _labelOwners)
        {
            if (n == null || !IsInstanceValid(n)) continue;
            foreach (var c in n.FindChildren("W*", "Label3D", true, false))
            {
                if (c is not Label3D l) continue;
                string nm = l.Name;
                if (nm != "What" && nm != "Wants") continue;
                l.Visible = false;
                MapTag g;
                if (used < _tags.Count) g = _tags[used];
                else { g = new MapTag { Pill = NewTagPill() }; _tags.Add(g); }
                used++;
                if (g.Source != l) { g.Source = l; g.A = 0; g.Out = 0; }
                g.Boat = nm == "Wants";
                string text = TagText(l.Text);
                if (text != g.Text) { g.Text = text; g.Pill.Text = text; g.Pill.ResetSize(); }
            }
        }
        // Tags without a source any more go quiet (kept in the list for reuse).
        while (_tags.Count > used)
        {
            var g = _tags[^1];
            g.Pill.Visible = false;
            _tagPool.Add(g.Pill);
            _tags.RemoveAt(_tags.Count - 1);
        }
    }

    private Label NewTagPill()
    {
        if (_tagPool.Count > 0) { var p = _tagPool[^1]; _tagPool.RemoveAt(_tagPool.Count - 1); p.Text = ""; return p; }
        var l = UiKit.Text("", _s, UiKit.Caption, UiKit.UiFont);
        l.AddThemeFontSizeOverride(FontSizeName, UiKit.Type(_s, UiKit.Caption));
        l.Name = "Tag";
        var box = UiKit.CardBox(_s, UiKit.CardKind.Quiet, 4);
        box.SetCornerRadiusAll(UiKit.U(_s, 10));
        box.ContentMarginLeft = box.ContentMarginRight = UiKit.U(_s, 8);
        box.ContentMarginTop = box.ContentMarginBottom = UiKit.U(_s, 1);
        l.AddThemeStyleboxOverride(NormalName, box);
        l.Visible = false;
        _tagLayer!.AddChild(l);
        return l;
    }

    /// <summary>A tag's words: one line, no "loads" after a count ("Grain 21 of 656").</summary>
    public static string TagText(string s)
    {
        int nl = s.IndexOf('\n');
        if (nl >= 0) s = s[..nl];
        return s.Replace(" loads", "").Trim();
    }
}
