$env:Llm__Orchestrator__Model = "claude-sonnet-4-5-20250929"
$env:Llm__Orchestrator__ApiKey = [System.Environment]::GetEnvironmentVariable("ANTHROPIC_API_KEY", "User")

if (-not $env:Llm__Orchestrator__ApiKey) {
    Write-Host "ERROR: ANTHROPIC_API_KEY not set at user level"
    exit 1
}

Write-Host "Using model: $($env:Llm__Orchestrator__Model)"
Write-Host "API key: $($env:Llm__Orchestrator__ApiKey.Substring(0, 10))..."

# Pass through any extra arguments (e.g., --chemical dopamine --mode work)
dotnet run --project "$PSScriptRoot\NeuroGateway.Calibration.csproj" -- generate @args
