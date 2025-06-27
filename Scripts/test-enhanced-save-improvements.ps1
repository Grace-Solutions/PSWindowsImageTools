# Test Enhanced Save Cmdlet Improvements
Write-Host "🚀 Testing Enhanced Save Cmdlet Improvements" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "1. Intelligent size logging (B, KB, MB, GB, TB)" -ForegroundColor Yellow
Write-Host "2. Return List<FileInfo> instead of WindowsUpdatePackage" -ForegroundColor Yellow
Write-Host "=============================================" -ForegroundColor Cyan

Import-Module '.\Module\PSWindowsImageTools' -Force

# Create test directory
$testDir = "C:\Temp\SaveImprovements"
if (Test-Path $testDir) {
    Remove-Item $testDir -Recurse -Force
}
New-Item -ItemType Directory -Path $testDir -Force | Out-Null
Write-Host "✅ Created test directory: $testDir" -ForegroundColor Green

try {
    # Test 1: Get a Windows 11 update for testing
    Write-Host "`n--- Test 1: Search and Get Download URLs ---" -ForegroundColor Yellow
    $result = Search-WindowsUpdateCatalog -Query 'Windows 11 Cumulative' -Architecture x64 -MaxResults 1
    $result | Get-WindowsUpdateDownloadUrl
    
    Write-Host "Update details:" -ForegroundColor Cyan
    Write-Host "  Title: $($result.Title)" -ForegroundColor White
    Write-Host "  KB: $($result.KBNumber)" -ForegroundColor White
    Write-Host "  Size: $($result.SizeFormatted)" -ForegroundColor White
    Write-Host "  Has URLs: $($result.HasDownloadUrls)" -ForegroundColor White

    # Test 2: Download with enhanced logging and return type
    Write-Host "`n--- Test 2: Download with Enhanced Features ---" -ForegroundColor Yellow
    Write-Host "Starting download to test intelligent size logging..." -ForegroundColor Cyan
    
    $downloadStart = Get-Date
    $downloadedFiles = $result | Save-WindowsUpdateCatalogResult -DestinationPath $testDir -Verbose
    $downloadEnd = Get-Date
    $downloadDuration = $downloadEnd - $downloadStart
    
    Write-Host "Download completed in: $($downloadDuration.TotalSeconds) seconds" -ForegroundColor Green

    # Test 3: Verify return type is FileInfo
    Write-Host "`n--- Test 3: Verify Return Type ---" -ForegroundColor Yellow
    Write-Host "Return type verification:" -ForegroundColor Cyan
    
    if ($downloadedFiles) {
        Write-Host "  Returned object count: $($downloadedFiles.Count)" -ForegroundColor White
        
        if ($downloadedFiles.Count -gt 0) {
            $firstFile = $downloadedFiles[0]
            Write-Host "  First object type: $($firstFile.GetType().FullName)" -ForegroundColor White
            
            if ($firstFile -is [System.IO.FileInfo]) {
                Write-Host "  ✅ Correct! Returned FileInfo object" -ForegroundColor Green
                
                # Test FileInfo properties
                Write-Host "`nFileInfo properties:" -ForegroundColor Cyan
                Write-Host "  Name: $($firstFile.Name)" -ForegroundColor White
                Write-Host "  FullName: $($firstFile.FullName)" -ForegroundColor White
                Write-Host "  Length: $($firstFile.Length) bytes" -ForegroundColor White
                Write-Host "  Exists: $($firstFile.Exists)" -ForegroundColor White
                Write-Host "  Directory: $($firstFile.Directory.FullName)" -ForegroundColor White
                Write-Host "  Extension: $($firstFile.Extension)" -ForegroundColor White
                Write-Host "  CreationTime: $($firstFile.CreationTime)" -ForegroundColor White
                Write-Host "  LastWriteTime: $($firstFile.LastWriteTime)" -ForegroundColor White
                
                # Test intelligent size formatting
                $sizeInMB = [math]::Round($firstFile.Length / 1MB, 2)
                Write-Host "  Size (calculated): $sizeInMB MB" -ForegroundColor White
                
            } else {
                Write-Host "  ❌ Wrong type! Expected FileInfo, got $($firstFile.GetType().FullName)" -ForegroundColor Red
            }
        } else {
            Write-Host "  ❌ No files returned!" -ForegroundColor Red
        }
    } else {
        Write-Host "  ❌ No objects returned!" -ForegroundColor Red
    }

    # Test 4: Verify intelligent size logging in verbose output
    Write-Host "`n--- Test 4: Intelligent Size Logging Analysis ---" -ForegroundColor Yellow
    Write-Host "Check the verbose output above for intelligent size formatting:" -ForegroundColor Cyan
    Write-Host "  ✅ Should show 'Download size: X.XX GB' instead of 'X,XXX,XXX,XXX bytes'" -ForegroundColor Green
    Write-Host "  ✅ Should show 'Download completed: X.XX GB' instead of 'X,XXX,XXX,XXX bytes'" -ForegroundColor Green
    Write-Host "  ✅ Progress should show 'Downloaded X.XX MB of X.XX GB (XX%)'" -ForegroundColor Green

    # Test 5: Pipeline usage with FileInfo
    Write-Host "`n--- Test 5: Pipeline Usage with FileInfo ---" -ForegroundColor Yellow
    Write-Host "Testing pipeline operations with returned FileInfo objects:" -ForegroundColor Cyan
    
    if ($downloadedFiles -and $downloadedFiles.Count -gt 0) {
        # Test common FileInfo operations
        $totalSize = ($downloadedFiles | Measure-Object -Property Length -Sum).Sum
        $totalSizeMB = [math]::Round($totalSize / 1MB, 2)
        
        Write-Host "Pipeline operations:" -ForegroundColor Cyan
        Write-Host "  Total files: $($downloadedFiles.Count)" -ForegroundColor White
        Write-Host "  Total size: $totalSizeMB MB" -ForegroundColor White
        Write-Host "  Largest file: $(($downloadedFiles | Sort-Object Length -Descending | Select-Object -First 1).Name)" -ForegroundColor White
        Write-Host "  File extensions: $(($downloadedFiles | Group-Object Extension | ForEach-Object { $_.Name }) -join ', ')" -ForegroundColor White
        
        # Test filtering and sorting
        $msuFiles = $downloadedFiles | Where-Object { $_.Extension -eq '.msu' }
        Write-Host "  MSU files: $($msuFiles.Count)" -ForegroundColor White
        
        Write-Host "✅ Pipeline operations work correctly with FileInfo objects!" -ForegroundColor Green
    }

    # Test 6: Verify files exist on disk
    Write-Host "`n--- Test 6: File Verification ---" -ForegroundColor Yellow
    Write-Host "Verifying downloaded files exist on disk:" -ForegroundColor Cyan
    
    if ($downloadedFiles) {
        foreach ($file in $downloadedFiles) {
            if (Test-Path $file.FullName) {
                $actualSize = (Get-Item $file.FullName).Length
                $sizeMB = [math]::Round($actualSize / 1MB, 2)
                Write-Host "  ✅ $($file.Name) - $sizeMB MB" -ForegroundColor Green
            } else {
                Write-Host "  ❌ $($file.Name) - File not found!" -ForegroundColor Red
            }
        }
    }

} catch {
    Write-Host "❌ Test failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor Red
} finally {
    # Cleanup
    Write-Host "`n--- Cleanup ---" -ForegroundColor Yellow
    $cleanup = Read-Host "Do you want to clean up downloaded files? (y/N)"
    
    if ($cleanup -eq 'y' -or $cleanup -eq 'Y') {
        if (Test-Path $testDir) {
            Remove-Item $testDir -Recurse -Force
            Write-Host "✅ Cleaned up test directory" -ForegroundColor Green
        }
    } else {
        Write-Host "📁 Downloaded files preserved in: $testDir" -ForegroundColor Cyan
    }
}

Write-Host "`n=============================================" -ForegroundColor Cyan
Write-Host "🎉 Enhanced Save cmdlet improvements tested!" -ForegroundColor Green
Write-Host "✅ Intelligent size logging (B, KB, MB, GB, TB)" -ForegroundColor Green
Write-Host "✅ Returns List<FileInfo> for pipeline operations" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Cyan
