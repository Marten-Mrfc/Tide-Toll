using System;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Work you can watch (n5): what the villagers hold and set down. Loads come up from the goods into the arms
/// (<see cref="WorkSites.TakeSeconds"/>), leave the arms in a short arc onto the market's table or in at a cottage's
/// door (<see cref="WorkSites.DropSeconds"/>, then <see cref="WorkSites.Delivered"/>), the mill's grain sack goes in at
/// the door (<see cref="TipSeconds"/>); a load is never drawn before the simulation hands it over and never vanishes from
/// the arms. The pack frame and the handcart stay with their owner (on the back, pushed ahead) and stand parked beside
/// them at work. The sickle and the scythe are stand-in meshes made here (a wooden handle and a pale blade) until the
/// places' builder's models <c>villager-sickle</c> / <c>villager-scythe</c> exist; they are held in the right hand.
/// </summary>
public partial class TownVillagers
{
    private readonly float[] _fly = new float[Capacity], _cartK = new float[Capacity], _park = new float[Capacity];
    private readonly bool[] _flyHome = new bool[Capacity], _handOff = new bool[Capacity], _loadPrev = new bool[Capacity];
    private readonly Transform3D[] _flyFrom = new Transform3D[Capacity];
    private readonly Vector3[] _flyTo = new Vector3[Capacity];
    private readonly Good[] _flyGood = new Good[Capacity], _shownGood = new Good[Capacity];
    private readonly int[] _flyPlot = new int[Capacity], _flyAmount = new int[Capacity], _flyLevel = new int[Capacity];
    private readonly Transform3D[] _lastXf = new Transform3D[Capacity];
    private readonly byte[] _tool = new byte[Capacity];
    private MultiMeshInstance3D _sickles = null!, _scythes = null!;
    private int _cartLevel = -1;
    private float _frameOut;
    /// <summary>Where a load sits in the arms, in the body's frame (its visual middle), for the lift and the arc.</summary>
    private static readonly Vector3 Arms = new(0, 1.15f, 0.45f);

    /// <summary>Tests: the good drawn on villager i this frame (None: nothing in the arms, on the back or in the cart).</summary>
    public Good LoadShown(int i) => _shownGood[i];
    /// <summary>Tests: whether a load of villager i is on its way to a table or door, the tool in its hand (0, 1 sickle, 2 scythe), its cart parked (0..1).</summary>
    public bool Dropping(int i) => _fly[i] > 0;
    public int ToolOf(int i) => _tool[i];
    public float Parked(int i) => _park[i];
    /// <summary>Tests: loads that appeared on or left a villager in one frame without being taken up or set down (should stay 0).</summary>
    public int Pops { get; private set; }
    public int Drops { get; private set; }

    private void InitProps()
    {
        _sickles = ToolMulti(ModelLib.Has("villager-sickle") ? ModelLib.Mesh("villager-sickle") : ToolMesh(0.55f, 0.32f), "Sickles");
        _scythes = ToolMulti(ModelLib.Has("villager-scythe") ? ModelLib.Mesh("villager-scythe") : ToolMesh(1.5f, 0.62f), "Scythes");
    }

    private MultiMeshInstance3D ToolMulti(Mesh mesh, string name)
    {
        var mm = new MultiMeshInstance3D { Name = name, Multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, InstanceCount = Capacity, Mesh = mesh } };
        mm.Multimesh.VisibleInstanceCount = 0;
        AddChild(mm);
        return mm;
    }

    /// <summary>A stand-in tool: a handle up +Y from the hand and a curved blade at its top, toward +Z.</summary>
    private static ArrayMesh ToolMesh(float handle, float blade)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        var wood = new Color(0.45f, 0.30f, 0.16f);
        var steel = new Color(0.86f, 0.86f, 0.80f);
        Box(st, wood, new Vector3(-0.035f, -0.12f, -0.035f), new Vector3(0.035f, handle, 0.035f));
        const int seg = 6;
        for (int k = 0; k < seg; k++)
        {
            float a0 = k * 1.9f / seg, a1 = (k + 1) * 1.9f / seg;
            var c0 = new Vector3(0, handle + MathF.Sin(a0) * blade * 0.45f, MathF.Sin(a0 * 0.8f + 0.2f) * blade);
            var c1 = new Vector3(0, handle + MathF.Sin(a1) * blade * 0.45f, MathF.Sin(a1 * 0.8f + 0.2f) * blade);
            Box(st, steel, c0 - new Vector3(0.02f, 0.05f, 0), c1 + new Vector3(0.02f, 0.05f, 0));
        }
        st.GenerateNormals();
        var mesh = st.Commit();
        mesh.SurfaceSetMaterial(0, new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 0.9f, EmissionEnabled = true, Emission = new Color(0.25f, 0.24f, 0.22f) });
        return mesh;
    }

    private static void Box(SurfaceTool st, Color c, Vector3 a, Vector3 b)
    {
        st.SetColor(c);
        Vector3 lo = new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Min(a.Z, b.Z)), hi = new(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z));
        Span<Vector3> p = stackalloc Vector3[8];
        for (int k = 0; k < 8; k++) p[k] = new Vector3((k & 1) != 0 ? hi.X : lo.X, (k & 2) != 0 ? hi.Y : lo.Y, (k & 4) != 0 ? hi.Z : lo.Z);
        ReadOnlySpan<int> f = stackalloc int[] { 0, 2, 3, 0, 3, 1, 4, 5, 7, 4, 7, 6, 0, 1, 5, 0, 5, 4, 2, 6, 7, 2, 7, 3, 0, 4, 6, 0, 6, 2, 1, 3, 7, 1, 7, 5 };
        for (int k = 0; k < f.Length; k++) st.AddVertex(p[f[k]]);
    }

    /// <summary>A load left the arms (Carry went to None): to the table or door of the delivery, else down to the ground.</summary>
    private void StartDrop(Town t, int i, Good g)
    {
        if (_fly[i] > 0 && _flyPlot[i] >= 0) WorkSites.Delivered(_flyPlot[i], i, _flyGood[i], _flyAmount[i]);
        int plot = -1, amount = 0;
        var ev = _drv.Events;
        for (int e = 0; e < ev.Count; e++)
            if (ev[e].Kind == TownEventKind.Delivery && ev[e].Villager == i) { plot = ev[e].Plot; amount = ev[e].Amount; }
        _flyPlot[i] = plot; _flyAmount[i] = amount; _flyGood[i] = g; _flyLevel[i] = t.LoadLevel;
        _flyFrom[i] = _lastXf[i];
        _flyHome[i] = plot >= 0 && t.Plan.Plots[plot].Kind != PlotKind.Market;
        _flyTo[i] = plot >= 0 ? WorkSites.Goods(t, plot, 0) : _lastXf[i].Origin;
        _fly[i] = WorkSites.DropSeconds;
        _handOff[i] = true;
        Drops++;
    }

    /// <summary>The loads, frame, cart and tool of villager i (body transform xf, the yaw without the swing).</summary>
    private void DrawProps(Town t, int i, in VillagerView v, Transform3D xf, Vector3 feet, float dt, ref int frames, ref int carts, ref int sickles, ref int scythes)
    {
        int lv = t.LoadLevel, bundles = BundlesAt(lv);
        _cartK[i] = Mathf.MoveToward(_cartK[i], 1f, dt * 3.5f);
        bool working = _atWork[i];
        _park[i] = Mathf.MoveToward(_park[i], working ? 1f : 0f, dt * 3.5f);
        Good shown = Good.None;
        // The load carried (not the mill's sack once it has gone in at the door).
        if (v.Carry != Good.None && (int)v.Carry < 6 && !(working && _tip[i] <= 0 && _kind[i] == 3))
        {
            int g = (int)v.Carry;
            float lift = _take[i] > 0 ? 1f - _take[i] / WorkSites.TakeSeconds : 1f;
            lift = lift * lift * (3f - 2f * lift);
            float tip = _tip[i] > 0 ? 1f - _tip[i] / TipSeconds : 0f;
            for (int k = 0; k < bundles && _loadCount[g] < Capacity * 2; k++)
            {
                if (lv >= 3 && v.Carry == Good.Water && k > 0) continue;
                var lx = InCart(xf, i, lv) * LoadPlace(v.Carry, lv, k);
                if (lift < 1f) lx.Origin += (_takeFrom[i] - xf * Arms) * (1f - lift);
                if (tip > 0f)
                {
                    // Tipped forward and in at the door, a little smaller as it goes in.
                    lx.Origin += (_tipTo[i] - xf * Arms) * tip;
                    lx.Basis = Sc(lx.Basis * new Basis(Vector3.Right, 1.2f * tip), 1f - 0.6f * tip);
                }
                _loads[g].Multimesh.SetInstanceTransform(_loadCount[g]++, lx);
            }
            shown = v.Carry;
        }
        // A load on its way from the arms to the table or the door.
        if (_fly[i] > 0)
        {
            _fly[i] = Math.Max(0f, _fly[i] - dt);
            float e = 1f - _fly[i] / WorkSites.DropSeconds;
            float ee = e * e * (3f - 2f * e);
            int g = (int)_flyGood[i];
            var from = _flyFrom[i];
            for (int k = 0; g > 0 && g < 6 && k < BundlesAt(_flyLevel[i]) && _loadCount[g] < Capacity * 2; k++)
            {
                if (_flyLevel[i] >= 3 && _flyGood[i] == Good.Water && k > 0) continue;
                var lx = from * LoadPlace(_flyGood[i], _flyLevel[i], k);
                lx.Origin += (_flyTo[i] + new Vector3(0, 0.15f * k, 0) - from * Arms) * ee + new Vector3(0, 0.7f * MathF.Sin(MathF.PI * e), 0);
                lx.Basis = Sc(lx.Basis, 1f - (_flyHome[i] ? 0.6f : 0.2f) * ee);
                _loads[g].Multimesh.SetInstanceTransform(_loadCount[g]++, lx);
            }
            if (_fly[i] == 0 && _flyPlot[i] >= 0) WorkSites.Delivered(_flyPlot[i], i, _flyGood[i], _flyAmount[i]);
            if (_fly[i] == 0) _handOff[i] = true;
        }
        bool drawn = shown != Good.None;
        if (drawn != _loadPrev[i] && !_handOff[i]) Pops++;
        _handOff[i] = false;
        _loadPrev[i] = drawn;
        _shownGood[i] = shown;
        // The pack frame on the back, the handcart pushed ahead; both stand parked beside the owner at work.
        if (lv != _cartLevel)
        {
            // A new Load step: the new frame or cart grows in, the pack frame it replaces shrinks away (never in one frame).
            if (_cartLevel >= 0) { for (int j = 0; j < Capacity; j++) _cartK[j] = 0f; if (FrameAt(_cartLevel)) _frameOut = 1f; }
            _cartLevel = lv;
        }
        if (FrameAt(lv) && frames < Capacity) _frames.Multimesh.SetInstanceTransform(frames++, Grown(Parked(xf, feet, i, 0.95f), _cartK[i]));
        else if (_frameOut > 0f && frames < Capacity) _frames.Multimesh.SetInstanceTransform(frames++, Grown(xf, _frameOut));
        if (CartAt(lv) && carts < Capacity) _carts.Multimesh.SetInstanceTransform(carts++, Grown(InCart(xf, i, lv), _cartK[i]));
        // The tool in the right hand, swinging with the body (a field's workers only).
        _tool[i] = 0;
        if (_kind[i] == 2 && _poseW[i] > 0.02f)
        {
            bool scythe = ScytheAt(t.ToolsLevel);
            float raise = (MathF.Sin(_rh[i]) + 1f) * 0.5f;
            var hand = new Transform3D(Sc(new Basis(Vector3.Right, 1.75f - 1.45f * raise), Math.Min(1f, _poseW[i] * 1.5f)), new Vector3(-0.62f, 1.25f, 0.55f));
            if (scythe && scythes < Capacity) _scythes.Multimesh.SetInstanceTransform(scythes++, xf * hand);
            else if (!scythe && sickles < Capacity) _sickles.Multimesh.SetInstanceTransform(sickles++, xf * hand);
            _tool[i] = (byte)(scythe ? 2 : 1);
        }
        _lastXf[i] = xf;
    }

    /// <summary>The body's frame, or (at work) the cart's parked place beside the owner, upright: eased between the two.</summary>
    private Transform3D InCart(Transform3D xf, int i, int lv) => CartAt(lv) ? Parked(xf, new Vector3(xf.Origin.X, 0, xf.Origin.Z), i, 1.4f) : xf;

    private Transform3D Parked(Transform3D xf, Vector3 feet, int i, float side)
    {
        float k = _park[i];
        if (k <= 0f) return xf;
        k = k * k * (3f - 2f * k);
        var up = new Basis(Vector3.Up, _yaw[i]).Scaled(Vector3.One * WalkerScale);
        // To the owner's right (the model's -X), a little behind, and turned to stand along the way they face.
        var at = feet + up * new Vector3(-side, 0, -0.35f) - new Vector3(0, feet.Y, 0);
        var q0 = xf.Basis.Orthonormalized().GetRotationQuaternion();
        var q1 = up.Orthonormalized().GetRotationQuaternion();
        var b = new Basis(q0.Slerp(q1, k)).Scaled(Vector3.One * WalkerScale);
        return new Transform3D(b, xf.Origin.Lerp(at, k));
    }

    private static Transform3D Grown(Transform3D x, float k) => k >= 1f ? x : new Transform3D(Sc(x.Basis, Math.Max(0.02f, k * k * (3f - 2f * k))), x.Origin);
    private static Basis Sc(Basis b, float s) => b.Scaled(Vector3.One * s);
}
