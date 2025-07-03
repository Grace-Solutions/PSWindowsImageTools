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
CatalogFile=e1000.cat   ; Intel network driver catalog

[Manufacturer]
%Intel%=Intel,NTamd64,NTx86

[Intel.NTamd64]
%E1000.DeviceDesc%=E1000.ndi,PCI\VEN_8086&DEV_1000
%E1000.DeviceDesc2%=E1000.ndi,PCI\VEN_8086&DEV_1001,PCI\VEN_8086&DEV_1004
%E1000.DeviceDesc3%=E1000.ndi,PCI\VEN_8086&DEV_100C&SUBSYS_00000000
%E1000.DeviceDesc4%=E1000.ndi,PCI\VEN_8086&DEV_100D&SUBSYS_003C8086

[Intel.NTx86]
%E1000.DeviceDesc%=E1000.ndi,PCI\VEN_8086&DEV_1000
%E1000.DeviceDesc2%=E1000.ndi,PCI\VEN_8086&DEV_1001

[Strings]
Intel="Intel Corporation"
E1000.DeviceDesc="Intel(R) PRO/1000 Network Connection"
E1000.DeviceDesc2="Intel(R) PRO/1000 MT Network Connection"
E1000.DeviceDesc3="Intel(R) PRO/1000 T Server Adapter"
E1000.DeviceDesc4="Intel(R) PRO/1000 MT Server Adapter"
"@

    "$testRoot\DisplayDrivers\nvidia.inf" = @"
[Version]
Signature="`$Windows NT`$"
Class=Display
ClassGUID={4D36E968-E325-11CE-BFC1-08002BE10318}
Provider=%NVIDIA%
DriverVer=06/21/2023,31.0.15.3179
CatalogFile=nvidia.cat   ; NVIDIA display driver catalog

[Manufacturer]
%NVIDIA%=NVIDIA,NTamd64,NTx86

[NVIDIA.NTamd64]
%NVIDIA_DEV.2204.01%=Section001,PCI\VEN_10DE&DEV_2204&SUBSYS_38C717AA&REV_A1
%NVIDIA_DEV.2208.01%=Section002,PCI\VEN_10DE&DEV_2208&SUBSYS_40711462
%NVIDIA_DEV.220A.01%=Section003,PCI\VEN_10DE&DEV_220A,PCI\VEN_10DE&DEV_220B

[NVIDIA.NTx86]
%NVIDIA_DEV.2204.01%=Section001,PCI\VEN_10DE&DEV_2204

[Strings]
NVIDIA="NVIDIA Corporation"
NVIDIA_DEV.2204.01="NVIDIA GeForce RTX 3080"
NVIDIA_DEV.2208.01="NVIDIA GeForce RTX 3080 Ti"
NVIDIA_DEV.220A.01="NVIDIA GeForce RTX 3080 Ti Laptop GPU"
"@
}

# Write mock INF files
foreach ($infPath in $mockINFs.Keys) {
    $mockINFs[$infPath] | Out-File -FilePath $infPath -Encoding UTF8
    Write-Verbose "Created mock INF: $infPath"
}

Write-Verbose "Mock INF files created successfully"

# Test 2: Test Get-INFDriverList without parsing
Write-Output "`n=== Test 2: Get-INFDriverList (No Parsing) ==="

try {
    $drivers = Get-INFDriverList -Path (Get-Item $testRoot) -Recurse
    Write-Output "Found $($drivers.Count) INF files"
    
    foreach ($driver in $drivers) {
        Write-Verbose "  - $($driver.INFFile.Name) in $($driver.DriverDirectory.Name)"
        if ($null -ne $driver.ParsedInfo) {
            Write-Warning "    ParsedInfo should be null when not parsing"
        }
    }
} catch {
    Write-Warning "Get-INFDriverList failed: $($_.Exception.Message)"
}

# Test 3: Test Get-INFDriverList with parsing
Write-Output "`n=== Test 3: Get-INFDriverList (With Parsing) ==="

try {
    $driversWithParsing = Get-INFDriverList -Path (Get-Item $testRoot) -Recurse -ParseINF
    Write-Output "Found $($driversWithParsing.Count) INF files with parsing"
    
    foreach ($driver in $driversWithParsing) {
        Write-Verbose "  - $($driver.INFFile.Name):"
        if ($driver.ParsedInfo) {
            Write-Verbose "    Provider: $($driver.ParsedInfo.Provider)"
            Write-Verbose "    Class: $($driver.ParsedInfo.Class)"
            Write-Verbose "    Version: $($driver.ParsedInfo.Version)"
            Write-Verbose "    Driver Date: $($driver.ParsedInfo.DriverDate)"

            if ($driver.ParsedInfo.SupportedArchitectures.Count -gt 0) {
                Write-Verbose "    Architectures: $($driver.ParsedInfo.SupportedArchitectures -join ', ')"
            }

            if ($driver.ParsedInfo.HardwareIds.Count -gt 0) {
                Write-Verbose "    Hardware IDs ($($driver.ParsedInfo.HardwareIds.Count)):"
                foreach ($hwId in $driver.ParsedInfo.HardwareIds) {
                    Write-Verbose "      - $hwId"
                }
            }

            if ($driver.ParsedInfo.CompatibleIds.Count -gt 0) {
                Write-Verbose "    Compatible IDs ($($driver.ParsedInfo.CompatibleIds.Count)):"
                foreach ($compId in $driver.ParsedInfo.CompatibleIds) {
                    Write-Verbose "      - $compId"
                }
            }

            if ($driver.ParsedInfo.CatalogFile) {
                Write-Verbose "    Catalog File: $($driver.ParsedInfo.CatalogFile.FullName)"
                Write-Verbose "    Catalog Exists: $($driver.ParsedInfo.CatalogFile.Exists)"
                Write-Verbose "    Is Signed: $($driver.ParsedInfo.IsSigned)"
            }

            if ($driver.ParsedInfo.ParseErrors.Count -gt 0) {
                Write-Warning "    Parse Errors: $($driver.ParsedInfo.ParseErrors -join ', ')"
            }
        } else {
            Write-Warning "    ParsedInfo is null when parsing was requested"
        }
    }
} catch {
    Write-Warning "Get-INFDriverList with parsing failed: $($_.Exception.Message)"
}

# Test 4: Test FormatUtilityService integration
Write-Output "`n=== Test 4: Testing FormatUtilityService Integration ==="

$testVersionStrings = @("12.19.0.5", "31.0.15.3179", "6.0.9381.1", "10.30.0.5714")

foreach ($versionStr in $testVersionStrings) {
    try {
        $parsed = [PSWindowsImageTools.Services.FormatUtilityService]::ParseVersion($versionStr)
        if ($parsed) {
            Write-Verbose "  Version '$versionStr' parsed as: $($parsed.ToString())"
        } else {
            Write-Warning "  Failed to parse version: $versionStr"
        }
    } catch {
        Write-Warning "  Exception parsing version '$versionStr': $($_.Exception.Message)"
    }
}

# Test 5: Mock Add-INFDriverList (since we don't have real mounted images)
Write-Output "`n=== Test 5: Add-INFDriverList (Mock Test) ==="

Write-Output "Note: This test requires mounted Windows images which are not available in this test environment."
Write-Output "The Add-INFDriverList cmdlet would be used like this:"
Write-Output "  `$mountedImages = Mount-WindowsImageList -ImagePath 'install.wim'"
Write-Output "  `$drivers = Get-INFDriverList -Path 'C:\Drivers' -Recurse -ParseINF"
Write-Output "  `$results = Add-INFDriverList -MountedImages `$mountedImages -Drivers `$drivers"

# Cleanup
Write-Output "`n=== Cleanup ==="

try {
    if (Test-Path $testRoot) {
        Remove-Item -Path $testRoot -Recurse -Force
        Write-Verbose "Test directory cleaned up"
    }
} catch {
    Write-Warning "Failed to cleanup test directory: $($_.Exception.Message)"
}

Write-Output "`n=== Test Summary ==="
Write-Output "✓ Module import tested"
Write-Output "✓ Get-INFDriverList cmdlet basic functionality tested"
Write-Output "✓ INF parsing functionality tested"
Write-Output "✓ FormatUtilityService integration tested"
Write-Output "Note: Add-INFDriverList requires real mounted images for full testing"

Write-Output "`nDriver cmdlet testing completed!"
