# Test Windows 11 search to see if we get results
Write-Host "Testing Windows 11 search..." -ForegroundColor Cyan

try {
    $query = "Windows 11 Security Update"
    $encodedQuery = [System.Uri]::EscapeDataString($query)
    $searchUrl = "https://www.catalog.update.microsoft.com/Search.aspx?q=$encodedQuery"
    
    Write-Host "Query: $query" -ForegroundColor Gray
    Write-Host "URL: $searchUrl" -ForegroundColor Gray
    
    $response = Invoke-WebRequest -Uri $searchUrl -UseBasicParsing
    $html = $response.Content
    
    Write-Host "Response length: $($html.Length) characters" -ForegroundColor Green
    
    # Save HTML
    $htmlFile = "catalog-response-windows11.html"
    $html | Out-File -FilePath $htmlFile -Encoding UTF8
    Write-Host "HTML saved to: $htmlFile" -ForegroundColor Magenta
    
    # Check for results
    if ($html -match 'ctl00_catalogBody_updateMatches') {
        Write-Host "✅ Found expected table ID" -ForegroundColor Green
    } else {
        Write-Host "❌ Table ID not found" -ForegroundColor Red
    }
    
    if ($html -match 'We did not find any results') {
        Write-Host "⚠️ No results message found" -ForegroundColor Yellow
    } else {
        Write-Host "✅ Results may be present" -ForegroundColor Green
    }
    
    # Count table rows if table exists
    $tableMatches = [regex]::Matches($html, '<tr[^>]*>')
    Write-Host "Table rows found: $($tableMatches.Count)" -ForegroundColor Cyan
    
} catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n=== Test Complete ===" -ForegroundColor Cyan
