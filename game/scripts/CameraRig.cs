using Godot;

namespace TideToll.Game;

/// <summary>
/// Pivot on the ground, then yaw, then pitch, then the camera at a zoom distance (Engine tab,
/// "3D from day one"). Phase 0 locks it north-up and straight down; rotation, tilt and zoom
/// limits arrive later through data, so nothing else may assume this fixed view.
/// </summary>
public partial class CameraRig : Node3D
{
    public Node3D Yaw { get; private set; } = null!;
    public Node3D Pitch { get; private set; } = null!;
    public Camera3D Camera { get; private set; } = null!;

    public float Distance = 400f;
    public float PitchDegrees = -90f;
    /// <summary>Vertical extent of the orthographic view, in metres.</summary>
    public float ViewHeight = 430f;

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
            Size = ViewHeight,
            Position = new Vector3(0, 0, Distance),
            Near = 1f,
            Far = Distance * 3f,
            Current = true,
        };
        Pitch.AddChild(Camera);
    }
}
