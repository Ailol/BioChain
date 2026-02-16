using System.Text;
using NeuroGateway.Service;
using Spectre.Console;

namespace NeuroGateway.Calibration.Calibration;

/// <summary>
/// Thin CLI wrapper around CalibrationService — formats diagnostics with Spectre.Console.
/// </summary>
public class SignalAblation(CalibrationService calibrationService)
{
    private static readonly string OutputDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "NeuroGateway.Calibration", "Outputs");

    public async Task RunAsync()
    {
        AnsiConsole.MarkupLine("[bold]Running scoring diagnostics...[/]");

        var report = await calibrationService.RunDiagnosticsAsync();

        if (report.PersonCount == 0)
        {
            AnsiConsole.MarkupLine("[red]No profiles with evidence found in database.[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[dim]{report.PersonCount} persons scored.[/]");

        // Overall metrics
        var metricsTable = new Table()
            .AddColumn("Metric")
            .AddColumn("Value");

        metricsTable.AddRow("Mean Confidence", $"{report.Metrics.MeanConfidence:F3}");
        metricsTable.AddRow("Mean Consistency", $"{report.Metrics.MeanConsistency:F3}");
        metricsTable.AddRow("Mean Spread (stddev)", $"{report.Metrics.MeanSpread:F1}");
        metricsTable.AddRow("Discrimination", $"{report.Metrics.Discrimination:F3}");
        metricsTable.AddRow("Coverage Ratio", $"{report.Metrics.CoverageRatio:F3}");

        AnsiConsole.Write(metricsTable);

        // Per-dimension diagnostics
        var dimTable = new Table()
            .AddColumn("Dimension")
            .AddColumn("Section")
            .AddColumn("Mean Score")
            .AddColumn("StdDev")
            .AddColumn("Confidence")
            .AddColumn("Consistency")
            .AddColumn("Evidence")
            .AddColumn("Discrimination");

        foreach (var dim in report.Dimensions)
        {
            var discColor = dim.Discrimination > 0.5 ? "green"
                : dim.Discrimination > 0.2 ? "yellow" : "red";

            dimTable.AddRow(
                dim.Name,
                dim.Section,
                $"{dim.MeanScore:F1}",
                $"{dim.StdDev:F1}",
                $"{dim.MeanConfidence:F3}",
                $"{dim.MeanConsistency:F3}",
                $"{dim.TotalEvidence}",
                $"[{discColor}]{dim.Discrimination:F3}[/]");
        }

        AnsiConsole.Write(dimTable);

        // Write markdown report
        var md = new StringBuilder();
        md.AppendLine("# Scoring Diagnostics Report");
        md.AppendLine();
        md.AppendLine($"Date: {report.Timestamp:yyyy-MM-dd HH:mm} UTC");
        md.AppendLine($"Persons scored: {report.PersonCount}");
        md.AppendLine();
        md.AppendLine("## Overall Metrics");
        md.AppendLine();
        md.AppendLine("| Metric | Value |");
        md.AppendLine("|--------|-------|");
        md.AppendLine($"| Mean Confidence | {report.Metrics.MeanConfidence:F3} |");
        md.AppendLine($"| Mean Consistency | {report.Metrics.MeanConsistency:F3} |");
        md.AppendLine($"| Mean Spread | {report.Metrics.MeanSpread:F1} |");
        md.AppendLine($"| Discrimination | {report.Metrics.Discrimination:F3} |");
        md.AppendLine($"| Coverage | {report.Metrics.CoverageRatio:F3} |");
        md.AppendLine();
        md.AppendLine("## Per-Dimension Diagnostics");
        md.AppendLine();
        md.AppendLine("| Dimension | Section | Mean | StdDev | Confidence | Consistency | Evidence | Discrimination |");
        md.AppendLine("|-----------|---------|------|--------|------------|-------------|----------|----------------|");

        foreach (var dim in report.Dimensions)
        {
            md.AppendLine($"| {dim.Name} | {dim.Section} | {dim.MeanScore:F1} | {dim.StdDev:F1} | {dim.MeanConfidence:F3} | {dim.MeanConsistency:F3} | {dim.TotalEvidence} | {dim.Discrimination:F3} |");
        }

        Directory.CreateDirectory(OutputDir);
        var reportPath = Path.Combine(OutputDir, "diagnostics_report.md");
        await File.WriteAllTextAsync(reportPath, md.ToString());
        AnsiConsole.MarkupLine($"[green]Report written: {reportPath}[/]");
    }
}
