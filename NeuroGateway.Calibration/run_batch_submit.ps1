# Submit shadow profile generation requests as an Anthropic Message Batch.
# Prerequisites: run `dotnet run -- export-prompts` first to create batch_requests.jsonl
#
# Usage: .\run_batch_submit.ps1

$ErrorActionPreference = "Stop"

$apiKey = [System.Environment]::GetEnvironmentVariable("ANTHROPIC_API_KEY", "User")
if (-not $apiKey) {
    Write-Host "ERROR: ANTHROPIC_API_KEY not set at user level" -ForegroundColor Red
    exit 1
}

$outputDir = Join-Path $PSScriptRoot "Outputs"
$jsonlPath = Join-Path $outputDir "batch_requests.jsonl"

if (-not (Test-Path $jsonlPath)) {
    Write-Host "ERROR: $jsonlPath not found. Run 'dotnet run -- export-prompts' first." -ForegroundColor Red
    exit 1
}

# Read JSONL and build batch request body
$lines = Get-Content $jsonlPath
$requests = @()
foreach ($line in $lines) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $obj = $line | ConvertFrom-Json
    $requests += @{
        custom_id = $obj.custom_id
        params = $obj.params
    }
}

Write-Host "Submitting batch with $($requests.Count) requests..." -ForegroundColor Cyan

$body = @{ requests = $requests } | ConvertTo-Json -Depth 10 -Compress

$response = Invoke-RestMethod `
    -Uri "https://api.anthropic.com/v1/messages/batches" `
    -Method Post `
    -Headers @{
        "x-api-key" = $apiKey
        "anthropic-version" = "2023-06-01"
        "content-type" = "application/json"
    } `
    -Body $body `
    -TimeoutSec 120

$batchId = $response.id
$status = $response.processing_status

Write-Host "Batch submitted: $batchId" -ForegroundColor Green
Write-Host "Status: $status"
Write-Host "Requests: $($requests.Count)"

# Save batch info
$infoPath = Join-Path $outputDir "batch_info.txt"
@($batchId, (Get-Date -Format "o"), $requests.Count) | Set-Content $infoPath

Write-Host ""
Write-Host "Batch ID saved to $infoPath"
Write-Host "Collect results with: .\run_batch_collect.ps1" -ForegroundColor Yellow
