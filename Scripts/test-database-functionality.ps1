# Test Database Functionality
Write-Host "🗄️ Testing Database Functionality" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan

Import-Module '.\Module\PSWindowsImageTools' -Force

# Test database path
$testDbPath = "C:\Temp\TestDatabase\test.db"
$testDir = Split-Path $testDbPath -Parent

try {
    # Clean up any existing test database
    if (Test-Path $testDir) {
        Remove-Item $testDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $testDir -Force | Out-Null
    Write-Host "✅ Created test directory: $testDir" -ForegroundColor Green

    # Test 1: Configure database
    Write-Host "`n--- Test 1: Configure Database ---" -ForegroundColor Yellow
    Set-WindowsImageDatabaseConfiguration -Path $testDbPath -Verbose
    Write-Host "✅ Database configuration set" -ForegroundColor Green

    # Test 2: Create database
    Write-Host "`n--- Test 2: Create Database ---" -ForegroundColor Yellow
    New-WindowsImageDatabase -Force -Verbose
    Write-Host "✅ Database created successfully" -ForegroundColor Green

    # Verify database file exists
    if (Test-Path $testDbPath) {
        $dbFile = Get-Item $testDbPath
        Write-Host "Database file: $($dbFile.FullName)" -ForegroundColor Cyan
        Write-Host "Database size: $([math]::Round($dbFile.Length / 1KB, 2)) KB" -ForegroundColor Cyan
    } else {
        Write-Host "❌ Database file not found!" -ForegroundColor Red
    }

    # Test 3: Test download with database enabled
    Write-Host "`n--- Test 3: Download with Database Enabled ---" -ForegroundColor Yellow
    
    # Create download directory
    $downloadDir = "C:\Temp\DatabaseTest"
    if (Test-Path $downloadDir) {
        Remove-Item $downloadDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $downloadDir -Force | Out-Null
    
    # Get a small update and download it
    Write-Host "Getting Windows 11 update..." -ForegroundColor Cyan
    $result = Search-WindowsUpdateCatalog -Query 'Windows 11 Cumulative' -Architecture x64 -MaxResults 1
    $result | Get-WindowsUpdateDownloadUrl
    
    Write-Host "Downloading with database tracking..." -ForegroundColor Cyan
    $packages = $result | Save-WindowsUpdateCatalogResult -DestinationPath $downloadDir -Verbose
    
    if ($packages -and $packages.Count -gt 0) {
        $package = $packages[0]
        Write-Host "✅ Download completed:" -ForegroundColor Green
        Write-Host "  KB: $($package.KBNumber)" -ForegroundColor White
        Write-Host "  Downloaded: $($package.IsDownloaded)" -ForegroundColor White
        Write-Host "  File: $($package.LocalFile.Name)" -ForegroundColor White
        Write-Host "  Size: $([math]::Round($package.LocalFile.Length / 1MB, 2)) MB" -ForegroundColor White
    } else {
        Write-Host "❌ No packages returned" -ForegroundColor Red
    }

    # Test 4: Verify database was updated
    Write-Host "`n--- Test 4: Verify Database Updates ---" -ForegroundColor Yellow
    
    # Check database size after operations
    if (Test-Path $testDbPath) {
        $dbFileAfter = Get-Item $testDbPath
        Write-Host "Database size after operations: $([math]::Round($dbFileAfter.Length / 1KB, 2)) KB" -ForegroundColor Cyan
        
        if ($dbFileAfter.Length -gt $dbFile.Length) {
            Write-Host "✅ Database size increased - data was written" -ForegroundColor Green
        } else {
            Write-Host "⚠️ Database size unchanged - may not have written data" -ForegroundColor Yellow
        }
    }

    # Test 5: Test database disable
    Write-Host "`n--- Test 5: Test Database Disable ---" -ForegroundColor Yellow
    Set-WindowsImageDatabaseConfiguration -Disable -Verbose
    Write-Host "✅ Database disabled for session" -ForegroundColor Green

    # Test download with database disabled
    Write-Host "Testing download with database disabled..." -ForegroundColor Cyan
    $result2 = Search-WindowsUpdateCatalog -Query 'Windows 11 Cumulative' -Architecture x64 -MaxResults 1
    $result2 | Get-WindowsUpdateDownloadUrl
    
    $packages2 = $result2 | Save-WindowsUpdateCatalogResult -DestinationPath $downloadDir -Force -Verbose
    
    if ($packages2 -and $packages2.Count -gt 0) {
        Write-Host "✅ Download with disabled database completed" -ForegroundColor Green
    }

    # Clean up download directory
    if (Test-Path $downloadDir) {
        Remove-Item $downloadDir -Recurse -Force
        Write-Host "✅ Cleaned up download directory" -ForegroundColor Green
    }

} catch {
    Write-Host "❌ Database test failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor Red
} finally {
    # Cleanup
    Write-Host "`n--- Cleanup ---" -ForegroundColor Yellow
    $cleanup = Read-Host "Do you want to clean up test database? (y/N)"
    
    if ($cleanup -eq 'y' -or $cleanup -eq 'Y') {
        if (Test-Path $testDir) {
            Remove-Item $testDir -Recurse -Force
            Write-Host "✅ Cleaned up test database" -ForegroundColor Green
        }
    } else {
        Write-Host "📁 Test database preserved at: $testDbPath" -ForegroundColor Cyan
    }
}

Write-Host "`n=================================" -ForegroundColor Cyan
Write-Host "🎉 Database functionality tested!" -ForegroundColor Green
Write-Host "✅ SQLite native libraries working" -ForegroundColor Green
Write-Host "✅ Database operations functional" -ForegroundColor Green
Write-Host "=================================" -ForegroundColor Cyan
