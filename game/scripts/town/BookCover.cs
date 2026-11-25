using System;
using Godot;

namespace TideToll.Game;

/// <summary>
/// The book's front board, hinged at the spine. Open, it is hidden (the book lies open as BookBuilder made it). Closing
/// shows it lying over the left page with its endpaper up, hides the left page and the left half of the base board
/// beneath, and swings it over the spine onto the right page: the book closes. Opening plays the same backwards.
/// <see cref="Amount"/> is 0 open, 1 closed; the swing eases in and out.
/// </summary>
public partial class BookCover : Node3D
{
    public const float Seconds = 0.8f;
    public static readonly Color Leather = Palette.SootBrick;

    private Node3D _hinge = null!, _leftPage = null!;
    private MeshInstance3D _base = null!;
    private Vector3 _baseScale, _basePos;
    private float _from, _to = 0f, _t = 1f;

    /// <summary>0 open, 1 closed (eased).</summary>
    public float Amount { get; private set; }
    public bool Moving => _t < 1f;
    public bool Closed => Amount >= 1f && !Moving;
    /// <summary>The closed cover's centre and size on the book (x, z), for the camera and the lettering.</summary>
    public static Vector3 ClosedCentre => new(BookBuilder.CoverSize.X / 4f, 0, 0);
    public static Vector2 ClosedSize => new(BookBuilder.CoverSize.X / 2f, BookBuilder.CoverSize.Y);

    private Node3D? _town;

    /// <summary>The board over the book; <paramref name="town"/> (the map's models) is hidden while the board lies on it.</summary>
    public void Init(Node3D book, Node3D? town = null)
    {
        _town = town;
        Name = "FrontCover";
        book.AddChild(this);
        _leftPage = book.GetNode<Node3D>("LeftPage");
        _base = book.GetNode<MeshInstance3D>("Cover");
        _baseScale = _base.Scale; _basePos = _base.Position;
        // The hinge sits on the spine a little above the pages, so the board clears them as it swings.
        _hinge = new Node3D { Name = "Hinge", Position = new Vector3(0, 3f, 0) };
        AddChild(_hinge);
        var size = ClosedSize;
        var board = new MeshInstance3D
        {
            Name = "Board", Mesh = new BoxMesh { Size = new Vector3(size.X, 4f, size.Y) },
            Position = new Vector3(size.X / 2f, 2f, 0), MaterialOverride = Palette.Matte(Leather),
        };
        _hinge.AddChild(board);
        // The endpaper on the inside face (down when closed, up while it lies open on the left).
        var paper = new MeshInstance3D
        {
            Name = "Endpaper", Mesh = new BoxMesh { Size = new Vector3(size.X - 10f, 0.4f, size.Y - 10f) },
            Position = new Vector3(size.X / 2f, -0.25f, 0), MaterialOverride = Palette.Matte(Palette.Paper.Darkened(0.04f)),
        };
        _hinge.AddChild(paper);
        // Gilt rules tooled into the leather (the lettering itself is the menu's, drawn over it).
        var gilt = Palette.Matte(PauseMenu.Gilt);
        float inset = 16f, w = 1.6f;
        AddRule(board, gilt, new Vector3(size.X - 2 * inset, 0.3f, w), new Vector3(0, 2.1f, -(size.Y / 2f - inset)));
        AddRule(board, gilt, new Vector3(size.X - 2 * inset, 0.3f, w), new Vector3(0, 2.1f, size.Y / 2f - inset));
        AddRule(board, gilt, new Vector3(w, 0.3f, size.Y - 2 * inset), new Vector3(-(size.X / 2f - inset), 2.1f, 0));
        AddRule(board, gilt, new Vector3(w, 0.3f, size.Y - 2 * inset), new Vector3(size.X / 2f - inset, 2.1f, 0));
        Apply();
    }

    private static void AddRule(Node3D parent, Material m, Vector3 size, Vector3 pos)
        => parent.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, Position = pos, MaterialOverride = m });

    public void Close(bool instant = false) => Go(1f, instant);
    /// <summary>Holds the board part way (screenshots): 0 open, 1 closed.</summary>
    public void Hold(float amount) { _t = 1f; _from = _to = Amount = Math.Clamp(amount, 0f, 1f); Apply(); }
    public void Open(bool instant = false) => Go(0f, instant);

    private void Go(float to, bool instant)
    {
        _from = Amount; _to = to; _t = instant ? 1f : 0f;
        if (instant) Amount = to;
        Apply();
    }

    public override void _Process(double delta)
    {
        if (_t >= 1f) return;
        _t = Math.Min(1f, _t + (float)delta / Seconds);
        float e = _t * _t * (3f - 2f * _t);
        Amount = Mathf.Lerp(_from, _to, e);
        Apply();
    }

    private void Apply()
    {
        bool open = Amount <= 0f && !Moving;
        Visible = !open;
        _leftPage.Visible = open;
        // Trees and buildings stand taller than the hinge: hide them once the board is nearly down on them.
        if (_town != null) _town.Visible = Amount < 0.8f;
        // The left half of the base board goes with the front board.
        _base.Scale = open ? _baseScale : new Vector3(_baseScale.X * 0.5f, _baseScale.Y, _baseScale.Z);
        _base.Position = open ? _basePos : _basePos + new Vector3(BookBuilder.CoverSize.X / 4f, 0, 0);
        // 180 degrees: lying open on the left, endpaper up; 0: closed over the right page.
        _hinge.RotationDegrees = new Vector3(0, 0, 180f * (1f - Amount));
    }
}
