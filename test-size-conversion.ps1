# Test Size Conversion Methods
Write-Host "Testing Size Conversion Methods" -ForegroundColor Cyan

Import-Module '.\Module\PSWindowsImageTools' -Force
Write-Host "Module loaded" -ForegroundColor Green

$results = Search-WindowsUpdateCatalog -Query 'Windows 11 Security Update' -MaxResults 1
Write-Host "Search completed, results: $($results.Count)" -ForegroundColor Green

if ($results.Count -gt 0) {
    $update = $results[0]
    
    Write-Host "`nUpdate Information:" -ForegroundColor Yellow
    Write-Host "Title: $($update.Title)" -ForegroundColor White
    Write-Host "SizeInBytes: $($update.SizeInBytes)" -ForegroundColor White
    Write-Host "SizeFormatted: $($update.SizeFormatted)" -ForegroundColor White
    
    Write-Host "`nTesting Size Conversion Methods:" -ForegroundColor Yellow
    
    try {
        $kb = $update.ToKB()
        Write-Host "ToKB(): $kb KB" -ForegroundColor Green
        
        $mb = $update.ToMB()
        Write-Host "ToMB(): $mb MB" -ForegroundColor Green
        
        $gb = $update.ToGB()
        Write-Host "ToGB(): $gb GB" -ForegroundColor Green
        
        $tb = $update.ToTB()
        Write-Host "ToTB(): $tb TB" -ForegroundColor Green
        
        $humanReadable = $update.ToHumanReadableSize()
        Write-Host "ToHumanReadableSize(): $humanReadable" -ForegroundColor Green
        
        $humanReadable1 = $update.ToHumanReadableSize(1)
        Write-Host "ToHumanReadableSize(1): $humanReadable1" -ForegroundColor Green
        
        Write-Host "`n✅ All size conversion methods working!" -ForegroundColor Green
        
    } catch {
        Write-Host "❌ Error testing size methods: $($_.Exception.Message)" -ForegroundColor Red
    }
    
} else {
    Write-Host "❌ No results found for testing" -ForegroundColor Red
}

Write-Host "`nTest completed" -ForegroundColor Cyan
