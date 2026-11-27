using Godot;

namespace TideToll.Game;

/// <summary>
/// Night 3 (asset round 2, track BK): what is fixed on the front board's outer face besides its gilt rules: four Thatch
/// metal corner pieces, a clasp at the fore-edge (a leather strap from the back board ending in a Thatch catch) and
/// five raised bands on the spine edge. The board's outer face is its local -Y side (its underside while the book lies
/// open, on top once shut); the spine is its local +X edge, the fore-edge its -X edge. Decoration only: the board's
/// size, place and name stay as they are, and nothing reaches inside the gilt rules, where the menu's lettering sits.
/// </summary>
public static class BookCoverDress
{
    public static void Dress(MeshInstance3D board, Vector2 size)
    {
        float hx = size.X / 2f, hz = size.Y / 2f, face = -2f;
        var metal = Palette.Matte(Palette.Thatch);
        var metalShade = Palette.Matte(Palette.Thatch.Lerp(Palette.Ink, 0.18f));
        var strap = Palette.Matte(Palette.SootBrick.Lerp(Palette.Ink, 0.18f));
        var band = Palette.Matte(Palette.SootBrick.Lerp(Palette.Paper, 0.12f));
        // Corner pieces: an L of two straps 13 m long with a square plate across the corner, 0.9 m proud. Night 3 fix:
        // everything sits 0.15 m inside the board's edge, and the plate (turned 45 degrees) stays inside the corner
        // with a darker rim plate under it, so nothing floats past the board.
        const float leg = 13f, wid = 3.2f, t = 0.9f, inset = 0.15f;
        const float plate = 6f, rim = 7f;
        float rimHalf = rim * 0.7071f;   // a square turned 45 degrees reaches this far along each axis
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                var corner = new Vector3(sx * (hx - inset), face - t / 2f, sz * (hz - inset));
                Box(board, metal, new Vector3(leg, t, wid), corner + new Vector3(-sx * leg / 2f, 0, -sz * wid / 2f));
                Box(board, metal, new Vector3(wid, t, leg), corner + new Vector3(-sx * wid / 2f, 0, -sz * leg / 2f));
                float c = rimHalf + 0.2f;        // plate centre, in from the corner on both axes: its points stay inside
                var under = Box(board, metalShade, new Vector3(rim, t * 0.5f, rim), corner + new Vector3(-sx * c, t * 0.35f, -sz * c));
                under.RotationDegrees = new Vector3(0, 45, 0);
                var top = Box(board, metal, new Vector3(plate, t * 0.6f, plate), corner + new Vector3(-sx * c, t * 0.25f, -sz * c));
                top.RotationDegrees = new Vector3(0, 45, 0);
                // a rivet on each leg
                Box(board, metalShade, new Vector3(1.4f, t * 1.6f, 1.4f), corner + new Vector3(-sx * (leg - 2f), 0, -sz * wid / 2f));
                Box(board, metalShade, new Vector3(1.4f, t * 1.6f, 1.4f), corner + new Vector3(-sx * wid / 2f, 0, -sz * (leg - 2f)));
            }
        // The clasp: a strap over the fore-edge, ending in a Thatch catch with a pin, all outside the gilt rule (16 m).
        Box(board, strap, new Vector3(16f, 0.8f, 10f), new Vector3(-hx + 4f, face - 0.4f, 0));
        Box(board, strap, new Vector3(1.2f, 5f, 10f), new Vector3(-hx - 0.6f, -0.2f, 0));          // round the edge
        Box(board, metal, new Vector3(7f, 1.2f, 12f), new Vector3(-hx + 9.5f, face - 0.6f, 0));
        Box(board, metalShade, new Vector3(2.2f, 2f, 2.2f), new Vector3(-hx + 10.5f, face - 1f, 0));
        // Raised bands on the spine edge.
        for (int i = -2; i <= 2; i++)
            Box(board, band, new Vector3(7f, 1.4f, 3f), new Vector3(hx - 3.5f, face - 0.7f, i * 62f));
    }

    private static MeshInstance3D Box(Node3D parent, Material m, Vector3 size, Vector3 pos)
    {
        var b = new MeshInstance3D { Mesh = new BoxMesh { Size = size }, Position = pos, MaterialOverride = m };
        parent.AddChild(b);
        return b;
    }
}
