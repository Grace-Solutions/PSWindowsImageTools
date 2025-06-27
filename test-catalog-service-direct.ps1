# Direct test of the Windows Update Catalog Service without full module import
# This tests the core parsing logic

Write-Host "Testing Windows Update Catalog Service Directly" -ForegroundColor Cyan
Write-Host "===============================================" -ForegroundColor Cyan

try {
    # Load just the essential assemblies
    Add-Type -Path "Module\PSWindowsImageTools\bin\PSWindowsImageTools.dll"
    Add-Type -Path "Module\PSWindowsImageTools\bin\HtmlAgilityPack.dll"
    
    Write-Host "✅ Assemblies loaded successfully" -ForegroundColor Green
    
    # Create an instance of the catalog service
    $catalogService = New-Object PSWindowsImageTools.Services.WindowsUpdateCatalogService
    
    Write-Host "✅ Catalog service created" -ForegroundColor Green
    
    # Test with a simple search
    $criteria = New-Object PSWindowsImageTools.Models.WindowsUpdateSearchCriteria
    $criteria.Query = "Windows 11 Security Update"
    $criteria.MaxResults = 5
    
    Write-Host "🔍 Searching for: $($criteria.Query)" -ForegroundColor Yellow
    
    # Perform the search
    $result = $catalogService.SearchUpdates($criteria, $false, $true, $null)
    
    Write-Host "Search completed!" -ForegroundColor Green
    Write-Host "Success: $($result.Success)" -ForegroundColor Cyan
    Write-Host "Updates found: $($result.Updates.Count)" -ForegroundColor Cyan
    Write-Host "Total count: $($result.TotalCount)" -ForegroundColor Cyan
    
    if ($result.Success -and $result.Updates.Count -gt 0) {
        Write-Host "`nSample Results:" -ForegroundColor White
        $result.Updates | Select-Object -First 3 | ForEach-Object {
            Write-Host "  ID: $($_.UpdateId)" -ForegroundColor Gray
            Write-Host "  Title: $($_.Title)" -ForegroundColor White
            Write-Host "  KB: $($_.KBNumber)" -ForegroundColor Gray
            Write-Host "  Products: $($_.Products)" -ForegroundColor Gray
            Write-Host "  Classification: $($_.Classification)" -ForegroundColor Gray
            Write-Host "  Architecture: $($_.Architecture)" -ForegroundColor Gray
            Write-Host "  Size: $($_.SizeFormatted)" -ForegroundColor Gray
            Write-Host "  Last Updated: $($_.LastUpdated)" -ForegroundColor Gray
            Write-Host ""
        }
        
        Write-Host "🎉 Test PASSED - Catalog parsing is working!" -ForegroundColor Green
        
        # Test download URLs for first result
        if ($result.Updates.Count -gt 0 -and -not [string]::IsNullOrEmpty($result.Updates[0].UpdateId)) {
            Write-Host "`n--- Testing Download URLs ---" -ForegroundColor Yellow
            try {
                $downloadUrls = $catalogService.GetDownloadUrls($result.Updates[0].UpdateId, $null)
                Write-Host "Download URLs found: $($downloadUrls.Count)" -ForegroundColor Cyan
                if ($downloadUrls.Count -gt 0) {
                    Write-Host "Sample URL: $($downloadUrls[0])" -ForegroundColor Gray
                    Write-Host "✅ Download URL retrieval working!" -ForegroundColor Green
                } else {
                    Write-Host "⚠️ No download URLs found" -ForegroundColor Yellow
                }
            } catch {
                Write-Host "❌ Download URL test failed: $($_.Exception.Message)" -ForegroundColor Red
            }
        }
        
    } elseif ($result.Success) {
        Write-Host "⚠️ Search succeeded but no results found" -ForegroundColor Yellow
    } else {
        Write-Host "❌ Search failed: $($result.ErrorMessage)" -ForegroundColor Red
    }
    
    # Cleanup
    $catalogService.Dispose()
    
} catch {
    Write-Host "❌ Test failed with exception: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Stack trace: $($_.Exception.StackTrace)" -ForegroundColor Red
}

Write-Host "`nTest completed." -ForegroundColor Cyan
