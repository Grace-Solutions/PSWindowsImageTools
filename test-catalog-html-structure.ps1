# Test script to examine Microsoft Update Catalog HTML structure
# This will help us debug why the catalog search isn't finding results

Add-Type -AssemblyName System.Net.Http
Add-Type -Path ".\Module\PSWindowsImageTools\bin\HtmlAgilityPack.dll"

# Create HTTP client
$httpClient = New-Object System.Net.Http.HttpClient

try {
    Write-Host "Testing Microsoft Update Catalog HTML structure..." -ForegroundColor Cyan
    
    # Test different search queries
    $testQueries = @(
        "KB5000001",
        "Windows 11", 
        "Security Update",
        "2024-01"
    )
    
    foreach ($query in $testQueries) {
        Write-Host "`n=== Testing Query: '$query' ===" -ForegroundColor Yellow
        
        # Build search URL
        $encodedQuery = [System.Uri]::EscapeDataString($query)
        $searchUrl = "https://www.catalog.update.microsoft.com/Search.aspx?q=$encodedQuery"
        
        Write-Host "URL: $searchUrl" -ForegroundColor Gray
        
        try {
            # Make HTTP request
            $response = $httpClient.GetAsync($searchUrl).Result
            $response.EnsureSuccessStatusCode()
            $html = $response.Content.ReadAsStringAsync().Result
            
            Write-Host "Response received. HTML length: $($html.Length) characters" -ForegroundColor Green
            
            # Parse with HtmlAgilityPack
            $doc = New-Object HtmlAgilityPack.HtmlDocument
            $doc.LoadHtml($html)
            
            # Look for various table structures
            Write-Host "`nAnalyzing HTML structure:" -ForegroundColor Cyan
            
            # Check for the expected table ID
            $expectedTable = $doc.DocumentNode.SelectSingleNode("//table[@id='ctl00_catalogBody_updateMatches']")
            if ($expectedTable) {
                Write-Host "✅ Found expected table: ctl00_catalogBody_updateMatches" -ForegroundColor Green
                $rows = $expectedTable.SelectNodes(".//tr")
                Write-Host "   Rows found: $($rows.Count)" -ForegroundColor Green
            } else {
                Write-Host "❌ Expected table 'ctl00_catalogBody_updateMatches' NOT found" -ForegroundColor Red
            }
            
            # Look for any tables
            $allTables = $doc.DocumentNode.SelectNodes("//table")
            if ($allTables) {
                Write-Host "`nAll tables found:" -ForegroundColor Cyan
                foreach ($table in $allTables) {
                    $id = $table.GetAttributeValue("id", "")
                    $class = $table.GetAttributeValue("class", "")
                    $rowCount = $table.SelectNodes(".//tr")?.Count ?? 0
                    Write-Host "  Table ID: '$id', Class: '$class', Rows: $rowCount" -ForegroundColor White
                }
            } else {
                Write-Host "❌ No tables found at all!" -ForegroundColor Red
            }
            
            # Look for divs that might contain results
            $resultDivs = $doc.DocumentNode.SelectNodes("//div[contains(@id, 'result') or contains(@class, 'result') or contains(@id, 'update') or contains(@class, 'update')]")
            if ($resultDivs) {
                Write-Host "`nResult-related divs found:" -ForegroundColor Cyan
                foreach ($div in $resultDivs) {
                    $id = $div.GetAttributeValue("id", "")
                    $class = $div.GetAttributeValue("class", "")
                    Write-Host "  Div ID: '$id', Class: '$class'" -ForegroundColor White
                }
            }
            
            # Look for any elements with 'update' in ID or class
            $updateElements = $doc.DocumentNode.SelectNodes("//*[contains(@id, 'update') or contains(@class, 'update')]")
            if ($updateElements) {
                Write-Host "`nUpdate-related elements found:" -ForegroundColor Cyan
                foreach ($element in $updateElements) {
                    $tagName = $element.Name
                    $id = $element.GetAttributeValue("id", "")
                    $class = $element.GetAttributeValue("class", "")
                    Write-Host "  $tagName - ID: '$id', Class: '$class'" -ForegroundColor White
                }
            }
            
            # Check for error messages or "no results" indicators
            $noResultsText = $doc.DocumentNode.SelectNodes("//text()[contains(., 'No results') or contains(., 'no results') or contains(., 'not found')]")
            if ($noResultsText) {
                Write-Host "`n'No results' text found:" -ForegroundColor Yellow
                foreach ($text in $noResultsText) {
                    Write-Host "  '$($text.InnerText.Trim())'" -ForegroundColor Yellow
                }
            }
            
            # Save HTML for manual inspection (first query only)
            if ($query -eq $testQueries[0]) {
                $htmlFile = "catalog-response-$($query.Replace(' ', '-')).html"
                $html | Out-File -FilePath $htmlFile -Encoding UTF8
                Write-Host "`nHTML saved to: $htmlFile" -ForegroundColor Magenta
            }
            
        } catch {
            Write-Host "❌ Error testing query '$query': $($_.Exception.Message)" -ForegroundColor Red
        }
        
        Start-Sleep -Milliseconds 500  # Be nice to Microsoft's servers
    }
    
} finally {
    $httpClient.Dispose()
}

Write-Host "`n=== Test Complete ===" -ForegroundColor Cyan
Write-Host "Check the saved HTML file to manually inspect the structure." -ForegroundColor Gray
