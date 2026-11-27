using System;
using Godot;
using TideToll.Sim.Town;

namespace TideToll.Game;

/// <summary>
/// Trees on the meadow round the town, so the page is not bare: oaks, poplars, birches and hawthorn bushes on dry ground, willows near the
/// river, never inside the first age's town window (with a margin), on water, or on ground either age's town uses
/// (<see cref="TownSpace"/>). Placed from the seed by a fixed hash,
/// so the same town always has the same trees. Five MultiMeshes, built once.
/// </summary>
public static class TownTrees
{
    public static Node3D Make(TownPlan plan)
    {
        var root = new Node3D { Name = "Trees" };
        var map = plan.Map;
        int n = TownGround.N;
        // Night 3: five kinds, so a wood does not repeat; a missing new file falls back to the oak (ModelLib.Pick).
        string[] models = { "tree-oak", "tree-poplar", "tree-willow", ModelLib.Pick("tree-birch", "tree-oak"), ModelLib.Pick("tree-hawthorn", "tree-oak") };
        const int K = 5;
        var xf = new System.Collections.Generic.List<Transform3D>[K];
        for (int k = 0; k < K; k++) xf[k] = new();
        for (int k = 0; k < 4; k++) TownLife.RockSpot(plan, k, out _, out _, out _);   // trees stay off the bank's rocks
        uint h = (uint)(plan.Seed * 2654435761u + 12345u);
        for (int tries = 0; tries < 2600; tries++)
        {
            h = h * 1664525u + 1013904223u; float fx = (h >> 8) / 16777216f;
            h = h * 1664525u + 1013904223u; float fy = (h >> 8) / 16777216f;
            h = h * 1664525u + 1013904223u; float fr = (h >> 8) / 16777216f;
            int x = (int)(fx * (n - 4)) + 2, y = (int)(fy * (n - 4)) + 2;
            if (x >= plan.WindowX - 3 && x < plan.WindowX + plan.WindowW + 3 && y >= plan.WindowY - 3 && y < plan.WindowY + plan.WindowH + 3) continue;
            float dRiver = MathF.Abs(x + 0.5f - map.RiverX[y]) - (float)map.RiverHalfW;
            if (dRiver < 2.5f || map.Water[y * n + x] != 0) continue;
            // Round 3 (track LF): never on a lane, road, landing, crossing's end, plot or queue spot of either age; the
            // crown needs the cells round its trunk free too.
            if (!TownSpace.FreeRect(plan, x - 1, y - 1, x + 2, y + 2) || TownSpace.Yard(plan, x, y) || TownSpace.KeptClear(plan, x, y)) continue;
            // Clumps: keep a tree only where low-frequency noise says "wood", and keep the river bank open-ish.
            float clump = map.Noise.Length == n * n ? map.Noise[y * n + x] : 0.5f;
            if (clump < 0.45f && fr > 0.12f) continue;
            int kind = dRiver < 7f ? 2 : fr < 0.22f ? 1 : fr < 0.4f ? 3 : fr < 0.56f ? 4 : 0;
            float s = 0.85f + 0.35f * fr;
            var basis = new Basis(Vector3.Up, fr * 6.283f).Scaled(Vector3.One * s);
            xf[kind].Add(new Transform3D(basis, new Vector3((x + fx * 7 % 1) * TownGround.Cell, 0, (y + fy * 5 % 1) * TownGround.Cell)));
            int total = 0;
            for (int k = 0; k < K; k++) total += xf[k].Count;
            if (total >= 260) break;
        }
        for (int k = 0; k < K; k++)
        {
            if (xf[k].Count == 0) continue;
            var mmi = ModelLib.Multi(models[k], models[k], xf[k].Count);
            mmi.MaterialOverride = TreeSway.For(mmi.Multimesh.Mesh);
            for (int i = 0; i < xf[k].Count; i++) mmi.Multimesh.SetInstanceTransform(i, xf[k][i]);
            mmi.Multimesh.VisibleInstanceCount = xf[k].Count;
            root.AddChild(mmi);
        }
        return root;
    }
}
