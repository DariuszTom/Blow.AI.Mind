using BenchmarkDotNet.Attributes;

namespace Blow.AI.Mind.Benchmarks;

[MemoryDiagnoser]
public class WorkbookGeneratorBenchmarks
{
    [Benchmark(Baseline = true)]
    public static async Task<long> Plain()
    {
        using var output = new MemoryStream();
        await WorkbookGenerator.Create().WithTemplate(Templates.UserSecrets).WithRows(100).GenerateAsync(output);
        return output.Length;
    }

    [Benchmark]
    public static async Task<long> Encrypted()
    {
        using var output = new MemoryStream();
        await WorkbookGenerator.Create().WithTemplate(Templates.UserSecrets).WithRows(100).Encrypted().GenerateAsync(output);
        return output.Length;
    }
}