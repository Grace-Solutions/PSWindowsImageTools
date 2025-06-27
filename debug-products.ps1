# Debug Products parsing
Write-Host "Debugging Products Parsing" -ForegroundColor Cyan

# Get actual data from the module
Import-Module '.\Module\PSWindowsImageTools' -Force
$results = Search-WindowsUpdateCatalog -Query 'Windows 11 Security Update' -MaxResults 1

if ($results.Count -gt 0) {
    $productsText = $results[0].Products
    Write-Host "Raw Products: '$productsText'" -ForegroundColor Yellow
    Write-Host "Length: $($productsText.Length)" -ForegroundColor Gray
    
    # Show character codes for first 30 characters
    Write-Host "`nCharacter analysis (first 30 chars):" -ForegroundColor Cyan
    for ($i = 0; $i -lt [Math]::Min(30, $productsText.Length); $i++) {
        $char = $productsText[$i]
        $code = [int][char]$char
        if ($code -eq 32) {
            Write-Host "[$i]: SPACE" -ForegroundColor Yellow
        } elseif ($code -eq 160) {
            Write-Host "[$i]: NBSP" -ForegroundColor Red
        } else {
            Write-Host "[$i]: '$char'" -ForegroundColor Gray
        }
    }
    
    # Test different splitting approaches
    Write-Host "`nTesting splits:" -ForegroundColor Cyan
    
    # Try splitting by double space
    $split1 = $productsText -split '  '
    Write-Host "Split by '  ': $($split1.Count) items" -ForegroundColor White
    $split1 | ForEach-Object { Write-Host "  - '$_'" -ForegroundColor Gray }
    
    # Try splitting by non-breaking space
    $split2 = $productsText -split [char]160
    Write-Host "Split by NBSP: $($split2.Count) items" -ForegroundColor White
    $split2 | ForEach-Object { Write-Host "  - '$_'" -ForegroundColor Gray }
    
} else {
    Write-Host "No results found" -ForegroundColor Red
}

Write-Host "`nDebug completed" -ForegroundColor Cyan
