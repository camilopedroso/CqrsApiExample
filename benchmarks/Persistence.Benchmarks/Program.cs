using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using CqrsApiExample.Persistence.Benchmarks;

// EF Core vs ADO.NET on the same SQL Server, through the real persistence implementations.
//
//   dotnet run -c Release --project benchmarks/Persistence.Benchmarks                     # all
//   dotnet run -c Release --project benchmarks/Persistence.Benchmarks -- --filter *Read*  # a subset
//
// Results: BenchmarkDotNet.Artifacts/results/*-report-github.md (a Markdown table).

await using var database = await BenchmarkDatabase.StartAsync();

// In-process: the benchmarks share the database prepared above (and no child project is
// generated, which would inherit this repository's strict build settings).
var config = ManualConfig.Create(DefaultConfig.Instance)
                         .AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance))
                         .AddDiagnoser(MemoryDiagnoser.Default)
                         .AddColumn(StatisticColumn.P95, CategoriesColumn.Default)
                         .AddExporter(MarkdownExporter.GitHub)
                         .AddLogicalGroupRules(BenchmarkLogicalGroupRule.ByCategory)
                         .HideColumns(Column.Job);

// Without arguments BenchmarkSwitcher asks interactively which benchmarks to run; run them all.
string[] arguments = args.Length > 0 ? args : ["--filter", "*"];
BenchmarkSwitcher.FromAssembly(typeof(ReadBenchmarks).Assembly).Run(arguments, config);
