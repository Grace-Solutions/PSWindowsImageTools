# Test script for the fixed Windows Update Catalog implementation
# This script tests various search queries and validates the parsing logic

param(
    [switch]$Debug,
    [switch]$IncludeDownloadUrls,
    [int]$MaxResults = 5
)

Write-Host "Testing Fixed Windows Update Catalog Implementation" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

# Import the module
try {
    Import-Module ".\Module\PSWindowsImageTools" -Force
    Write-Host "✅ Module imported successfully" -ForegroundColor Green
} catch {
    Write-Host "❌ Failed to import module: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Test queries with expected results
$testQueries = @(
    @{
        Query = "Windows 11 Security Update"
        Description = "General Windows 11 security updates"
        ExpectedResults = $true
    },
    @{
        Query = "KB5000001"
        Description = "Specific KB number (may not exist)"
        ExpectedResults = $false
    },
    @{
        Query = "2024-12"
        Description = "Recent monthly updates"
        ExpectedResults = $true
    },
    @{
        Query = "Windows Server 2022"
        Description = "Windows Server updates"
        ExpectedResults = $true
    }
)

$totalTests = 0
$passedTests = 0
$failedTests = 0

foreach ($test in $testQueries) {
    $totalTests++
    Write-Host "`n--- Test ${totalTests}: $($test.Description) ---" -ForegroundColor Yellow
    Write-Host "Query: '$($test.Query)'" -ForegroundColor Gray
    
    try {
        $searchParams = @{
            Query = $test.Query
            MaxResults = $MaxResults
        }
        
        if ($Debug) {
            $searchParams.Debug = $true
        }
        
        if ($IncludeDownloadUrls) {
            $searchParams.IncludeDownloadUrls = $true
        }
        
        # Measure execution time
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        $results = Search-WindowsUpdateCatalog @searchParams
        $stopwatch.Stop()
        
        Write-Host "Execution time: $($stopwatch.ElapsedMilliseconds)ms" -ForegroundColor Cyan
        Write-Host "Results found: $($results.Count)" -ForegroundColor Cyan
        
        # Validate results
        $testPassed = $true
        $issues = @()
        
        if ($test.ExpectedResults -and $results.Count -eq 0) {
            $testPassed = $false
            $issues += "Expected results but got none"
        }
        
        if ($results.Count -gt 0) {
            # Validate first result structure
            $firstResult = $results[0]
            
            if ([string]::IsNullOrEmpty($firstResult.UpdateId)) {
                $testPassed = $false
                $issues += "Missing UpdateId"
            }
            
            if ([string]::IsNullOrEmpty($firstResult.Title)) {
                $testPassed = $false
                $issues += "Missing Title"
            }
            
            if ([string]::IsNullOrEmpty($firstResult.Classification)) {
                $testPassed = $false
                $issues += "Missing Classification"
            }
            
            if ($firstResult.LastModified -eq [DateTime]::MinValue) {
                $testPassed = $false
                $issues += "Missing or invalid LastModified date"
            }
            
            # Display sample results
            Write-Host "`nSample Results:" -ForegroundColor White
            $results | Select-Object -First 3 | ForEach-Object {
                Write-Host "  ID: $($_.UpdateId)" -ForegroundColor Gray
                Write-Host "  Title: $($_.Title)" -ForegroundColor White
                Write-Host "  KB: $($_.KBNumber)" -ForegroundColor Gray
                Write-Host "  Products: $($_.Products -join ', ')" -ForegroundColor Gray
                Write-Host "  Classification: $($_.Classification)" -ForegroundColor Gray
                Write-Host "  Architecture: $($_.Architecture)" -ForegroundColor Gray
                Write-Host "  Size: $($_.SizeFormatted)" -ForegroundColor Gray
                Write-Host "  Last Modified: $($_.LastModified)" -ForegroundColor Gray
                if ($IncludeDownloadUrls) {
                    Write-Host "  Download URLs: $($_.DownloadUrls.Count)" -ForegroundColor Gray
                }
                Write-Host ""
            }
        }
        
        if ($testPassed) {
            Write-Host "✅ Test PASSED" -ForegroundColor Green
            $passedTests++
        } else {
            Write-Host "❌ Test FAILED" -ForegroundColor Red
            Write-Host "Issues:" -ForegroundColor Red
            $issues | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
            $failedTests++
        }
        
    } catch {
        Write-Host "❌ Test FAILED with exception: $($_.Exception.Message)" -ForegroundColor Red
        $failedTests++
    }
}

# Summary
Write-Host "`n=============================================" -ForegroundColor Cyan
Write-Host "Test Summary" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "Total Tests: $totalTests" -ForegroundColor White
Write-Host "Passed: $passedTests" -ForegroundColor Green
Write-Host "Failed: $failedTests" -ForegroundColor Red

if ($failedTests -eq 0) {
    Write-Host "`n🎉 All tests passed! Implementation is working correctly." -ForegroundColor Green
} else {
    Write-Host "`n⚠️ Some tests failed. Check the implementation." -ForegroundColor Yellow
}

# Test download URLs if requested
if ($IncludeDownloadUrls -and $passedTests -gt 0) {
    Write-Host "`n--- Download URL Test ---" -ForegroundColor Yellow
    try {
        $sampleResult = Search-WindowsUpdateCatalog -Query "Windows 11 Security Update" -MaxResults 1 -IncludeDownloadUrls
        if ($sampleResult.Count -gt 0 -and $sampleResult[0].DownloadUrls.Count -gt 0) {
            Write-Host "✅ Download URLs retrieved successfully" -ForegroundColor Green
            Write-Host "Sample URL: $($sampleResult[0].DownloadUrls[0])" -ForegroundColor Gray
        } else {
            Write-Host "⚠️ No download URLs found" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "❌ Download URL test failed: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "`nTest completed." -ForegroundColor Cyan
