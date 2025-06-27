# Test the final Products parsing
Write-Host "Testing Final Products Parsing" -ForegroundColor Cyan

Import-Module '.\Module\PSWindowsImageTools' -Force
Write-Host "Module loaded" -ForegroundColor Green

$results = Search-WindowsUpdateCatalog -Query 'Windows 11 Security Update' -MaxResults 1
Write-Host "Search completed, results: $($results.Count)" -ForegroundColor Green

if ($results.Count -gt 0) {
    $first = $results[0]
    
    Write-Host "`nFirst result analysis:" -ForegroundColor Yellow
    Write-Host "Title: $($first.Title)" -ForegroundColor White
    Write-Host "UpdateId: $($first.UpdateId)" -ForegroundColor White
    
    Write-Host "`nProducts analysis:" -ForegroundColor Yellow
    Write-Host "Products type: $($first.Products.GetType().Name)" -ForegroundColor Cyan
    Write-Host "Products value: '$($first.Products)'" -ForegroundColor Cyan
    
    Write-Host "`nProductsList analysis:" -ForegroundColor Yellow
    Write-Host "ProductsList type: $($first.ProductsList.GetType().Name)" -ForegroundColor Cyan
    Write-Host "ProductsList count: $($first.ProductsList.Count)" -ForegroundColor Cyan
    
    if ($first.ProductsList.Count -gt 0) {
        Write-Host "ProductsList items:" -ForegroundColor Cyan
        for ($i = 0; $i -lt $first.ProductsList.Count; $i++) {
            Write-Host "  [$i]: '$($first.ProductsList[$i])'" -ForegroundColor Gray
        }
    }
    
    Write-Host "`n✅ Products parsing test completed!" -ForegroundColor Green
} else {
    Write-Host "❌ No results found" -ForegroundColor Red
}

Write-Host "`nTest completed" -ForegroundColor Cyan
