# Test HTML parsing logic using the captured HTML
Write-Host "Testing HTML Parsing Logic" -ForegroundColor Cyan

try {
    # Load the HTML content we captured earlier
    $htmlContent = Get-Content -Path "catalog-response-windows11.html" -Raw
    Write-Host "HTML content loaded: $($htmlContent.Length) characters" -ForegroundColor Green
    
    # Load HtmlAgilityPack
    Add-Type -Path "Module\PSWindowsImageTools\bin\HtmlAgilityPack.dll"
    Write-Host "HtmlAgilityPack loaded" -ForegroundColor Green
    
    # Parse the HTML
    $doc = New-Object HtmlAgilityPack.HtmlDocument
    $doc.LoadHtml($htmlContent)
    Write-Host "HTML parsed" -ForegroundColor Green
    
    # Find the results table
    $resultsTable = $doc.GetElementbyId("ctl00_catalogBody_updateMatches")
    if ($resultsTable) {
        Write-Host "✅ Results table found!" -ForegroundColor Green
        
        # Get all rows
        $allRows = $resultsTable.SelectNodes(".//tr")
        Write-Host "Total rows: $($allRows.Count)" -ForegroundColor Cyan
        
        # Filter data rows (exclude header)
        $dataRows = $allRows | Where-Object { $_.Id -ne "headerRow" -and $_.Id -ne $null -and $_.Id -ne "" }
        Write-Host "Data rows: $($dataRows.Count)" -ForegroundColor Cyan
        
        if ($dataRows.Count -gt 0) {
            Write-Host "`nAnalyzing first data row:" -ForegroundColor Yellow
            $firstRow = $dataRows[0]
            Write-Host "Row ID: $($firstRow.Id)" -ForegroundColor Gray
            
            # Extract update ID from row ID
            if ($firstRow.Id -match "^([a-f0-9\-]+)_R\d+$") {
                $updateId = $matches[1]
                Write-Host "✅ Update ID extracted: $updateId" -ForegroundColor Green
            } else {
                Write-Host "❌ Failed to extract update ID from: $($firstRow.Id)" -ForegroundColor Red
            }
            
            # Get cells
            $cells = $firstRow.SelectNodes(".//td")
            Write-Host "Cell count: $($cells.Count)" -ForegroundColor Cyan
            
            if ($cells.Count -ge 7) {
                Write-Host "`nCell contents:" -ForegroundColor Yellow
                for ($i = 0; $i -lt [Math]::Min(8, $cells.Count); $i++) {
                    $cellText = $cells[$i].InnerText.Trim()
                    if ($cellText.Length -gt 100) { $cellText = $cellText.Substring(0, 100) + "..." }
                    Write-Host "  C$i`: $cellText" -ForegroundColor Gray
                }
                
                # Test parsing according to our specification
                Write-Host "`nParsing according to specification:" -ForegroundColor Yellow
                
                # C1: Title
                $titleCell = $cells[1]
                $titleLink = $titleCell.SelectSingleNode(".//a")
                if ($titleLink) {
                    $title = $titleLink.InnerText.Trim()
                    Write-Host "✅ Title: $title" -ForegroundColor Green
                } else {
                    $title = $titleCell.InnerText.Trim()
                    Write-Host "⚠️ Title (no link): $title" -ForegroundColor Yellow
                }
                
                # C2: Products
                $products = $cells[2].InnerText.Trim()
                Write-Host "✅ Products: $products" -ForegroundColor Green
                
                # C3: Classification
                $classification = $cells[3].InnerText.Trim()
                Write-Host "✅ Classification: $classification" -ForegroundColor Green
                
                # C4: Last Updated
                $lastUpdated = $cells[4].InnerText.Trim()
                Write-Host "✅ Last Updated: $lastUpdated" -ForegroundColor Green
                
                # C5: Version
                $version = $cells[5].InnerText.Trim()
                Write-Host "✅ Version: $version" -ForegroundColor Green
                
                # C6: Size
                $size = $cells[6].InnerText.Trim()
                Write-Host "✅ Size: $size" -ForegroundColor Green
                
                # Extract KB number
                if ($title -match "\(KB(\d+)\)") {
                    $kbNumber = "KB" + $matches[1]
                    Write-Host "✅ KB Number: $kbNumber" -ForegroundColor Green
                } else {
                    Write-Host "⚠️ No KB number found in title" -ForegroundColor Yellow
                }
                
                Write-Host "`n🎉 HTML parsing logic is working correctly!" -ForegroundColor Green
                Write-Host "The fixed implementation should be able to parse this data." -ForegroundColor Green
                
            } else {
                Write-Host "❌ Insufficient cells: $($cells.Count) (need at least 7)" -ForegroundColor Red
            }
        } else {
            Write-Host "❌ No data rows found" -ForegroundColor Red
        }
    } else {
        Write-Host "❌ Results table not found" -ForegroundColor Red
    }
    
} catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`nTest completed" -ForegroundColor Cyan
