$b64 = Get-Content -Raw 'C:\Users\ailon\repo\MultiAgentAiMcp\cv\cv_b64.txt'
$b64 = $b64.Trim()

# Build JSON manually to avoid ConvertTo-Json wrapping long strings
$json = '{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"analyze_document_file","arguments":{"base64Content":"' + $b64 + '","documentType":"docx","targetPersonalityName":"ailo-cv","embeddings":false}}}'

[IO.File]::WriteAllText('C:\Users\ailon\repo\MultiAgentAiMcp\cv\request.json', $json)
Write-Host "Request file written"
