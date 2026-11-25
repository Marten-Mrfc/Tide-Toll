using System;
using Godot;
using TideToll.Sim;

namespace TideToll.Game;

/// <summary>
/// Short notes that float up from the river and fade (UX tab: "after each cycle, float"; Sound tab: the coin's
/// visual twin is a yellow coin rising and fading): coins by the landing when a boat pays ("+36c"), "Turned back"
/// where a boat gave up, "Mast too tall" where a barge is held below a footbridge, the drawbridge's cycle summary
/// ("+2 boats through; 9 walkers waited") and "Keeper hired". A pool of eight paper slips anchored to map points,
/// placed every frame; text is set only when a note starts. One note per boat at a time (a new note for a boat takes
/// over its slip), and notes never stack over boats or over each other: each frame they are laid out in order, pushed
/// up (and, failing that, aside) until they clear every boat drawn on screen and every note already placed.
/// </summary>
public partial class RiverFloats : Control
{
    public const int Pool = 8;
    public const float Seconds = 4.5f;
    private static readonly Color CoinYellow = Color.FromHtml("#F0E442");

    private sealed partial class Slip : PanelContainer
    {
        public Label Text = null!;
        public Vector3 Pin;
        public double Born = -1e9;
        public bool Coin;
        public float Scale1;
        public int Boat = -1;

        public override void _Draw()
        {
            if (!Coin) return;
            // A yellow coin with an ink edge at the slip's left end (shape and colour: the money glyph).
            float r = 8f * Scale1;
            var c = new Vector2(-r - 3 * Scale1, Size.Y / 2f);
            DrawCircle(c, r, CoinYellow);
            DrawArc(c, r, 0, Mathf.Tau, 20, UiKit.Ink, Mathf.Max(1f, 1.5f * Scale1));
            DrawArc(c, r * 0.55f, 0, Mathf.Tau, 14, UiKit.Ink, Mathf.Max(1f, Scale1));
        }
    }

    private Main _main = null!;
    private float _s;
    private readonly Slip[] _slips = new Slip[Pool];
    private int _next;
    private double _now;
    private readonly Rect2[] _boxes = new Rect2[64];
    private readonly Rect2[] _placed = new Rect2[Pool];

    /// <summary>Overlaps left after the last layout between a note and a boat or another note (tests: 0).</summary>
    public int Overlaps { get; private set; }

    /// <summary>Notes started so far, and coins among them (tests).</summary>
    public int Started { get; private set; }
    public int Coins { get; private set; }
    public string LastText { get; private set; } = "";

    public void Build(Main main, float s)
    {
        _main = main;
        _s = s;
        Name = "RiverFloats";
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
        for (int i = 0; i < Pool; i++)
        {
            var slip = new Slip { Name = $"Float{i}", MouseFilter = MouseFilterEnum.Ignore, Visible = false, Scale1 = s };
            var style = UiKit.Card(s, new Color(UiKit.Paper, 0.94f), 1f);
            style.SetContentMarginAll(UiKit.Px(s, 4));
            style.ContentMarginLeft = style.ContentMarginRight = UiKit.Px(s, 7);
            slip.AddThemeStyleboxOverride("panel", style);
            slip.Text = UiKit.Text("", s, 14, UiKit.UiBold);
            slip.AddChild(slip.Text);
            AddChild(slip);
            _slips[i] = slip;
        }
    }

    public void Clear()
    {
        foreach (var s in _slips) { s.Born = -1e9; s.Visible = false; s.Boat = -1; }
    }

    /// <summary>A note at a map-local point (metres); with a boat id, it replaces any note about the same boat.</summary>
    public void At(Vector3 mapLocal, string text, bool coin, int boat = -1)
    {
        Slip? s = null;
        if (boat >= 0)
            foreach (var o in _slips) if (o.Boat == boat && _now - o.Born < Seconds) { s = o; break; }
        if (s == null)
        {
            s = _slips[_next];
            _next = (_next + 1) % Pool;
        }
        s.Boat = boat;
        s.Pin = mapLocal;
        s.Born = _now;
        s.Coin = coin;
        s.Text.Text = text;
        s.ResetSize();
        s.QueueRedraw();
        Started++;
        if (coin) Coins++;
        LastText = text;
    }

    /// <summary>A note over a crossing's middle.</summary>
    public void AtCrossing(int id, string text, bool coin)
    {
        var w = _main.Sim.World;
        var r = w.River;
        if (r == null) return;
        var xs = r.Crossings;
        float cell = (float)w.Params.CellMeters;
        int n = w.Grid.Width;
        for (int i = 0; i < xs.Count; i++)
        {
            var x = xs[i];
            if (x.Id != id || x.Cells.Count == 0) continue;
            int mid = x.Cells[x.Cells.Count / 2];
            At(new Vector3((mid % n + 0.5f) * cell, 6f, (x.Row + 0.5f) * cell - 4f), text, coin);
            return;
        }
    }

    /// <summary>The river feed's events that get a float.</summary>
    public void OnEvent(RiverEvent e)
    {
        var boats = _main.Boats;
        switch (e.Kind)
        {
            case RiverEventKind.BoatSold when e.Value > 0:
                At(boats.LandingAt + new Vector3(0, 3f, -2f), "+" + RiverFeed.Coin(e.Value), true);
                break;
            case RiverEventKind.BoatTurnedBack:
                if (boats.BoatPosition(e.Boat, out var at))
                {
                    boats.MarkTurnBack(at);
                    At(at + new Vector3(0, 4f, 0), "Turned back", false, e.Boat);
                }
                break;
            case RiverEventKind.BoatWaiting when IsLowBridge(e.Crossing) && IsBarge(e.Boat):
                if (boats.BoatPosition(e.Boat, out var bat)) At(bat + new Vector3(0, 4f, -3f), "Mast too tall", false, e.Boat);
                break;
            case RiverEventKind.KeeperHired:
                AtCrossing(e.Crossing, "Keeper hired", false);
                break;
        }
    }

    private bool IsLowBridge(int id)
    {
        var r = _main.Sim.World.River;
        if (r == null || id < 0) return false;
        var xs = r.Crossings;
        for (int i = 0; i < xs.Count; i++) if (xs[i].Id == id) return xs[i].Type == CrossingType.Footbridge;
        return false;
    }

    private bool IsBarge(int id)
    {
        var r = _main.Sim.World.River;
        if (r == null) return false;
        var bs = r.Boats;
        for (int i = 0; i < bs.Length; i++) if (bs[i].Id == id) return bs[i].Kind == BoatKind.Barge;
        return false;
    }

    public override void _Process(double delta)
    {
        _now += delta;
        var rig = _main.Rig;
        // Every boat drawn, as a box on screen (its hull's length both ways, as it may lie at any angle).
        int nb = 0;
        var boats = _main.Boats;
        float mpp = Mathf.Max(1e-3f, rig.Camera.Size / Mathf.Max(1f, GetViewportRect().Size.Y));
        for (int i = 0; i < boats.Slots && nb < _boxes.Length; i++)
        {
            if (!boats.BoatBox(i, out var bat, out float half)) continue;
            var c = rig.LocalToScreen(bat);
            float r = half / mpp + 2f;
            _boxes[nb++] = new Rect2(c.X - r * 0.45f, c.Y - r, r * 0.9f, 2f * r);
        }
        // Every crossing too (its span and a few metres either side of its row): notes never hide a crossing.
        var w = _main.Sim.World;
        var rv = w.River;
        float cell = (float)w.Params.CellMeters;
        int n = w.Grid.Width;
        if (rv != null)
        {
            var xs = rv.Crossings;
            for (int i = 0; i < xs.Count && nb < _boxes.Length; i++)
            {
                var x = xs[i];
                if (x.Type == CrossingType.Ford || x.Cells.Count == 0) continue;
                var a = rig.LocalToScreen(new Vector3((x.Cells[0] % n) * cell - 3f, 0, (x.Row + 0.5f) * cell - 4f));
                var b = rig.LocalToScreen(new Vector3((x.Cells[x.Cells.Count - 1] % n + 1) * cell + 3f, 0, (x.Row + 0.5f) * cell + 4f));
                _boxes[nb++] = new Rect2(a, b - a).Abs();
            }
        }
        float water = (float)(w.Map.RiverHalfW * cell) / mpp;
        int placed = 0, overlaps = 0;
        float gap = 3f * _s;
        foreach (var s in _slips)
        {
            double age = _now - s.Born;
            bool on = age >= 0 && age < Seconds;
            if (s.Visible != on) s.Visible = on;
            if (!on) continue;
            float t = (float)(age / Seconds);
            var p = rig.LocalToScreen(s.Pin);
            var size = s.Size;
            // On the bank beside the pin (east, then west of the river at that row), rising as it fades; then a slip's
            // height higher at a time; then straight over the pin as a last resort.
            float rx = RiverLayer.RiverXAt(w.RiverX, s.Pin.Z / cell, n) * cell;
            var centre = rig.LocalToScreen(new Vector3(rx, 0, s.Pin.Z));
            float side = water + 14f * _s;
            var at = new Vector2(Mathf.Round(centre.X + side), Mathf.Round(p.Y - size.Y / 2f - 26f * _s * t));
            var best = new Vector2(Mathf.Round(p.X - size.X / 2f), at.Y);
            bool clear = false;
            for (int attempt = 0; attempt < 18 && !clear; attempt++)
            {
                int col = attempt % 3, row = attempt / 3;
                float x0 = col == 0 ? at.X : col == 1 ? centre.X - side - size.X : p.X - size.X / 2f;
                var cand = new Vector2(Mathf.Round(x0), at.Y - row * (size.Y + gap));
                if (Free(new Rect2(cand, size).Grow(gap * 0.5f), nb, placed)) { best = cand; clear = true; }
            }
            if (!clear) overlaps++;
            if (s.Position != best) s.Position = best;
            if (placed < _placed.Length) _placed[placed++] = new Rect2(best, size);
            float a = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
            var m = new Color(1, 1, 1, a);
            if (s.Modulate != m) s.Modulate = m;
        }
        Overlaps = overlaps;
    }

    private bool Free(Rect2 r, int boats, int placed)
    {
        for (int i = 0; i < boats; i++) if (r.Intersects(_boxes[i])) return false;
        for (int i = 0; i < placed; i++) if (r.Intersects(_placed[i])) return false;
        return true;
    }
}
