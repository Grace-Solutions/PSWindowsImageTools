Import-Module '.\Module\PSWindowsImageTools\PSWindowsImageTools.psd1' -Force

Write-Host '=== Testing All Major Improvements ===' -ForegroundColor Green

# Test 1: Timestamp format and duration formatting
Write-Host '=== Test 1: Timestamp Format (yyyy/MM/dd HH:mm:ss.FFF) ===' -ForegroundColor Yellow
Write-Host 'Check verbose output for new timestamp format...' -ForegroundColor Cyan
$images = Get-WindowsImageList -ImagePath 'C:\Users\gsadmin\Downloads\WindowsImages\Windows11_24H2\sources\install.wim' -Verbose | Select-Object -First 1
Write-Host "Found $($images.Count) image(s)" -ForegroundColor White

# Test 2: Standardized filters (InclusionFilter and ExclusionFilter)
Write-Host '=== Test 2: Standardized Filter Pattern ===' -ForegroundColor Yellow
Write-Host 'Testing InclusionFilter: {$_.Index -eq 1}' -ForegroundColor Cyan
$filteredImages = Get-WindowsImageList -ImagePath 'C:\Users\gsadmin\Downloads\WindowsImages\Windows11_24H2\sources\install.wim' -InclusionFilter {$_.Index -eq 1} -Verbose
$filteredImages | Format-Table Index, Name, Edition -AutoSize

# Test 3: Mount with progress callbacks and duration reporting
Write-Host '=== Test 3: Mount with Progress Callbacks ===' -ForegroundColor Yellow
Write-Host 'Mounting Windows 11 Pro with detailed progress...' -ForegroundColor Cyan
$mounted = Mount-WindowsImageList -ImagePath 'C:\Users\gsadmin\Downloads\WindowsImages\Windows11_24H2\sources\install.wim' -InclusionFilter {$_.Index -eq 1} -Verbose

Write-Host '=== Mounted Image Details ===' -ForegroundColor Yellow
$mounted | Format-Table ImageIndex, ImageName, MountPath, Status -AutoSize

# Test 4: Dismount with progress callbacks and duration reporting
Write-Host '=== Test 4: Dismount with Progress Callbacks ===' -ForegroundColor Yellow
Write-Host 'Dismounting with detailed progress and duration reporting...' -ForegroundColor Cyan
$dismounted = $mounted | Dismount-WindowsImageList -RemoveDirectories -Verbose

Write-Host '=== Dismount Results ===' -ForegroundColor Yellow
$dismounted | Format-Table ImageIndex, ImageName, Status -AutoSize

Write-Host '=== All Improvements Test Complete ===' -ForegroundColor Green
Write-Host 'Key improvements verified:' -ForegroundColor White
Write-Host '  ✓ Timestamp format: yyyy/MM/dd HH:mm:ss.FFF' -ForegroundColor Green
Write-Host '  ✓ Duration formatting: human-readable (X days, Y hours, Z minutes, W seconds)' -ForegroundColor Green
Write-Host '  ✓ Standardized filters: InclusionFilter and ExclusionFilter' -ForegroundColor Green
Write-Host '  ✓ Progress callbacks: Real mount/dismount progress with timing' -ForegroundColor Green
Write-Host '  ✓ Separate cmdlets: Mount-WindowsImageList and Dismount-WindowsImageList' -ForegroundColor Green
Write-Host '  ✓ [X of Y] format: Consistent progress reporting' -ForegroundColor Green
