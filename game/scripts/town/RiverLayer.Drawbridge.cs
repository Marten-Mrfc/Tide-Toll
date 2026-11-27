using System;
using Godot;

namespace TideToll.Game;

/// <summary>
/// Round n3 (asset track RV): the drawbridge drawn from its model's parts (drawbridge.py): the frame (pier and the two
/// towers) stands still, the leaf and the balance beam turn by the same angle about their pivots, the chains follow
/// the beam's tip (beam, leaf and chains are a parallelogram, so the chains hang straight), and the signal arm stands
/// up while the leaf is raised. Before this round the whole model, towers and all, was stretched and tipped as one.
/// </summary>
public partial class RiverLayer
{
    private const string LiftModel = "drawbridge-lift";
    // Model frame (drawbridge.py): hinge at (0, DECK, HZ), beam pivot DECK + H above the deck, the leaf LEAF long.
    private const float DbDeck = 0.9f, DbH = 3.6f, DbHz = 2f, DbLeaf = 6f;
    private Node3D? _dbBeam, _dbChains, _dbSignal;

    /// <summary>The lifting section, hinged at a (near bank) with its leaf reaching to mid. Sets _lift (the leaf).</summary>
    private void MakeDrawbridge(Node3D root, Vector3 a, Vector3 mid)
    {
        _dbBeam = _dbChains = _dbSignal = null;
        var d = mid - a;
        float len = Math.Max(0.5f, d.Length());
        d /= len;
        _liftLen = len;
        if (!ModelLib.HasPart(LiftModel, "leaf") || !ModelLib.HasPart(LiftModel, "beam"))
        {
            // Stand-in: the old way, one stretched piece tipped about the hinge.
            var hinge = new Node3D { Name = "Hinge", Position = a, Rotation = new Vector3(0, MathF.Atan2(-d.Z, d.X), 0) };
            _lift = new Node3D { Name = "Leaf" };
            hinge.AddChild(_lift);
            _lift.AddChild(Stretch(ModelLib.Pick(LiftModel, "footbridge-span"), Vector3.Zero, new Vector3(len, 0, 0), 3f));
            root.AddChild(hinge);
            return;
        }
        float s = Math.Clamp(len / DbLeaf, 0.6f, 1.6f);
        // Local -Z (the leaf's way) toward the far bank; the model's hinge (0, 0, HZ) lands on a.
        var rig = new Node3D { Name = "Hinge", Rotation = new Vector3(0, MathF.Atan2(-d.X, -d.Z), 0), Scale = Vector3.One * s };
        rig.Position = a + d * DbHz * s;
        rig.AddChild(ModelLib.PartInstance(LiftModel, "frame", "Frame"));
        var leaf = ModelLib.PartInstance(LiftModel, "leaf", "Leaf");
        rig.AddChild(leaf);
        _lift = leaf;
        _dbBeam = ModelLib.PartInstance(LiftModel, "beam", "Beam");
        rig.AddChild(_dbBeam);
        if (ModelLib.HasPart(LiftModel, "chains")) { _dbChains = ModelLib.PartInstance(LiftModel, "chains", "Chains"); rig.AddChild(_dbChains); }
        if (ModelLib.HasPart(LiftModel, "signal")) { _dbSignal = ModelLib.PartInstance(LiftModel, "signal", "Signal"); rig.AddChild(_dbSignal); }
        root.AddChild(rig);
    }

    /// <summary>Poses the drawbridge for a leaf angle (radians, 0 down).</summary>
    private void DrawbridgePose(float angle)
    {
        if (_lift == null) return;
        if (_dbBeam == null) { _lift.Rotation = new Vector3(0, 0, angle); return; }
        _lift.Rotation = new Vector3(angle, 0, 0);
        _dbBeam.Rotation = new Vector3(angle, 0, 0);
        if (_dbChains != null)
            _dbChains.Position = new Vector3(0, DbDeck + DbH + DbLeaf * MathF.Sin(angle), DbHz - DbLeaf * MathF.Cos(angle));
        if (_dbSignal != null)
            _dbSignal.Rotation = new Vector3(0, 0, Math.Clamp(angle / 0.6f, 0f, 1f) * MathF.PI / 2f);
    }
}
