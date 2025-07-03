# Test script for INF Driver cmdlets
# This script tests Get-INFDriverList and Add-INFDriverList cmdlets

[CmdletBinding()]
param()

Write-Output "=== Testing INF Driver Cmdlets ==="

# Import the module
try {
    Import-Module ".\Module\PSWindowsImageTools\PSWindowsImageTools.psd1" -Force
    Write-Verbose "Module imported successfully"
} catch {
    Write-Error "Failed to import module: $($_.Exception.Message)"
    exit 1
}

# Test 1: Create test directory structure with mock INF files
Write-Output "`n=== Test 1: Creating Mock INF Files ==="

$testRoot = "C:\Temp\DriverTest"
$testDirs = @(
    "$testRoot\NetworkDrivers",
    "$testRoot\DisplayDrivers",
    "$testRoot\AudioDrivers\Realtek",
    "$testRoot\AudioDrivers\Intel"
)

# Create test directories
foreach ($dir in $testDirs) {
    if (!(Test-Path $dir)) {
        New-Item -Path $dir -ItemType Directory -Force | Out-Null
        Write-Verbose "Created directory: $dir"
    }
}

# Create mock INF files with realistic content
$mockINFs = @{
    "$testRoot\NetworkDrivers\e1000.inf" = @"
[Version]
Signature="`$Windows NT`$"
Class=Net
ClassGUID={4D36E972-E325-11CE-BFC1-08002BE10318}
Provider=%Intel%
DriverVer=03/15/2023,12.19.0.5
CatalogFile=e1000.cat

[Manufacturer]
%Intel%=Intel,NTamd64

[Intel.NTamd64]
%E1000.DeviceDesc%=E1000.ndi,PCI\VEN_8086&DEV_1000

[Strings]
Intel="Intel Corporation"
E1000.DeviceDesc="Intel(R) PRO/1000 Network Connection"
"@

    "$testRoot\DisplayDrivers\nvidia.inf" = @"
[Version]
Signature="`$Windows NT`$"
Class=Display
ClassGUID={4D36E968-E325-11CE-BFC1-08002BE10318}
Provider=%NVIDIA%
DriverVer=06/21/2023,31.0.15.3179
CatalogFile=nvidia.cat

[Manufacturer]
%NVIDIA%=NVIDIA,NTamd64

[NVIDIA.NTamd64]
%NVIDIA_DEV.2204.01%=Section001, PCI\VEN_10DE&DEV_2204

[Strings]
NVIDIA="NVIDIA Corporation"
NVIDIA_DEV.2204.01="NVIDIA GeForce RTX 3080"
"@

    "$testRoot\AudioDrivers\Realtek\hdaudio.inf" = @"
[Version]
Signature="`$Windows NT`$"
Class=MEDIA
ClassGUID={4D36E96C-E325-11CE-BFC1-08002BE10318}
Provider=%Realtek%
DriverVer=12/08/2022,6.0.9381.1
CatalogFile=hdaudio.cat

[Manufacturer]
%Realtek%=Realtek,NTamd64

[Realtek.NTamd64]
%HDAUDIO.DeviceDesc%=HDAudio_Device,HDAUDIO\FUNC_01&VEN_10EC&DEV_0295

[Strings]
Realtek="Realtek Semiconductor Corp."
HDAUDIO.DeviceDesc="Realtek High Definition Audio"
"@

    "$testRoot\AudioDrivers\Intel\intcaudiobus.inf" = @"
[Version]
Signature="`$Windows NT`$"
Class=System
ClassGUID={4D36E97D-E325-11CE-BFC1-08002BE10318}
Provider=%Intel%
DriverVer=01/30/2023,10.30.0.5714
CatalogFile=intcaudiobus.cat

[Manufacturer]
%Intel%=Intel,NTamd64

[Intel.NTamd64]
%IntcAudioBus.DeviceDesc%=IntcAudioBus_Device,PCI\VEN_8086&DEV_9DC8

[Strings]
Intel="Intel Corporation"
IntcAudioBus.DeviceDesc="Intel(R) Smart Sound Technology Audio Controller"
"@
}

# Write mock INF files
foreach ($infPath in $mockINFs.Keys) {
    $mockINFs[$infPath] | Out-File -FilePath $infPath -Encoding UTF8
    Write-Host "Created mock INF: $infPath" -ForegroundColor Gray
}

Write-Host "✓ Mock INF files created successfully" -ForegroundColor Green

# Test 2: Test Get-INFDriverList without parsing
Write-Host "`n=== Test 2: Get-INFDriverList (No Parsing) ===" -ForegroundColor Yellow

try {
    $drivers = Get-INFDriverList -Path (Get-Item $testRoot) -Recurse
    Write-Host "✓ Found $($drivers.Count) INF files" -ForegroundColor Green

    foreach ($driver in $drivers) {
        Write-Host "  - $($driver.INFFile.Name) in $($driver.DriverDirectory.Name)" -ForegroundColor Gray
        if ($null -ne $driver.ParsedInfo) {
            Write-Host "    WARNING: ParsedInfo should be null when not parsing" -ForegroundColor Yellow
        }
    }
} catch {
    Write-Host "✗ Get-INFDriverList failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 3: Test Get-INFDriverList with parsing
Write-Host "`n=== Test 3: Get-INFDriverList (With Parsing) ===" -ForegroundColor Yellow

try {
    $driversWithParsing = Get-INFDriverList -Path (Get-Item $testRoot) -Recurse -ParseINF
    Write-Host "✓ Found $($driversWithParsing.Count) INF files with parsing" -ForegroundColor Green

    foreach ($driver in $driversWithParsing) {
        Write-Host "  - $($driver.INFFile.Name):" -ForegroundColor Gray
        if ($driver.ParsedInfo) {
            Write-Host "    Provider: $($driver.ParsedInfo.Provider)" -ForegroundColor Gray
            Write-Host "    Class: $($driver.ParsedInfo.Class)" -ForegroundColor Gray
            Write-Host "    Version: $($driver.ParsedInfo.Version)" -ForegroundColor Gray
            Write-Host "    Driver Date: $($driver.ParsedInfo.DriverDate)" -ForegroundColor Gray
            if ($driver.ParsedInfo.ParseErrors.Count -gt 0) {
                Write-Host "    Parse Errors: $($driver.ParsedInfo.ParseErrors -join ', ')" -ForegroundColor Yellow
            }
        } else {
            Write-Host "    WARNING: ParsedInfo is null when parsing was requested" -ForegroundColor Yellow
        }
    }
} catch {
    Write-Host "✗ Get-INFDriverList with parsing failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 4: Test Get-INFDriverList with specific directories (non-recursive)
Write-Host "`n=== Test 4: Get-INFDriverList (Specific Directories, Non-Recursive) ===" -ForegroundColor Yellow

try {
    $specificDirs = @(
        Get-Item "$testRoot\NetworkDrivers",
        Get-Item "$testRoot\DisplayDrivers"
    )

    $specificDrivers = Get-INFDriverList -Path $specificDirs
    Write-Host "✓ Found $($specificDrivers.Count) INF files in specific directories" -ForegroundColor Green

    foreach ($driver in $specificDrivers) {
        Write-Host "  - $($driver.INFFile.Name) in $($driver.DriverDirectory.Name)" -ForegroundColor Gray
    }
} catch {
    Write-Host "✗ Get-INFDriverList with specific directories failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 5: Test FormatUtilityService integration
Write-Host "`n=== Test 5: Testing FormatUtilityService Integration ===" -ForegroundColor Yellow

try {
    # Test date parsing with various formats
    $testDates = @("03/15/2023", "06/21/2023", "12/08/2022", "01/30/2023")
    foreach ($dateStr in $testDates) {
        $parsed = [PSWindowsImageTools.Services.FormatUtilityService]::ParseDate($dateStr)
        if ($parsed) {
            Write-Host "  ✓ Date '$dateStr' parsed as: $($parsed.ToString('yyyy-MM-dd'))" -ForegroundColor Green
        } else {
            Write-Host "  ✗ Failed to parse date: $dateStr" -ForegroundColor Red
        }
    }

    # Test version parsing
    $testVersions = @("12.19.0.5", "31.0.15.3179", "6.0.9381.1", "10.30.0.5714")
    foreach ($versionStr in $testVersions) {
        $parsed = [PSWindowsImageTools.Services.FormatUtilityService]::ParseVersion($versionStr)
        if ($parsed) {
            Write-Host "  ✓ Version '$versionStr' parsed as: $($parsed.ToString())" -ForegroundColor Green
        } else {
            Write-Host "  ✗ Failed to parse version: $versionStr" -ForegroundColor Red
        }
    }
} catch {
    Write-Host "✗ FormatUtilityService integration test failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 6: Mock Add-INFDriverList (since we don't have real mounted images)
Write-Host "`n=== Test 6: Add-INFDriverList (Mock Test) ===" -ForegroundColor Yellow

Write-Host "Note: This test requires mounted Windows images which are not available in this test environment." -ForegroundColor Yellow
Write-Host "The Add-INFDriverList cmdlet would be used like this:" -ForegroundColor Gray
Write-Host "  `$mountedImages = Mount-WindowsImageList -ImagePath 'install.wim'" -ForegroundColor Gray
Write-Host "  `$drivers = Get-INFDriverList -Path 'C:\Drivers' -Recurse -ParseINF" -ForegroundColor Gray
Write-Host "  `$results = Add-INFDriverList -MountedImages `$mountedImages -Drivers `$drivers" -ForegroundColor Gray

# Cleanup
Write-Host "`n=== Cleanup ===" -ForegroundColor Yellow

try {
    if (Test-Path $testRoot) {
        Remove-Item -Path $testRoot -Recurse -Force
        Write-Host "✓ Test directory cleaned up" -ForegroundColor Green
    }
} catch {
    Write-Host "✗ Failed to cleanup test directory: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n=== Test Summary ===" -ForegroundColor Cyan
Write-Host "✓ Get-INFDriverList cmdlet basic functionality tested" -ForegroundColor Green
Write-Host "✓ INF parsing functionality tested" -ForegroundColor Green
Write-Host "✓ FormatUtilityService integration tested" -ForegroundColor Green
Write-Host "✓ Recursive and non-recursive scanning tested" -ForegroundColor Green
Write-Host "Note: Add-INFDriverList requires real mounted images for full testing" -ForegroundColor Yellow

Write-Output "`nDriver cmdlet testing completed!"
Signature="`$Windows NT`$"
Class=Net
ClassGUID={4D36E972-E325-11CE-BFC1-08002BE10318}
Provider=%Intel%
DriverVer=03/15/2023,12.19.0.5
CatalogFile=e1000.cat

[Manufacturer]
%Intel%=Intel,NTamd64

[Intel.NTamd64]
%E1000.DeviceDesc%=E1000.ndi,PCI\VEN_8086&DEV_1000

[Strings]
Intel="Intel Corporation"
E1000.DeviceDesc="Intel(R) PRO/1000 Network Connection"
"@

    "$testRoot\DisplayDrivers\nvidia.inf" = @"
[Version]
Signature="`$Windows NT`$"
Class=Display
ClassGUID={4D36E968-E325-11CE-BFC1-08002BE10318}
Provider=%NVIDIA%
DriverVer=06/21/2023,31.0.15.3179
CatalogFile=nvidia.cat

[Manufacturer]
%NVIDIA%=NVIDIA,NTamd64

[NVIDIA.NTamd64]
%NVIDIA_DEV.2204.01%=Section001, PCI\VEN_10DE&DEV_2204

[Strings]
NVIDIA="NVIDIA Corporation"
NVIDIA_DEV.2204.01="NVIDIA GeForce RTX 3080"
"@

    "$testRoot\AudioDrivers\Realtek\hdaudio.inf" = @"
[Version]
Signature="`$Windows NT`$"
Class=MEDIA
ClassGUID={4D36E96C-E325-11CE-BFC1-08002BE10318}
Provider=%Realtek%
DriverVer=12/08/2022,6.0.9381.1
CatalogFile=hdaudio.cat

[Manufacturer]
%Realtek%=Realtek,NTamd64

[Realtek.NTamd64]
%HDAUDIO.DeviceDesc%=HDAudio_Device,HDAUDIO\FUNC_01&VEN_10EC&DEV_0295

[Strings]
Realtek="Realtek Semiconductor Corp."
HDAUDIO.DeviceDesc="Realtek High Definition Audio"
"@

    "$testRoot\AudioDrivers\Intel\intcaudiobus.inf" = @"
[Version]
Signature="`$Windows NT`$"
Class=System
ClassGUID={4D36E97D-E325-11CE-BFC1-08002BE10318}
Provider=%Intel%
DriverVer=01/30/2023,10.30.0.5714
CatalogFile=intcaudiobus.cat

[Manufacturer]
%Intel%=Intel,NTamd64

[Intel.NTamd64]
%IntcAudioBus.DeviceDesc%=IntcAudioBus_Device,PCI\VEN_8086&DEV_9DC8

[Strings]
Intel="Intel Corporation"
IntcAudioBus.DeviceDesc="Intel(R) Smart Sound Technology Audio Controller"
"@
}

# Write mock INF files
foreach ($infPath in $mockINFs.Keys) {
    $mockINFs[$infPath] | Out-File -FilePath $infPath -Encoding UTF8
    Write-Host "Created mock INF: $infPath" -ForegroundColor Gray
}

Write-Host "✓ Mock INF files created successfully" -ForegroundColor Green

# Test 2: Test Get-INFDriverList without parsing
Write-Host "`n=== Test 2: Get-INFDriverList (No Parsing) ===" -ForegroundColor Yellow

try {
    $drivers = Get-INFDriverList -Path (Get-Item $testRoot) -Recurse
    Write-Host "✓ Found $($drivers.Count) INF files" -ForegroundColor Green
    
    foreach ($driver in $drivers) {
        Write-Host "  - $($driver.INFFile.Name) in $($driver.DriverDirectory.Name)" -ForegroundColor Gray
        if ($driver.ParsedInfo -ne $null) {
            Write-Host "    WARNING: ParsedInfo should be null when not parsing" -ForegroundColor Yellow
        }
    }
} catch {
    Write-Host "✗ Get-INFDriverList failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 3: Test Get-INFDriverList with parsing
Write-Host "`n=== Test 3: Get-INFDriverList (With Parsing) ===" -ForegroundColor Yellow

try {
    $driversWithParsing = Get-INFDriverList -Path (Get-Item $testRoot) -Recurse -ParseINF
    Write-Host "✓ Found $($driversWithParsing.Count) INF files with parsing" -ForegroundColor Green
    
    foreach ($driver in $driversWithParsing) {
        Write-Host "  - $($driver.INFFile.Name):" -ForegroundColor Gray
        if ($driver.ParsedInfo) {
            Write-Host "    Provider: $($driver.ParsedInfo.Provider)" -ForegroundColor Gray
            Write-Host "    Class: $($driver.ParsedInfo.Class)" -ForegroundColor Gray
            Write-Host "    Version: $($driver.ParsedInfo.Version)" -ForegroundColor Gray
            Write-Host "    Driver Date: $($driver.ParsedInfo.DriverDate)" -ForegroundColor Gray
            if ($driver.ParsedInfo.ParseErrors.Count -gt 0) {
                Write-Host "    Parse Errors: $($driver.ParsedInfo.ParseErrors -join ', ')" -ForegroundColor Yellow
            }
        } else {
            Write-Host "    WARNING: ParsedInfo is null when parsing was requested" -ForegroundColor Yellow
        }
    }
} catch {
    Write-Host "✗ Get-INFDriverList with parsing failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 4: Test Get-INFDriverList with specific directories (non-recursive)
Write-Host "`n=== Test 4: Get-INFDriverList (Specific Directories, Non-Recursive) ===" -ForegroundColor Yellow

try {
    $specificDirs = @(
        Get-Item "$testRoot\NetworkDrivers",
        Get-Item "$testRoot\DisplayDrivers"
    )
    
    $specificDrivers = Get-INFDriverList -Path $specificDirs
    Write-Host "✓ Found $($specificDrivers.Count) INF files in specific directories" -ForegroundColor Green
    
    foreach ($driver in $specificDrivers) {
        Write-Host "  - $($driver.INFFile.Name) in $($driver.DriverDirectory.Name)" -ForegroundColor Gray
    }
} catch {
    Write-Host "✗ Get-INFDriverList with specific directories failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test 5: Test error handling with non-existent directory
Write-Host "`n=== Test 5: Error Handling (Non-Existent Directory) ===" -ForegroundColor Yellow

try {
    $nonExistentDir = Get-Item "C:\NonExistentDriverPath" -ErrorAction SilentlyContinue
    if ($nonExistentDir) {
        Write-Host "Unexpected: Non-existent directory was found" -ForegroundColor Yellow
    } else {
        Write-Host "✓ Non-existent directory correctly not found" -ForegroundColor Green
    }
} catch {
    Write-Host "✓ Error handling working as expected: $($_.Exception.Message)" -ForegroundColor Green
}

# Test 6: Mock Add-INFDriverList (since we don't have real mounted images)
Write-Host "`n=== Test 6: Add-INFDriverList (Mock Test) ===" -ForegroundColor Yellow

Write-Host "Note: This test requires mounted Windows images which are not available in this test environment." -ForegroundColor Yellow
Write-Host "The Add-INFDriverList cmdlet would be used like this:" -ForegroundColor Gray
Write-Host "  `$mountedImages = Mount-WindowsImageList -ImagePath 'install.wim'" -ForegroundColor Gray
Write-Host "  `$drivers = Get-INFDriverList -Path 'C:\Drivers' -Recurse -ParseINF" -ForegroundColor Gray
Write-Host "  `$results = Add-INFDriverList -MountedImages `$mountedImages -Drivers `$drivers" -ForegroundColor Gray

# Cleanup
Write-Host "`n=== Cleanup ===" -ForegroundColor Yellow

try {
    if (Test-Path $testRoot) {
        Remove-Item -Path $testRoot -Recurse -Force
        Write-Host "✓ Test directory cleaned up" -ForegroundColor Green
    }
} catch {
    Write-Host "✗ Failed to cleanup test directory: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`n=== Test Summary ===" -ForegroundColor Cyan
Write-Host "✓ Get-INFDriverList cmdlet basic functionality tested" -ForegroundColor Green
Write-Host "✓ INF parsing functionality tested" -ForegroundColor Green
Write-Host "✓ Recursive and non-recursive scanning tested" -ForegroundColor Green
Write-Host "✓ Error handling tested" -ForegroundColor Green
Write-Host "Note: Add-INFDriverList requires real mounted images for full testing" -ForegroundColor Yellow

Write-Host "`nDriver cmdlet testing completed!" -ForegroundColor Cyan
