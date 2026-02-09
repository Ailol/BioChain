param([string]$tool, [string]$argsJson = "{}")

$body = @{
    jsonrpc = "2.0"
    id = 1
    method = "tools/call"
    params = @{
        name = $tool
        arguments = ($argsJson | ConvertFrom-Json)
    }
} | ConvertTo-Json -Depth 5

$headers = @{
    "Accept" = "application/json, text/event-stream"
    "Content-Type" = "application/json"
}

try {
    $response = Invoke-WebRequest -Uri "http://localhost:13370/mcp" -Method POST -Headers $headers -Body $body -TimeoutSec 300
    Write-Host "Status: $($response.StatusCode)"
    Write-Host $response.Content
} catch {
    Write-Host "Error: $($_.Exception.Message)"
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        Write-Host $reader.ReadToEnd()
    }
}
