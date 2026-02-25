using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using BioChain.Service;

namespace BioChain.Server.Tools;

[McpServerToolType]
public class CalibrationTools(CalibrationService calibrationService)
{
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    [McpServerTool(Name = "run_calibration")]
    [Description("Run scoring diagnostics across all persons in the database. " +
                 "Evaluates dimension score discrimination, confidence, consistency, and coverage. " +
                 "Returns quality metrics and per-dimension diagnostics.")]
    public async Task<string> RunCalibration()
    {
        var report = await calibrationService.RunDiagnosticsAsync();
        return JsonSerializer.Serialize(report, IndentedJson);
    }
}
