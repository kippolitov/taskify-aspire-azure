using BenchmarkDotNet.Running;
using Taskify.Benchmarks;

BenchmarkSwitcher.FromAssembly(typeof(TaskListBenchmark).Assembly).Run(args);
