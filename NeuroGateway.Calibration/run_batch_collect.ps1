# Poll an Anthropic Message Batch until complete, then download results.
# Saves each response to Outputs/raw_responses/{chemical}_{mode}.txt
# Then run `dotnet run -- assemble` to produce ShadowProfiles.yaml
#
# Usage: .\run_batch_collect.ps1 [batch_id]

$ErrorActionPreference = "Stop"

$apiKey = [System.Environment]::GetEnvironmentVariable("ANTHROPIC_API_KEY", "User")
if (-not $apiKey) {
    Write-Host "ERROR: ANTHROPIC_API_KEY not set at user level" -ForegroundColor Red
    exit 1
}

$outputDir = Join-Path $PSScriptRoot "Outputs"
$rawDir = Join-Path $outputDir "raw_responses"

# Get batch ID from argument or batch_info.txt
$batchId = $args[0]
if (-not $batchId) {
    $infoPath = Join-Path $outputDir "batch_info.txt"
    if (-not (Test-Path $infoPath)) {
        Write-Host "ERROR: No batch ID provided and no batch_info.txt found." -ForegroundColor Red
        Write-Host "Usage: .\run_batch_collect.ps1 [batch_id]"
        exit 1
    }
    $batchId = (Get-Content $infoPath)[0].Trim()
}

Write-Host "Checking batch: $batchId" -ForegroundColor Cyan

$headers = @{
    "x-api-key" = $apiKey
    "anthropic-version" = "2023-06-01"
}

# Poll until processing_status == "ended"
while ($true) {
    $batch = Invoke-RestMethod `
        -Uri "https://api.anthropic.com/v1/messages/batches/$batchId" `
        -Method Get `
        -Headers $headers

    $status = $batch.processing_status
    $counts = $batch.request_counts

    if ($status -eq "ended") {
        Write-Host "Batch complete!" -ForegroundColor Green
        Write-Host "  Succeeded: $($counts.succeeded)"
        Write-Host "  Errored:   $($counts.errored)"
        Write-Host "  Canceled:  $($counts.canceled)"
        Write-Host "  Expired:   $($counts.expired)"
        break
    }

    Write-Host "Status: $status | Processing: $($counts.processing), Succeeded: $($counts.succeeded), Errored: $($counts.errored)" -ForegroundColor DarkGray
    Write-Host "Waiting 30s..." -ForegroundColor DarkGray
    Start-Sleep -Seconds 30
}

# Download results JSONL
Write-Host ""
Write-Host "Downloading results..." -ForegroundColor Cyan

$resultsUrl = "https://api.anthropic.com/v1/messages/batches/$batchId/results"
$resultsPath = Join-Path $outputDir "batch_results.jsonl"

Invoke-WebRequest `
    -Uri $resultsUrl `
    -Method Get `
    -Headers $headers `
    -OutFile $resultsPath

# Parse results and save individual response files
if (-not (Test-Path $rawDir)) { New-Item -ItemType Directory -Path $rawDir | Out-Null }

$lines = Get-Content $resultsPath
$succeeded = 0
$failed = 0

foreach ($line in $lines) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    $result = $line | ConvertFrom-Json

    $customId = $result.custom_id
    $parts = $customId -split "__", 2
    if ($parts.Count -ne 2) { $failed++; continue }
    $chemical = $parts[0]
    $mode = $parts[1]

    if ($result.result.type -eq "succeeded") {
        # Extract text from content blocks
        $text = ($result.result.message.content | Where-Object { $_.type -eq "text" } | ForEach-Object { $_.text }) -join ""
        $outFile = Join-Path $rawDir "${chemical}__${mode}.txt"
        Set-Content -Path $outFile -Value $text -Encoding UTF8
        $succeeded++
        Write-Host "  ${chemical}/${mode}: saved" -ForegroundColor Green
    } else {
        $failed++
        Write-Host "  ${chemical}/${mode}: $($result.result.type)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "Saved $succeeded responses, $failed failed" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Yellow" })
Write-Host "Results JSONL: $resultsPath"
Write-Host ""
Write-Host "Now run: dotnet run --project NeuroGateway.Calibration -- assemble" -ForegroundColor Yellow
