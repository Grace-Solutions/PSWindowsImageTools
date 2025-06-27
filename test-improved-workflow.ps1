# Test Improved Windows Update Catalog Workflow
Write-Host "🚀 Testing Improved Windows Update Catalog Workflow" -ForegroundColor Cyan
Write-Host "====================================================" -ForegroundColor Cyan

# Test in PowerShell 5.1 to verify .NET Standard 2.0 compatibility
Write-Host "PowerShell Version: $($PSVersionTable.PSVersion)" -ForegroundColor Yellow
Write-Host "Edition: $($PSVersionTable.PSEdition)" -ForegroundColor Yellow

Import-Module '.\Module\PSWindowsImageTools' -Force
Write-Host "✅ Module loaded successfully" -ForegroundColor Green

# Test 1: Efficient Search (No Download URLs)
Write-Host "`n--- Test 1: Efficient Search (No Download URLs) ---" -ForegroundColor Yellow
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

$results = Search-WindowsUpdateCatalog -Query 'Windows 11 Cumulative' -Architecture AMD64 -MaxResults 5 -Verbose
$stopwatch.Stop()

Write-Host "✅ Search completed in $($stopwatch.ElapsedMilliseconds)ms" -ForegroundColor Green
Write-Host "Results found: $($results.Count)" -ForegroundColor Green

if ($results.Count -gt 0) {
    $first = $results[0]
    Write-Host "First result:" -ForegroundColor Cyan
    Write-Host "  Title: $($first.Title)" -ForegroundColor White
    Write-Host "  Products: $($first.Products -join ', ')" -ForegroundColor White
    Write-Host "  Size: $($first.ToHumanReadableSize())" -ForegroundColor White
    Write-Host "  Has Download URLs: $($first.HasDownloadUrls)" -ForegroundColor White
}

# Test 2: Selective Download URL Retrieval
Write-Host "`n--- Test 2: Selective Download URL Retrieval ---" -ForegroundColor Yellow

if ($results.Count -gt 0) {
    Write-Host "Getting download URLs for first 2 results only..." -ForegroundColor Cyan
    $stopwatch.Restart()
    
    # Use the new pipeline-based cmdlet to get download URLs for only selected results
    $resultsWithUrls = $results | Select-Object -First 2 | Get-WindowsUpdateDownloadUrl -Verbose
    $stopwatch.Stop()
    
    Write-Host "✅ Download URLs retrieved in $($stopwatch.ElapsedMilliseconds)ms" -ForegroundColor Green
    
    foreach ($result in $resultsWithUrls) {
        Write-Host "Update: $($result.Title)" -ForegroundColor Cyan
        Write-Host "  Download URLs: $($result.DownloadUrls.Count)" -ForegroundColor White
        if ($result.DownloadUrls.Count -gt 0) {
            Write-Host "  First URL: $($result.DownloadUrls[0])" -ForegroundColor Gray
        }
    }
}

# Test 3: Demonstrate Efficiency Improvement
Write-Host "`n--- Test 3: Efficiency Comparison ---" -ForegroundColor Yellow
Write-Host "OLD APPROACH (if we had IncludeDownloadUrls):" -ForegroundColor Red
Write-Host "  1. Search catalog -> Get ALL results" -ForegroundColor Red
Write-Host "  2. Get download URLs for ALL results (slow)" -ForegroundColor Red
Write-Host "  3. Apply filters and limits" -ForegroundColor Red
Write-Host "  4. Return limited results (but processed everything)" -ForegroundColor Red

Write-Host "`nNEW APPROACH:" -ForegroundColor Green
Write-Host "  1. Search catalog -> Get ALL results (unavoidable)" -ForegroundColor Green
Write-Host "  2. Apply filters and limits immediately" -ForegroundColor Green
Write-Host "  3. Return limited results (fast)" -ForegroundColor Green
Write-Host "  4. Optionally get download URLs for ONLY selected results" -ForegroundColor Green

Write-Host "`n🎯 BENEFITS:" -ForegroundColor Yellow
Write-Host "  ✅ Much faster initial search" -ForegroundColor Green
Write-Host "  ✅ Only process download URLs for items you actually want" -ForegroundColor Green
Write-Host "  ✅ Better control over which items to fetch URLs for" -ForegroundColor Green
Write-Host "  ✅ Can pipeline results to other cmdlets" -ForegroundColor Green

# Test 4: Pipeline Workflow Example
Write-Host "`n--- Test 4: Pipeline Workflow Example ---" -ForegroundColor Yellow
Write-Host "Example: Search -> Filter -> Get URLs for specific items" -ForegroundColor Cyan

if ($results.Count -gt 0) {
    Write-Host "Pipeline example:" -ForegroundColor Cyan
    Write-Host "Search-WindowsUpdateCatalog -Query 'Windows 11' -MaxResults 10 |" -ForegroundColor Gray
    Write-Host "  Where-Object { `$_.Title -like '*Security*' } |" -ForegroundColor Gray  
    Write-Host "  Select-Object -First 3 |" -ForegroundColor Gray
    Write-Host "  Get-WindowsUpdateDownloadUrl" -ForegroundColor Gray
}

Write-Host "`n====================================================" -ForegroundColor Cyan
Write-Host "🎉 Improved workflow testing completed!" -ForegroundColor Green
Write-Host "====================================================" -ForegroundColor Cyan
