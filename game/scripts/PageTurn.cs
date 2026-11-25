using Godot;

namespace TideToll.Game;

/// <summary>
/// The page-turn moment between the chapters: a leaf of the book's paper turns over from the right-hand page's outer
/// edge to the left, covering the open book; while it covers it the book is built again for the new chapter (a fresh
/// town from the same seed), the leaf shows the new chapter's heading for a beat, then it lifts away to the left and
/// reveals the new pages. A soft shadow runs along the leaf's moving edge and a pale fold line beside it, so it reads
/// as paper turning, not a wipe. About 1.2 s of real time (it runs while the game is paused); the pointer and the keys
/// do nothing to the map meanwhile. Lives in its own canvas layer above the interface and is kept across chapters.
/// </summary>
public partial class PageTurn : CanvasLayer
{
    /// <summary>Seconds: the leaf covering the book, the heading held, the leaf lifting away.</summary>
    public const double CoverSec = 0.42, HoldSec = 0.38, RevealSec = 0.45;

    private Main _main = null!;
    private Leaf _leaf = null!;
    private double _t = -1;
    private int _to;
    private bool _switched;

    /// <summary>True while a page is turning (input to the map and the keys wait).</summary>
    public bool Busy => _t >= 0;
    /// <summary>Page turns finished so far, and the chapter being turned to (tests).</summary>
    public int Turns { get; private set; }
    public int Target => _to;
    /// <summary>Screenshots: the turn stops at this many seconds in until set back to a negative value.</summary>
    public double PinAt = -1;
    /// <summary>How much of the book the leaf covers now, 0..1 (tests and screenshots).</summary>
    public float Cover { get; private set; }

    public void Init(Main main)
    {
        _main = main;
        Layer = 30;
        _leaf = new Leaf { Name = "Leaf", Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        _leaf.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_leaf);
        SetProcess(false);
    }

    /// <summary>Starts turning to a chapter (Main.TurnTo checks that it is another one).</summary>
    public void Start(int chapter)
    {
        _to = chapter;
        _t = 0;
        _switched = false;
        _main.Tools.SetTool(Tool.None);
        // The old town is held from the first frame of the turn: under the leaf it must not tick on (a house growing,
        // boats moving, their sounds) while the page covers it; the new town is held again once it is built.
        _main.Sim.Held = true;
        _leaf.Heading1 = "Chapter " + Main.ChapterNumerals[chapter];
        _leaf.Heading2 = Main.ChapterNames[chapter];
        _leaf.Visible = true;
        SetProcess(true);
        Step(0);
    }

    public override void _Process(double delta) => Step(System.Math.Min(delta, 0.1));

    private void Step(double dt)
    {
        _t += dt;
        if (PinAt >= 0 && _t > PinAt) _t = PinAt;
        // The book's spread on screen (the rig is the new chapter's once it is built; the framing is kept).
        var cam = _main.Rig.Camera;
        var l = _main.LeftPageRect;
        var r = _main.RightPageRect;
        var a = cam.UnprojectPosition(new Vector3(l.Position.X, 0, l.Position.Y));
        var b = cam.UnprojectPosition(new Vector3(r.End.X - BookBuilder.StackSteps * 1.3f, 0, r.End.Y));
        _leaf.Book = new Rect2(a, b - a).Abs();
        float lx = _leaf.Book.Position.X, rx = _leaf.Book.End.X;
        if (_t < CoverSec)
        {
            float p = Ease((float)(_t / CoverSec));
            _leaf.From = rx - p * (rx - lx); _leaf.To = rx; _leaf.EdgeLeft = true;
            _leaf.HeadingAlpha = Mathf.Clamp((p - 0.6f) / 0.4f, 0, 1);
            Cover = p;
        }
        else if (_t < CoverSec + HoldSec)
        {
            if (!_switched)
            {
                _switched = true;
                _main.SwitchChapter(_to); // the frame this takes is hidden under the leaf
                _main.Sim.Held = true;    // the new town waits on day one until the page is open
            }
            _leaf.From = lx; _leaf.To = rx; _leaf.HeadingAlpha = 1;
            Cover = 1;
        }
        else if (_t < CoverSec + HoldSec + RevealSec)
        {
            float p = Ease((float)((_t - CoverSec - HoldSec) / RevealSec));
            _leaf.From = lx; _leaf.To = rx - p * (rx - lx); _leaf.EdgeLeft = false;
            _leaf.HeadingAlpha = Mathf.Clamp(1 - p * 2.5f, 0, 1);
            Cover = 1 - p;
        }
        else
        {
            _t = -1;
            Cover = 0;
            _main.Sim.Held = false;
            Turns++;
            _leaf.Visible = false;
            SetProcess(false);
            _main.Tools.Say($"Chapter {Main.ChapterNumerals[_main.Chapter]}: {Main.ChapterNames[_main.Chapter]}. A fresh town from seed {_main.Sim.Seed}.");
            return;
        }
        _leaf.QueueRedraw();
    }

    private static float Ease(float x) => x < 0.5f ? 2 * x * x : 1 - (-2 * x + 2) * (-2 * x + 2) / 2;

    /// <summary>The turning leaf: paper over [From, To] of the book's spread, the shadow and fold at its moving edge.</summary>
    public partial class Leaf : Control
    {
        public Rect2 Book;
        public float From, To, HeadingAlpha;
        public bool EdgeLeft = true;
        public string Heading1 = "", Heading2 = "";

        public override void _Draw()
        {
            float y0 = Book.Position.Y, h = Book.Size.Y;
            if (To - From < 0.5f || h < 1) return;
            float vpH = GetViewportRect().Size.Y;
            float s = Mathf.Max(0.6f, vpH / 1080f);
            DrawRect(new Rect2(From, y0, To - From, h), UiKit.Paper);
            // The moving edge: a soft shadow cast on the page beside it, then a fold line and a darker band of paper
            // curling up along the edge itself.
            float edge = EdgeLeft ? From : To, dir = EdgeLeft ? -1 : 1;
            for (int k = 0; k < 10; k++)
            {
                float x = edge + dir * (k + 0.5f) * 3f * s;
                DrawRect(new Rect2(Mathf.Min(x, x + dir * 3f * s), y0, 3f * s, h), new Color(UiKit.Ink, 0.22f * (1 - k / 10f)));
            }
            for (int k = 0; k < 8; k++)
            {
                float x = edge - dir * (k + 0.5f) * 4f * s;
                DrawRect(new Rect2(Mathf.Min(x, x - dir * 4f * s), y0, 4f * s, h), new Color(UiKit.Ink, 0.10f * (1 - k / 8f)));
            }
            DrawLine(new Vector2(edge, y0), new Vector2(edge, y0 + h), UiKit.Ink, Mathf.Max(1, 1.4f * s));
            // A hairline frame like the book's own page edges.
            DrawRect(new Rect2(From, y0, To - From, h).Grow(-14 * s), new Color(UiKit.Ink, 0.35f), false, Mathf.Max(1, s));
            if (HeadingAlpha <= 0) return;
            var font = UiKit.Display;
            int f1 = UiKit.FontPx(s, 64), f2 = UiKit.FontPx(s, 44);
            float cx = Book.Position.X + Book.Size.X / 2f, cy = y0 + h / 2f;
            var ink = new Color(UiKit.Ink, HeadingAlpha);
            float w1 = font.GetStringSize(Heading1, HorizontalAlignment.Left, -1, f1).X;
            float w2 = font.GetStringSize(Heading2, HorizontalAlignment.Left, -1, f2).X;
            DrawString(font, new Vector2(cx - w1 / 2, cy - 10 * s), Heading1, HorizontalAlignment.Left, -1, f1, ink);
            DrawString(font, new Vector2(cx - w2 / 2, cy + f2 + 4 * s), Heading2, HorizontalAlignment.Left, -1, f2, ink);
            DrawLine(new Vector2(cx - w1 * 0.6f, cy + 8 * s), new Vector2(cx + w1 * 0.6f, cy + 8 * s), ink, Mathf.Max(1, 1.5f * s));
        }
    }
}
