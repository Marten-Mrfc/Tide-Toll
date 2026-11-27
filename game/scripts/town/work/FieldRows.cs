using System;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Night 5 (build sheet C, "A field"): a field is rows of crop on its bed (work-field-bed, drawn by TownBuildings). Each
/// row is cut up to a front; behind the front the cut stretch lies as swath, then stubble, then shoots, then green, then
/// gold again on a fixed clock of game time (<see cref="Ripe"/>). Each unit of work finished moves its row's front one
/// piece on (and lays a stook where it passed; the stook before it is taken up by the sheaf carried off). Work spot s
/// works row <see cref="RowOf"/>; there are at most four places at a field and always at least seven rows, so the front
/// row and the two back rows are never worked: with the stretches ahead of the fronts, at least a third of the field is always gold.
/// This keeps what the old one-piece field (always field-ripe) meant: a field never runs out and is always ready.
/// Every piece is an instance of a shared mesh (one MultiMesh per row state, all fields together); a piece changing
/// state shrinks into the ground as its new state grows (0.35 s), so nothing changes in one frame.
/// </summary>
public partial class FieldRows : Node3D
{
    public enum St : sbyte { None = -1, Ripe = 0, Green = 1, Shoots = 2, Stubble = 3, Swath = 4, Edge = 5 }
    private static readonly string[] Models = { "work-row-ripe", "work-row-green", "work-row-shoots", "work-row-stubble", "work-row-swath", "work-row-edge" };
    private const int Kinds = 6;
    /// <summary>Game seconds after a cut at which a stretch turns from swath to stubble, to shoots, to green, to gold.</summary>
    public const double Swath = 2, Stubble = 7, Shoots = 13, Ripe = 22;
    public const float Piece = 2f, Pitch = 1.6f, Fade = 0.35f, StookScale = 1.5f;
    private const int MaxFields = 4, MaxRows = 8, MaxPieces = 10;
    /// <summary>At least seven rows: with at most four places at a field (two and two extra hands) three rows, the front
    /// one and the two at the back, are never worked, and they alone are over a third of the field.</summary>
    public const int MinRows = 7;

    private WorkPlaces _w = null!;
    private readonly MultiMesh[] _mm = new MultiMesh[Kinds];
    private MultiMesh _stooks = null!;
    private readonly int[] _count = new int[Kinds];

    private sealed class Field
    {
        public int Plot = -1, Rows, Places;
        public bool Built;
        public Vector3 Centre;
        public readonly float[] RowZ = new float[MaxRows];
        public readonly int[] N = new int[MaxRows];
        public readonly float[] X0 = new float[MaxRows], X1 = new float[MaxRows];   // ragged row ends
        public readonly int[] Front = new int[MaxRows];
        public readonly bool[] Active = new bool[MaxRows];
        public readonly double[] Cut = new double[MaxRows * MaxPieces];
        public readonly St[] Shown = new St[MaxRows * MaxPieces], Prev = new St[MaxRows * MaxPieces];
        public readonly float[] T = new float[MaxRows * MaxPieces];
        // stooks: two per row at most (the one standing and one being taken up or newly laid)
        public readonly float[] StookX = new float[MaxRows * 2], StookGrow = new float[MaxRows * 2];
        public readonly bool[] StookOn = new bool[MaxRows * 2];
        public readonly int[] StookAge = new int[MaxRows * 2];
        public int Steps, StookSerial;
        public readonly int[] TakenFrom = new int[MaxRows];
    }
    private readonly Field[] _f = new Field[MaxFields];
    private int _nf;
    private bool _dirty = true;

    /// <summary>Fronts moved in all (tests: they move only when work is finished there).</summary>
    public int FrontSteps { get; private set; }

    public FieldRows() { }
    public FieldRows(WorkPlaces w) { _w = w; Name = "FieldRows"; for (int i = 0; i < MaxFields; i++) _f[i] = new Field(); }

    public override void _Ready()
    {
        int cap = MaxFields * MaxRows * MaxPieces * 2;
        for (int k = 0; k < Kinds; k++)
        {
            var mi = ModelLib.Multi(ModelLib.Pick(Models[k], "field-ripe"), "Rows" + k, cap);
            AddChild(mi);
            _mm[k] = mi.Multimesh;
        }
        var st = ModelLib.Multi(ModelLib.Pick("work-stook", "field-ripe"), "Stooks", MaxFields * MaxRows * 2);
        AddChild(st);
        _stooks = st.Multimesh;
    }

    public void Reset()
    {
        _nf = 0;
        var plots = _w.Town.Plan.Plots;
        for (int p = 0; p < plots.Length && _nf < MaxFields; p++)
            if (plots[p].Kind == PlotKind.Field) { var f = _f[_nf++]; f.Plot = p; f.Built = false; }
        _dirty = true;
    }

    private Field? Of(int plot)
    {
        for (int i = 0; i < _nf; i++) if (_f[i].Plot == plot) return _f[i];
        return null;
    }

    /// <summary>The row worked from work spot <paramref name="slot"/>: second from the front first, then back across the
    /// field; the front row and the two back rows are never worked.</summary>
    public static int RowOf(int rows, int slot)
    {
        int usable = Math.Max(1, rows - 3);
        if (slot < 0) slot = 0;
        return rows - 2 - slot % usable;
    }

    // ---- building a field's rows ----

    private void Build(Field f, bool lived)
    {
        var t = _w.Town;
        var p = t.Plan.Plots[f.Plot];
        float fw = p.W * WorkPlaces.Cell, fh = p.H * WorkPlaces.Cell;
        f.Centre = new Vector3((p.X + p.W / 2f) * WorkPlaces.Cell, 0.09f, (p.Y + p.H / 2f) * WorkPlaces.Cell);
        float ux = fw * 0.96f * 0.88f / 2f - 0.5f, uz = fh * 0.96f * 0.88f / 2f - 0.55f;
        f.Rows = Math.Clamp((int)(2f * uz / Pitch) + 1, MinRows, MaxRows);
        float pitch = Math.Min(Pitch, 2f * uz / Math.Max(1, f.Rows - 1));
        int n = Math.Clamp((int)(2f * ux / Piece), 4, MaxPieces);
        int wp = _w.WorkplaceOf(f.Plot);
        f.Places = wp >= 0 ? t.Workplace(wp).Places : 2;
        var rng = new Random(f.Plot * 7919 + 5);
        double now = t.Time;
        for (int r = 0; r < f.Rows; r++)
        {
            f.RowZ[r] = (-(f.Rows - 1) / 2f + r) * pitch;
            bool outer = MathF.Abs(f.RowZ[r]) > uz * 0.72f;
            f.N[r] = outer ? n - 1 : n;
            f.X0[r] = 0.2f + (float)rng.NextDouble() * 0.4f;
            f.X1[r] = 0.2f + (float)rng.NextDouble() * 0.4f;
            f.Front[r] = 0;
            f.Active[r] = false;
            for (int k = 0; k < MaxPieces; k++)
            {
                int i = r * MaxPieces + k;
                f.Cut[i] = double.NegativeInfinity;
                f.Shown[i] = St.None; f.Prev[i] = St.None;
                // a new field grows in row by row; a loaded town shows its fields at once
                f.T[i] = lived ? 1f : -(r * 0.09f + k * 0.04f);
            }
            for (int s = 0; s < 2; s++) { f.StookOn[r * 2 + s] = false; f.StookGrow[r * 2 + s] = 0f; }
            f.TakenFrom[r] = -1;
        }
        if (lived)
        {
            // Lived in: the rows its work spots reach are part cut, from the plot's number (the same town looks the same).
            for (int s = 0; s < Math.Min(f.Places, f.Rows - 2); s++)
            {
                int r = RowOf(f.Rows, s);
                int nr = f.N[r];
                f.Active[r] = true;
                f.Front[r] = rng.Next(nr);
                for (int j = 1; j < nr; j++)
                {
                    int k = ((f.Front[r] - j) % nr + nr) % nr;
                    f.Cut[r * MaxPieces + k] = now - 0.8 - j * 3.0;
                }
                int back = (f.Front[r] - 1 + nr) % nr;
                LayStook(f, r, back, grown: true);
            }
            for (int r = 0; r < f.Rows; r++)
                for (int k = 0; k < f.N[r]; k++) { int i = r * MaxPieces + k; f.Shown[i] = Logical(f, r, k, now); f.T[i] = 1f; }
        }
        f.Built = true;
        _dirty = true;
    }

    private float PieceX(Field f, int r, int k)
    {
        int n = f.N[r];
        float x = (k - (n - 1) / 2f) * Piece;
        if (k == 0) x += f.X0[r];
        if (k == n - 1) x -= f.X1[r];
        return x;
    }

    /// <summary>What a piece of a row is now: the front, or by its age since it was cut.</summary>
    private static St Logical(Field f, int r, int k, double now)
    {
        if (f.Active[r] && f.Front[r] == k) return St.Edge;
        double age = now - f.Cut[r * MaxPieces + k];
        if (age < Swath) return St.Swath;
        if (age < Stubble) return St.Stubble;
        if (age < Shoots) return St.Shoots;
        if (age < Ripe) return St.Green;
        return St.Ripe;
    }

    private void LayStook(Field f, int r, int k, bool grown)
    {
        int a = r * 2, b = a + 1;
        int slot = !f.StookOn[a] ? a : !f.StookOn[b] ? b : (f.StookAge[a] < f.StookAge[b] ? a : b);
        f.StookOn[slot] = true;
        f.StookX[slot] = PieceX(f, r, k);
        f.StookGrow[slot] = grown ? 1f : 0.0001f;
        f.StookAge[slot] = ++f.StookSerial;
    }

    // ---- events ----

    public void UnitFinished(int plot, int slot)
    {
        var f = Of(plot);
        if (f == null || !f.Built) return;
        int r = RowOf(f.Rows, slot);
        int n = f.N[r];
        if (!f.Active[r]) { f.Active[r] = true; f.Front[r] = 0; }
        int k = f.Front[r];
        f.Cut[r * MaxPieces + k] = _w.Town.Time;
        f.Front[r] = (k + 1) % n;
        f.Steps++;
        FrontSteps++;
        LayStook(f, r, k, grown: false);
        _dirty = true;
    }

    /// <summary>The sheaf carried off from <paramref name="slot"/>'s row takes up the older stook (one is always left).</summary>
    public void Taken(int plot, int slot)
    {
        var f = Of(plot);
        if (f == null || !f.Built) return;
        int r = RowOf(f.Rows, slot), a = r * 2, b = a + 1;
        if (!(f.StookOn[a] && f.StookOn[b])) return;
        int old = f.StookAge[a] < f.StookAge[b] ? a : b;
        f.StookOn[old] = false;   // shrinks away (StookGrow eases to 0)
        f.TakenFrom[r] = old;
        _dirty = true;
    }

    // ---- positions ----

    public Vector3 WorkSpot(int plot, int slot, out float yaw)
    {
        yaw = MathF.PI / 2f;   // facing the standing crop (+X)
        var f = Of(plot);
        if (f == null || !f.Built) return Vector3.Zero;
        int r = RowOf(f.Rows, slot);
        float x = PieceX(f, r, f.Front[r]) - Piece * 0.55f;
        return f.Centre + new Vector3(x, 0.03f, f.RowZ[r] + 0.15f);
    }

    public Vector3 StookAt(int plot, int slot)
    {
        var f = Of(plot);
        if (f == null || !f.Built) return Vector3.Zero;
        int r = RowOf(f.Rows, slot), a = r * 2, b = a + 1;
        // the stook being taken up now (the sheaf rises from it), else the older one standing
        int s = f.TakenFrom[r] >= 0 && !f.StookOn[f.TakenFrom[r]] && f.StookGrow[f.TakenFrom[r]] > 0f ? f.TakenFrom[r]
            : f.StookOn[a] && (!f.StookOn[b] || f.StookAge[a] < f.StookAge[b]) ? a : b;
        return f.Centre + new Vector3(f.StookX[s], 0.45f, f.RowZ[r] + 0.1f);
    }

    /// <summary>The share of a field's pieces that are gold now (an edge piece counts half; tests).</summary>
    public float GoldShare(int plot)
    {
        var f = Of(plot);
        if (f == null || !f.Built) return 1f;
        double now = _w.Town.Time;
        float gold = 0; int all = 0;
        for (int r = 0; r < f.Rows; r++)
            for (int k = 0; k < f.N[r]; k++)
            {
                var s = Logical(f, r, k, now);
                gold += s == St.Ripe ? 1f : s == St.Edge ? 0.5f : 0f;
                all++;
            }
        return all == 0 ? 1f : gold / all;
    }

    /// <summary>The share of the drawn crop that shows gold (ripe pieces by their drawn height; tests).</summary>
    public float DrawnGoldShare(int plot)
    {
        var f = Of(plot);
        if (f == null || !f.Built) return 1f;
        float gold = 0; int all = 0;
        for (int r = 0; r < f.Rows; r++)
            for (int k = 0; k < f.N[r]; k++)
            {
                int i = r * MaxPieces + k;
                float e = WorkPlaces.Smooth(f.T[i]);
                float g(St s) => s == St.Ripe ? 1f : s == St.Edge ? 0.5f : 0f;
                gold += g(f.Shown[i]) * e + g(f.Prev[i]) * (1f - e);
                all++;
            }
        return all == 0 ? 1f : gold / all;
    }

    /// <summary>A row's front (piece index) on a field, for tests; -1 when the row was never worked.</summary>
    public int FrontOf(int plot, int row)
    {
        var f = Of(plot);
        return f == null || !f.Built || row >= f.Rows || !f.Active[row] ? -1 : f.Front[row];
    }

    /// <summary>A field just bought is still growing in (its first crop rising row by row, with the plot's pop).</summary>
    public bool GrowingIn(int plot)
    {
        var f = Of(plot);
        if (f == null || !f.Built) return false;
        for (int r = 0; r < f.Rows; r++)
            for (int k = 0; k < f.N[r]; k++) { int i = r * MaxPieces + k; if (f.Prev[i] == St.None && f.T[i] < 1f) return true; }
        return false;
    }

    public int StepsAt(int plot) => Of(plot)?.Steps ?? 0;
    public int RowsAt(int plot) => Of(plot)?.Rows ?? 0;

    /// <summary>The largest change of any drawn piece's height share this frame (tests: never all at once).</summary>
    public float LastLargestChange { get; private set; }

    // ---- each frame ----

    public void Step(float dt)
    {
        var t = _w.Town;
        double now = t.Time;
        float largest = 0f;
        for (int fi = 0; fi < _nf; fi++)
        {
            var f = _f[fi];
            bool drawn = _w.Buildings.PlotDrawn(f.Plot);
            if (drawn && !f.Built) Build(f, lived: !_w.Primed);
            if (!drawn && f.Built) { f.Built = false; _dirty = true; }
            if (!f.Built) continue;
            for (int r = 0; r < f.Rows; r++)
            {
                for (int k = 0; k < f.N[r]; k++)
                {
                    int i = r * MaxPieces + k;
                    if (f.T[i] < 1f)
                    {
                        float before = WorkPlaces.Smooth(f.T[i]);
                        f.T[i] = Math.Min(1f, f.T[i] + dt / Fade);
                        largest = Math.Max(largest, WorkPlaces.Smooth(f.T[i]) - before);
                        _dirty = true;
                        if (f.Shown[i] == St.None) f.Shown[i] = Logical(f, r, k, now);
                        continue;
                    }
                    var want = Logical(f, r, k, now);
                    if (want != f.Shown[i]) { f.Prev[i] = f.Shown[i]; f.Shown[i] = want; f.T[i] = 0f; _dirty = true; }
                }
                for (int s = r * 2; s < r * 2 + 2; s++)
                {
                    float target = f.StookOn[s] ? 1f : 0f;
                    if (f.StookGrow[s] != target)
                    {
                        f.StookGrow[s] = Mathf.MoveToward(f.StookGrow[s], target, dt / WorkSites.TakeSeconds);   // taken up as the sheaf rises
                        _dirty = true;
                    }
                }
            }
        }
        LastLargestChange = largest;
        if (_dirty) Draw();
    }

    private void Draw()
    {
        _dirty = false;
        for (int k = 0; k < Kinds; k++) _count[k] = 0;
        int ns = 0;
        for (int fi = 0; fi < _nf; fi++)
        {
            var f = _f[fi];
            if (!f.Built) continue;
            for (int r = 0; r < f.Rows; r++)
            {
                for (int k = 0; k < f.N[r]; k++)
                {
                    int i = r * MaxPieces + k;
                    var at = f.Centre + new Vector3(PieceX(f, r, k), 0f, f.RowZ[r]);
                    float e = WorkPlaces.Smooth(Math.Max(0f, f.T[i]));
                    Put(f.Shown[i], at, e, r, k);
                    if (e < 1f) Put(f.Prev[i], at, 1f - e, r, k);
                }
                for (int s = r * 2; s < r * 2 + 2; s++)
                {
                    if (f.StookGrow[s] <= 0f) continue;
                    float g = WorkPlaces.Smooth(f.StookGrow[s]) * StookScale;
                    var b = Basis.Identity.Rotated(Vector3.Up, 0.4f + s * 1.3f).Scaled(new Vector3(g, g, g));
                    _stooks.SetInstanceTransform(ns++, new Transform3D(b, f.Centre + new Vector3(f.StookX[s], 0f, f.RowZ[r] + 0.1f)));
                }
            }
        }
        for (int k = 0; k < Kinds; k++) _mm[k].VisibleInstanceCount = _count[k];
        _stooks.VisibleInstanceCount = ns;
    }

    private void Put(St s, Vector3 at, float h, int r, int k)
    {
        if (s == St.None || h <= 0.001f) return;
        int kind = (int)s;
        float yaw = s == St.Edge ? MathF.PI : (k + r) % 2 == 0 ? 0f : MathF.PI;
        // grows from (and shrinks into) the ridge: height first, a little width
        var b = Basis.Identity.Rotated(Vector3.Up, yaw).Scaled(new Vector3(0.85f + 0.15f * h, h, 0.85f + 0.15f * h));
        _mm[kind].SetInstanceTransform(_count[kind]++, new Transform3D(b, at));
    }
}
