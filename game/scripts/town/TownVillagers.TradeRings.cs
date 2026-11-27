using System;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// n7 trades: hovering a line of the Trades card rings that trade's members on the map (the hover ring's look, one
/// MultiMesh). The rings fade in over 0.1 s; a change from one trade to another fades the old rings out first.
/// </summary>
public partial class TownVillagers
{
    /// <summary>The trade whose members are ringed (-1 none; else (int)Trade, 0 the Helpers); set by the interface.</summary>
    public int HoverTrade = -1;
    private MultiMeshInstance3D _tradeRings = null!;
    private StandardMaterial3D _tradeMat = null!;
    private int _tradeShown = -1;
    private float _tradeA;
    /// <summary>Opacity of the trade rings and how many are drawn (tests).</summary>
    public float TradeRingAlpha => _tradeA;
    public int TradeRingsDrawn { get; private set; }
    public int TradeRingsFor => _tradeA > 0.001f ? _tradeShown : -1;

    private void InitTradeRings()
    {
        _tradeMat = RingMaterial();
        _tradeRings = new MultiMeshInstance3D
        {
            Name = "TradeRings", Visible = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, MaterialOverride = _tradeMat,
            Multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, InstanceCount = Capacity, Mesh = RingMesh(0.98f, 1.16f) },
        };
        AddChild(_tradeRings);
    }

    private void TradeRingFrame(Town t, float dt, int n)
    {
        int want = HoverTrade;
        // Another trade: the old rings fade out before the new ones fade in.
        bool towards = want >= 0 && want == _tradeShown;
        if (want >= 0 && _tradeShown < 0) { _tradeShown = want; towards = true; }
        _tradeA = Math.Clamp(_tradeA + (towards ? dt : -dt) / 0.1f, 0f, 1f);
        if (_tradeA <= 0f) _tradeShown = want;
        bool show = _tradeA > 0.001f && _tradeShown >= 0;
        if (_tradeRings.Visible != show) _tradeRings.Visible = show;
        TradeRingsDrawn = 0;
        if (!show) { _tradeRings.Multimesh.VisibleInstanceCount = 0; return; }
        _tradeMat.AlbedoColor = new Color(UiKit.Wheat, _tradeA * 0.9f);
        var b = Basis.Identity.Scaled(Vector3.One * WalkerScale);
        int c = 0;
        for (int i = 0; i < n; i++)
        {
            if ((int)t.Villager(i).Trade != _tradeShown) continue;
            _tradeRings.Multimesh.SetInstanceTransform(c++, new Transform3D(b, FeetLocal(i) + new Vector3(0, 0.06f, 0)));
        }
        _tradeRings.Multimesh.VisibleInstanceCount = c;
        TradeRingsDrawn = c;
    }
}
