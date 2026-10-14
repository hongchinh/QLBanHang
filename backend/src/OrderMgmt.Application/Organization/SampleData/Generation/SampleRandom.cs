using System.Text;

namespace OrderMgmt.Application.Organization.SampleData.Generation;

/// Deterministic random source seeded from the branch code, so the same code always yields the same dataset.
public sealed class SampleRandom
{
    private readonly Random _random;

    private SampleRandom(int seed) => _random = new Random(seed);

    public static SampleRandom ForBranch(string branchCode) =>
        new(unchecked((int)Fnv1a32(branchCode)));

    /// FNV-1a 32-bit over the UTF-8 bytes of `text`.
    public static uint Fnv1a32(string text)
    {
        var hash = 2166136261u;
        foreach (var b in Encoding.UTF8.GetBytes(text))
        {
            hash ^= b;
            hash = unchecked(hash * 16777619u);
        }
        return hash;
    }

    /// Integer in [min, max], both ends inclusive.
    public int Between(int min, int max) => _random.Next(min, max + 1);

    /// True with probability `percent`/100.
    public bool Chance(int percent) => _random.Next(0, 100) < percent;

    /// Uniform double in [0, 1), for factors such as 0.95 to 1.05.
    public double NextDouble() => _random.NextDouble();

    /// Uniform multiple of `step` offset from `min`, in [min, max].
    public decimal NextAmount(decimal min, decimal max, decimal step)
    {
        if (step <= 0) throw new ArgumentOutOfRangeException(nameof(step), "Step must be positive.");
        if (max < min) throw new ArgumentOutOfRangeException(nameof(max), "Max must not be below min.");
        return min + step * Between(0, (int)((max - min) / step));
    }

    public T Pick<T>(IReadOnlyList<T> items) => items[_random.Next(0, items.Count)];
}
