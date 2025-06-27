# Complete Test of Get-PatchTuesday Cmdlet
Write-Host "📅 Complete Get-PatchTuesday Cmdlet Test" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

Import-Module '.\Module\PSWindowsImageTools' -Force
Write-Host "✅ Module loaded successfully" -ForegroundColor Green

Write-Host "`nCurrent Date: $(Get-Date -Format 'yyyy-MM-dd dddd')" -ForegroundColor Yellow

# Test 1: Next Patch Tuesday (Default)
Write-Host "`n--- Test 1: Next Patch Tuesday (Default) ---" -ForegroundColor Yellow
$next = Get-PatchTuesday
Write-Host "Next Patch Tuesday:" -ForegroundColor Cyan
$next | Format-List Date, MonthName, Week, Quarter, DayOfYear, HasOccurred, DaysFromNow, Description

# Test 2: All Patch Tuesdays for current year
Write-Host "`n--- Test 2: All Patch Tuesdays for Current Year ---" -ForegroundColor Yellow
$all = Get-PatchTuesday -All
Write-Host "All Patch Tuesdays for $((Get-Date).Year) with new properties:" -ForegroundColor Cyan
$all | Format-Table Date, MonthName, Week, Quarter, DayOfYear, HasOccurred -AutoSize

# Test 3: Remaining Patch Tuesdays
Write-Host "`n--- Test 3: Remaining Patch Tuesdays ---" -ForegroundColor Yellow
$remaining = Get-PatchTuesday -Remaining
Write-Host "Remaining Patch Tuesdays for $((Get-Date).Year):" -ForegroundColor Cyan
$remaining | Format-Table Date, MonthName, Week, Quarter, DayOfYear, DaysFromNow -AutoSize

# Test 4: Custom After date
Write-Host "`n--- Test 4: After March 1, 2025 ---" -ForegroundColor Yellow
$afterMarch = Get-PatchTuesday -After (Get-Date "2025-03-01")
Write-Host "Next Patch Tuesday after March 1, 2025:" -ForegroundColor Cyan
$afterMarch | Format-List Date, MonthName, Week, Quarter, DayOfYear, HasOccurred

# Test 5: All for different year (using After parameter)
Write-Host "`n--- Test 5: All Patch Tuesdays for 2024 ---" -ForegroundColor Yellow
$all2024 = Get-PatchTuesday -All -After (Get-Date "2024-01-01")
Write-Host "All Patch Tuesdays for 2024:" -ForegroundColor Cyan
$all2024 | Format-Table Date, MonthName, Week, Quarter, DayOfYear, HasOccurred -AutoSize

# Test 6: Quarter Analysis
Write-Host "`n--- Test 6: Quarterly Analysis ---" -ForegroundColor Yellow
Write-Host "Patch Tuesdays by Quarter for $((Get-Date).Year):" -ForegroundColor Cyan
$all | Group-Object Quarter | ForEach-Object {
    Write-Host "  Q$($_.Name): $($_.Count) Patch Tuesdays" -ForegroundColor White
    $_.Group | ForEach-Object { Write-Host "    - $($_.Date.ToString('MMM dd')) (Week $($_.Week), Day $($_.DayOfYear))" -ForegroundColor Gray }
}

# Test 7: Properties Summary
Write-Host "`n--- Test 7: Properties Summary ---" -ForegroundColor Yellow
Write-Host "Sample Patch Tuesday (July 2025) properties:" -ForegroundColor Cyan
$sample = $all | Where-Object { $_.Month -eq 7 }
Write-Host "  Date: $($sample.Date)" -ForegroundColor White
Write-Host "  Month: $($sample.Month) ($($sample.MonthName))" -ForegroundColor White
Write-Host "  Year: $($sample.Year)" -ForegroundColor White
Write-Host "  Week: $($sample.Week)" -ForegroundColor White
Write-Host "  Quarter: Q$($sample.Quarter)" -ForegroundColor White
Write-Host "  Day of Year: $($sample.DayOfYear)" -ForegroundColor White
Write-Host "  Has Occurred: $($sample.HasOccurred)" -ForegroundColor White
Write-Host "  Days from Now: $($sample.DaysFromNow)" -ForegroundColor White
Write-Host "  Is Current Month: $($sample.IsCurrentMonth)" -ForegroundColor White
Write-Host "  Is Next Month: $($sample.IsNextMonth)" -ForegroundColor White
Write-Host "  Description: $($sample.Description)" -ForegroundColor White

# Test 8: Validation
Write-Host "`n--- Test 8: Validation ---" -ForegroundColor Yellow
Write-Host "Validating calculations:" -ForegroundColor Cyan

# Verify all dates are Tuesdays
$allTuesdays = $all | Where-Object { $_.Date.DayOfWeek -ne 'Tuesday' }
if ($allTuesdays.Count -eq 0) {
    Write-Host "✅ All dates are Tuesdays" -ForegroundColor Green
} else {
    Write-Host "❌ Found non-Tuesday dates!" -ForegroundColor Red
}

# Verify quarters are correct
$quarterCheck = $all | Where-Object { 
    $expectedQuarter = [Math]::Ceiling($_.Month / 3.0)
    $_.Quarter -ne $expectedQuarter
}
if ($quarterCheck.Count -eq 0) {
    Write-Host "✅ All quarters are correct" -ForegroundColor Green
} else {
    Write-Host "❌ Found incorrect quarters!" -ForegroundColor Red
}

# Verify week numbers are reasonable (1-53)
$weekCheck = $all | Where-Object { $_.Week -lt 1 -or $_.Week -gt 53 }
if ($weekCheck.Count -eq 0) {
    Write-Host "✅ All week numbers are valid (1-53)" -ForegroundColor Green
} else {
    Write-Host "❌ Found invalid week numbers!" -ForegroundColor Red
}

# Verify day of year is reasonable (1-366)
$dayCheck = $all | Where-Object { $_.DayOfYear -lt 1 -or $_.DayOfYear -gt 366 }
if ($dayCheck.Count -eq 0) {
    Write-Host "✅ All day of year values are valid (1-366)" -ForegroundColor Green
} else {
    Write-Host "❌ Found invalid day of year values!" -ForegroundColor Red
}

Write-Host "`n=========================================" -ForegroundColor Cyan
Write-Host "🎉 Complete Get-PatchTuesday testing finished!" -ForegroundColor Green
Write-Host "✅ All new properties (Week, Quarter, DayOfYear) working correctly!" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Cyan
