using System;
using System.Collections.Generic;
using Godot;

namespace TideToll.Game;

/// <summary>
/// Night 3 (style sheet section 7, "sails that turn"; asset review r1): a still mill looks dead, so every model node
/// named "Sails" under the scene turns slowly about its local Z at all times. TownBuildings turns the mill's sails
/// faster while the mill works by writing an absolute angle; this node runs after it (high process priority), takes
/// only the change of that angle since its last write and adds it to its own, so the two never fight and the sails
/// never jump. Nodes are found as they enter the tree (no polling); nothing is allocated per frame.
/// </summary>
public partial class MillSails : Node
{
    private const float IdleRate = 0.25f;                  // rad/s at speed 1 while the mill stands idle
    private static readonly StringName SailsName = "Sails";
    private readonly List<Node3D> _nodes = new();
    private readonly List<float> _own = new(), _theirs = new();
    private TownDriver? _drv;

    public void Init(TownDriver drv) => _drv = drv;

    public override void _Ready()
    {
        ProcessPriority = 1000;
        GetTree().NodeAdded += OnNodeAdded;
        foreach (var n in GetTree().Root.FindChildren("Sails", "Node3D", true, false)) OnNodeAdded(n);   // already there
    }

    public override void _ExitTree()
    {
        if (IsInsideTree()) GetTree().NodeAdded -= OnNodeAdded;
    }

    private void OnNodeAdded(Node n)
    {
        if (n is Node3D s && n.Name == SailsName && !_nodes.Contains(s))
        {
            _nodes.Add(s);
            _own.Add(s.Rotation.Z);
            _theirs.Add(s.Rotation.Z);
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        bool idle = _drv == null || !_drv.Paused;
        float speed = _drv == null ? 1f : (float)Math.Min(_drv.Speed, 4);
        for (int i = _nodes.Count - 1; i >= 0; i--)
        {
            var s = _nodes[i];
            if (!IsInstanceValid(s) || !s.IsInsideTree())
            {
                _nodes.RemoveAt(i); _own.RemoveAt(i); _theirs.RemoveAt(i);
                continue;
            }
            float z = s.Rotation.Z, a = _own[i];
            if (MathF.Abs(z - a) > 1e-5f)
            {
                // someone else (TownBuildings, while the mill works) wrote an angle: follow its step, not its value
                float step = Mathf.Wrap(z - _theirs[i], -MathF.PI, MathF.PI);
                _theirs[i] = z;
                a += step;
            }
            else if (idle) a += dt * IdleRate * speed;
            a %= MathF.PI * 2f;
            _own[i] = a;
            s.Rotation = new Vector3(s.Rotation.X, s.Rotation.Y, a);
        }
    }
}
