using System;
using System.Collections.Generic;

/// <summary>
/// Deterministic random-number source for dungeon generation.
/// Create one instance per generated dungeon and pass it to every generator.
/// </summary>
public sealed class SeededRandom
{
    private readonly System.Random random;

    public int Seed { get; }

    public SeededRandom(int seed)
    {
        Seed = seed;
        random = new System.Random(seed);
    }

    public int Range(int minInclusive, int maxExclusive)
    {
        if (maxExclusive < minInclusive)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), "The maximum cannot be less than the minimum.");

        if (maxExclusive == minInclusive)
            return minInclusive;

        return random.Next(minInclusive, maxExclusive);
    }

    public T Choose<T>(IReadOnlyList<T> values)
    {
        if (values == null || values.Count == 0)
            throw new ArgumentException("Cannot choose from an empty collection.", nameof(values));

        return values[Range(0, values.Count)];
    }
}
