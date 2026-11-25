using System;
using Godot;
using TideToll.Sim;

namespace TideToll.Game;

/// <summary>
/// Chapter I's rain on the ground (the toy's ground.ts): the whole map darkens toward a cool grey as the ground wets
/// (the weather's wetness, eased), and mud is drawn as wet trail, the same worn ground darker, richer and more solid with
/// a few puddle glints, on exactly the cells the library says are mud (World.Mud). While the mud dries (the last
/// SimParams.DrySimSec before Weather.MudUntil) each cell dries on its own: a cell worn to just trail when the drying
/// began dries first, the busiest track last (the toy's wetAmount and DryOrder). Never drawn wetter than the wetness
/// the walkers feel. A small R8 texture, uploaded with the wear while there is mud, cleared once when it is gone.
/// </summary>
public partial class MapGround
{
    private byte[] _wetMud = Array.Empty<byte>();
    private float[] _drySnap = Array.Empty<float>();
    private double _dryFor = double.NaN;
    private Image? _wetMudImg;
    private ImageTexture? _wetMudTex;
    private bool _mudShown;
    private float _wet = -1;
    private static readonly StringName RainWetS = new("rain_wet"), MudOnS = new("mud_on");

    /// <summary>The ground's wetness as drawn (eased), the wettest mud cell drawn (0..1) and mud uploads (tests).</summary>
    public float Wet => Math.Max(0, _wet);
    public float MudShownMax { get; private set; }
    public int MudUploads { get; private set; }

    /// <summary>The toy's DRY_SPREAD: the share of the drying window over which one cell goes from wet to dry.</summary>
    public const double DrySpread = 0.2;

    /// <summary>The toy's wetAmount: how wet a mud cell is drawn, from its wear when the drying began.</summary>
    public static double WetAmount(double wear, double dry, double wetness)
    {
        if (dry <= 0) return wetness;
        double r = wear <= 25 ? 0 : wear >= 85 ? 1 : (wear - 25) / 60;
        double a = ((1 - DrySpread) * r + DrySpread - dry) / DrySpread;
        double v = a <= 0 ? 0 : a >= 1 ? 1 : a;
        return v < wetness ? v : wetness;
    }

    private void InitRain()
    {
        _wetMud = new byte[_n * _n];
        _drySnap = new float[_n * _n];
        _wetMudImg = Image.CreateFromData(_n, _n, false, Image.Format.R8, _wetMud);
        _wetMudTex = ImageTexture.CreateFromImage(_wetMudImg);
        Material.SetShaderParameter("wetmud_tex", _wetMudTex);
        Material.SetShaderParameter(MudOnS, 0f);
        _mudShown = false;
        _dryFor = double.NaN;
        _wet = -1;
        MudShownMax = 0;
    }

    /// <summary>The ground's wetness, eased toward the weather's each frame (a new world snaps it).</summary>
    private void RainFrame(float dt)
    {
        var w = _sim.World;
        float target = w.RainOn ? (float)w.Weather.Wetness : 0f;
        float nv = _wet < 0 ? target : Mathf.MoveToward(_wet, target, dt * 0.8f);
        if (nv == _wet) return;
        _wet = nv;
        Material.SetShaderParameter(RainWetS, _wet);
    }

    /// <summary>Per-cell wetness of the mud into its texture (with the wear upload). Allocation-free.</summary>
    private void UploadMud()
    {
        var w = _sim.World;
        if (_wetMudImg == null) return;
        var wx = w.Weather;
        double wetness = w.RainOn ? wx.Wetness : 0;
        double dry = !w.RainOn ? 1 : wx.Raining ? -1 : 1 - (wx.MudUntil - w.Time) / w.Params.DrySimSec;
        bool any = w.RainOn && w.MudCells > 0 && dry < 1 && (dry > 0 || wetness > 0.01);
        if (!any)
        {
            if (!_mudShown) return;
            Array.Clear(_wetMud);
            _mudShown = false;
            MudShownMax = 0;
            Material.SetShaderParameter(MudOnS, 0f);
        }
        else
        {
            // The drying order: the wear as the drying window opens, kept through it.
            if (dry <= 0 || _dryFor != wx.MudUntil) { w.Wear.CopyTo(_drySnap); _dryFor = wx.MudUntil; }
            var mud = w.Mud;
            float most = 0;
            for (int c = 0; c < _wetMud.Length; c++)
            {
                float v = mud[c] != 0 ? (float)WetAmount(_drySnap[c], dry, wetness) : 0f;
                if (v > most) most = v;
                _wetMud[c] = (byte)(v * 255f + 0.5f);
            }
            MudShownMax = most;
            if (!_mudShown) Material.SetShaderParameter(MudOnS, 1f);
            _mudShown = true;
        }
        _wetMudImg.SetData(_n, _n, false, Image.Format.R8, _wetMud);
        _wetMudTex!.Update(_wetMudImg);
        MudUploads++;
    }
}
