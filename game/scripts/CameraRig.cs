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
    /// <summary>View height of the play view: the map page with its title and a little paper round it.</summary>
    public float PlaySize => MapSize * 1.13f;

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
        _target = new Vector3(cx, 0, MapSize / 2f - 14f);
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
        if (instant) { Camera.Size = _targetSize; SetPivotLocal(_target); }
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
    /// Keeps the view centre inside the allowed area: the map plus a margin at play zoom, widening smoothly to the
    /// whole book as the view zooms out to it; when the view is wider than that area it is centred on it.
    /// Measured along the map's axes with the rotated view's bounding box, so it holds under yaw.
    /// </summary>
    private Vector3 Clamp(Vector3 p, float size)
    {
        var vp = ViewportSize;
        float hh = size / 2f, hw = hh * vp.X / Math.Max(1f, vp.Y);
        float yaw = Yaw.Rotation.Y, c = MathF.Abs(MathF.Cos(yaw)), s = MathF.Abs(MathF.Sin(yaw));
        float ex = hw * c + hh * s, ey = hw * s + hh * c;
        // Book rectangle in map space.
        var inv = MapXf.AffineInverse();
        var b0 = inv * (BookCentre - new Vector3(BookSize.X / 2f, 0, BookSize.Y / 2f));
        var b1 = inv * (BookCentre + new Vector3(BookSize.X / 2f, 0, BookSize.Y / 2f));
        float play = MapSize * 1.05f, book = Math.Max(BookSize.Y * 1.10f, play + 1f);
        float t = Mathf.Clamp((size - play) / (book - play), 0f, 1f);
        float lx = Mathf.Lerp(-PanMargin, Math.Min(b0.X, b1.X), t), hx = Mathf.Lerp(MapSize + PanMargin, Math.Max(b0.X, b1.X), t);
        float lz = Mathf.Lerp(-PanMargin, Math.Min(b0.Z, b1.Z), t), hz = Mathf.Lerp(MapSize + PanMargin, Math.Max(b0.Z, b1.Z), t);
        // A view wider than the area keeps the whole area in view (rather than centring), so the views can shift
        // the book aside for the interface.
        p.X = ex * 2 >= hx - lx ? Mathf.Clamp(p.X, hx - ex, lx + ex) : Mathf.Clamp(p.X, lx + ex, hx - ex);
        p.Z = ey * 2 >= hz - lz ? Mathf.Clamp(p.Z, hz - ey, lz + ey) : Mathf.Clamp(p.Z, lz + ey, hz - ey);
        p.Y = 0;
        return p;
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

    private void KeyboardPan(float dt)
    {
        // Arrow keys only: W is the wear overlay and E the eraser (Prototype 1 bindings), and while an interface
        // control has keyboard focus the arrows belong to it (moving focus, nudging a slider).
        if (GetViewport().GuiGetFocusOwner() != null) return;
        float x = 0, y = 0;
        if (Input.IsKeyPressed(Key.Left)) x -= 1;
        if (Input.IsKeyPressed(Key.Right)) x += 1;
        if (Input.IsKeyPressed(Key.Up)) y -= 1;
        if (Input.IsKeyPressed(Key.Down)) y += 1;
        if (x == 0 && y == 0) return;
        // Screen-relative: right is the yaw's +X, screen-up is the yaw's -Z, measured in map space.
        var inv = MapXf.AffineInverse().Basis;
        var right = inv * Yaw.GlobalTransform.Basis.X;
        var down = inv * Yaw.GlobalTransform.Basis.Z;
        var d = (right * x + down * y) with { Y = 0 };
        PanLocal(d.Normalized() * Camera.Size * 0.8f * dt);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (MapOrigin == null) return;
        switch (e)
        {
            case InputEventMouseButton mb when mb.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown:
                if (mb.Pressed && !mb.ShiftPressed) // Shift+wheel is reserved for tool variants
                {
                    ZoomAt(mb.Position, mb.ButtonIndex == MouseButton.WheelUp ? 0.85f : 1f / 0.85f);
                    GetViewport().SetInputAsHandled();
                }
                break;
            case InputEventMouseButton mb when mb.ButtonIndex is MouseButton.Middle or MouseButton.Right:
                _dragging = mb.Pressed;
                _dragLast = mb.Position;
                break;
            case InputEventMouseMotion mm when _dragging:
                if (PickLocal(_dragLast, out var a) && PickLocal(mm.Position, out var b)) PanLocal(a - b, instant: true);
                _dragLast = mm.Position;
                GetViewport().SetInputAsHandled();
                break;
            case InputEventKey { Pressed: true, Echo: false } key:
                if (key.Keycode == Key.Home && !key.ShiftPressed) { ToggleView(); GetViewport().SetInputAsHandled(); }
                else if (key.Keycode is Key.Plus or Key.Equal or Key.KpAdd or Key.Pageup) ZoomAt(ViewportSize / 2f, 0.8f);
                else if (key.Keycode is Key.Minus or Key.KpSubtract or Key.Pagedown) ZoomAt(ViewportSize / 2f, 1.25f);
                break;
        }
    }
}
