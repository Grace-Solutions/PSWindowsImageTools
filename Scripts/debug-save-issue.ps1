# Debug Save cmdlet issue
Write-Host "🔍 Debugging Save Cmdlet Issue" -ForegroundColor Cyan
Write-Host "==============================" -ForegroundColor Cyan

Import-Module '.\Module\PSWindowsImageTools' -Force

# Create test directory
$testDir = "C:\Temp\DebugSave"
if (Test-Path $testDir) {
    Remove-Item $testDir -Recurse -Force
}
New-Item -ItemType Directory -Path $testDir -Force | Out-Null
Write-Host "Created test directory: $testDir" -ForegroundColor Green

try {
    # Get a single result
    $result = Search-WindowsUpdateCatalog -Query 'Windows 11 Cumulative' -Architecture x64 -MaxResults 1
    $result | Get-WindowsUpdateDownloadUrl
    
    Write-Host "`nUpdate details:" -ForegroundColor Cyan
    Write-Host "  Title: $($result.Title)" -ForegroundColor White
    Write-Host "  KB: $($result.KBNumber)" -ForegroundColor White
    Write-Host "  Has URLs: $($result.HasDownloadUrls)" -ForegroundColor White
    Write-Host "  URL Count: $($result.DownloadUrls.Count)" -ForegroundColor White
    
    if ($result.DownloadUrls.Count -gt 0) {
        $url = $result.DownloadUrls[0]
        Write-Host "  First URL: $($url.OriginalString)" -ForegroundColor Gray
        
        # Test filename generation manually
        Write-Host "`nTesting filename generation:" -ForegroundColor Cyan
        $fileName = [PSWindowsImageTools.Services.NetworkService]::GetSuggestedFilename($url.OriginalString)
        Write-Host "  Generated filename: '$fileName'" -ForegroundColor White
        
        if ([string]::IsNullOrEmpty($fileName)) {
            $fileName = "$($result.KBNumber).cab"
            Write-Host "  Using fallback filename: '$fileName'" -ForegroundColor Yellow
        }
        
        # Test path combination
        $fullPath = [System.IO.Path]::Combine($testDir, $fileName)
        Write-Host "  Full path: '$fullPath'" -ForegroundColor White
        
        # Test FileInfo creation
        $fileInfo = New-Object System.IO.FileInfo($fullPath)
        Write-Host "  FileInfo.FullName: '$($fileInfo.FullName)'" -ForegroundColor White
        Write-Host "  FileInfo.DirectoryName: '$($fileInfo.DirectoryName)'" -ForegroundColor White
        Write-Host "  FileInfo.Name: '$($fileInfo.Name)'" -ForegroundColor White
        
        # Test the actual Save cmdlet with verbose output
        Write-Host "`nTesting Save cmdlet:" -ForegroundColor Cyan
        try {
            $packages = $result | Save-WindowsUpdateCatalogResult -DestinationPath $testDir -Verbose
            Write-Host "✅ Save cmdlet succeeded!" -ForegroundColor Green
            
            if ($packages -and $packages.Count -gt 0) {
                $package = $packages[0]
                Write-Host "  Downloaded: $($package.IsDownloaded)" -ForegroundColor White
                Write-Host "  Local file: $($package.LocalFile.FullName)" -ForegroundColor White
                Write-Host "  File size: $($package.FileSize)" -ForegroundColor White
            }
        } catch {
            Write-Host "❌ Save cmdlet failed: $($_.Exception.Message)" -ForegroundColor Red
            Write-Host "Exception type: $($_.Exception.GetType().FullName)" -ForegroundColor Red
            Write-Host "Stack trace:" -ForegroundColor Red
            Write-Host $_.ScriptStackTrace -ForegroundColor Red
        }
    }
    
} catch {
    Write-Host "❌ Test failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor Red
} finally {
    # Cleanup
    if (Test-Path $testDir) {
        Remove-Item $testDir -Recurse -Force
        Write-Host "`nCleaned up test directory" -ForegroundColor Green
    }
}

Write-Host "`n==============================" -ForegroundColor Cyan
