using System;
using System.Collections.Generic;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Round 5: the small touches the town dresses itself with (Town.Touch(i): woodpiles, washing lines, flower beds, hen
/// coops, benches, beehives, haystacks, apple trees, handcarts, signposts; assets/models/prop-*.glb named after the
/// simulation's TouchKind). Each stands at the spot the simulation gives (already a cell centre), turned to its plot,
/// and pops in gently when it arrives; a town loaded or replaced shows its touches at once.
/// </summary>
public partial class TownTouches : Node3D
{
    private TownDriver _drv = null!;
    private readonly List<Node3D> _items = new();
    private readonly List<float> _pop = new();
    private readonly List<float> _scale = new();
    /// <summary>Seconds a new touch takes to pop in.</summary>
    public const float PopSeconds = 0.7f;

    public int Shown => _items.Count;
    /// <summary>Touches still popping in (tests and screenshots wait for 0).</summary>
    public int Popping { get { int n = 0; foreach (var p in _pop) if (p < 1f) n++; return n; } }
    public Node3D? Item(int i) => i >= 0 && i < _items.Count ? _items[i] : null;

    public void Init(TownDriver drv)
    {
        _drv = drv;
        Name = "Touches";
        drv.Replaced += Clear;
    }

    public override void _ExitTree() { if (_drv != null) _drv.Replaced -= Clear; }

    private void Clear()
    {
        foreach (var n in _items) { RemoveChild(n); n.QueueFree(); }
        _items.Clear(); _pop.Clear(); _scale.Clear();
    }

    public static string ModelOf(TouchKind k) => k switch
    {
        TouchKind.Woodpile => "prop-woodpile", TouchKind.WashingLine => "prop-washing-line", TouchKind.FlowerBed => "prop-flower-bed",
        TouchKind.HenCoop => "prop-hen-coop", TouchKind.Bench => "prop-bench", TouchKind.Beehives => "prop-beehives",
        TouchKind.Haystack => "prop-haystack", TouchKind.AppleTree => "prop-apple-tree", TouchKind.Handcart => "prop-handcart",
        TouchKind.Signpost => "prop-signpost", _ => "prop-woodpile",
    };

    /// <summary>Footprint of each kind in cells (its longer side on the ground).</summary>
    private static float CellsOf(TouchKind k) => k switch
    {
        TouchKind.AppleTree => 1.5f, TouchKind.Haystack => 1.1f, TouchKind.WashingLine => 1.3f, TouchKind.Signpost => 0.6f,
        TouchKind.Bench => 0.9f, TouchKind.FlowerBed => 1.0f, _ => 0.85f,
    };

    public override void _Process(double delta)
    {
        var t = _drv.Town;
        bool instant = _items.Count == 0 && _drv.StepsRun == 0;
        if (t.TouchCount < _items.Count) Clear();
        while (_items.Count < t.TouchCount)
        {
            var v = t.Touch(_items.Count);
            string model = ModelLib.Pick(ModelOf(v.Kind), "prop-woodpile");
            var body = new Node3D { Name = "Touch" + _items.Count };
            float s = 1f;
            if (ModelLib.Has(model))
            {
                var mi = ModelLib.Instance(model, "Model");
                var a = ModelLib.Mesh(model).GetAabb();
                float side = Math.Max(0.05f, Math.Max(a.Size.X, a.Size.Z));
                s = CellsOf(v.Kind) * TownGround.Cell / side;
                body.AddChild(mi);
            }
            body.Position = new Vector3((float)v.X * TownGround.Cell, 0, (float)v.Y * TownGround.Cell);
            body.Rotation = new Vector3(0, -(float)v.Heading + MathF.PI / 2f, 0);
            AddChild(body);
            _items.Add(body);
            _scale.Add(s);
            float p = instant || _drv.StepsRun == 0 ? 1f : 0.0001f;
            _pop.Add(p);
            body.Scale = Vector3.One * s * Ease(p);
        }
        float dt = (float)delta;
        for (int i = 0; i < _items.Count; i++)
        {
            if (_pop[i] >= 1f) continue;
            _pop[i] = Math.Min(1f, _pop[i] + dt / PopSeconds);
            _items[i].Scale = Vector3.One * _scale[i] * Ease(_pop[i]);
        }
    }

    /// <summary>A gentle overshoot: up to a little more than full size, then settles.</summary>
    private static float Ease(float p)
    {
        const float c = 1.4f;
        float x = p - 1f;
        return Math.Max(0.001f, 1f + (c + 1f) * x * x * x + c * x * x);
    }
}
