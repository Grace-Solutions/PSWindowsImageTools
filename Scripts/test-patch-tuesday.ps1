# Test Get-PatchTuesday Cmdlet
Write-Host "📅 Testing Get-PatchTuesday Cmdlet" -ForegroundColor Cyan
Write-Host "===================================" -ForegroundColor Cyan

Import-Module '.\Module\PSWindowsImageTools' -Force
Write-Host "✅ Module loaded successfully" -ForegroundColor Green

Write-Host "`nCurrent Date: $(Get-Date -Format 'yyyy-MM-dd dddd')" -ForegroundColor Yellow

# Test 1: Default behavior - Next Patch Tuesday
Write-Host "`n--- Test 1: Next Patch Tuesday (Default) ---" -ForegroundColor Yellow
$nextPatchTuesday = Get-PatchTuesday
Write-Host "Next Patch Tuesday:" -ForegroundColor Cyan
$nextPatchTuesday | Format-List Date, MonthName, Year, HasOccurred, DaysFromNow, Description

# Test 2: All Patch Tuesdays for current year
Write-Host "`n--- Test 2: All Patch Tuesdays for Current Year ---" -ForegroundColor Yellow
$allThisYear = Get-PatchTuesday -AllInYear
Write-Host "All Patch Tuesdays for $((Get-Date).Year):" -ForegroundColor Cyan
$allThisYear | Format-Table Date, MonthName, HasOccurred, DaysFromNow, IsCurrentMonth, IsNextMonth -AutoSize

# Test 3: Remaining Patch Tuesdays for current year
Write-Host "`n--- Test 3: Remaining Patch Tuesdays for Current Year ---" -ForegroundColor Yellow
$remainingThisYear = Get-PatchTuesday -RemainingInYear
Write-Host "Remaining Patch Tuesdays for $((Get-Date).Year):" -ForegroundColor Cyan
$remainingThisYear | Format-Table Date, MonthName, HasOccurred, DaysFromNow -AutoSize

# Test 4: All Patch Tuesdays for specific year
Write-Host "`n--- Test 4: All Patch Tuesdays for 2024 ---" -ForegroundColor Yellow
$all2024 = Get-PatchTuesday -AllInYear -Year 2024
Write-Host "All Patch Tuesdays for 2024:" -ForegroundColor Cyan
$all2024 | Format-Table Date, MonthName, HasOccurred -AutoSize

# Test 5: From specific date
Write-Host "`n--- Test 5: From Specific Date (2025-03-01) ---" -ForegroundColor Yellow
$fromMarch = Get-PatchTuesday -FromDate (Get-Date "2025-03-01")
Write-Host "Next Patch Tuesday from March 1, 2025:" -ForegroundColor Cyan
$fromMarch | Format-List Date, MonthName, Year, HasOccurred, DaysFromNow

# Test 6: Remaining in year from specific date
Write-Host "`n--- Test 6: Remaining in 2025 from March 1 ---" -ForegroundColor Yellow
$remainingFromMarch = Get-PatchTuesday -RemainingInYear -FromDate (Get-Date "2025-03-01")
Write-Host "Remaining Patch Tuesdays in 2025 from March 1:" -ForegroundColor Cyan
$remainingFromMarch | Format-Table Date, MonthName, HasOccurred, DaysFromNow -AutoSize

# Test 7: Validation - Check if calculations are correct
Write-Host "`n--- Test 7: Validation ---" -ForegroundColor Yellow
Write-Host "Validating Patch Tuesday calculations:" -ForegroundColor Cyan

# Check that all dates are actually Tuesdays
$allDatesAreTuesdays = $allThisYear | ForEach-Object { $_.Date.DayOfWeek -eq 'Tuesday' } | Where-Object { $_ -eq $false }
if ($allDatesAreTuesdays.Count -eq 0) {
    Write-Host "✅ All calculated dates are Tuesdays" -ForegroundColor Green
} else {
    Write-Host "❌ Some dates are not Tuesdays!" -ForegroundColor Red
}

# Check that all dates are second Tuesdays of their months
$allAreSecondTuesdays = $allThisYear | ForEach-Object {
    $firstTuesday = 1..7 | ForEach-Object { Get-Date "$($_.Year)-$($_.Month.ToString('00'))-$($_.ToString('00'))" } | Where-Object { $_.DayOfWeek -eq 'Tuesday' } | Select-Object -First 1
    $secondTuesday = $firstTuesday.AddDays(7)
    $_.Date.Date -eq $secondTuesday.Date
} | Where-Object { $_ -eq $false }

if ($allAreSecondTuesdays.Count -eq 0) {
    Write-Host "✅ All dates are second Tuesdays of their months" -ForegroundColor Green
} else {
    Write-Host "❌ Some dates are not second Tuesdays!" -ForegroundColor Red
}

# Test 8: Properties and methods
Write-Host "`n--- Test 8: Properties and Methods ---" -ForegroundColor Yellow
$samplePatchTuesday = $allThisYear[0]
Write-Host "Sample Patch Tuesday properties:" -ForegroundColor Cyan
Write-Host "  Date: $($samplePatchTuesday.Date)" -ForegroundColor White
Write-Host "  Month: $($samplePatchTuesday.Month)" -ForegroundColor White
Write-Host "  Year: $($samplePatchTuesday.Year)" -ForegroundColor White
Write-Host "  MonthName: $($samplePatchTuesday.MonthName)" -ForegroundColor White
Write-Host "  HasOccurred: $($samplePatchTuesday.HasOccurred)" -ForegroundColor White
Write-Host "  DaysFromNow: $($samplePatchTuesday.DaysFromNow)" -ForegroundColor White
Write-Host "  Description: $($samplePatchTuesday.Description)" -ForegroundColor White
Write-Host "  IsCurrentMonth: $($samplePatchTuesday.IsCurrentMonth)" -ForegroundColor White
Write-Host "  IsNextMonth: $($samplePatchTuesday.IsNextMonth)" -ForegroundColor White
Write-Host "  ToString(): $($samplePatchTuesday.ToString())" -ForegroundColor White

Write-Host "`n===================================" -ForegroundColor Cyan
Write-Host "🎉 Get-PatchTuesday testing completed!" -ForegroundColor Green
Write-Host "===================================" -ForegroundColor Cyan
