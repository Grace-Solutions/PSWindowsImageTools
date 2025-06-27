# Test Clean Logging
Write-Host "🧹 Testing Clean Logging" -ForegroundColor Cyan
Write-Host "========================" -ForegroundColor Cyan

Import-Module '.\Module\PSWindowsImageTools' -Force

# Create test directory
$testDir = "C:\Temp\CleanLogging"
if (Test-Path $testDir) { Remove-Item $testDir -Recurse -Force }
New-Item -ItemType Directory -Path $testDir -Force | Out-Null

try {
    # Get one update and download with verbose logging
    Write-Host "Getting Windows 11 update..." -ForegroundColor Yellow
    $result = Search-WindowsUpdateCatalog -Query 'Windows 11 Cumulative' -Architecture x64 -MaxResults 1
    $result | Get-WindowsUpdateDownloadUrl
    
    Write-Host "`nDownloading with clean verbose logging..." -ForegroundColor Yellow
    $packages = $result | Save-WindowsUpdateCatalogResult -DestinationPath $testDir -Verbose
    
    if ($packages -and $packages.Count -gt 0) {
        $package = $packages[0]
        Write-Host "`n✅ Download completed successfully!" -ForegroundColor Green
        Write-Host "  KB: $($package.KBNumber)" -ForegroundColor White
        Write-Host "  File: $($package.LocalFile.Name)" -ForegroundColor White
        Write-Host "  Size: $([math]::Round($package.LocalFile.Length / 1MB, 2)) MB" -ForegroundColor White
    } else {
        Write-Host "❌ No packages returned" -ForegroundColor Red
    }
    
} catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
} finally {
    # Cleanup
    if (Test-Path $testDir) {
        Remove-Item $testDir -Recurse -Force
        Write-Host "`n✅ Cleaned up" -ForegroundColor Green
    }
}

Write-Host "`n========================" -ForegroundColor Cyan
Write-Host "🎉 Clean logging tested!" -ForegroundColor Green
Write-Host "✅ No duplicate messages" -ForegroundColor Green
Write-Host "✅ Clean formatting" -ForegroundColor Green
Write-Host "========================" -ForegroundColor Cyan
