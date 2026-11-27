using System;
using Godot;

namespace TideToll.Game;

/// <summary>
/// The book's front board, hinged at the spine. The left half of the open book (the left page, its stack of page edges
/// and the left half of the binding under them) turns over the spine as one piece onto the right page: the book
/// closes. Nothing is swapped, lifted or resized along the way: at <see cref="Amount"/> 0 the turning half lies exactly
/// where the open book's left half lies, at 1 it lies face down on the right page with the board's outer face, its gilt
/// rules tooled into the leather, on top. The town's models (everything on the map) sink flat into the page while the
/// board comes over them, so no tree or roof pokes through it; they are hidden only once the book is shut and still.
/// PauseMenu drives it frame by frame through <see cref="Set"/> (one BookMotion timeline); <see cref="Open"/>,
/// <see cref="Close"/> and <see cref="Hold"/> are for tests and screenshots.
/// </summary>
public partial class BookCover : Node3D
{
    public const float Seconds = 0.8f;
    public static readonly Color Leather = Palette.SootBrick;
    /// <summary>Height of the hinge above the page, metres: the closed left page lies this twice over the right page.</summary>
    public const float HingeY = 0.3f;
    /// <summary>The town's models sink between these amounts of the swing (the board is then over them).</summary>
    public const float SinkFrom = 0.4f, SinkTo = 0.95f;
    /// <summary>The models' height at the bottom of their sinking (a share of their own).</summary>
    public const float SunkScale = 0.01f;

    private Node3D _hinge = null!, _leftPage = null!;
    private float _from, _to = 0f, _t = 1f;

    /// <summary>0 open, 1 closed.</summary>
    public float Amount { get; private set; }
    /// <summary>The board is moving: its own swing (Open/Close) or a transition driving it (<see cref="Driven"/>).</summary>
    public bool Moving => _t < 1f || Driven;
    /// <summary>PauseMenu sets this while a transition moves the board.</summary>
    public bool Driven { get; set; }
    public bool Closed => Amount >= 1f && !Moving;
    /// <summary>The closed cover's centre and size on the book (x, z), for the camera and the lettering.</summary>
    public static Vector3 ClosedCentre => new(BookBuilder.CoverSize.X / 4f, 0, 0);
    public static Vector2 ClosedSize => new(BookBuilder.CoverSize.X / 2f, BookBuilder.CoverSize.Y);
    /// <summary>Height of the shut board's top face above the page (the hinge mirrors the binding's underside).</summary>
    public const float ClosedTop = 2 * HingeY + BookBuilder.PageThickness + 4f;

    private Node3D? _town;
    private Transform3D _leftHome;
    /// <summary>Whether the left page (the town's summary) is drawn now (tests: W10).</summary>
    public bool LeftPageShown => _leftPage.Visible;
    /// <summary>The town's models' height now, 1 standing, <see cref="SunkScale"/> sunk (tests).</summary>
    public float TownScale => _town?.Scale.Y ?? 1f;
    /// <summary>The board's angle above the right page, degrees: 180 lying open on the left, 0 shut.</summary>
    public float AngleDegrees => 180f * (1f - Amount);

    /// <summary>The board over the book; <paramref name="town"/> (the map's models) sinks while the board comes over it.</summary>
    public void Init(Node3D book, Node3D? town = null)
    {
        _town = town;
        Name = "FrontCover";
        book.AddChild(this);
        _leftPage = book.GetNode<Node3D>("LeftPage");
        _leftHome = _leftPage.Transform;
        // The binding under the pages becomes two halves: the right one stays (the back board), the left one is the
        // front board and turns with the left page. Same box, same material, edge to edge: the open book looks the same.
        var base_ = book.GetNode<MeshInstance3D>("Cover");
        var size = ClosedSize;
        var basePos = base_.Position;
        base_.Scale = new Vector3(base_.Scale.X * 0.5f, base_.Scale.Y, base_.Scale.Z);
        base_.Position = basePos + new Vector3(size.X / 2f, 0, 0);
        _hinge = new Node3D { Name = "Hinge", Position = new Vector3(0, HingeY, 0) };
        AddChild(_hinge);
        var board = new MeshInstance3D
        {
            Name = "Board", Mesh = new BoxMesh { Size = new Vector3(size.X, 4f, size.Y) },
            Position = new Vector3(-size.X / 2f, basePos.Y - HingeY, 0), MaterialOverride = BookBuilder.LeatherMaterial(size),
        };
        _hinge.AddChild(board);
        // Gilt rules tooled into the board's outer face (its underside while the book lies open, inside the table there;
        // on top once it is shut). The lettering itself is the menu's, drawn over it.
        var gilt = Palette.Matte(PauseMenu.Gilt);
        float inset = 16f, w = 1.6f, y = -2.2f;
        AddRule(board, gilt, new Vector3(size.X - 2 * inset, 0.2f, w), new Vector3(0, y, -(size.Y / 2f - inset)));
        AddRule(board, gilt, new Vector3(size.X - 2 * inset, 0.2f, w), new Vector3(0, y, size.Y / 2f - inset));
        AddRule(board, gilt, new Vector3(w, 0.2f, size.Y - 2 * inset), new Vector3(-(size.X / 2f - inset), y, 0));
        AddRule(board, gilt, new Vector3(w, 0.2f, size.Y - 2 * inset), new Vector3(size.X / 2f - inset, y, 0));
        BookCoverDress.Dress(board, size);
        Apply();
    }

    private static void AddRule(Node3D parent, Material m, Vector3 size, Vector3 pos)
        => parent.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, Position = pos, MaterialOverride = m });

    public void Close(bool instant = false) => Go(1f, instant);
    /// <summary>Holds the board part way (tests): 0 open, 1 closed.</summary>
    public void Hold(float amount) { _sinkSet = false; _t = 1f; _from = _to = Amount = Math.Clamp(amount, 0f, 1f); Apply(); }
    public void Open(bool instant = false) => Go(0f, instant);

    /// <summary>A transition's frame: the board at this amount (0 open, 1 shut).</summary>
    public void Set(float amount)
    {
        _t = 1f;
        _from = _to = Amount = Math.Clamp(amount, 0f, 1f);
        Apply();
    }

    /// <summary>
    /// A transition's frame with the models' sinking on its own eased window (0 standing, 1 sunk): PauseMenu times it
    /// so they are down before the board is low over them, without hurrying while the board falls fastest.
    /// </summary>
    public void Set(float amount, float sink)
    {
        _sink = Math.Clamp(sink, 0f, 1f);
        _sinkSet = true;
        Set(amount);
    }

    private float _sink;
    private bool _sinkSet;
    /// <summary>How far the town's models have sunk, 0..1 (tests).</summary>
    public float Sink => _sinkSet ? _sink : BookMotion.Smooth((Amount - SinkFrom) / (SinkTo - SinkFrom));

    private void Go(float to, bool instant)
    {
        _sinkSet = false;
        _from = Amount; _to = to; _t = instant ? 1f : 0f;
        if (instant) Amount = to;
        Apply();
    }

    public override void _Process(double delta)
    {
        if (_t >= 1f) return;
        _t = Math.Min(1f, _t + (float)delta / Seconds);
        Amount = Mathf.Lerp(_from, _to, BookMotion.Smoother(_t));
        Apply();
    }

    /// <summary>Call when <see cref="Driven"/> ends: the board at rest (the hidden parts under a shut book).</summary>
    public void Rest() { Driven = false; _sink = Amount >= 1f ? 1f : Amount <= 0f ? 0f : _sink; Apply(); }

    private void Apply()
    {
        // Shut and still: the left page and the town lie under the board, out of sight; they are hidden only then.
        bool shut = Amount >= 1f && !Moving;
        _leftPage.Visible = !shut;
        var h = _hinge.Position;
        var turn = new Transform3D(new Basis(new Vector3(0, 0, 1), Mathf.DegToRad(-180f * Amount)), Vector3.Zero);
        _leftPage.Transform = new Transform3D(Basis.Identity, h) * turn * new Transform3D(Basis.Identity, -h) * _leftHome;
        _hinge.RotationDegrees = new Vector3(0, 0, -180f * Amount);
        if (_town != null)
        {
            float sink = Sink;
            float sy = BookMotion.Mix(1f, SunkScale, sink);
            if (_town.Scale.Y != sy) _town.Scale = new Vector3(1f, sy, 1f);
            _town.Visible = !shut;
        }
    }
}
