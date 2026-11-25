using System;

namespace TideToll.Sim;

/// <summary>
/// Per-cell override of a cell's state, for what-if costs: the toy's Ov bit set (preview.ts). Combine with |.
/// </summary>
[Flags]
public enum CostOverride : ushort
{
    None = 0,
    /// <summary>A new fence.</summary>
    Block = 1,
    /// <summary>A fence (and any gate in it) removed.</summary>
    Open = 2,
    /// <summary>Water that becomes a footbridge.</summary>
    Bridge = 4,
    /// <summary>A footbridge removed.</summary>
    CloseBridge = 8,
    Gravel = 16,
    Ungravel = 32,
    /// <summary>A fence cell that becomes a gate.</summary>
    Gate = 64,
    /// <summary>A gate that becomes plain fence.</summary>
    Ungate = 128,
    /// <summary>With Gate: one of the two cells of a stair-corner gate (half the delay each).</summary>
    GatePair = 256,
}

// The ground's routing cost (world.ts cost section of the finished toy): both walkers' views of a cell in one pass,
// the ordinary walkers' (cost, fields) and the frugal walkers' (costX, fieldsX), and blocking.
public sealed partial class World
{
    // Scratch of Grounds and CellCosts: [0] ordinary, [1] frugal (the toy's gc and cc).
    private double _gc0, _gc1, _cc0, _cc1;

    /// <summary>Whether walkers may not stand on cell c: fence (not a gate), footprint, or water that is neither ford nor bridge.</summary>
    public bool IsBlocked(int c)
    {
        if ((_fence[c] != 0 && _gate[c] == 0) || _map.Footprint[c] != 0) return true;
        if (_river != null) return RiverBlocked(c); // also the ice, the ford in a flood and a raised span
        return _map.Water[c] == 1 && _map.Ford[c] == 0 && _bridgeAt[c] < 0;
    }

    /// <summary>
    /// Both walkers' ground multipliers for cell c, into _gc0 (ordinary) and _gc1 (frugal): worn and busy ground is
    /// cheaper; gravel costs at most GravelCost. Frugal walkers see FrugalDisc of the wear and traffic discount, but all
    /// of the gravel. Operation order as the toy's grounds(), so every bit matches.
    /// </summary>
    private void Grounds(int c, bool gravel)
    {
        double f = _flowLut[Math.Min(_flowLut.Length - 1, (int)(_flow[c] * (double)FlowLutSteps))];
        double g = _p.TrailDiscount > 0
            ? _wearLut[Math.Min(_wearLut.Length - 1, (int)(_wear[c] * (double)WearLutSteps))]
            : _surfaceCost[(int)_p.GroundOf(_wear[c], _hollow[c] == 1)];
        double fr = 1 + (Math.Max(_p.MinSurface, f * g) - 1) * _p.FrugalDisc;
        _gc0 = gravel ? Math.Max(_p.GravelFloor, f * Math.Min(g, _p.GravelCost)) : Math.Max(_p.MinSurface, f * g);
        _gc1 = gravel ? Math.Min(fr, _p.GravelCost) : fr;
    }

    /// <summary>The ordinary walkers' ground multiplier for cell c (also their taste discount): the toy's surf.</summary>
    internal double Surf(int c)
    {
        Grounds(c, _gravel[c] != 0);
        return _gc0;
    }

    /// <summary>Step cost of cell c (+Infinity where blocked); <paramref name="ov"/> overrides its state for a what-if; <paramref name="frugal"/> gives the frugal walkers' cost.</summary>
    public double CellCost(int c, CostOverride ov = CostOverride.None, bool frugal = false)
    {
        CellCosts(c, ov);
        return frugal ? _cc1 : _cc0;
    }

    /// <summary>Step costs of cell c for both kinds of walker, into _cc0 (ordinary) and _cc1 (frugal).</summary>
    private void CellCosts(int c, CostOverride ov)
    {
        var m = _map;
        _cc0 = _cc1 = double.PositiveInfinity;
        if (m.Footprint[c] != 0) return;
        bool fenced = (ov & CostOverride.Block) != 0 || (_fence[c] != 0 && (ov & CostOverride.Open) == 0);
        bool gated = fenced && ((ov & CostOverride.Gate) != 0 || (_gate[c] != 0 && (ov & (CostOverride.Ungate | CostOverride.Block)) == 0));
        if (fenced && !gated) return;
        if (_river != null)
        {
            // Ferry water and drawbridge spans: their cost includes the expected and learned wait (a wait, not worn
            // ground, so the same in both views).
            double rc = RiverCellCost(c, ov);
            if (!double.IsNaN(rc)) { _cc0 = _cc1 = rc; return; }
        }
        bool bridge = (ov & CostOverride.Bridge) != 0 || (_bridgeAt[c] >= 0 && (ov & CostOverride.CloseBridge) == 0);
        bool ice = _river != null && _frozen && m.Water[c] != 0 && !bridge;
        if (m.Water[c] != 0 && m.Ford[c] == 0 && !bridge && !ice) return;
        double k, kx;
        if (bridge)
        {
            // A footbridge's deck gets the same traffic discount as land (floored at MinSurface) but none from wear.
            double flow = _flowLut[Math.Min(_flowLut.Length - 1, (int)(_flow[c] * (double)FlowLutSteps))];
            k = kx = _p.BridgeCost * (_p.BridgeFlow > 0 ? Math.Max(_p.MinSurface, flow) : 1);
        }
        else if (ice) { IceCosts(c); k = _gc0; kx = _gc1; }
        else
        {
            bool gravel = (ov & CostOverride.Gravel) != 0 || (_gravel[c] != 0 && (ov & CostOverride.Ungravel) == 0);
            double noise = 1 + _p.StaticNoise * m.Noise[c];
            // The ford never wears and is not made easier by traffic: only land is discounted.
            if (m.Water[c] != 0) k = kx = noise;
            else
            {
                Grounds(c, gravel);
                k = _gc0 * noise; kx = _gc1 * noise;
            }
            if (m.Crop[c] != 0) { k += _p.CropPenalty; kx += _p.CropPenalty; }
            if (m.Ford[c] != 0) { k *= _p.FordCostMul; kx *= _p.FordCostMul; }
            else if (_river != null) { double b = BandCostMul(c); k *= b; kx *= b; } // the flood band: wading, then mud
        }
        double rho = _occ[c] / _p.CellAreaM2;
        if (rho > 0.05)
        {
            double v = Math.Max(1 / _p.CrowdMaxCostMul, 1 - Math.Exp(-_p.WeidmannGamma * (1 / rho - 1 / _p.WeidmannRhoMax)));
            double crowd = 1 + _p.CrowdStrength * (1 / Math.Min(1, v) - 1);
            k *= crowd; kx *= crowd;
        }
        // A gate is a fixed delay: it does not grow with crowding. A two-cell gate splits it.
        if (gated)
        {
            bool pair = (ov & CostOverride.GatePair) != 0 || (_gatePair[c] != 0 && (ov & CostOverride.Gate) == 0);
            double g = pair ? _p.GateCost / 2 : _p.GateCost;
            k += g; kx += g;
        }
        _cc0 = k; _cc1 = kx;
    }

    /// <summary>Per-cell step cost for routing (ordinary walkers), written into <paramref name="output"/> as float (the toy's Float32Array).</summary>
    public void BuildCost(float[] output) => BuildCost(output, null, 0, _nc);

    /// <summary>Per-cell step costs for cells [from, to): ordinary walkers into <paramref name="output"/>, frugal ones into <paramref name="outputX"/> if given.</summary>
    public void BuildCost(float[] output, float[]? outputX, int from, int to)
    {
        for (int c = from; c < to; c++)
        {
            CellCosts(c, CostOverride.None);
            output[c] = (float)_cc0;
            if (outputX != null) outputX[c] = (float)_cc1;
        }
    }

    /// <summary>Rebuilds the world's own cost arrays (both views when frugal walkers exist).</summary>
    private void RebuildCosts() => BuildCost(_cost, _useFrugal ? _costX : null, 0, _nc);
}
