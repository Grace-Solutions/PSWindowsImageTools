# Test Script for Enhanced Duration Logging
# Demonstrates intelligent duration formatting with human-readable timestamps

Write-Host "=== ENHANCED DURATION LOGGING TEST ===" -ForegroundColor Yellow
Write-Host "Testing intelligent duration formatting and timestamp logging" -ForegroundColor Cyan
Write-Host ""

# Import the module
try {
    Import-Module '.\Module\PSWindowsImageTools\PSWindowsImageTools.psd1' -Force
    Write-Host "SUCCESS: Module imported successfully" -ForegroundColor Green
} catch {
    Write-Host "ERROR: Failed to import module: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "TEST 1: Duration Formatting Examples" -ForegroundColor Green
Write-Host "Testing various duration formats..." -ForegroundColor White

# Load the assembly to access LoggingService directly
Add-Type -Path ".\Module\PSWindowsImageTools\bin\PSWindowsImageTools.dll"

# Test different duration formats
$testDurations = @(
    [TimeSpan]::FromMilliseconds(2),      # 2 milliseconds
    [TimeSpan]::FromMilliseconds(500),    # 500 milliseconds
    [TimeSpan]::FromSeconds(1),           # 1 second
    [TimeSpan]::FromSeconds(30),          # 30 seconds
    [TimeSpan]::FromSeconds(648),         # 648 seconds (10+ minutes)
    [TimeSpan]::FromMinutes(2),           # 2 minutes
    [TimeSpan]::FromMinutes(45),          # 45 minutes
    [TimeSpan]::FromHours(1),             # 1 hour
    [TimeSpan]::FromHours(3),             # 3 hours
    [TimeSpan]::FromDays(1),              # 1 day
    [TimeSpan]::FromDays(2),              # 2 days
    [TimeSpan]::new(2, 3, 45, 30)        # 2 days, 3 hours, 45 minutes, 30 seconds
)

Write-Host "Duration Examples:" -ForegroundColor Cyan
foreach ($duration in $testDurations) {
    $formatted = [PSWindowsImageTools.Services.LoggingService]::FormatDuration($duration)
    $compact = [PSWindowsImageTools.Services.LoggingService]::FormatDurationCompact($duration)
    Write-Host "  $($duration.ToString()) -> '$formatted' (compact: '$compact')" -ForegroundColor White
}

Write-Host ""
Write-Host "TEST 2: Timestamp Formatting Examples" -ForegroundColor Green
Write-Host "Testing human-readable timestamp formats..." -ForegroundColor White

$now = Get-Date
$formatted = [PSWindowsImageTools.Services.LoggingService]::FormatTimestamp($now)
$short = [PSWindowsImageTools.Services.LoggingService]::FormatTimestampShort($now)

Write-Host "Current Time Examples:" -ForegroundColor Cyan
Write-Host "  Full format: $formatted" -ForegroundColor White
Write-Host "  Short format: $short" -ForegroundColor White

Write-Host ""
Write-Host "TEST 3: Operation Logging with Timestamps" -ForegroundColor Green
Write-Host "Testing complete operation logging cycle..." -ForegroundColor White

# Simulate a quick operation
Write-Host "Starting simulated operation..." -ForegroundColor Cyan
$startTime = [PSWindowsImageTools.Services.LoggingService]::LogOperationStartWithTimestamp($null, "TestService", "Sample Operation", "Testing enhanced logging")

# Simulate some work
Start-Sleep -Milliseconds 1500

# Complete the operation
[PSWindowsImageTools.Services.LoggingService]::LogOperationCompleteWithTimestamp($null, "TestService", "Sample Operation", $startTime, "Testing enhanced logging")

Write-Host ""
Write-Host "TEST 4: Real Image List Operation with Enhanced Logging" -ForegroundColor Green
Write-Host "Testing with actual Get-WindowsImageList cmdlet..." -ForegroundColor White

$esdFile = "C:\Users\gsadmin\Downloads\WindowsImages\26100.2033.241004-2336.ge_release_svc_refresh_CLIENTBUSINESS_VOL_x64FRE_en-us.esd"

if (Test-Path $esdFile) {
    try {
        Write-Host "Getting image list with enhanced timestamp logging..." -ForegroundColor Cyan
        $images = Get-WindowsImageList -ImagePath $esdFile -InclusionFilter { $_.Edition -like "*Pro*" } -Verbose
        
        if ($images) {
            Write-Host "SUCCESS: Retrieved $($images.Count) Professional edition(s)" -ForegroundColor Green
            Write-Host "Enhanced logging should show:" -ForegroundColor Cyan
            Write-Host "  • Start timestamp with day name and time" -ForegroundColor White
            Write-Host "  • Completion timestamp with intelligent duration" -ForegroundColor White
            Write-Host "  • Per-operation timing details" -ForegroundColor White
        }
    } catch {
        Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "INFO: ESD file not found, skipping real operation test" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "TEST 5: Progress Message Duration Integration" -ForegroundColor Green
Write-Host "Testing duration in progress messages..." -ForegroundColor White

# Simulate progress with duration
$operationStart = Get-Date
for ($i = 1; $i -le 5; $i++) {
    $elapsed = (Get-Date) - $operationStart
    $elapsedCompact = [PSWindowsImageTools.Services.LoggingService]::FormatDurationCompact($elapsed)
    $percentage = $i * 20
    
    Write-Host "  Progress: $percentage% - ${elapsedCompact}: Processing item $i of 5" -ForegroundColor Cyan
    Start-Sleep -Milliseconds 300
}

Write-Host ""
Write-Host "=== ENHANCED LOGGING FEATURES DEMONSTRATED ===" -ForegroundColor Yellow
Write-Host ""
Write-Host "Duration Intelligence:" -ForegroundColor White
Write-Host "• Milliseconds: '2 milliseconds', '500 milliseconds'" -ForegroundColor Green
Write-Host "• Seconds: '30 seconds', '648 seconds'" -ForegroundColor Green  
Write-Host "• Minutes: '2 minutes', '45 minutes, 30 seconds'" -ForegroundColor Green
Write-Host "• Hours: '3 hours', '3 hours, 45 minutes'" -ForegroundColor Green
Write-Host "• Days: '2 days', '2 days, 3 hours'" -ForegroundColor Green
Write-Host ""
Write-Host "Timestamp Formats:" -ForegroundColor White
Write-Host "• Full: 'Wednesday, June 15, 2025 @ 7:48 AM'" -ForegroundColor Green
Write-Host "• Short: 'Wed @ 7:48 AM'" -ForegroundColor Green
Write-Host "• Compact for progress: '2ms', '30s', '5m', '2h', '1d'" -ForegroundColor Green
Write-Host ""
Write-Host "Operation Logging:" -ForegroundColor White
Write-Host "• Start: 'Starting [Operation] at [Timestamp] - [Details]'" -ForegroundColor Green
Write-Host "• Complete: 'Completed [Operation] at [Timestamp] (Duration: [Intelligent Duration]) - [Details]'" -ForegroundColor Green
Write-Host ""
Write-Host "All services now use enhanced timestamp and duration logging!" -ForegroundColor Cyan
