$b64 = Get-Content -Raw 'C:\Users\ailon\repo\MultiAgentAiMcp\cv\cv_b64.txt'
$body = @{
    jsonrpc = "2.0"
    id = 1
    method = "tools/call"
    params = @{
        name = "analyze_document_file"
        arguments = @{
            base64Content = $b64
            documentType = "docx"
            targetPersonalityName = "ailo-cv"
            embeddings = $false
        }
    }
} | ConvertTo-Json -Depth 5 -Compress

$response = Invoke-RestMethod -Uri 'http://localhost:13370/mcp' -Method POST -ContentType 'application/json' -Body $body -TimeoutSec 600
$response
