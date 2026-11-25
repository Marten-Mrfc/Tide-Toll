using System;
using Godot;
using TideToll.Sim;

namespace TideToll.Game;

/// <summary>
/// The river's traffic on the map page, read from <see cref="RiverView"/> every frame (nothing when the river rules
/// are off):
/// - Boats: a pool of slots keyed by boat id, each holding a rowboat (with a grain crate when it carries cargo) and a
///   masted barge (hull, mast with its white sail cross-bar, and the cargo node shown only when loaded). A boat is
///   drawn at its interpolated row along the river's centre line, turned to the river's bend, the hull's bow at the
///   simulation's bow row; boats waiting in a queue step aside into lanes, moored boats lie either side of the jetty.
///   Jumps the simulation makes (mooring, leaving a berth, turning back) are eased out over about a second.
/// - Wakes: one MultiMesh of V-shaped trails behind boats under way (barges wider); none behind a boat that waits.
/// - Signs (one MultiMesh, shaders/river_sign.gdshader, drawn over everything at a fixed size in pixels): a patience
///   ring by every waiting boat (an hourglass while it waits its turn, a crossed-out mast when a crossing it can never
///   pass holds it), and a vermillion diamond where a boat turned back, fading over 15 seconds.
/// - The landing: the jetty on the market bank at the simulation's landing row.
/// No allocation per frame: the slots, buffers and nodes are made once.
/// </summary>
public partial class RiverLayer : Node3D
{
    /// <summary>Model numbers (assets/models/barge.glb, rowboat.glb): half length to the bow, beam, metres.</summary>
    private const float RowBow = 3.4f, RowBeam = 2.4f, BargeBow = 6.0f, BargeBeam = 4.4f;
    private const int SignStride = 12 + 4, MaxMarks = 4;
    public const float MarkSeconds = 15f;

    private sealed class Slot
    {
        public int Id = -1;
        public bool Seen;
        public Node3D Root = null!;
        public Node3D Row = null!, Barge = null!;
        public MeshInstance3D Crate = null!, Cargo = null!;
        public BoatState State;
        public Vector3 Off, Pos;
        public float Lateral, Yaw;
    }

    private SimDriver _sim = null!;
    private Slot[] _slots = Array.Empty<Slot>();
    private MultiMeshInstance3D _wakes = null!, _signs = null!;
    private float[] _wakeBuf = Array.Empty<float>(), _signBuf = Array.Empty<float>();
    private ShaderMaterial _wakeMat = null!;
    private Node3D _landing = null!;
    private float _mpp = 0.25f;
    private float[] _cy = Array.Empty<float>(), _lane = Array.Empty<float>();
    private int[] _order = Array.Empty<int>();
    private Node3D? _jetty;
    private float _jettyScale = -1;
    /// <summary>Boats drawn up to 2.2x when zoomed out (Main sets it from the zoom), as villagers are: longer, and as wide
    /// as the river allows, so a barge's length and mast read apart from a rowboat at the play view. The landing grows too.</summary>
    public float BoatScale = 1f;
    /// <summary>Queued boats pulled back from a crossing or the moored boats they would have overlapped (last frame; tests).</summary>
    public int Clamped { get; private set; }
    private double _clock;
    private readonly Vector3[] _markAt = new Vector3[MaxMarks];
    private readonly double[] _markT = { -1e9, -1e9, -1e9, -1e9 };
    private int _nextMark;
    private static readonly StringName AnimTime = new("anim_time");

    /// <summary>Boats drawn, waiting boats with a ring, and turn-back marks showing, in the last frame (tests).</summary>
    public int Drawn { get; private set; }
    public int Rings { get; private set; }
    public int Marks { get; private set; }
    public int Wakes { get; private set; }
    /// <summary>Map-local position of the landing jetty (tests, coins, camera shots); zero when the rules are off.</summary>
    public Vector3 LandingAt { get; private set; }

    public void Init(SimDriver sim)
    {
        _sim = sim;
        Name = "River";
        int max = sim.Params.River?.MaxBoats ?? 0;
        _slots = new Slot[max];
        _cy = new float[max];
        _lane = new float[max];
        _order = new int[max];
        for (int i = 0; i < max; i++) _slots[i] = MakeSlot(i);
        _wakeMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/wake.gdshader") };
        _wakeMat.SetShaderParameter("foam", Palette.Paper);
        _wakes = Multi("Wakes", _wakeMat, Math.Max(1, max), out _wakeBuf);
        var signMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/river_sign.gdshader"), RenderPriority = 9 };
        signMat.SetShaderParameter("ink", Palette.Ink);
        signMat.SetShaderParameter("paper", Palette.Paper);
        _signs = Multi("Signs", signMat, Math.Max(1, max) + MaxMarks, out _signBuf);
        _landing = new Node3D { Name = "Landing" };
        AddChild(_landing);
        sim.WorldReplaced += Rebuild;
        Rebuild();
    }

    public override void _ExitTree()
    {
        if (_sim != null) _sim.WorldReplaced -= Rebuild;
    }

    private MultiMeshInstance3D Multi(string name, ShaderMaterial mat, int count, out float[] buf)
    {
        var quad = new PlaneMesh { Size = Vector2.One, Material = mat };
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseCustomData = true, Mesh = quad, InstanceCount = count };
        mm.VisibleInstanceCount = 0;
        buf = new float[count * SignStride];
        var mmi = new MultiMeshInstance3D { Name = name, Multimesh = mm, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 600 };
        AddChild(mmi);
        return mmi;
    }

    private Slot MakeSlot(int i)
    {
        var s = new Slot { Root = new Node3D { Name = $"Boat{i}", Visible = false } };
        AddChild(s.Root);
        s.Row = new Node3D { Name = "Rowboat" };
        s.Row.AddChild(ModelLib.Instance("rowboat", "Hull"));
        // Grain sacks in the rowboat once it has bought some: a small thatch-coloured bundle amidships.
        var sack = new StandardMaterial3D { AlbedoColor = Palette.Thatch.Lerp(Palette.Paper, 0.2f), Roughness = 1, SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled };
        s.Crate = new MeshInstance3D { Name = "Cargo", Mesh = new BoxMesh { Size = new Vector3(1.3f, 0.7f, 1.9f), Material = sack }, Position = new Vector3(0, 0.55f, -0.4f) };
        s.Row.AddChild(s.Crate);
        s.Root.AddChild(s.Row);
        s.Barge = new Node3D { Name = "Barge" };
        s.Barge.AddChild(ModelLib.PartInstance("barge", "hull", "Hull"));
        s.Barge.AddChild(ModelLib.PartInstance("barge", "mast", "Mast"));
        s.Cargo = ModelLib.PartInstance("barge", "cargo", "Cargo");
        s.Barge.AddChild(s.Cargo);
        s.Root.AddChild(s.Barge);
        return s;
    }

    /// <summary>The landing jetty, and every boat slot freed (a new world).</summary>
    public void Rebuild()
    {
        foreach (var c in _landing.GetChildren()) { _landing.RemoveChild(c); c.QueueFree(); }
        _jetty = null;
        foreach (var s in _slots) { s.Id = -1; s.Root.Visible = false; }
        for (int i = 0; i < MaxMarks; i++) _markT[i] = -1e9;
        var w = _sim.World;
        var r = w.River;
        LandingAt = Vector3.Zero;
        if (r == null) return;
        float cell = (float)w.Params.CellMeters;
        int n = w.Grid.Width, lc = r.LandingCell, row = r.LandingRow;
        int side = lc % n + 0.5f > w.RiverX[row] ? 1 : -1;          // +1: the landing is on the east bank
        float edge = (lc % n + (side > 0 ? 0 : 1)) * cell;             // the bank's edge against the water
        var jetty = ModelLib.Instance("landing", "Jetty");
        jetty.Scale = new Vector3(0.85f, 0.85f, 0.85f);
        jetty.Rotation = new Vector3(0, side * MathF.PI / 2f, 0);      // its -Z (the water end) toward the river
        jetty.Position = new Vector3(edge + side * 0.6f, 0.05f, (row + 0.5f) * cell);
        _landing.AddChild(jetty);
        _jetty = jetty;
        _jettyScale = -1;
        LandingAt = jetty.Position;
    }

    public void SetMetresPerPixel(float mpp)
    {
        if (float.IsFinite(mpp) && mpp > 0) _mpp = mpp;
    }

    /// <summary>A boat turned back: a diamond on the water where it gave up (from the river feed).</summary>
    public void MarkTurnBack(Vector3 at)
    {
        _markAt[_nextMark] = at;
        _markT[_nextMark] = _clock;
        _nextMark = (_nextMark + 1) % MaxMarks;
    }

    /// <summary>The i-th drawn boat slot's hull centre (map-local) and half its drawn length in metres; false if the slot is empty (floats keep clear of boats).</summary>
    public bool BoatBox(int slot, out Vector3 at, out float halfLength)
    {
        at = default; halfLength = 0;
        if (slot < 0 || slot >= _slots.Length) return false;
        var s = _slots[slot];
        if (s.Id < 0 || !s.Root.Visible) return false;
        at = s.Pos;
        halfLength = (s.Barge.Visible ? BargeBow : RowBow) * s.Root.Scale.Z;
        return true;
    }
    public int Slots => _slots.Length;

    /// <summary>Map-local position of a boat's hull centre, false if no such boat is drawn.</summary>
    public bool BoatPosition(int id, out Vector3 at)
    {
        foreach (var s in _slots)
            if (s.Id == id) { at = s.Pos; return true; }
        at = default;
        return false;
    }

    public override void _Process(double delta)
    {
        _clock += delta;
        Sync((float)delta);
    }

    /// <summary>Places every boat, wake and sign for this frame. Allocation-free.</summary>
    public void Sync(float dt)
    {
        var w = _sim.World;
        var r = w.River;
        Drawn = Rings = Marks = Wakes = 0;
        if (r == null) { _wakes.Multimesh.VisibleInstanceCount = 0; _signs.Multimesh.VisibleInstanceCount = 0; return; }
        float cell = (float)w.Params.CellMeters;
        float alpha = _sim.Alpha;
        int n = w.Grid.Width;
        var rx = w.RiverX;
        float riverW = (float)(w.Map.RiverHalfW * 2 + 1) * cell;
        int landSide = r.LandingCell % n + 0.5f > rx[r.LandingRow] ? 1 : -1;
        float ease = 1f - MathF.Exp(-dt * 3f), turn = 1f - MathF.Exp(-dt * 5f);
        float ring = Math.Clamp(34f * _mpp, 3.5f, 120f); // a fixed size on screen at every zoom
        float water = (float)(w.Map.RiverHalfW * 2 * cell);
        float js = 0.85f * (1f + (BoatScale - 1f) * 0.6f);
        if (_jetty != null && js != _jettyScale) { _jettyScale = js; _jetty.Scale = new Vector3(js, js, js); }
        foreach (var s in _slots) s.Seen = false;
        var boats = r.Boats;
        int wakes = 0, signs = 0, count = Math.Min(boats.Length, _cy.Length);
        float sc = BoatScale;
        // Hull centres in rows: the bow row plus half a hull to the south; moored boats lie either side of the jetty.
        for (int i = 0; i < count; i++)
        {
            ref readonly Boat b = ref boats[i];
            float bow = (b.Kind == BoatKind.Barge ? BargeBow : RowBow) * sc;
            float y = (float)(b.PrevY + (b.Y - b.PrevY) * alpha);
            _cy[i] = b.State == BoatState.Mooring
                ? (float)(r.LandingRow + 0.5) + (b.Berth == 0 ? -1f : 1f) * (bow + 2.2f) / cell
                : y + bow / cell;
        }
        SpaceQueue(boats, count, cell, sc);
        for (int i = 0; i < count; i++)
        {
            ref readonly Boat b = ref boats[i];
            var s = SlotFor(b.Id);
            if (s == null) continue;
            bool barge = b.Kind == BoatKind.Barge;
            // Longer at overview, but never wider than the water: the beam grows only as far as the river allows.
            float baseBeam = barge ? BargeBeam : RowBeam;
            float wide = MathF.Max(1f, MathF.Min(sc, (water - 1.2f) / baseBeam));
            float bow = (barge ? BargeBow : RowBow) * sc, beam = baseBeam * wide;
            float cy = _cy[i];
            float room = MathF.Max(0f, (riverW - beam) / 2f - 0.2f);
            float lateral = 0f;
            if (b.State == BoatState.Mooring) lateral = landSide * room;
            else if (b.State == BoatState.TurnedBack) lateral = -landSide * room * 0.6f;
            else if (b.State == BoatState.Waiting) lateral = _lane[i] * MathF.Max(room, beam * 0.45f);
            float x = RiverXAt(rx, cy, n);
            // Heading: along the river's bend, northbound unless turned back.
            float slope = RiverXAt(rx, cy - 1f, n) - RiverXAt(rx, cy + 1f, n); // x change per two rows going north
            var fwd = new Vector3(slope / 2f, 0, -1f).Normalized();
            if (b.State == BoatState.TurnedBack) fwd = -fwd;
            float yaw = MathF.Atan2(fwd.X, fwd.Z);
            bool fresh = s.Id != b.Id;
            if (fresh) { s.Id = b.Id; s.Off = Vector3.Zero; s.Lateral = lateral; s.Yaw = yaw; s.State = b.State; }
            s.Lateral += (lateral - s.Lateral) * ease;
            var target = new Vector3(x * cell + s.Lateral, 0.02f, cy * cell);
            if (s.State != b.State)
            {
                // The simulation jumped (moored, left its berth, turned back): ease out of the old place.
                if ((s.State == BoatState.Mooring) != (b.State == BoatState.Mooring)) s.Off = s.Pos - target;
                s.State = b.State;
            }
            s.Off *= 1f - ease;
            s.Yaw = Mathf.LerpAngle(s.Yaw, yaw, turn);
            s.Pos = target + s.Off;
            s.Root.Position = s.Pos;
            s.Root.Rotation = new Vector3(0, s.Yaw, 0);
            s.Root.Scale = new Vector3(wide, sc, sc);
            if (!s.Root.Visible) s.Root.Visible = true;
            if (s.Row.Visible == barge) s.Row.Visible = !barge;
            if (s.Barge.Visible != barge) s.Barge.Visible = barge;
            bool loaded = b.Cargo > 0;
            if (barge) { if (s.Cargo.Visible != loaded) s.Cargo.Visible = loaded; }
            else if (s.Crate.Visible != loaded) s.Crate.Visible = loaded;
            Drawn++;
            float speed = MathF.Abs((float)(b.Y - b.PrevY));
            if (speed > 1e-4f && b.State != BoatState.Mooring)
            {
                var sf = new Vector3(MathF.Sin(s.Yaw), 0, MathF.Cos(s.Yaw));
                float len = (barge ? 18f : 10f) * sc, wakeW = barge ? 1f : 0.65f;
                var at = s.Pos - sf * (bow + len / 2f - 0.5f) + new Vector3(0, 0.06f, 0);
                var right = new Vector3(sf.Z, 0, -sf.X) * MathF.Min((barge ? 10f : 6f) * sc, water + 2f);
                Put(_wakeBuf, wakes++, right, -sf * len, at, 1f, wakeW, 0);
            }
            if (b.State == BoatState.Waiting)
            {
                float fill = (float)(b.PatienceLeft / r.Rules.PatienceSec);
                float kind = HeldForGood(r, b.WaitingAt, b.Mast) ? 1f : 0f;
                var side = new Vector3(s.Lateral >= 0 ? 1f : -1f, 0, 0);
                var at = s.Pos + side * (beam / 2f + ring * 0.55f) + new Vector3(0, 4f, 0);
                Put(_signBuf, signs++, new Vector3(ring, 0, 0), new Vector3(0, 0, ring), at, kind, fill, 1f);
                Rings++;
            }
        }
        foreach (var s in _slots)
            if (!s.Seen && s.Id >= 0) { s.Id = -1; s.Root.Visible = false; }
        for (int k = 0; k < MaxMarks; k++)
        {
            double age = _clock - _markT[k];
            if (age < 0 || age > MarkSeconds) continue;
            float size = ring * 0.9f, a = (float)Math.Min(1.0, (MarkSeconds - age) / 4.0);
            Put(_signBuf, signs++, new Vector3(size, 0, 0), new Vector3(0, 0, size), _markAt[k] + new Vector3(0, 3.5f, 0), 2f, 0, a);
            Marks++;
        }
        Wakes = wakes;
        Upload(_wakes, _wakeBuf, wakes);
        Upload(_signs, _signBuf, signs);
        _wakeMat.SetShaderParameter(AnimTime, (float)(_sim.AnimTime % 3600.0));
    }

    /// <summary>
    /// The simulation queues boats 2 rows apart, as points; a barge is over 6 rows long (more at overview). Boats waiting
    /// in a queue are drawn nose to tail instead (each pushed south of the one ahead by its length), so a queue reads as
    /// a queue, but never over a crossing or a moored boat further south: a boat that would reach one is held just short
    /// of it and drawn rafted up beside the boat ahead (a side lane) instead. Allocation-free.
    /// </summary>
    private void SpaceQueue(ReadOnlySpan<Boat> boats, int count, float cell, float sc)
    {
        int m = 0;
        Clamped = 0;
        for (int i = 0; i < count; i++)
        {
            _lane[i] = 0;
            if (boats[i].State == BoatState.Waiting) _order[m++] = i;
        }
        // Insertion sort by centre, north first (a handful of boats).
        for (int a = 1; a < m; a++)
        {
            int v = _order[a], j = a - 1;
            while (j >= 0 && _cy[_order[j]] > _cy[v]) { _order[j + 1] = _order[j]; j--; }
            _order[j + 1] = v;
        }
        var r = _sim.World.River!;
        if (m > 0) ClearOfMoored(boats, count, _order[0], cell, sc);
        for (int a = 1; a < m; a++)
        {
            int p = _order[a - 1], q = _order[a];
            ClearOfMoored(boats, count, q, cell, sc);
            float hq = Half(boats[q], sc);
            float half = Half(boats[p], sc) + hq;
            float want = _cy[p] + (half + 1f) / cell;
            if (_cy[q] >= want) continue;
            // The first obstacle south of where the simulation has this boat: a crossing, or a moored boat.
            float limit = SouthLimit(r, boats, count, _cy[q] - hq / cell, cell);
            float most = limit - (hq + 0.6f) / cell;
            if (want <= most) { _cy[q] = want; continue; }
            _cy[q] = MathF.Max(_cy[q], most);
            _lane[q] = _lane[p] > 0 ? -1f : 1f; // rafted beside the boat ahead
            Clamped++;
        }
    }

    /// <summary>A boat waiting for a berth lies south of the moored boats, never over them.</summary>
    private void ClearOfMoored(ReadOnlySpan<Boat> boats, int count, int q, float cell, float sc)
    {
        float hq = Half(boats[q], sc) / cell;
        for (int i = 0; i < count; i++)
        {
            if (boats[i].State != BoatState.Mooring || _cy[i] > _cy[q]) continue;
            float south = _cy[i] + Half(boats[i], sc) / cell + 0.4f;
            if (_cy[q] - hq < south) { _cy[q] = south + hq; Clamped++; }
        }
    }

    private static float Half(in Boat b, float sc) => (b.Kind == BoatKind.Barge ? BargeBow : RowBow) * sc;

    /// <summary>The northern edge (rows) of the nearest crossing deck or moored hull south of row <paramref name="from"/>.</summary>
    private float SouthLimit(RiverView r, ReadOnlySpan<Boat> boats, int count, float from, float cell)
    {
        float best = float.MaxValue;
        var xs = r.Crossings;
        for (int i = 0; i < xs.Count; i++)
        {
            if (xs[i].Type == CrossingType.Ford) continue;
            float north = xs[i].Row - 0.6f;
            if (north > from && north < best) best = north;
        }
        for (int i = 0; i < count; i++)
        {
            if (boats[i].State != BoatState.Mooring) continue;
            float north = _cy[i] - Half(boats[i], BoatScale) / cell;
            if (north > from && north < best) best = north;
        }
        return best;
    }

    private Slot? SlotFor(int id)
    {
        foreach (var s in _slots) if (s.Id == id) { s.Seen = true; return s; }
        foreach (var s in _slots) if (s.Id < 0 && !s.Seen) { s.Seen = true; s.Id = -2; return s; } // fresh: Id set by the caller
        return null;
    }

    /// <summary>Whether the crossing a boat waits at will never let its mast through (a low footbridge, a drawbridge never raised is still "its turn").</summary>
    private static bool HeldForGood(RiverView r, int crossing, int mast)
    {
        if (crossing < 0) return false;
        var xs = r.Crossings;
        for (int i = 0; i < xs.Count; i++)
            if (xs[i].Id == crossing) return xs[i].Type == CrossingType.Footbridge && mast > xs[i].Clearance;
        return false;
    }

    /// <summary>River centre (cells) at a continuous row, interpolated between rows.</summary>
    public static float RiverXAt(ReadOnlySpan<float> rx, float row, int n)
    {
        float f = Math.Clamp(row - 0.5f, 0f, n - 1.001f);
        int i = (int)f;
        float t = f - i;
        return rx[i] + (rx[Math.Min(n - 1, i + 1)] - rx[i]) * t;
    }

    private static void Put(float[] buf, int i, Vector3 bx, Vector3 bz, Vector3 at, float c0, float c1, float c2)
    {
        int o = i * SignStride;
        if (o + SignStride > buf.Length) return;
        // Row-major 3x4: the quad's X axis is bx, its Z axis bz (PlaneMesh lies in XZ), Y stays up.
        buf[o + 0] = bx.X; buf[o + 1] = 0; buf[o + 2] = bz.X; buf[o + 3] = at.X;
        buf[o + 4] = bx.Y; buf[o + 5] = 1; buf[o + 6] = bz.Y; buf[o + 7] = at.Y;
        buf[o + 8] = bx.Z; buf[o + 9] = 0; buf[o + 10] = bz.Z; buf[o + 11] = at.Z;
        buf[o + 12] = c0; buf[o + 13] = c1; buf[o + 14] = c2; buf[o + 15] = 0;
    }

    private static void Upload(MultiMeshInstance3D mmi, float[] buf, int count)
    {
        var mm = mmi.Multimesh;
        count = Math.Min(count, mm.InstanceCount);
        if (count > 0) RenderingServer.MultimeshSetBuffer(mm.GetRid(), buf);
        if (mm.VisibleInstanceCount != count) mm.VisibleInstanceCount = count;
    }
}
