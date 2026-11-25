using System;
using Godot;

namespace TideToll.Game;

/// <summary>
/// Pivot on the ground, then yaw, then pitch, then an orthographic camera (Engine tab, "3D from day one").
/// The prototype keeps it north-up and straight down, but nothing here assumes that: panning, limits, zoom to
/// the cursor and picking all work in the map's own space through general ray-plane maths (rendering proof 6),
/// so switching on yaw or tilt later needs no change. Motion is smoothed toward targets.
/// Input (unhandled, so UI controls get it first): middle or right drag pans, the arrow keys pan,
/// the wheel zooms at the cursor, + and - (and Page Up / Page Down) zoom at the centre, Home switches between
/// the whole book and the map page.
/// </summary>
public partial class CameraRig : Node3D
{
    public Node3D Yaw { get; private set; } = null!;
    public Node3D Pitch { get; private set; } = null!;
    public Camera3D Camera { get; private set; } = null!;

    public float Distance = 300f;
    public float PitchDegrees = -90f;
    /// <summary>
    /// Tilt with zoom (round 2): at view heights up to <see cref="TiltFullSize"/> the camera looks down at
    /// <see cref="TiltDegrees"/> so buildings and villagers show their sides; it flattens smoothly (smoothstep on the
    /// view height) and is straight down from <see cref="TiltFlatSize"/> (the whole page) out. 90 turns tilting off.
    /// </summary>
    public float TiltDegrees = 56f;
    public float TiltFullSize = 70f, TiltFlatSize = 300f;
    /// <summary>The pitch (degrees below the horizon) for a view height.</summary>
    public float PitchFor(float size)
    {
        float t = Mathf.Clamp((size - TiltFullSize) / Math.Max(1f, TiltFlatSize - TiltFullSize), 0f, 1f);
        t = t * t * (3f - 2f * t);
        return Mathf.Lerp(TiltDegrees, 90f, t);
    }
    /// <summary>The pitch in use now, degrees below the horizon (90 = straight down).</summary>
    public float PitchNow => -Pitch.RotationDegrees.X;
    /// <summary>The view height the zoom eases toward.</summary>
    public float TargetSize => _targetSize;
    /// <summary>Vertical extent of the orthographic view (Camera.Size), metres: limits.</summary>
    public float MinSize = 28f, MaxSize = 440f;
    /// <summary>Metres of map that may leave the screen edge when panned to the limit.</summary>
    public float PanMargin = 24f;
    /// <summary>How fast the view eases toward its target (1/s).</summary>
    public float Smoothing = 12f;

    /// <summary>The map's north-west corner (Book/RightPage/MapOrigin), map cells and their size in metres.</summary>
    public Node3D MapOrigin = null!;
    public int MapCells = 128;
    public float CellMetres = 2f;
    /// <summary>The whole book (cover) size in metres and its centre in world space, for the book view.</summary>
    public Vector2 BookSize = new(618f, 346f);
    public Vector3 BookCentre = Vector3.Zero;

    /// <summary>
    /// Screen pixels kept free to the right of the page edge in the map and book views, for the interface's bookmark
    /// tabs (GameUi sets it); the views shift the book left to make the room, showing more of the chapter page.
    /// </summary>
    public float PlayRoomPx = 0f;
    /// <summary>
    /// View height of the play view: the map page with its title and compass (about 30 m above the map) and the
    /// frame's outer rule (5 m below it) fully in view, with a few metres of paper round them.
    /// </summary>
    public float PlaySize => MapSize * 1.2f;
    /// <summary>Map-local z of the play view's centre: the middle of the compass top (-30 m) and the frame bottom.</summary>
    public float PlayCentreZ => (MapSize + 5f - 30f) / 2f;

    public bool InPlayView { get; private set; }
    public float MapSize => MapCells * CellMetres;
    /// <summary>Paper between the map's edge and the page's outer edge, metres (BookBuilder.Margin).</summary>
    public float BookBuilderPageMargin = 22f;

    private float _targetSize;
    private Vector3 _target;            // pivot target, map-local metres (y = 0)
    private bool _anchored;             // zooming at the cursor: keep _anchorLocal under _anchorScreen
    private Vector2 _anchorScreen;
    private Vector3 _anchorLocal;
    private bool _dragging;
    /// <summary>A middle or right drag is panning the view.</summary>
    public bool Dragging => _dragging;
    private Vector2 _dragLast;
    private bool _leftDown;
    private Vector2 _leftFrom;
    /// <summary>The player moved the view (drag, keys or wheel): the town game stops following the town until Home.</summary>
    public bool UserMoved;
    /// <summary>The last left press turned into a drag (so its release is not a click).</summary>
    public bool LeftDragged { get; private set; }
    /// <summary>Left-button drag pans too (the town game: there is no drawing tool on the left button).</summary>
    public bool LeftDragPans = false;
    /// <summary>Pixels a left press must move before it is a drag.</summary>
    public float DragThresholdPx = 6f;

    private static bool Act(string action, Key fallback)
        => InputMap.HasAction(action) ? Input.IsActionPressed(action) : Input.IsKeyPressed(fallback);
    private static bool ActEvent(InputEvent e, string action) => InputMap.HasAction(action) && e.IsActionPressed(action);

    public override void _Ready()
    {
        Yaw = new Node3D { Name = "Yaw" };
        AddChild(Yaw);
        Pitch = new Node3D { Name = "Pitch", RotationDegrees = new Vector3(PitchDegrees, 0, 0) };
        Yaw.AddChild(Pitch);
        Camera = new Camera3D
        {
            Name = "Camera",
            Projection = Camera3D.ProjectionType.Orthogonal,
            Size = 380f,
            Position = new Vector3(0, 0, Distance),
            // Tight near/far: the ink pass reads depth differences, so precision matters (proof 4).
            Near = Distance - 100f,
            Far = Distance + 100f,
            Current = true,
        };
        Pitch.AddChild(Camera);
        _targetSize = Camera.Size;
    }

    private Vector2 ViewportSize => GetViewport().GetVisibleRect().Size;
    private Transform3D MapXf => MapOrigin.GlobalTransform;

    // ------------------------------------------------------------------ picking

    /// <summary>
    /// The point of the map plane (y = 0 in MapOrigin's space) under a screen position, in map-local metres.
    /// A ray-plane test in map space: right for any yaw, pitch, zoom, book placement and for perspective too.
    /// </summary>
    public bool PickLocal(Vector2 screen, out Vector3 local)
    {
        var inv = MapXf.AffineInverse();
        var o = inv * Camera.ProjectRayOrigin(screen);
        var d = inv.Basis * Camera.ProjectRayNormal(screen); // a direction: basis only, never the translation
        local = default;
        if (MathF.Abs(d.Y) < 1e-6f) return false;
        float t = -o.Y / d.Y;
        if (t < 0 && Camera.Projection == Camera3D.ProjectionType.Perspective) return false;
        local = o + d * t;
        return true;
    }

    /// <summary>The map cell under a screen position; false (and -1, -1) when the point is off the map.</summary>
    public bool PickCell(Vector2 screen, out int cx, out int cy)
    {
        cx = cy = -1;
        if (!PickLocal(screen, out var l)) return false;
        int x = (int)MathF.Floor(l.X / CellMetres), y = (int)MathF.Floor(l.Z / CellMetres);
        if (x < 0 || y < 0 || x >= MapCells || y >= MapCells) return false;
        cx = x; cy = y;
        return true;
    }

    /// <summary>Screen position of a map-local point (inverse of picking), for tests and screen labels.</summary>
    public Vector2 LocalToScreen(Vector3 local) => Camera.UnprojectPosition(MapXf * local);

    // ------------------------------------------------------------------ views

    /// <summary>The whole open book in view, whatever the window shape.</summary>
    public void FrameBook(bool instant = false)
    {
        var vp = ViewportSize;
        float aspect = vp.X / Math.Max(1f, vp.Y);
        // Wide enough for the book plus the tabs' room on its right: size * aspect = book + room * size / height.
        float room = PlayRoomPx / Math.Max(1f, vp.Y);
        _targetSize = Math.Min(MaxSize, Math.Max(BookSize.Y * 1.10f, BookSize.X * 1.03f / Math.Max(0.3f, aspect - room)));
        _target = MapXf.AffineInverse() * (BookCentre + new Vector3(room * _targetSize / 2f, 0, 0));
        _target.Y = 0;
        InPlayView = false;
        Finish(instant);
    }

    /// <summary>
    /// The play view: the map page fills the screen height with its title in view; the book sits as far right as the
    /// tabs' room allows (<see cref="PlayRoomPx"/>), so the chapter page shows on the left. Centred on narrow screens.
    /// </summary>
    public void FramePlay(bool instant = false)
    {
        var vp = ViewportSize;
        _targetSize = PlaySize;
        float hw = _targetSize / 2f * vp.X / Math.Max(1f, vp.Y);
        float pageEdge = MapSize + (BookBuilderPageMargin + 6f);
        float cx = pageEdge + PlayRoomPx * _targetSize / Math.Max(1f, vp.Y) - hw;
        cx = Mathf.Clamp(cx, MapSize / 2f - Math.Max(0f, hw - MapSize / 2f - 6f), MapSize / 2f);
        _target = new Vector3(cx, 0, PlayCentreZ);
        InPlayView = true;
        Finish(instant);
    }

    public void ToggleView() { if (InPlayView) FrameBook(); else FramePlay(); }

    /// <summary>Look at a map-local point with a given view height (tests and tools).</summary>
    public void LookAt(Vector3 local, float size, bool instant = true)
    {
        _targetSize = Mathf.Clamp(size, MinSize, MaxSize);
        _target = new Vector3(local.X, 0, local.Z);
        Finish(instant);
    }

    private void Finish(bool instant)
    {
        _anchored = false;
        _target = Clamp(_target, _targetSize);
        if (instant) { Camera.Size = _targetSize; ApplyPitch(); SetPivotLocal(_target); }
    }

    /// <summary>Zoom by a factor keeping the map point under a screen position fixed (wheel).</summary>
    public void ZoomAt(Vector2 screen, float factor)
    {
        _targetSize = Mathf.Clamp(_targetSize * factor, MinSize, MaxSize);
        if (PickLocal(screen, out _anchorLocal)) { _anchored = true; _anchorScreen = screen; }
    }

    /// <summary>Pan by a map-local offset in metres.</summary>
    public void PanLocal(Vector3 delta, bool instant = false)
    {
        _anchored = false;
        _target = Clamp(_target + delta, _targetSize);
        if (instant) SetPivotLocal(Clamp(PivotLocal + delta, Camera.Size));
    }

    public Vector3 PivotLocal { get { var p = MapXf.AffineInverse() * GlobalPosition; p.Y = 0; return p; } }
    private void SetPivotLocal(Vector3 p) => GlobalPosition = MapXf * new Vector3(p.X, 0, p.Z);

    /// <summary>
    /// Where the view centre may be, for a view of height <paramref name="size"/>. Two rules, both must hold:
    /// 1. the centre stays over the map plus PanMargin at play zoom, an area that widens smoothly to the whole book as
    ///    the view zooms out to it (you can never lose the map);
    /// 2. the view stays on the book (on its right with the tabs' room, <see cref="PlayRoomPx"/>, added), or, when it
    ///    is wider than that, keeps the whole book in view.
    /// Zooming in toward any point over the map always satisfies both (the centre moves toward the point, the view
    /// shrinks), so zoom at the cursor holds there; only zooming out at the very edge can be stopped.
    /// Measured along the map's axes with the rotated view's bounding box, so it holds under yaw.
    /// </summary>
    private Vector3 Clamp(Vector3 p, float size)
    {
        var vp = ViewportSize;
        float h = Math.Max(1f, vp.Y);
        float hh = size / 2f / MathF.Sin(Mathf.DegToRad(PitchFor(size))), hw = size / 2f * vp.X / h;
        float yaw = Yaw.Rotation.Y, c = MathF.Abs(MathF.Cos(yaw)), s = MathF.Abs(MathF.Sin(yaw));
        float ex = hw * c + hh * s, ey = hw * s + hh * c;
        // Book rectangle in map space.
        var inv = MapXf.AffineInverse();
        var b0 = inv * (BookCentre - new Vector3(BookSize.X / 2f, 0, BookSize.Y / 2f));
        var b1 = inv * (BookCentre + new Vector3(BookSize.X / 2f, 0, BookSize.Y / 2f));
        float bx0 = Math.Min(b0.X, b1.X), bx1 = Math.Max(b0.X, b1.X), bz0 = Math.Min(b0.Z, b1.Z), bz1 = Math.Max(b0.Z, b1.Z);
        float play = MapSize * 1.05f, book = Math.Max(BookSize.Y * 1.10f, play + 1f);
        float t = Mathf.Clamp((size - play) / (book - play), 0f, 1f);
        float lx = Mathf.Lerp(-PanMargin, bx0, t), hx = Mathf.Lerp(MapSize + PanMargin, bx1, t);
        float lz = Mathf.Lerp(-PanMargin, bz0, t), hz = Mathf.Lerp(MapSize + PanMargin, bz1, t);
        float room = PlayRoomPx * size / h; // metres of table right of the book kept for the tabs
        p.X = Axis(p.X, lx, hx, bx0, bx1 + room, ex);
        p.Z = Axis(p.Z, lz, hz, bz0, bz1, ey);
        p.Y = 0;
        return p;
    }

    // One axis of Clamp: centre in [lo, hi] (rule 1) and the view's half extent e within [b0, b1] (rule 2).
    private static float Axis(float v, float lo, float hi, float b0, float b1, float e)
    {
        float r0, r1;
        if (2 * e <= b1 - b0) { r0 = b0 + e; r1 = b1 - e; }   // narrower than the book: stay on it
        else { r0 = b1 - e; r1 = b0 + e; }                     // wider: keep all of it in view
        float a = Math.Max(lo, r0), b = Math.Min(hi, r1);
        return a <= b ? Mathf.Clamp(v, a, b) : Mathf.Clamp(v, r0, r1);
    }

    // ------------------------------------------------------------------ per frame

    public override void _Process(double delta)
    {
        if (MapOrigin == null) return;
        float dt = (float)Math.Min(delta, 0.1);
        KeyboardPan(dt);
        float k = 1f - MathF.Exp(-Smoothing * dt);
        if (MathF.Abs(Camera.Size - _targetSize) > 1e-3f) Camera.Size = Mathf.Lerp(Camera.Size, _targetSize, k);
        else Camera.Size = _targetSize;
        ApplyPitch();
        if (_anchored)
        {
            // Re-solve the pivot so the anchored map point stays under the cursor while the size eases.
            if (PickLocal(_anchorScreen, out var now))
            {
                var p = Clamp(PivotLocal + (_anchorLocal - now), Camera.Size);
                SetPivotLocal(p);
                _target = Clamp(p, _targetSize);
            }
            if (Camera.Size == _targetSize) _anchored = false;
        }
        else
        {
            var p = PivotLocal;
            SetPivotLocal(p.DistanceTo(_target) < 1e-3f ? _target : p.Lerp(_target, k));
        }
    }

    private void ApplyPitch()
    {
        float p = PitchFor(Camera.Size);
        if (MathF.Abs(PitchNow - p) > 1e-4f) Pitch.RotationDegrees = new Vector3(-p, 0, 0);
    }

    private void KeyboardPan(float dt)
    {
        // Arrow keys only: W is the wear overlay and E the eraser (Prototype 1 bindings), and while an interface
        // control has keyboard focus the arrows belong to it (moving focus, nudging a slider).
        if (GetViewport().GuiGetFocusOwner() != null) return;
        float x = 0, y = 0;
        if (Act("tt_move_left", Key.Left)) x -= 1;
        if (Act("tt_move_right", Key.Right)) x += 1;
        if (Act("tt_move_up", Key.Up)) y -= 1;
        if (Act("tt_move_down", Key.Down)) y += 1;
        if (x == 0 && y == 0) return;
        UserMoved = true;
        // Screen-relative: right is the yaw's +X, screen-up is the yaw's -Z, measured in map space.
        var inv = MapXf.AffineInverse().Basis;
        var right = inv * Yaw.GlobalTransform.Basis.X;
        var down = inv * Yaw.GlobalTransform.Basis.Z;
        var d = (right * x + down * y) with { Y = 0 };
        PanLocal(d.Normalized() * Camera.Size * 0.8f * dt);
    }

    /// <summary>When set, Home calls this instead of switching between the book and the page (the town game: back to the town).</summary>
    public System.Action? HomePressed;

    public override void _UnhandledInput(InputEvent e)
    {
        if (MapOrigin == null) return;
        switch (e)
        {
            case InputEventMouseButton mb when mb.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown:
                if (mb.Pressed && !mb.ShiftPressed) // Shift+wheel is reserved for tool variants
                {
                    ZoomAt(mb.Position, mb.ButtonIndex == MouseButton.WheelUp ? 0.85f : 1f / 0.85f);
                    UserMoved = true;
                    GetViewport().SetInputAsHandled();
                }
                break;
            case InputEventMouseButton mb when mb.ButtonIndex is MouseButton.Middle or MouseButton.Right:
                _dragging = mb.Pressed;
                _dragLast = mb.Position;
                break;
            case InputEventMouseButton mb when mb.ButtonIndex == MouseButton.Left && LeftDragPans:
                if (mb.Pressed) { _leftDown = true; _leftFrom = mb.Position; _dragLast = mb.Position; LeftDragged = false; }
                else { _leftDown = false; if (_dragging && !Input.IsMouseButtonPressed(MouseButton.Right) && !Input.IsMouseButtonPressed(MouseButton.Middle)) _dragging = false; }
                break;
            case InputEventMouseMotion mm when _leftDown && !_dragging && mm.Position.DistanceTo(_leftFrom) > DragThresholdPx:
                _dragging = true;
                LeftDragged = true;
                if (PickLocal(_dragLast, out var a0) && PickLocal(mm.Position, out var b0)) PanLocal(a0 - b0, instant: true);
                _dragLast = mm.Position;
                UserMoved = true;
                GetViewport().SetInputAsHandled();
                break;
            case InputEventMouseMotion mm when _dragging:
                if (PickLocal(_dragLast, out var a) && PickLocal(mm.Position, out var b)) PanLocal(a - b, instant: true);
                _dragLast = mm.Position;
                UserMoved = true;
                GetViewport().SetInputAsHandled();
                break;
            case InputEventKey { Pressed: true, Echo: false } key:
                if (HomePressed != null && (ActEvent(key, "tt_home_view") || key.Keycode == Key.Home)) { HomePressed(); GetViewport().SetInputAsHandled(); }
                else if (key.Keycode == Key.Home && !key.ShiftPressed) { ToggleView(); GetViewport().SetInputAsHandled(); }
                else if (ActEvent(key, "tt_zoom_in") || key.Keycode is Key.Plus or Key.Equal or Key.KpAdd or Key.Pageup) { ZoomAt(ViewportSize / 2f, 0.8f); UserMoved = true; }
                else if (ActEvent(key, "tt_zoom_out") || key.Keycode is Key.Minus or Key.KpSubtract or Key.Pagedown) { ZoomAt(ViewportSize / 2f, 1.25f); UserMoved = true; }
                break;
        }
    }
}
