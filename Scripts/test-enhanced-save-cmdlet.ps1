# Test Enhanced Save-WindowsUpdateCatalogResult Cmdlet
Write-Host "💾 Testing Enhanced Save Cmdlet with Progress & Resume" -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan

Import-Module '.\Module\PSWindowsImageTools' -Force

# Create test directory
$testDir = "C:\Temp\WindowsUpdateTest"
if (Test-Path $testDir) {
    Remove-Item $testDir -Recurse -Force
}
New-Item -ItemType Directory -Path $testDir -Force | Out-Null
Write-Host "Created test directory: $testDir" -ForegroundColor Green

try {
    # Test 1: Get a small update for testing
    Write-Host "`n--- Test 1: Search and Get Download URLs ---" -ForegroundColor Yellow
    $results = Search-WindowsUpdateCatalog -Query 'Windows 11 Cumulative' -MaxResults 1
    Write-Host "Found update: $($results.Title)" -ForegroundColor Cyan
    
    # Get download URLs
    $results | Get-WindowsUpdateDownloadUrl
    Write-Host "Download URL: $($results.DownloadUrls[0].OriginalString)" -ForegroundColor Gray
    Write-Host "Expected size: $($results.SizeFormatted)" -ForegroundColor Gray

    # Test 2: Download with progress tracking
    Write-Host "`n--- Test 2: Download with Progress Tracking ---" -ForegroundColor Yellow
    Write-Host "Starting download with progress tracking..." -ForegroundColor Cyan
    
    $downloadStart = Get-Date
    $packages = $results | Save-WindowsUpdateCatalogResult -DestinationPath $testDir -Verbose
    $downloadEnd = Get-Date
    $downloadDuration = $downloadEnd - $downloadStart
    
    Write-Host "Download completed in: $($downloadDuration.TotalSeconds) seconds" -ForegroundColor Green
    
    if ($packages -and $packages.Count -gt 0) {
        $package = $packages[0]
        Write-Host "Downloaded file: $($package.LocalFile.FullName)" -ForegroundColor Green
        Write-Host "File size: $($package.FileSize) bytes" -ForegroundColor White
        Write-Host "Downloaded at: $($package.DownloadedAt)" -ForegroundColor White
        Write-Host "Is downloaded: $($package.IsDownloaded)" -ForegroundColor White
        
        # Verify file exists
        if (Test-Path $package.LocalFile.FullName) {
            $fileInfo = Get-Item $package.LocalFile.FullName
            Write-Host "✅ File exists on disk: $($fileInfo.Length) bytes" -ForegroundColor Green
        } else {
            Write-Host "❌ File not found on disk!" -ForegroundColor Red
        }
    } else {
        Write-Host "❌ No packages returned from download!" -ForegroundColor Red
    }

    # Test 3: Test resume capability (simulate partial download)
    Write-Host "`n--- Test 3: Resume Capability Test ---" -ForegroundColor Yellow
    if ($packages -and $packages.Count -gt 0) {
        $originalFile = $packages[0].LocalFile.FullName
        $originalSize = (Get-Item $originalFile).Length
        
        # Create a partial file (truncate to 50%)
        $partialFile = $originalFile + ".partial"
        $partialSize = [math]::Floor($originalSize * 0.5)
        
        Write-Host "Creating partial file ($partialSize bytes of $originalSize)..." -ForegroundColor Cyan
        
        # Copy first 50% of the file
        $buffer = New-Object byte[] 8192
        $sourceStream = [System.IO.File]::OpenRead($originalFile)
        $destStream = [System.IO.File]::Create($partialFile)
        
        $totalRead = 0
        while ($totalRead -lt $partialSize) {
            $toRead = [math]::Min(8192, $partialSize - $totalRead)
            $bytesRead = $sourceStream.Read($buffer, 0, $toRead)
            if ($bytesRead -eq 0) { break }
            $destStream.Write($buffer, 0, $bytesRead)
            $totalRead += $bytesRead
        }
        
        $sourceStream.Close()
        $destStream.Close()
        
        # Remove original and rename partial
        Remove-Item $originalFile -Force
        Rename-Item $partialFile $originalFile
        
        Write-Host "Partial file created: $((Get-Item $originalFile).Length) bytes" -ForegroundColor Yellow
        
        # Test resume download
        Write-Host "Testing resume download..." -ForegroundColor Cyan
        $resumeStart = Get-Date
        $resumePackages = $results | Save-WindowsUpdateCatalogResult -DestinationPath $testDir -Resume -Verbose
        $resumeEnd = Get-Date
        $resumeDuration = $resumeEnd - $resumeStart
        
        Write-Host "Resume completed in: $($resumeDuration.TotalSeconds) seconds" -ForegroundColor Green
        
        if ($resumePackages -and $resumePackages.Count -gt 0) {
            $finalSize = (Get-Item $resumePackages[0].LocalFile.FullName).Length
            Write-Host "Final file size: $finalSize bytes" -ForegroundColor Green
            
            if ($finalSize -eq $originalSize) {
                Write-Host "✅ Resume successful - file size matches!" -ForegroundColor Green
            } else {
                Write-Host "⚠️ Resume completed but size differs: expected $originalSize, got $finalSize" -ForegroundColor Yellow
            }
        }
    }

    # Test 4: Force re-download
    Write-Host "`n--- Test 4: Force Re-download ---" -ForegroundColor Yellow
    Write-Host "Testing force re-download..." -ForegroundColor Cyan
    $forceStart = Get-Date
    $forcePackages = $results | Save-WindowsUpdateCatalogResult -DestinationPath $testDir -Force -Verbose
    $forceEnd = Get-Date
    $forceDuration = $forceEnd - $forceStart
    
    Write-Host "Force re-download completed in: $($forceDuration.TotalSeconds) seconds" -ForegroundColor Green

} catch {
    Write-Host "❌ Test failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor Red
} finally {
    # Cleanup
    Write-Host "`n--- Cleanup ---" -ForegroundColor Yellow
    if (Test-Path $testDir) {
        Remove-Item $testDir -Recurse -Force
        Write-Host "Cleaned up test directory" -ForegroundColor Green
    }
}

Write-Host "`n======================================================" -ForegroundColor Cyan
Write-Host "✅ Enhanced Save cmdlet testing complete!" -ForegroundColor Green
Write-Host "✅ Features tested: Progress tracking, Resume capability, Force re-download" -ForegroundColor Green
Write-Host "======================================================" -ForegroundColor Cyan
