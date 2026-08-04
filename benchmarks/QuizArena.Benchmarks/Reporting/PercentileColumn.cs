using System.Globalization;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace QuizArena.Benchmarks.Reporting;

// BenchmarkDotNet's built-in StatisticColumn лише виставляє фіксовані
// P0/P25/P50/P67/P80/P85/P90/P95/P100 (див. StatisticColumn.cs), а сам
// клас і CreatePercentileColumn() - private, тож ззовні не переюзати.
// Тому власна колонка з довільним персентилем (P99 і будь-який інший).
//
// Свідомо НЕ чіпаємо ResultStatistics.Sample (internal) і НЕ намагаємось
// повторити внутрішнє форматування BenchmarkDotNet (UnitHelper тощо) -
// беремо сирі виміри через публічний BenchmarkReport.GetResultRuns()
// і форматуємо простим C#, без залежності від внутрішнього API бібліотеки.
public sealed class PercentileColumn(int percentile) : IColumn
{
    public string Id => $"{nameof(PercentileColumn)}.P{percentile}";
    public string ColumnName => $"P{percentile}";
    public string Legend => $"Percentile {percentile}";

    public bool AlwaysShow => true;
    public ColumnCategory Category => ColumnCategory.Statistics;
    public int PriorityInCategory => 2; // після Quartile-групи, поруч із рештою персентилів
    public bool IsNumeric => true;
    public UnitType UnitType => UnitType.Time;

    public bool IsAvailable(Summary summary) => true;
    public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
        => GetValue(summary, benchmarkCase, SummaryStyle.Default);

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
    {
        var report = summary[benchmarkCase];
        if (report is null)
            return "NA";

        var values = report.GetResultRuns()
            .Select(m => m.Nanoseconds / m.Operations)
            .ToArray();
        var valueNs = CalculatePercentile(values, percentile);

        return double.IsNaN(valueNs) ? "NA" : FormatNanoseconds(valueNs);
    }

    public override string ToString() => ColumnName;

    private static double CalculatePercentile(IReadOnlyList<double> values, int percentileValue)
    {
        if (values.Count == 0)
            return double.NaN;

        var sorted = values.OrderBy(v => v).ToArray();
        if (sorted.Length == 1)
            return sorted[0];

        var rank = percentileValue / 100.0 * (sorted.Length - 1);
        var lowerIndex = (int)Math.Floor(rank);
        var upperIndex = (int)Math.Ceiling(rank);

        if (lowerIndex == upperIndex)
            return sorted[lowerIndex];

        var fraction = rank - lowerIndex;
        return sorted[lowerIndex] + (sorted[upperIndex] - sorted[lowerIndex]) * fraction;
    }
    
    private static string FormatNanoseconds(double ns) => ns switch
    {
        >= 1_000_000_000 => (ns / 1_000_000_000).ToString("N3", CultureInfo.InvariantCulture) + " s",
        >= 1_000_000 => (ns / 1_000_000).ToString("N3", CultureInfo.InvariantCulture) + " ms",
        >= 1_000 => (ns / 1_000).ToString("N3", CultureInfo.InvariantCulture) + " us",
        _ => ns.ToString("N3", CultureInfo.InvariantCulture) + " ns",
    };
}