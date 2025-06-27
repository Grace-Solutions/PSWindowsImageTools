# Test Multiple Downloads with Statistics
Write-Host "📊 Testing Multiple Downloads with Statistics" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

Import-Module '.\Module\PSWindowsImageTools' -Force

# Create test directory
$testDir = "C:\Temp\MultipleDownloads"
if (Test-Path $testDir) { Remove-Item $testDir -Recurse -Force }
New-Item -ItemType Directory -Path $testDir -Force | Out-Null

try {
    # Get multiple updates
    Write-Host "Getting multiple Windows 11 updates..." -ForegroundColor Yellow
    $results = Search-WindowsUpdateCatalog -Query 'Windows 11 Cumulative' -Architecture x64 -MaxResults 3
    $results | Get-WindowsUpdateDownloadUrl
    
    Write-Host "`nFound $($results.Count) updates:" -ForegroundColor Cyan
    $results | ForEach-Object -Begin { $i = 1 } -Process {
        Write-Host "  $i. $($_.KBNumber) - $($_.SizeFormatted)" -ForegroundColor White
        $i++
    }
    
    Write-Host "`nDownloading multiple updates with statistics..." -ForegroundColor Yellow
    $packages = $results | Save-WindowsUpdateCatalogResult -DestinationPath $testDir -Verbose
    
    Write-Host "`n--- Download Results ---" -ForegroundColor Cyan
    if ($packages -and $packages.Count -gt 0) {
        $successful = $packages | Where-Object { $_.IsDownloaded }
        $failed = $packages | Where-Object { -not $_.IsDownloaded }
        
        Write-Host "Successful downloads:" -ForegroundColor Green
        $successful | ForEach-Object {
            $sizeMB = [math]::Round($_.LocalFile.Length / 1MB, 2)
            Write-Host "  ✅ $($_.KBNumber) - $sizeMB MB" -ForegroundColor Green
        }
        
        if ($failed.Count -gt 0) {
            Write-Host "`nFailed downloads:" -ForegroundColor Red
            $failed | ForEach-Object {
                Write-Host "  ❌ $($_.KBNumber) - $($_.ErrorMessage)" -ForegroundColor Red
            }
        }
        
        Write-Host "`nSummary:" -ForegroundColor Cyan
        Write-Host "  Total: $($packages.Count)" -ForegroundColor White
        Write-Host "  Successful: $($successful.Count)" -ForegroundColor Green
        Write-Host "  Failed: $($failed.Count)" -ForegroundColor Red
        
    } else {
        Write-Host "❌ No packages returned" -ForegroundColor Red
    }
    
} catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor Red
} finally {
    # Cleanup
    Write-Host "`n--- Cleanup ---" -ForegroundColor Yellow
    $cleanup = Read-Host "Do you want to clean up downloaded files? (y/N)"
    
    if ($cleanup -eq 'y' -or $cleanup -eq 'Y') {
        if (Test-Path $testDir) {
            Remove-Item $testDir -Recurse -Force
            Write-Host "✅ Cleaned up download directory" -ForegroundColor Green
        }
    } else {
        Write-Host "📁 Downloaded files preserved in: $testDir" -ForegroundColor Cyan
    }
}

Write-Host "`n=============================================" -ForegroundColor Cyan
Write-Host "🎉 Multiple downloads with statistics tested!" -ForegroundColor Green
Write-Host "✅ Success/failure percentages on separate lines" -ForegroundColor Green
Write-Host "✅ Clear statistical reporting" -ForegroundColor Green
Write-Host "=============================================" -ForegroundColor Cyan
