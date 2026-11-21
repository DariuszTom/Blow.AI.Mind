using System.Buffers.Binary;

namespace Blow.AI.Mind;

internal sealed class CryptographicRandomizer : Randomizer
{
    public CryptographicRandomizer() => localSeed = new CryptographicRandom();
}

internal sealed class CryptographicRandom : Random
{
    public override int Next() => RandomNumberGenerator.GetInt32(int.MaxValue);

    public override int Next(int maxValue) => maxValue == 0 ? 0 : RandomNumberGenerator.GetInt32(maxValue);

    public override int Next(int minValue, int maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minValue, maxValue);
        if (minValue == maxValue) return minValue;
        return (int)(minValue + (long)Below((ulong)((long)maxValue - minValue)));
    }

    public override long NextInt64() => (long)Below(long.MaxValue);

    public override long NextInt64(long maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxValue);
        return maxValue == 0 ? 0 : (long)Below((ulong)maxValue);
    }

    public override long NextInt64(long minValue, long maxValue)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minValue, maxValue);
        if (minValue == maxValue) return minValue;
        return unchecked((long)((ulong)minValue + Below(unchecked((ulong)(maxValue - minValue)))));
    }

    public override double NextDouble() => (Below(1UL << 53)) * (1.0 / (1UL << 53));

    public override float NextSingle() => (float)(Below(1UL << 24) * (1.0 / (1UL << 24)));

    public override void NextBytes(byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        RandomNumberGenerator.Fill(buffer);
    }

    public override void NextBytes(Span<byte> buffer) => RandomNumberGenerator.Fill(buffer);

    protected override double Sample() => NextDouble();

    private static ulong Below(ulong bound)
    {
        var threshold = unchecked(0UL - bound) % bound;
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        ulong value;
        do
        {
            RandomNumberGenerator.Fill(bytes);
            value = BinaryPrimitives.ReadUInt64LittleEndian(bytes);
        } while (value < threshold);
        return value % bound;
    }
}
