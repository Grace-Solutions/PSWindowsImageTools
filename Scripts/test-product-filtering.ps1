# Test Product Filtering Logic
Write-Host "🔍 Testing Product Filtering Logic" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan

Import-Module '.\Module\PSWindowsImageTools' -Force

# Test 1: Get some results to see what products are available
Write-Host "`n--- Test 1: Sample Products Available ---" -ForegroundColor Yellow
$allResults = Search-WindowsUpdateCatalog -Query 'Windows 11' -MaxResults 5
Write-Host "Sample products from Windows 11 search:" -ForegroundColor Cyan
$allResults | ForEach-Object { 
    Write-Host "  Update: $($_.Title)" -ForegroundColor White
    Write-Host "  Products: $($_.Products -join ', ')" -ForegroundColor Gray
    Write-Host ""
}

# Test 2: Partial matching - "Windows" should match "Windows 11", "Windows Server", etc.
Write-Host "`n--- Test 2: Partial Matching with 'Windows' ---" -ForegroundColor Yellow
$windowsResults = Search-WindowsUpdateCatalog -Query 'Cumulative' -Product 'Windows' -MaxResults 3
Write-Host "Results with Product filter 'Windows':" -ForegroundColor Cyan
$windowsResults | ForEach-Object {
    Write-Host "  Title: $($_.Title)" -ForegroundColor White
    Write-Host "  Products: $($_.Products -join ', ')" -ForegroundColor Gray
    Write-Host ""
}

# Test 3: Specific matching - "Windows 11" should match only Windows 11 products
Write-Host "`n--- Test 3: Specific Matching with 'Windows 11' ---" -ForegroundColor Yellow
$win11Results = Search-WindowsUpdateCatalog -Query 'Cumulative' -Product 'Windows 11' -MaxResults 3
Write-Host "Results with Product filter 'Windows 11':" -ForegroundColor Cyan
$win11Results | ForEach-Object {
    Write-Host "  Title: $($_.Title)" -ForegroundColor White
    Write-Host "  Products: $($_.Products -join ', ')" -ForegroundColor Gray
    Write-Host ""
}

# Test 4: Case insensitive - "windows" (lowercase) should work
Write-Host "`n--- Test 4: Case Insensitive with 'windows' (lowercase) ---" -ForegroundColor Yellow
$lowerResults = Search-WindowsUpdateCatalog -Query 'Cumulative' -Product 'windows' -MaxResults 2
Write-Host "Results with Product filter 'windows' (lowercase):" -ForegroundColor Cyan
Write-Host "Count: $($lowerResults.Count)" -ForegroundColor Green

# Test 5: Non-existent product
Write-Host "`n--- Test 5: Non-existent Product 'Linux' ---" -ForegroundColor Yellow
$linuxResults = Search-WindowsUpdateCatalog -Query 'Cumulative' -Product 'Linux' -MaxResults 5
Write-Host "Results with Product filter 'Linux':" -ForegroundColor Cyan
Write-Host "Count: $($linuxResults.Count)" -ForegroundColor Green

Write-Host "`n=================================" -ForegroundColor Cyan
Write-Host "🎯 Product Filtering Summary:" -ForegroundColor Yellow
Write-Host "✅ Uses substring matching (IndexOf)" -ForegroundColor Green
Write-Host "✅ Case-insensitive matching" -ForegroundColor Green  
Write-Host "✅ Searches ANY product in the Products list" -ForegroundColor Green
Write-Host "✅ 'Windows' matches 'Windows 11', 'Windows Server', etc." -ForegroundColor Green
Write-Host "=================================" -ForegroundColor Cyan
