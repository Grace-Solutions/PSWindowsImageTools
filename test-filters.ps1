Import-Module '.\Module\PSWindowsImageTools\PSWindowsImageTools.psd1' -Force

Write-Host '=== Testing Standardized Filter Pattern ===' -ForegroundColor Green

# Test 1: Get all images (baseline)
Write-Host '=== Test 1: All Images (No Filters) ===' -ForegroundColor Yellow
$allImages = Get-WindowsImageList -ImagePath 'C:\Users\gsadmin\Downloads\WindowsImages\Windows11_24H2\sources\install.wim'
$allImages | Format-Table Index, Name, Edition -AutoSize

# Test 2: Inclusion filter for Pro edition
Write-Host '=== Test 2: Inclusion Filter (Pro Edition) ===' -ForegroundColor Yellow
Write-Host 'InclusionFilter: {$_.Name -like "*Pro*"}' -ForegroundColor Cyan
$proImages = Get-WindowsImageList -ImagePath 'C:\Users\gsadmin\Downloads\WindowsImages\Windows11_24H2\sources\install.wim' -InclusionFilter {$_.Name -like '*Pro*'} -Verbose
$proImages | Format-Table Index, Name, Edition -AutoSize

# Test 3: Exclusion filter to exclude Enterprise
Write-Host '=== Test 3: Exclusion Filter (Exclude Enterprise) ===' -ForegroundColor Yellow
Write-Host 'ExclusionFilter: {$_.Name -like "*Enterprise*"}' -ForegroundColor Cyan
$nonEnterpriseImages = Get-WindowsImageList -ImagePath 'C:\Users\gsadmin\Downloads\WindowsImages\Windows11_24H2\sources\install.wim' -ExclusionFilter {$_.Name -like '*Enterprise*'} -Verbose
$nonEnterpriseImages | Format-Table Index, Name, Edition -AutoSize

# Test 4: Combined filters (Include all, exclude multi-session)
Write-Host '=== Test 4: Combined Filters (Include all Windows 11, Exclude multi-session) ===' -ForegroundColor Yellow
Write-Host 'InclusionFilter: {$_.Name -like "*Windows 11*"}' -ForegroundColor Cyan
Write-Host 'ExclusionFilter: {$_.Name -like "*multi-session*"}' -ForegroundColor Cyan
$combinedImages = Get-WindowsImageList -ImagePath 'C:\Users\gsadmin\Downloads\WindowsImages\Windows11_24H2\sources\install.wim' -InclusionFilter {$_.Name -like '*Windows 11*'} -ExclusionFilter {$_.Name -like '*multi-session*'} -Verbose
$combinedImages | Format-Table Index, Name, Edition -AutoSize

Write-Host '=== Filter Tests Complete ===' -ForegroundColor Green
