# Debug filename generation issue
Write-Host "🔍 Debugging Filename Generation Issue" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

Import-Module '.\Module\PSWindowsImageTools' -Force

# Get a single result with download URL
$result = Search-WindowsUpdateCatalog -Query 'Windows 11 Cumulative' -Architecture x64 -MaxResults 1
$result | Get-WindowsUpdateDownloadUrl

Write-Host "Update: $($result.Title)" -ForegroundColor Yellow
Write-Host "KB: $($result.KBNumber)" -ForegroundColor White
Write-Host "Has URLs: $($result.HasDownloadUrls)" -ForegroundColor White
Write-Host "URL Count: $($result.DownloadUrls.Count)" -ForegroundColor White

if ($result.DownloadUrls.Count -gt 0) {
    $url = $result.DownloadUrls[0]
    Write-Host "`nFirst URL Details:" -ForegroundColor Cyan
    Write-Host "  Type: $($url.GetType().FullName)" -ForegroundColor White
    Write-Host "  OriginalString: '$($url.OriginalString)'" -ForegroundColor White
    Write-Host "  AbsoluteUri: '$($url.AbsoluteUri)'" -ForegroundColor White
    Write-Host "  Host: '$($url.Host)'" -ForegroundColor White
    Write-Host "  PathAndQuery: '$($url.PathAndQuery)'" -ForegroundColor White
    
    # Test filename generation
    Write-Host "`nTesting filename generation:" -ForegroundColor Cyan
    try {
        $filename = [PSWindowsImageTools.Services.NetworkService]::GetSuggestedFilename($url.OriginalString)
        Write-Host "  Suggested filename: '$filename'" -ForegroundColor Green
        
        if ([string]::IsNullOrEmpty($filename)) {
            Write-Host "  ❌ Filename is null or empty!" -ForegroundColor Red
        } else {
            Write-Host "  ✅ Filename generated successfully" -ForegroundColor Green
        }
    } catch {
        Write-Host "  ❌ Error generating filename: $($_.Exception.Message)" -ForegroundColor Red
    }
    
    # Test fallback filename
    $fallbackFilename = "$($result.KBNumber).cab"
    Write-Host "  Fallback filename: '$fallbackFilename'" -ForegroundColor Yellow
    
    # Test path combination
    $testDir = "C:\Temp\Test"
    Write-Host "`nTesting path combination:" -ForegroundColor Cyan
    try {
        $testFilename = $filename ?? $fallbackFilename
        $fullPath = [System.IO.Path]::Combine($testDir, $testFilename)
        Write-Host "  Combined path: '$fullPath'" -ForegroundColor Green
    } catch {
        Write-Host "  ❌ Error combining path: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "`n=====================================" -ForegroundColor Cyan
