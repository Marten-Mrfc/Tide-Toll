using System;
using System.Collections.Generic;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Round n3 (asset track RV): what grows and lies on the river's banks in the second age. Reeds in rows of five to nine (three
/// shapes) (reeds-small, reeds, reeds-tall) along both banks, standing on the wet lip and in the shallows, and a few
/// bank stones, on a seeded scatter (the same town always has the same banks). Never across a crossing, a lane, the
/// landing, a plot or a berth. Three MultiMeshes and one for the stones, built once when the river plan is drawn; nothing
/// runs per frame. The ground shader draws the earth lip and the shallows (ground.gdshader, "the bank").
/// </summary>
public static class RiverBanks
{
    private const float Cell = TownGround.Cell;
    private static readonly string[] Kinds = { "reeds-small", "reeds", "reeds-tall" };

    /// <summary>For tests and the audit: reeds planted by the last build.</summary>
    public static int ReedsPlanted { get; private set; }

    public static Node3D Build(Town t, RiverPlan rp)
    {
        var root = new Node3D { Name = "Banks" };
        var map = t.Plan.Map;
        float half = (float)map.RiverHalfW * Cell;
        int rows = map.RiverX.Length;
        float len = rows * Cell;
        var rng = new Random(rp.Seed * 7919 + 101);
        var xf = new List<Transform3D>[Kinds.Length + 1];
        for (int k = 0; k < xf.Length; k++) xf[k] = new List<Transform3D>();
        foreach (int side in new[] { -1, 1 })
        {
            float z = 2f + (float)rng.NextDouble() * 3f;
            while (z < len - 2f)
            {
                // After the review: a row of five to nine reeds along the bank (a band of reed, not dots), the sizes
                // mixed with the tall ones toward the water, then a gap.
                int n = 5 + rng.Next(5);
                float off0 = -0.4f + (float)rng.NextDouble() * 0.9f;
                for (int i = 0; i < n; i++)
                {
                    float zz = z + i * (0.75f + (float)rng.NextDouble() * 0.35f);
                    float off = off0 + ((float)rng.NextDouble() - 0.5f) * 0.7f;   // the row wanders a little
                    float x = CentreAt(map.RiverX, zz) + side * (half + off);
                    if (!Clear(t, rp, x, zz, 0f)) continue;
                    int kind = off < 0f ? 2 - rng.Next(2) : (i == 0 || i == n - 1 ? 0 : rng.Next(3));  // small at the row's ends
                    float s = 0.85f + (float)rng.NextDouble() * 0.3f;
                    var basis = Basis.FromEuler(new Vector3(0, (float)rng.NextDouble() * MathF.Tau, 0)).Scaled(Vector3.One * s);
                    xf[kind].Add(new Transform3D(basis, new Vector3(x, 0, zz)));
                }
                z += n * 0.9f;
                // Now and then a stone on the lip.
                if (rng.NextDouble() < 0.22)
                {
                    float zz = z + 1.5f + (float)rng.NextDouble() * 1.5f;
                    float x = CentreAt(map.RiverX, zz) + side * (half + 0.2f + (float)rng.NextDouble() * 0.8f);
                    if (Clear(t, rp, x, zz, 0f))
                    {
                        var basis = Basis.FromEuler(new Vector3(0, (float)rng.NextDouble() * MathF.Tau, 0)).Scaled(Vector3.One * (0.7f + (float)rng.NextDouble() * 0.6f));
                        xf[Kinds.Length].Add(new Transform3D(basis, new Vector3(x, 0, zz)));
                    }
                }
                z += 3f + (float)rng.NextDouble() * 6f;
            }
        }
        ReedsPlanted = 0;
        for (int k = 0; k <= Kinds.Length; k++)
        {
            string model = k < Kinds.Length ? ModelLib.Pick(Kinds[k], "reeds") : ModelLib.Pick("bank-stones", "ford-stones");
            if (!ModelLib.Has(model) || xf[k].Count == 0) continue;
            var mmi = ModelLib.Multi(model, k < Kinds.Length ? "Reeds" + k : "Stones", xf[k].Count);
            mmi.CastShadow = k < Kinds.Length ? GeometryInstance3D.ShadowCastingSetting.Off : GeometryInstance3D.ShadowCastingSetting.On;
            for (int i = 0; i < xf[k].Count; i++) mmi.Multimesh.SetInstanceTransform(i, xf[k][i]);
            mmi.Multimesh.VisibleInstanceCount = xf[k].Count;
            if (k < Kinds.Length) ReedsPlanted += xf[k].Count;
            root.AddChild(mmi);
        }
        GD.Print($"river banks: {ReedsPlanted} reeds, {xf[Kinds.Length].Count} stones");
        return root;
    }

    /// <summary>The river's centre x (metres) at map z (metres), as the ground shader reads it (linear between rows).</summary>
    public static float CentreAt(float[] riverX, float z)
    {
        float r = z / Cell - 0.5f;
        int r0 = Math.Clamp((int)MathF.Floor(r), 0, riverX.Length - 1), r1 = Math.Min(riverX.Length - 1, r0 + 1);
        float f = Math.Clamp(r - r0, 0f, 1f);
        return (riverX[r0] + (riverX[r1] - riverX[r0]) * f) * Cell;
    }

    /// <summary>True when (x, z) metres is clear of every crossing and lane, every plot, the berths and the landing.</summary>
    private static bool Clear(Town t, RiverPlan rp, float x, float z, float pad)
    {
        float cx = x / Cell, cz = z / Cell;   // map cells
        foreach (var l in rp.Links)
        {
            float keep = l.Kind == RiverLinkKind.Lane ? 1.3f : 3.5f;   // cells (a lane may run along the bank: keep off its track only)
            for (int i = 0; i + 1 < l.Px.Length; i++)
                if (SegDist(cx, cz, (float)l.Px[i], (float)l.Py[i], (float)l.Px[i + 1], (float)l.Py[i + 1]) < keep + pad) return false;
            if (l.Px.Length == 1 && Math.Abs(cx - l.Px[0]) + Math.Abs(cz - l.Py[0]) < keep + pad) return false;
        }
        foreach (var p in rp.Plots)
        {
            float m = p.Kind == RiverPlotKind.Landing ? 4.5f : 1f;
            if (cx > p.X - m && cx < p.X + p.W + m && cz > p.Y - m && cz < p.Y + p.H + m) return false;
        }
        for (int b = 0; b < rp.BerthX.Length; b++)
            if (Math.Abs(cz - rp.BerthY[b]) < 5f && Math.Abs(cx - rp.BerthX[b]) < 6f) return false;
        for (int i = 0; i < rp.Px.Length; i++)
            if ((cx - rp.Px[i]) * (cx - rp.Px[i]) + (cz - rp.Py[i]) * (cz - rp.Py[i]) < 9f) return false;
        return true;
    }

    private static float SegDist(float px, float pz, float ax, float az, float bx, float bz)
    {
        float dx = bx - ax, dz = bz - az;
        float l2 = dx * dx + dz * dz;
        float k = l2 < 1e-6f ? 0f : Math.Clamp(((px - ax) * dx + (pz - az) * dz) / l2, 0f, 1f);
        float qx = ax + k * dx - px, qz = az + k * dz - pz;
        return MathF.Sqrt(qx * qx + qz * qz);
    }
}
