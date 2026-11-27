using System;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Round n3 (asset track RV): the landing's furniture. The tide post at its own size (a stubby 3.2 m banded gauge on the bank) with its pointer
/// sliding up and down the post with the tide, so the post shows what the tide dial speaks of; and the crane beside
/// the landing once the town has bought it (Town.CraneFactor above 1), its jib swinging slowly with a sack on the hook.
/// </summary>
public partial class RiverLayer
{
    private Node3D? _tidePointer, _crane, _craneJib;

    /// <summary>The tide post as its parts (post and pointer) at scale 1; the stand-in is fitted as before.</summary>
    private Node3D MakeTidePost()
    {
        const string model = "tide-post";
        if (!ModelLib.HasPart(model, "Pointer") || !ModelLib.HasPart(model, "Post"))
            return Fit(ModelLib.Pick(model, "fence-post"), 1.0f, 1.0f, 0f, 0.6f);
        var root = new Node3D();
        root.AddChild(ModelLib.PartInstance(model, "Post", "Post"));
        _tidePointer = ModelLib.PartInstance(model, "Pointer", "Pointer");
        root.AddChild(_tidePointer);
        return root;
    }

    /// <summary>The tide post's row (cells): the landing's upstream end, on the bank (boats lie in the water, so no
    /// berth is in its way); the crane's row at the landing's other end.</summary>
    private static float ClearOfBerths(RiverPlan rp, RiverPlot landing, out float craneZ)
    {
        float north = landing.Y - 0.5f, south = landing.Y + landing.H + 0.6f;
        craneZ = south + 1.2f;
        return north;
    }

    private void MakeCrane(double cx, float half, float side, float z)
    {
        const string model = "crane";
        if (!ModelLib.HasPart(model, "Base") || !ModelLib.HasPart(model, "Jib")) return;
        _crane = new Node3D { Name = "Crane", Visible = false };
        _crane.AddChild(ModelLib.PartInstance(model, "Base", "Base"));
        _craneJib = ModelLib.PartInstance(model, "Jib", "Jib");
        _crane.AddChild(_craneJib);
        _crane.Position = new Vector3((float)(cx + side * (half + 0.9f)) * Cell, 0, z * Cell);   // on the lip, its jib over the water
        AddChild(_crane);
    }

    private void LandingFrame(Town t, float drop)
    {
        if (_tidePointer != null)
        {
            // High water: the pointer near the top band; low water: near the footing.
            float level = 1f - drop / TideDropMetres;
            _tidePointer.Position = new Vector3(0, 0.45f + 2.45f * level, 0);
        }
        if (_crane != null)
        {
            bool on = t.CraneFactor > 1.0001 && t.RiverPlotGiven(0);
            _crane.Visible = on;
            if (on && _craneJib != null)
            {
                // The jib swings between the water (toward the river) and the quay, slowly.
                float sw = MathF.Sin((float)_drv.AnimTime * 0.25f);
                _craneJib.Rotation = new Vector3(0, (_craneSide < 0 ? MathF.PI / 2f : -MathF.PI / 2f) + 0.45f * sw, 0);
            }
        }
    }

    private float _craneSide = 1f;
}
