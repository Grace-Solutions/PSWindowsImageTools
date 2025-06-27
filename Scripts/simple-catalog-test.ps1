# Simple test of catalog parsing
Write-Host "Testing Catalog Service" -ForegroundColor Cyan

try {
    # Load assemblies
    Add-Type -Path "Module\PSWindowsImageTools\bin\PSWindowsImageTools.dll"
    Add-Type -Path "Module\PSWindowsImageTools\bin\HtmlAgilityPack.dll"
    
    Write-Host "Assemblies loaded" -ForegroundColor Green
    
    # Create service
    $service = New-Object PSWindowsImageTools.Services.WindowsUpdateCatalogService
    Write-Host "Service created" -ForegroundColor Green
    
    # Create criteria
    $criteria = New-Object PSWindowsImageTools.Models.WindowsUpdateSearchCriteria
    $criteria.Query = "Windows 11 Security Update"
    $criteria.MaxResults = 3
    
    Write-Host "Searching for: $($criteria.Query)" -ForegroundColor Yellow
    
    # Search
    $result = $service.SearchUpdates($criteria, $false, $true, $null)
    
    Write-Host "Search completed" -ForegroundColor Green
    Write-Host "Success: $($result.Success)" -ForegroundColor Cyan
    Write-Host "Updates found: $($result.Updates.Count)" -ForegroundColor Cyan
    
    if ($result.Updates.Count -gt 0) {
        $first = $result.Updates[0]
        Write-Host "First result:" -ForegroundColor White
        Write-Host "  ID: $($first.UpdateId)" -ForegroundColor Gray
        Write-Host "  Title: $($first.Title)" -ForegroundColor Gray
        Write-Host "  KB: $($first.KBNumber)" -ForegroundColor Gray
        Write-Host "  Products: $($first.Products)" -ForegroundColor Gray
        Write-Host "  Classification: $($first.Classification)" -ForegroundColor Gray
        Write-Host "  Architecture: $($first.Architecture)" -ForegroundColor Gray
        Write-Host "  Size: $($first.SizeFormatted)" -ForegroundColor Gray
        Write-Host "  Last Updated: $($first.LastUpdated)" -ForegroundColor Gray
        
        Write-Host "SUCCESS - Parsing is working!" -ForegroundColor Green
    } else {
        Write-Host "No results found" -ForegroundColor Yellow
    }
    
    $service.Dispose()
    
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "Test completed" -ForegroundColor Cyan
