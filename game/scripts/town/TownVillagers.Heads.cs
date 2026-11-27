using System;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// n7 trades (build sheet part 3): a villager wears its trade's headwear (carrier: kerchief, reaper: straw hat, miller:
/// flat cap and apron); a Helper (no trade) wears hair only, in one of three dark tones by its number. Heads and the apron
/// are their own MultiMeshes, never the tunic shader's. A change of trade is a small puff: the old head shrinks into the
/// neck and the new one grows out of it over <see cref="HeadSwapSeconds"/>, never in one frame.
/// </summary>
public partial class TownVillagers
{
    /// <summary>The six heads: the Helpers' hair (mud, earth, soot), then the kerchief, the straw hat, the miller's cap.</summary>
    public static readonly string[] HeadModels =
    {
        "villager-head-helper-mud", "villager-head-helper-earth", "villager-head-helper-soot",
        "villager-head-kerchief", "villager-head-strawhat", "villager-head-cap",
    };
    public const int HeadKinds = 6, HelperHeads = 3, MillerHead = 5;
    /// <summary>Fix round trial (review: the kerchief and the cap carry less than the straw hat at the play view): the
    /// kerchief's and the cap's drawn size, about the neck. 1 = as modelled.</summary>
    public static float WearScale = 1f;
    /// <summary>Length of a head's change (shrink and grow), seconds of frame time.</summary>
    public const float HeadSwapSeconds = 0.3f;
    /// <summary>Height of the neck in model units (the point the head shrinks into).</summary>
    private const float NeckY = 1.95f, WaistY = 1.2f;

    /// <summary>The head a villager of trade <paramref name="t"/> wears: a Helper's hair tone by its number, else its trade's.</summary>
    public static int HeadOf(int i, Trade t) => t == Trade.None ? i % HelperHeads : HelperHeads - 1 + (int)t;
    public static bool IsHelperHead(int k) => k >= 0 && k < HelperHeads;

    private readonly MultiMeshInstance3D?[] _headKinds = new MultiMeshInstance3D?[HeadKinds];
    private readonly int[] _headCount = new int[HeadKinds];
    private MultiMeshInstance3D? _aprons;
    private int _apronCount;
    // Per villager: the head drawn now, the one it is changing to, and the change's clock (0 none, up to HeadSwapSeconds).
    private readonly sbyte[] _headShown = new sbyte[Capacity], _headWant = new sbyte[Capacity];
    private readonly bool[] _headFresh = new bool[Capacity];
    private readonly float[] _headSwap = new float[Capacity], _headScale = new float[Capacity];

    /// <summary>The head drawn for villager i now (tests), its scale now (1 at rest), and the largest change of any head's
    /// scale in the last frame (tests: never a jump).</summary>
    public int HeadShown(int i) => _headShown[i];
    public float HeadScale(int i) => _headScale[i];
    public float MaxHeadStep { get; private set; }
    /// <summary>Heads drawn last frame per kind (tests).</summary>
    public int HeadsDrawn(int kind) => _headCount[kind];
    public int ApronsDrawn => _apronCount;

    private void InitHeads()
    {
        for (int h = 0; h < HeadKinds; h++)
        {
            string hm = ModelLib.Has(HeadModels[h]) && ModelLib.HasPart(HeadModels[h], "head") ? HeadModels[h] : "villager";
            _headKinds[h] = new MultiMeshInstance3D
            {
                Name = "Heads" + h,
                Multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, InstanceCount = Capacity, Mesh = SoftInk(ModelLib.Part(hm, "head")) },
            };
            AddChild(_headKinds[h]);
        }
        if (ModelLib.Has("villager-apron") && ModelLib.HasPart("villager-apron", "apron"))
        {
            _aprons = new MultiMeshInstance3D
            {
                Name = "Aprons",
                Multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, InstanceCount = Capacity, Mesh = SoftInk(ModelLib.Part("villager-apron", "apron")) },
            };
            AddChild(_aprons);
        }
        Array.Fill(_headShown, (sbyte)-1);
        Array.Fill(_headScale, 1f);
    }

    private void ResetHeads()
    {
        Array.Fill(_headShown, (sbyte)-1);
        Array.Clear(_headSwap);
        Array.Fill(_headScale, 1f);
    }

    private void BeginHeads() { Array.Clear(_headCount); _apronCount = 0; MaxHeadStep = 0f; }

    /// <summary>Villager i's head and apron this frame, on the body's transform <paramref name="xf"/>.</summary>
    private void DrawHead(int i, Trade trade, bool fresh, in Transform3D xf, float dt)
    {
        int want = HeadOf(i, trade);
        if (fresh || _headShown[i] < 0) { _headShown[i] = (sbyte)want; _headWant[i] = (sbyte)want; _headSwap[i] = 0f; }
        else if (want != _headWant[i])
        {
            // A new change: start it, or (already growing a new head) turn it back to shrinking from where it is.
            _headWant[i] = (sbyte)want;
            if (_headSwap[i] <= 0f) _headSwap[i] = 1e-4f;
            else if (_headSwap[i] > HeadSwapSeconds * 0.5f) _headSwap[i] = HeadSwapSeconds - _headSwap[i];
        }
        float s = 1f;
        if (_headSwap[i] > 0f)
        {
            _headSwap[i] += dt;
            float half = HeadSwapSeconds * 0.5f;
            if (_headSwap[i] >= half && _headShown[i] != _headWant[i]) _headShown[i] = _headWant[i];
            if (_headSwap[i] >= HeadSwapSeconds) _headSwap[i] = 0f;
            else
            {
                float u = Math.Abs(_headSwap[i] - half) / half;   // 1 at the ends, 0 in the middle
                s = u * u * (3f - 2f * u);
            }
        }
        MaxHeadStep = Math.Max(MaxHeadStep, Math.Abs(s - _headScale[i]));
        _headScale[i] = s;
        int k = _headShown[i];
        float ws = k == HelperHeads || k == MillerHead ? WearScale : 1f;   // the kerchief and the cap (a trial: 1 = as modelled)
        var hx = s * ws >= 0.999f && s * ws <= 1.001f ? xf : xf * About(NeckY, s * ws);
        var mm = _headKinds[k];
        if (mm != null && s > 0.02f) mm.Multimesh.SetInstanceTransform(_headCount[k]++, hx);
        if (k == MillerHead && _aprons != null && s > 0.02f)
            _aprons.Multimesh.SetInstanceTransform(_apronCount++, s >= 0.999f ? xf : xf * About(WaistY, s));
    }

    private static Transform3D About(float y, float s) =>
        new(Basis.Identity.Scaled(new Vector3(s, s, s)), new Vector3(0, y * (1f - s), 0));

    private void EndHeads()
    {
        for (int h = 0; h < HeadKinds; h++) if (_headKinds[h] != null) _headKinds[h]!.Multimesh.VisibleInstanceCount = _headCount[h];
        if (_aprons != null) _aprons.Multimesh.VisibleInstanceCount = _apronCount;
    }
}
