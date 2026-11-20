using BenchmarkDotNet.Running;
using Blow.AI.Mind.Benchmarks;

BenchmarkSwitcher.FromAssembly(typeof(WorkbookGeneratorBenchmarks).Assembly).Run(args);
