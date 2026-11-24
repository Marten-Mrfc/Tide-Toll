namespace TideToll.Sim.Tests;

public class HeapTests
{
    [Fact]
    public void PopsInKeyOrderAndGrows()
    {
        var heap = new BinaryHeap(2); // small on purpose, so Push has to grow it
        var rng = new Mulberry32(3);
        var keys = new double[500];
        for (int i = 0; i < keys.Length; i++) { keys[i] = Math.Floor(rng.NextDouble() * 50); heap.Push(keys[i], i); }
        double last = double.NegativeInfinity;
        var seen = new HashSet<int>();
        while (heap.Count > 0)
        {
            int v = heap.Pop();
            Assert.True(heap.LastKey >= last);
            Assert.Equal(keys[v], heap.LastKey);
            Assert.True(seen.Add(v));
            last = heap.LastKey;
        }
        Assert.Equal(keys.Length, seen.Count);
    }

    [Fact]
    public void ClearEmptiesAndPopOnEmptyThrows()
    {
        var heap = new BinaryHeap(4);
        heap.Push(1, 1);
        heap.Clear();
        Assert.Equal(0, heap.Count);
        Assert.Throws<InvalidOperationException>(() => heap.Pop());
    }
}
