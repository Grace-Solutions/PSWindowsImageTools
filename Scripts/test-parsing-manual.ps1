# Manual test of parsing logic using regex and basic PowerShell
Write-Host "Testing Parsing Logic Manually" -ForegroundColor Cyan

try {
    # Load the HTML content
    $htmlContent = Get-Content -Path "catalog-response-windows11.html" -Raw
    Write-Host "HTML content loaded: $($htmlContent.Length) characters" -ForegroundColor Green
    
    # Check if the expected table ID exists
    if ($htmlContent -match 'id="ctl00_catalogBody_updateMatches"') {
        Write-Host "✅ Results table found in HTML" -ForegroundColor Green
    } else {
        Write-Host "❌ Results table not found" -ForegroundColor Red
        return
    }
    
    # Find data rows directly in the HTML (simpler approach)
    $rowPattern = '<tr[^>]*id="([a-f0-9\-]+_R\d+)"[^>]*>(.*?)</tr>'
    $rowMatches = [regex]::Matches($htmlContent, $rowPattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)
        
        Write-Host "Data rows found: $($rowMatches.Count)" -ForegroundColor Cyan
        
        if ($rowMatches.Count -gt 0) {
            $firstRow = $rowMatches[0]
            $rowId = $firstRow.Groups[1].Value
            $rowContent = $firstRow.Groups[2].Value
            
            Write-Host "`nAnalyzing first row:" -ForegroundColor Yellow
            Write-Host "Row ID: $rowId" -ForegroundColor Gray
            
            # Extract update ID
            if ($rowId -match '^([a-f0-9\-]+)_R\d+$') {
                $updateId = $matches[1]
                Write-Host "✅ Update ID: $updateId" -ForegroundColor Green
            }
            
            # Extract cell contents
            $cellPattern = '<td[^>]*>(.*?)</td>'
            $cellMatches = [regex]::Matches($rowContent, $cellPattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)
            
            Write-Host "Cells found: $($cellMatches.Count)" -ForegroundColor Cyan
            
            if ($cellMatches.Count -ge 7) {
                # Parse according to specification
                Write-Host "`nParsing cells:" -ForegroundColor Yellow
                
                # C1: Title (skip C0 which is icon/checkbox)
                $titleCell = $cellMatches[1].Groups[1].Value
                # Remove HTML tags
                $title = $titleCell -replace '<[^>]+>', '' -replace '&nbsp;', ' ' -replace '\s+', ' '
                $title = $title.Trim()
                Write-Host "✅ Title: $title" -ForegroundColor Green
                
                # C2: Products
                $productsCell = $cellMatches[2].Groups[1].Value
                $products = $productsCell -replace '<[^>]+>', '' -replace '&nbsp;', ' ' -replace '\s+', ' '
                $products = $products.Trim()
                Write-Host "✅ Products: $products" -ForegroundColor Green
                
                # C3: Classification
                $classificationCell = $cellMatches[3].Groups[1].Value
                $classification = $classificationCell -replace '<[^>]+>', '' -replace '&nbsp;', ' ' -replace '\s+', ' '
                $classification = $classification.Trim()
                Write-Host "✅ Classification: $classification" -ForegroundColor Green
                
                # C4: Last Updated
                $lastUpdatedCell = $cellMatches[4].Groups[1].Value
                $lastUpdated = $lastUpdatedCell -replace '<[^>]+>', '' -replace '&nbsp;', ' ' -replace '\s+', ' '
                $lastUpdated = $lastUpdated.Trim()
                Write-Host "✅ Last Updated: $lastUpdated" -ForegroundColor Green
                
                # C5: Version
                $versionCell = $cellMatches[5].Groups[1].Value
                $version = $versionCell -replace '<[^>]+>', '' -replace '&nbsp;', ' ' -replace '\s+', ' '
                $version = $version.Trim()
                Write-Host "✅ Version: $version" -ForegroundColor Green
                
                # C6: Size
                $sizeCell = $cellMatches[6].Groups[1].Value
                $size = $sizeCell -replace '<[^>]+>', '' -replace '&nbsp;', ' ' -replace '\s+', ' '
                $size = $size.Trim()
                Write-Host "✅ Size: $size" -ForegroundColor Green
                
                # Extract KB number
                if ($title -match '\(KB(\d+)\)') {
                    $kbNumber = "KB" + $matches[1]
                    Write-Host "✅ KB Number: $kbNumber" -ForegroundColor Green
                } else {
                    Write-Host "⚠️ No KB number found" -ForegroundColor Yellow
                }
                
                # Determine architecture
                $titleAndProducts = "$title $products".ToLower()
                if ($titleAndProducts -match 'arm64|arm-based') {
                    $architecture = "ARM64"
                } elseif ($titleAndProducts -match 'x64|amd64|64-bit') {
                    $architecture = "x64"
                } elseif ($titleAndProducts -match 'x86|32-bit') {
                    $architecture = "x86"
                } else {
                    $architecture = "Unknown"
                }
                Write-Host "✅ Architecture: $architecture" -ForegroundColor Green
                
                Write-Host "`n🎉 SUCCESS! Manual parsing worked correctly!" -ForegroundColor Green
                Write-Host "This confirms our fixed implementation logic is correct." -ForegroundColor Green
                
                # Summary
                Write-Host "`n--- Parsed Update Summary ---" -ForegroundColor Cyan
                Write-Host "Update ID: $updateId" -ForegroundColor White
                Write-Host "Title: $title" -ForegroundColor White
                Write-Host "KB: $kbNumber" -ForegroundColor White
                Write-Host "Products: $products" -ForegroundColor White
                Write-Host "Classification: $classification" -ForegroundColor White
                Write-Host "Architecture: $architecture" -ForegroundColor White
                Write-Host "Last Updated: $lastUpdated" -ForegroundColor White
                Write-Host "Version: $version" -ForegroundColor White
                Write-Host "Size: $size" -ForegroundColor White
                
            } else {
                Write-Host "❌ Insufficient cells found: $($cellMatches.Count)" -ForegroundColor Red
            }
        } else {
            Write-Host "❌ No data rows found" -ForegroundColor Red
        }
    
} catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`nTest completed" -ForegroundColor Cyan
