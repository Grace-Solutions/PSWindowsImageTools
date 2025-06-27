# Simple test to get catalog HTML structure
Write-Host "Testing Microsoft Update Catalog HTML structure..." -ForegroundColor Cyan

try {
    # Test with a simple query
    $query = "KB5000001"
    $encodedQuery = [System.Uri]::EscapeDataString($query)
    $searchUrl = "https://www.catalog.update.microsoft.com/Search.aspx?q=$encodedQuery"

    Write-Host "URL: $searchUrl" -ForegroundColor Gray

    # Make HTTP request using Invoke-WebRequest
    $response = Invoke-WebRequest -Uri $searchUrl -UseBasicParsing
    $html = $response.Content

    Write-Host "Response received. HTML length: $($html.Length) characters" -ForegroundColor Green

    # Save HTML for inspection
    $htmlFile = "catalog-response-$query.html"
    $html | Out-File -FilePath $htmlFile -Encoding UTF8
    Write-Host "HTML saved to: $htmlFile" -ForegroundColor Magenta

    # Basic analysis
    if ($html -match 'ctl00_catalogBody_updateMatches') {
        Write-Host "✅ Found expected table ID in HTML" -ForegroundColor Green
    } else {
        Write-Host "❌ Expected table ID NOT found in HTML" -ForegroundColor Red
    }

    if ($html -match 'No results found') {
        Write-Host "⚠️ 'No results found' message detected" -ForegroundColor Yellow
    }

    # Count table occurrences
    $tableMatches = [regex]::Matches($html, '<table[^>]*>')
    Write-Host "Tables found: $($tableMatches.Count)" -ForegroundColor Cyan

} catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n=== Test Complete ===" -ForegroundColor Cyan
