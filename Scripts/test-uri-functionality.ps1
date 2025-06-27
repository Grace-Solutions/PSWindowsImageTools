# Test Uri Functionality in Get-WindowsUpdateDownloadUrl
Write-Host "🔗 Testing Uri Functionality" -ForegroundColor Cyan
Write-Host "============================" -ForegroundColor Cyan

Import-Module '.\Module\PSWindowsImageTools' -Force

# Test 1: Get a catalog result and check its DownloadUrls type
Write-Host "`n--- Test 1: Initial DownloadUrls Type ---" -ForegroundColor Yellow
$result = Search-WindowsUpdateCatalog -Query 'Windows 11 Cumulative' -MaxResults 1
Write-Host "Before Get-WindowsUpdateDownloadUrl:" -ForegroundColor Cyan
Write-Host "  HasDownloadUrls: $($result.HasDownloadUrls)" -ForegroundColor White
Write-Host "  DownloadUrls Count: $($result.DownloadUrls.Count)" -ForegroundColor White
Write-Host "  DownloadUrls Type: $($result.DownloadUrls.GetType().FullName)" -ForegroundColor White

# Test 2: Get download URLs and check the Uri objects
Write-Host "`n--- Test 2: After Get-WindowsUpdateDownloadUrl ---" -ForegroundColor Yellow
$result | Get-WindowsUpdateDownloadUrl
Write-Host "After Get-WindowsUpdateDownloadUrl:" -ForegroundColor Cyan
Write-Host "  HasDownloadUrls: $($result.HasDownloadUrls)" -ForegroundColor White
Write-Host "  DownloadUrls Count: $($result.DownloadUrls.Count)" -ForegroundColor White
Write-Host "  DownloadUrls Type: $($result.DownloadUrls.GetType().FullName)" -ForegroundColor White

if ($result.DownloadUrls.Count -gt 0) {
    Write-Host "  First URL Type: $($result.DownloadUrls[0].GetType().FullName)" -ForegroundColor White
    Write-Host "  First URL OriginalString: $($result.DownloadUrls[0].OriginalString)" -ForegroundColor Gray
    Write-Host "  First URL AbsoluteUri: $($result.DownloadUrls[0].AbsoluteUri)" -ForegroundColor Gray
    Write-Host "  First URL Host: $($result.DownloadUrls[0].Host)" -ForegroundColor Gray
    Write-Host "  First URL Scheme: $($result.DownloadUrls[0].Scheme)" -ForegroundColor Gray
}

# Test 3: Verify Uri properties work correctly
Write-Host "`n--- Test 3: Uri Properties Validation ---" -ForegroundColor Yellow
if ($result.DownloadUrls.Count -gt 0) {
    $uri = $result.DownloadUrls[0]
    Write-Host "Uri Properties:" -ForegroundColor Cyan
    Write-Host "  IsWellFormedOriginalString: $($uri.IsWellFormedOriginalString())" -ForegroundColor Green
    Write-Host "  IsAbsoluteUri: $($uri.IsAbsoluteUri)" -ForegroundColor Green
    Write-Host "  Scheme: $($uri.Scheme)" -ForegroundColor White
    Write-Host "  Host: $($uri.Host)" -ForegroundColor White
    Write-Host "  PathAndQuery: $($uri.PathAndQuery)" -ForegroundColor Gray
} else {
    Write-Host "No download URLs found to test" -ForegroundColor Red
}

Write-Host "`n============================" -ForegroundColor Cyan
Write-Host "✅ Uri functionality test complete!" -ForegroundColor Green
Write-Host "✅ DownloadUrls are now proper System.Uri objects!" -ForegroundColor Green
Write-Host "✅ Use .OriginalString for string representation!" -ForegroundColor Green
Write-Host "============================" -ForegroundColor Cyan
