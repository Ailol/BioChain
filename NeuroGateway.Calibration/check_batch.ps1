# Quick check on batch status
# Usage: .\check_batch.ps1 [batch_id]

$apiKey = [System.Environment]::GetEnvironmentVariable("ANTHROPIC_API_KEY", "User")
$batchId = $args[0]
if (-not $batchId) {
    $infoPath = Join-Path $PSScriptRoot "Outputs\batch_info.txt"
    $batchId = (Get-Content $infoPath)[0].Trim()
}

$r = Invoke-RestMethod `
    -Uri "https://api.anthropic.com/v1/messages/batches/$batchId" `
    -Method Get `
    -Headers @{
        "x-api-key" = $apiKey
        "anthropic-version" = "2023-06-01"
    }

Write-Host "Batch:      $($r.id)"
Write-Host "Status:     $($r.processing_status)"
Write-Host "Processing: $($r.request_counts.processing)"
Write-Host "Succeeded:  $($r.request_counts.succeeded)"
Write-Host "Errored:    $($r.request_counts.errored)"
Write-Host "Canceled:   $($r.request_counts.canceled)"
Write-Host "Expired:    $($r.request_counts.expired)"
