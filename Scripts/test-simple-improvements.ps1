# Simple test for Save cmdlet improvements
Write-Host "🧪 Simple Test: Save Cmdlet Improvements" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

Import-Module '.\Module\PSWindowsImageTools' -Force

# Create test directory
$testDir = "C:\Temp\SimpleTest"
if (Test-Path $testDir) { Remove-Item $testDir -Recurse -Force }
New-Item -ItemType Directory -Path $testDir -Force | Out-Null

try {
    # Get one small update
    Write-Host "Getting Windows 11 update..." -ForegroundColor Yellow
    $result = Search-WindowsUpdateCatalog -Query 'Windows 11 Cumulative' -Architecture x64 -MaxResults 1
    $result | Get-WindowsUpdateDownloadUrl
    
    Write-Host "`nUpdate: $($result.KBNumber) - $($result.SizeFormatted)" -ForegroundColor Cyan
    
    # Download and capture return value
    Write-Host "`nDownloading..." -ForegroundColor Yellow
    $packages = $result | Save-WindowsUpdateCatalogResult -DestinationPath $testDir -Verbose

    # Check return type
    Write-Host "`nReturn value analysis:" -ForegroundColor Cyan
    if ($packages) {
        Write-Host "  Count: $($packages.Count)" -ForegroundColor White
        if ($packages.Count -gt 0) {
            $package = $packages[0]
            Write-Host "  Type: $($package.GetType().FullName)" -ForegroundColor White
            Write-Host "  KB Number: $($package.KBNumber)" -ForegroundColor White
            Write-Host "  Is Downloaded: $($package.IsDownloaded)" -ForegroundColor White
            Write-Host "  Download URL: $($package.DownloadUrl)" -ForegroundColor White

            if ($package.LocalFile) {
                Write-Host "  ✅ LocalFile FileInfo object:" -ForegroundColor Green
                Write-Host "    Type: $($package.LocalFile.GetType().FullName)" -ForegroundColor White
                Write-Host "    Name: $($package.LocalFile.Name)" -ForegroundColor White
                Write-Host "    Size: $([math]::Round($package.LocalFile.Length / 1MB, 2)) MB" -ForegroundColor White
                Write-Host "    Exists: $($package.LocalFile.Exists)" -ForegroundColor White
                Write-Host "    Full Path: $($package.LocalFile.FullName)" -ForegroundColor White
            } else {
                Write-Host "  ❌ No LocalFile!" -ForegroundColor Red
            }
        }
    } else {
        Write-Host "  ❌ No return value!" -ForegroundColor Red
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

Write-Host "`n=========================================" -ForegroundColor Cyan
