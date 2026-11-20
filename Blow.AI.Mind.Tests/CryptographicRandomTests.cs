using System.Reflection;
using Bogus;
using Blow.AI.Mind.Injections;

namespace Blow.AI.Mind.Tests;

public class CryptographicRandomTests
{
    [Test]
    public void Bogus_randomizer_uses_crypto_backed_source()
    {
        var randomizer = new CryptographicRandomizer();
        var field = typeof(Randomizer).GetField("localSeed", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        field.ShouldNotBeNull();
        field.GetValue(randomizer).ShouldBeOfType<CryptographicRandom>();
        randomizer.Bytes(32).Length.ShouldBe(32);
        randomizer.Number(1, 10).ShouldBeInRange(1, 10);
    }

    [Test]
    public void Random_implements_range_and_argument_contracts()
    {
        Random random = new CryptographicRandom();
        random.Next(0).ShouldBe(0);
        random.Next(4, 4).ShouldBe(4);
        random.NextInt64(0).ShouldBe(0);
        random.NextInt64(long.MinValue, long.MinValue).ShouldBe(long.MinValue);
        Should.Throw<ArgumentOutOfRangeException>(() => random.Next(-1));
        Should.Throw<ArgumentOutOfRangeException>(() => random.Next(2, 1));
        Should.Throw<ArgumentNullException>(() => random.NextBytes((byte[])null!));
        for (var i = 0; i < 100; i++)
        {
            random.Next(int.MinValue, int.MaxValue).ShouldBeLessThan(int.MaxValue);
            random.NextInt64(long.MinValue, long.MaxValue).ShouldBeLessThan(long.MaxValue);
            random.NextDouble().ShouldBeInRange(0, 1);
        }
    }

    [Test]
    public async Task All_plain_injections_fit_exactly_into_matching_rows()
    {
        var count = InjectionLibrary.BuiltIn.Count;
        var (_, file) = await TestHelpers.GenerateAsync(WorkbookGenerator.Create().WithRows(count)
            .WithInjections(options => options.WithDifficulty(Difficulty.Plain)));
        file.Injections.Count.ShouldBe(count);
        file.Injections.SelectMany(i => i.Cells).ShouldBeUnique();
    }
}
