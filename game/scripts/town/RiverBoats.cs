using System;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Round 4: the boats on the river where the simulation has them (town.Boat(i), interpolated like villagers): a
/// rowboat or a barge, coming up from the south edge, waiting for a berth, moored, leaving. A boat's cargo shows on
/// its deck (loads of its good fill it as they arrive) and a label above it says what it wants and how much it has
/// ("Flour 12 of 30"), with the skipper's name on a request boat. A small wake trails a moving boat.
/// </summary>
public partial class RiverBoats : Node3D
{
    public const float Cell = TownGround.Cell;
    public const int CargoSlots = 6;

    private TownDriver _drv = null!;
    private Node3D[] _boat = Array.Empty<Node3D>();
    private Node3D[] _row = Array.Empty<Node3D>(), _barge = Array.Empty<Node3D>();
    private MeshInstance3D[][] _cargo = Array.Empty<MeshInstance3D[]>();
    private Label3D[] _label = Array.Empty<Label3D>();
    private MeshInstance3D[] _wake = Array.Empty<MeshInstance3D>();
    private int[] _sig = Array.Empty<int>();
    private Good[] _cargoGood = Array.Empty<Good>();

    /// <summary>For tests: where boat slot i is drawn (map cells), whether it is drawn, and its cargo pieces shown.</summary>
    public Vector2 Shown(int i) => i < _boat.Length ? new Vector2(_boat[i].Position.X / Cell, _boat[i].Position.Z / Cell) : default;
    public bool Drawn(int i) => i < _boat.Length && _boat[i].Visible;
    public int CargoShown(int i)
    {
        int n = 0;
        if (i < _cargo.Length) foreach (var c in _cargo[i]) if (c.Visible) n++;
        return n;
    }
    public string LabelText(int i) => i < _label.Length ? _label[i].Text : "";
    public bool Moving(int i) => i < _wake.Length && _wake[i].Visible;

    public void Init(TownDriver drv)
    {
        _drv = drv;
        Name = "Boats";
    }

    private void Build(int n)
    {
        _boat = new Node3D[n]; _row = new Node3D[n]; _barge = new Node3D[n];
        _cargo = new MeshInstance3D[n][]; _label = new Label3D[n]; _wake = new MeshInstance3D[n];
        _sig = new int[n]; _cargoGood = new Good[n];
        for (int i = 0; i < n; i++)
        {
            var b = new Node3D { Name = "Boat" + i, Visible = false };
            AddChild(b);
            _boat[i] = b;
            _row[i] = Hull(ModelLib.Pick("rowboat", "ferry-raft"), 4.2f);
            _barge[i] = Hull(ModelLib.Pick("barge", "rowboat"), 8.5f);
            b.AddChild(_row[i]); b.AddChild(_barge[i]);
            if (Crew(false) is { } bm) _row[i].AddChild(Unscaled(bm, _row[i]));
            if (Crew(true) is { } sk) _barge[i].AddChild(Unscaled(sk, _barge[i]));
            _cargo[i] = new MeshInstance3D[CargoSlots];
            _label[i] = new Label3D
            {
                Name = "Wants", Visible = false, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FontSize = 36, PixelSize = 0.026f,
                Modulate = Palette.Ink, OutlineModulate = new Color(Palette.Paper, 0.95f), OutlineSize = 16, Font = UiKit.UiBold,
                Position = new Vector3(0, 5.5f, 0), DoubleSided = true,
            };
            b.AddChild(_label[i]);
            _wake[i] = new MeshInstance3D { Name = "Wake", Mesh = WakeMesh(), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = false };
            b.AddChild(_wake[i]);
            _sig[i] = int.MinValue;
            _cargoGood[i] = Good.None;
        }
    }

    /// <summary>Undo the hull's fit scale on a child placed in game metres.</summary>
    private static Node3D Unscaled(Node3D child, Node3D hull)
    {
        float k = 1f / Math.Max(0.01f, hull.Scale.X);
        child.Scale *= k;
        child.Position *= k;
        return child;
    }

    /// <summary>A hull fitted to its length, its front (+Z) along the boat's heading.</summary>
    private static Node3D Hull(string model, float length)
    {
        var aabb = ModelLib.Mesh(model).GetAabb();
        float l = Math.Max(0.1f, Math.Max(aabb.Size.X, aabb.Size.Z));
        var root = new Node3D { Visible = false };
        var m = ModelLib.Instance(model, "Model");
        // Hull models are long along x or z; turn an x-long one so its length runs along +Z.
        if (aabb.Size.X > aabb.Size.Z) m.Rotation = new Vector3(0, MathF.PI / 2f, 0);
        root.Scale = Vector3.One * (length / l);
        root.AddChild(m);
        return root;
    }

    private void SetCargo(int i, Good g, bool barge)
    {
        foreach (var c in _cargo[i]) c?.QueueFree();
        if (g == Good.Cloth) { SetClothRolls(i, barge); _cargoGood[i] = g; return; }
        string model = TownVillagers.LoadModel(g);
        // Round n3 (RV): the load models are built in a carrier's frame (held at the hands), so each piece is set down by
        // its own box: its foot on the deck, its middle on its spot, two by three in the hold (the barge's hold is
        // amidships, the rowboat's between its thwarts), a little larger than a carried load so the good reads.
        var aabb = ModelLib.Mesh(model).GetAabb();
        float sc = barge ? 2.0f : 1.5f;
        float deck = barge ? 0.8f : 0.25f, zc = barge ? -0.35f : 0.1f, dzr = barge ? 1.05f : 0.72f, dxc = barge ? 1.0f : 0.62f;
        var mid = aabb.Position + aabb.Size / 2f;
        for (int k = 0; k < CargoSlots; k++)
        {
            var m = ModelLib.Instance(model, "Cargo" + k);
            int col = k % 2, rowk = k / 2;
            m.Scale = Vector3.One * sc;
            m.Rotation = new Vector3(0, (k % 3) * 0.35f - 0.35f, 0);
            var off = m.Basis * new Vector3(mid.X, aabb.Position.Y, mid.Z);
            m.Position = new Vector3((col - 0.5f) * dxc, deck, zc + (rowk - 1f) * dzr) - off;
            m.Visible = false;
            _boat[i].AddChild(m);
            _cargo[i][k] = m;
        }
        _cargoGood[i] = g;
    }

    private static CylinderMesh? _rollMesh;

    /// <summary>After the review (n3 RV): cloth in a hold is plain rolled bolts (style sheet section 6): Brick cylinders laid
    /// across the hold, two to a row, no stripe (the carried cloth's Limewash stripe read as a second awning on a barge).</summary>
    private void SetClothRolls(int i, bool barge)
    {
        _rollMesh ??= new CylinderMesh
        {
            TopRadius = 0.24f, BottomRadius = 0.24f, Height = 0.95f, RadialSegments = 8, Rings = 1,
            Material = new StandardMaterial3D { AlbedoColor = Palette.Brick, Roughness = 1f },
        };
        float sc = barge ? 1f : 0.7f;
        float deck = barge ? 0.8f : 0.25f, zc = barge ? -0.35f : 0.1f, dzr = barge ? 0.75f : 0.5f, dxc = barge ? 1.05f : 0.72f;
        for (int k = 0; k < CargoSlots; k++)
        {
            int col = k % 2, rowk = k / 2;
            var m = new MeshInstance3D { Name = "Cargo" + k, Mesh = _rollMesh, Visible = false };
            m.Scale = Vector3.One * sc;
            m.Rotation = new Vector3(0, 0, MathF.PI / 2f);   // lying across the hold (along the boat's X)
            m.Position = new Vector3((col - 0.5f) * dxc, deck + 0.24f * sc, zc + (rowk - 1f) * dzr);
            _boat[i].AddChild(m);
            _cargo[i][k] = m;
        }
    }

    /// <summary>Round n3 (RV): someone aboard: the boatman at the rowboat's stern, the skipper at the barge's tiller.</summary>
    private static Node3D? Crew(bool barge)
    {
        if (!ModelLib.Has("villager")) return null;
        var man = ModelLib.Instance("villager", barge ? "Skipper" : "Boatman");
        man.Scale = Vector3.One * (barge ? 0.95f : 0.8f);
        man.Position = barge ? new Vector3(0.4f, 0.75f, -3.6f) : new Vector3(0, 0.15f, -1.45f);
        return man;
    }

    private static ArrayMesh WakeMesh()
    {
        // A pale V behind the stern, flat on the water.
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        var c0 = new Color(Palette.Paper, 0.55f); var c1 = new Color(Palette.Paper, 0f);
        float y = 0.06f;
        void Arm(float s)
        {
            st.SetColor(c0); st.AddVertex(new Vector3(0.6f * s, y, -1.5f));
            st.SetColor(c0); st.AddVertex(new Vector3(0.9f * s, y, -1.8f));
            st.SetColor(c1); st.AddVertex(new Vector3(3.2f * s, y, -7f));
            st.SetColor(c1); st.AddVertex(new Vector3(2.6f * s, y, -7f));
            st.SetColor(c0); st.AddVertex(new Vector3(0.6f * s, y, -1.5f));
            st.SetColor(c1); st.AddVertex(new Vector3(3.2f * s, y, -7f));
        }
        Arm(1); Arm(-1);
        var mesh = st.Commit();
        mesh.SurfaceSetMaterial(0, new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            VertexColorUseAsAlbedo = true, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        });
        return mesh;
    }

    public override void _Process(double delta)
    {
        var t = _drv.Town;
        int n = t.BoatCount;
        if (n != _boat.Length) { foreach (var c in GetChildren()) { RemoveChild(c); c.QueueFree(); } Build(n); }
        float a = _drv.Alpha;
        for (int i = 0; i < n; i++)
        {
            var v = t.Boat(i);
            var b = _boat[i];
            bool on = v.Phase != BoatPhase.Gone && v.Id >= 0;
            b.Visible = on;
            if (!on) continue;
            float x = (float)(v.PrevX + (v.X - v.PrevX) * a), y = (float)(v.PrevY + (v.Y - v.PrevY) * a);
            b.Position = new Vector3(x * Cell, 0, y * Cell);
            b.Rotation = new Vector3(0, TownVillagers.HeadingYaw(v.Heading), 0);
            bool barge = v.Kind == BoatKind.Barge;
            _row[i].Visible = !barge; _barge[i].Visible = barge;
            bool moving = v.Phase is BoatPhase.Arriving or BoatPhase.Leaving;
            _wake[i].Visible = moving;
            // Round 5: the label stands over the water east of the boat (the quay is on the west bank), clear of both.
            _label[i].Position = b.Basis.Inverse() * new Vector3(barge ? 7.5f : 5.5f, barge ? 4f : 3.5f, 0);
            // Round 6: labels count loads (Town.BoatCargoText, a cached string per slot), so the signature uses the same text.
            string cargo = t.BoatCargoText(i);
            int sig = HashCode.Combine(v.Id, cargo, (int)v.Phase, (int)v.Good, v.Request, v.Loaded >= v.Want);
            if (sig == _sig[i]) continue;
            _sig[i] = sig;
            if (_cargoGood[i] != v.Good || _cargo[i][0] == null || _cargo[i][0].GetParent() != b) SetCargo(i, v.Good, barge);
            int show = v.Want <= 0 ? 0 : (int)Math.Ceiling(CargoSlots * Math.Min(1.0, v.Loaded / (double)v.Want));
            for (int k = 0; k < CargoSlots; k++) _cargo[i][k].Visible = k < show;
            // Round 5: short labels (the skipper's name is on the request card).
            _label[i].Text = v.Phase switch
            {
                BoatPhase.Leaving => v.Loaded >= v.Want ? "Full, leaving" : "Leaving, " + cargo,
                BoatPhase.Waiting => cargo + ", waiting",
                _ => cargo,
            };
        }
    }
}
