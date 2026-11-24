using System;

namespace TideToll.Sim;

/// <summary>
/// Min-heap of (double key, int value), ported from the toy's Heap. Kept instead of
/// PriorityQueue so pop order on equal keys matches the toy exactly, and so it can be
/// reused across fields without allocating (Clear just resets the count).
/// </summary>
public sealed class BinaryHeap
{
    private double[] _keys;
    private int[] _vals;

    public BinaryHeap(int capacity)
    {
        _keys = new double[Math.Max(1, capacity)];
        _vals = new int[Math.Max(1, capacity)];
    }

    public int Count { get; private set; }

    /// <summary>Key of the value returned by the last <see cref="Pop"/>.</summary>
    public double LastKey { get; private set; }

    public void Clear() => Count = 0;

    public void Push(double key, int val)
    {
        if (Count == _keys.Length) Grow();
        double[] k = _keys;
        int[] v = _vals;
        int i = Count++;
        while (i > 0)
        {
            int p = (i - 1) >> 1;
            if (k[p] <= key) break;
            k[i] = k[p]; v[i] = v[p]; i = p;
        }
        k[i] = key; v[i] = val;
    }

    public int Pop()
    {
        if (Count == 0) throw new InvalidOperationException("Pop from an empty heap.");
        double[] k = _keys;
        int[] v = _vals;
        int top = v[0];
        LastKey = k[0];
        int n = --Count;
        double key = k[n];
        int val = v[n];
        int i = 0;
        for (;;)
        {
            int c = 2 * i + 1;
            if (c >= n) break;
            if (c + 1 < n && k[c + 1] < k[c]) c++;
            if (k[c] >= key) break;
            k[i] = k[c]; v[i] = v[c]; i = c;
        }
        k[i] = key; v[i] = val;
        return top;
    }

    private void Grow()
    {
        Array.Resize(ref _keys, _keys.Length * 2);
        Array.Resize(ref _vals, _vals.Length * 2);
    }
}
