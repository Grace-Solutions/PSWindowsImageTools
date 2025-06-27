# Complete Download Workflow Test: Windows 11 Cumulative x64 (No Preview)
Write-Host "🚀 Complete Download Workflow Test" -ForegroundColor Cyan
Write-Host "===================================" -ForegroundColor Cyan
Write-Host "Target: Windows 11 Cumulative x64 updates (excluding previews)" -ForegroundColor Yellow
Write-Host "===================================" -ForegroundColor Cyan

Import-Module '.\Module\PSWindowsImageTools' -Force

# Create download directory
$downloadDir = "C:\Temp\WindowsUpdates"
if (Test-Path $downloadDir) {
    Write-Host "Cleaning existing download directory..." -ForegroundColor Yellow
    Remove-Item $downloadDir -Recurse -Force
}
New-Item -ItemType Directory -Path $downloadDir -Force | Out-Null
Write-Host "✅ Created download directory: $downloadDir" -ForegroundColor Green

try {
    # Step 1: Search for Windows 11 Cumulative x64 updates (no preview)
    Write-Host "`n--- Step 1: Search for Updates ---" -ForegroundColor Yellow
    Write-Host "Searching for: Windows 11 Cumulative x64 (excluding Preview)" -ForegroundColor Cyan
    
    $searchStart = Get-Date
    $results = Search-WindowsUpdateCatalog -Query 'Windows 11 Cumulative' -Architecture x64 -MaxResults 10 -Verbose
    $searchEnd = Get-Date
    $searchDuration = $searchEnd - $searchStart
    
    Write-Host "Search completed in: $($searchDuration.TotalSeconds) seconds" -ForegroundColor Green
    Write-Host "Found $($results.Count) total results" -ForegroundColor White
    
    # Filter out preview updates
    $nonPreviewResults = $results | Where-Object { $_.Title -notlike "*Preview*" }
    Write-Host "After filtering out Preview updates: $($nonPreviewResults.Count) results" -ForegroundColor Green
    
    if ($nonPreviewResults.Count -eq 0) {
        Write-Host "❌ No non-preview updates found! Showing all results instead..." -ForegroundColor Red
        $nonPreviewResults = $results | Select-Object -First 3
    } else {
        # Take top 3 non-preview results
        $nonPreviewResults = $nonPreviewResults | Select-Object -First 3
    }
    
    Write-Host "`nSelected updates for download:" -ForegroundColor Cyan
    $nonPreviewResults | ForEach-Object -Begin { $i = 1 } -Process {
        Write-Host "  $i. $($_.Title)" -ForegroundColor White
        Write-Host "     KB: $($_.KBNumber) | Size: $($_.SizeFormatted) | Date: $($_.LastModified.ToString('yyyy-MM-dd'))" -ForegroundColor Gray
        $i++
    }

    # Step 2: Get download URLs
    Write-Host "`n--- Step 2: Get Download URLs ---" -ForegroundColor Yellow
    Write-Host "Retrieving download URLs..." -ForegroundColor Cyan
    
    $urlStart = Get-Date
    $nonPreviewResults | Get-WindowsUpdateDownloadUrl -Verbose
    $urlEnd = Get-Date
    $urlDuration = $urlEnd - $urlStart
    
    Write-Host "URL retrieval completed in: $($urlDuration.TotalSeconds) seconds" -ForegroundColor Green
    
    # Verify URLs were retrieved
    $resultsWithUrls = $nonPreviewResults | Where-Object { $_.HasDownloadUrls }
    Write-Host "Updates with download URLs: $($resultsWithUrls.Count) of $($nonPreviewResults.Count)" -ForegroundColor Green
    
    if ($resultsWithUrls.Count -eq 0) {
        Write-Host "❌ No download URLs found! Cannot proceed with download test." -ForegroundColor Red
        return
    }
    
    Write-Host "`nDownload URLs retrieved:" -ForegroundColor Cyan
    $resultsWithUrls | ForEach-Object -Begin { $i = 1 } -Process {
        Write-Host "  $i. $($_.KBNumber): $($_.DownloadUrls.Count) URL(s)" -ForegroundColor White
        $_.DownloadUrls | ForEach-Object {
            Write-Host "     $($_.Host)$($_.PathAndQuery)" -ForegroundColor Gray
        }
        $i++
    }

    # Step 3: Download with progress and resume capability
    Write-Host "`n--- Step 3: Download Updates ---" -ForegroundColor Yellow
    Write-Host "Starting downloads with progress tracking and resume capability..." -ForegroundColor Cyan
    
    $downloadStart = Get-Date
    $packages = $resultsWithUrls | Save-WindowsUpdateCatalogResult -DestinationPath $downloadDir -Resume -Verbose
    $downloadEnd = Get-Date
    $downloadDuration = $downloadEnd - $downloadStart
    
    Write-Host "Downloads completed in: $($downloadDuration.TotalMinutes) minutes" -ForegroundColor Green

    # Step 4: Verify downloads
    Write-Host "`n--- Step 4: Verify Downloads ---" -ForegroundColor Yellow
    if ($packages -and $packages.Count -gt 0) {
        Write-Host "Download results:" -ForegroundColor Cyan
        
        $totalSize = 0
        $successCount = 0
        
        $packages | ForEach-Object -Begin { $i = 1 } -Process {
            Write-Host "`n  $i. $($_.KBNumber) - $($_.Title)" -ForegroundColor White
            Write-Host "     Status: $(if ($_.IsDownloaded) { '✅ Downloaded' } else { '❌ Failed' })" -ForegroundColor $(if ($_.IsDownloaded) { 'Green' } else { 'Red' })
            
            if ($_.IsDownloaded) {
                Write-Host "     File: $($_.LocalFile.Name)" -ForegroundColor Gray
                Write-Host "     Size: $($_.FileSize) bytes ($([math]::Round($_.FileSize / 1MB, 2)) MB)" -ForegroundColor Gray
                Write-Host "     Path: $($_.LocalFile.FullName)" -ForegroundColor Gray
                Write-Host "     Downloaded: $($_.DownloadedAt)" -ForegroundColor Gray
                
                # Verify file exists on disk
                if (Test-Path $_.LocalFile.FullName) {
                    $fileInfo = Get-Item $_.LocalFile.FullName
                    Write-Host "     Disk verification: ✅ File exists ($($fileInfo.Length) bytes)" -ForegroundColor Green
                    $totalSize += $fileInfo.Length
                    $successCount++
                } else {
                    Write-Host "     Disk verification: ❌ File not found!" -ForegroundColor Red
                }
            } else {
                Write-Host "     Error: Download failed" -ForegroundColor Red
            }
            $i++
        }
        
        Write-Host "`n--- Download Summary ---" -ForegroundColor Yellow
        Write-Host "Successful downloads: $successCount of $($packages.Count)" -ForegroundColor Green
        Write-Host "Total downloaded: $([math]::Round($totalSize / 1MB, 2)) MB" -ForegroundColor Green
        Write-Host "Average speed: $([math]::Round(($totalSize / 1MB) / $downloadDuration.TotalMinutes, 2)) MB/min" -ForegroundColor Green
        Write-Host "Download directory: $downloadDir" -ForegroundColor Cyan
        
        # List all files in download directory
        Write-Host "`nFiles in download directory:" -ForegroundColor Cyan
        Get-ChildItem $downloadDir | ForEach-Object {
            Write-Host "  $($_.Name) - $([math]::Round($_.Length / 1MB, 2)) MB" -ForegroundColor White
        }
        
    } else {
        Write-Host "❌ No packages returned from download operation!" -ForegroundColor Red
    }

    # Step 5: Test resume functionality (optional)
    Write-Host "`n--- Step 5: Test Resume Capability (Optional) ---" -ForegroundColor Yellow
    $testResume = Read-Host "Do you want to test resume capability? This will simulate a partial download (y/N)"
    
    if ($testResume -eq 'y' -or $testResume -eq 'Y') {
        if ($packages -and $packages.Count -gt 0) {
            $testPackage = $packages | Where-Object { $_.IsDownloaded } | Select-Object -First 1
            
            if ($testPackage) {
                Write-Host "Testing resume with: $($testPackage.KBNumber)" -ForegroundColor Cyan
                $originalFile = $testPackage.LocalFile.FullName
                $originalSize = (Get-Item $originalFile).Length
                
                # Create partial file (50% of original)
                $partialSize = [math]::Floor($originalSize * 0.5)
                Write-Host "Creating partial file ($([math]::Round($partialSize / 1MB, 2)) MB of $([math]::Round($originalSize / 1MB, 2)) MB)..." -ForegroundColor Yellow
                
                # Truncate file to 50%
                $stream = [System.IO.File]::OpenWrite($originalFile)
                $stream.SetLength($partialSize)
                $stream.Close()
                
                Write-Host "File truncated. Testing resume..." -ForegroundColor Cyan
                
                # Find the corresponding result and re-download with resume
                $originalResult = $resultsWithUrls | Where-Object { $_.KBNumber -eq $testPackage.KBNumber }
                if ($originalResult) {
                    $resumeStart = Get-Date
                    $resumePackages = $originalResult | Save-WindowsUpdateCatalogResult -DestinationPath $downloadDir -Resume -Force -Verbose
                    $resumeEnd = Get-Date
                    $resumeDuration = $resumeEnd - $resumeStart
                    
                    $finalSize = (Get-Item $originalFile).Length
                    Write-Host "Resume completed in: $($resumeDuration.TotalSeconds) seconds" -ForegroundColor Green
                    Write-Host "Final size: $([math]::Round($finalSize / 1MB, 2)) MB" -ForegroundColor Green
                    
                    if ($finalSize -eq $originalSize) {
                        Write-Host "✅ Resume test successful!" -ForegroundColor Green
                    } else {
                        Write-Host "⚠️ Resume test completed but size differs" -ForegroundColor Yellow
                    }
                }
            }
        }
    }

} catch {
    Write-Host "❌ Test failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor Red
} finally {
    Write-Host "`n--- Cleanup ---" -ForegroundColor Yellow
    $cleanup = Read-Host "Do you want to clean up downloaded files? (y/N)"
    
    if ($cleanup -eq 'y' -or $cleanup -eq 'Y') {
        if (Test-Path $downloadDir) {
            Remove-Item $downloadDir -Recurse -Force
            Write-Host "✅ Cleaned up download directory" -ForegroundColor Green
        }
    } else {
        Write-Host "📁 Downloaded files preserved in: $downloadDir" -ForegroundColor Cyan
    }
}

Write-Host "`n===================================" -ForegroundColor Cyan
Write-Host "🎉 Complete workflow test finished!" -ForegroundColor Green
Write-Host "✅ Search ➜ Filter ➜ Get URLs ➜ Download ➜ Verify" -ForegroundColor Green
Write-Host "===================================" -ForegroundColor Cyan
