using System;

namespace TideToll.Sim;

/// <summary>
/// Scratch for the bucket-queue route search (the finished toy's dijkstra in path.ts): a ring of buckets, each a linked
/// list of pushed cells, and a stamp per cell for "settled in this run". Allocated once (one per world); a search never
/// allocates. A ring wider than <see cref="MaxRing"/> falls back to the binary heap (the toy grows its ring instead;
/// the values are the same either way).
/// </summary>
public sealed class BucketQueue
{
    /// <summary>Most buckets in the ring (a cost range of hi / lo up to about MaxRing / 1.4).</summary>
    public int MaxRing => _head.Length;

    internal readonly int[] _head, _next, _node, _stamp;
    internal int _run;

    /// <param name="cells">Cells of the grid searched.</param>
    /// <param name="maxRing">Buckets in the ring (default 4096).</param>
    public BucketQueue(int cells, int maxRing = 4096)
    {
        _head = new int[Math.Max(4, maxRing)];
        // Every push follows an improvement along one of a settled cell's 8 edges, and each cell settles once.
        _next = new int[8 * cells + 1];
        _node = new int[8 * cells + 1];
        _stamp = new int[cells];
    }
}
