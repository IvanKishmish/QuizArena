using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using QuizArena.Benchmarks;
using QuizArena.Benchmarks.Reporting;

var config = ManualConfig.CreateMinimumViable()
    .AddJob(Job.Default
        .WithWarmupCount(5)
        .WithIterationCount(20) // достатньо ітерацій для стійких перцентилів
        .WithGcServer(true))
    .AddDiagnoser(MemoryDiagnoser.Default)
    .AddColumn(StatisticColumn.P95)
    .AddColumn(new PercentileColumn(99)) // BenchmarkDotNet не має вбудованого P99 - власна колонка, Reporting/PercentileColumn.cs
    .AddColumn(StatisticColumn.Max)
    .AddColumn(StatisticColumn.StdDev)
    // Mann-Whitney equivalence test проти [Baseline] (MediatR).
    // Це вже готовий built-in клас BenchmarkDotNet - фабричний метод зветься
    // Create(...), а не CreateMannWhitney(...) (моя попередня помилка).
    .AddColumn(StatisticalTestColumn.Create("5%"))
    .AddExporter(CsvMeasurementsExporter.Default); // сирі виміри -> для власних перевірок p95/p99/тестів 

BenchmarkRunner.Run<SubmitAnswerBenchmarks>(config);

