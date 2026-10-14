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

    /// Integer in [min, maxExclusive).
    public int Next(int min, int maxExclusive) => _random.Next(min, maxExclusive);

    /// Decimal in [min, max], rounded to whole units (VND amounts).
    public decimal NextDecimal(decimal min, decimal max) =>
        min + Math.Round((decimal)_random.NextDouble() * (max - min), MidpointRounding.AwayFromZero);

    public T Pick<T>(IReadOnlyList<T> items) => items[_random.Next(0, items.Count)];
}
