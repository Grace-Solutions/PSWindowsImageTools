# Debug script to test hardware ID parsing with a simple INF file
[CmdletBinding()]
param()

# Create a test directory
$testDir = "C:\Temp\HardwareIDTest"
if (!(Test-Path $testDir)) {
    New-Item -Path $testDir -ItemType Directory -Force | Out-Null
}

# Create a simple test INF file based on the real 3ware.inf structure
$testINF = @"
[Version]
Signature="`$Windows NT`$"
Class=SCSIAdapter
ClassGUID={4D36E97B-E325-11CE-BFC1-08002BE10318}
Provider=%LSI%
DriverVer=04/11/2013, 5.01.00.051
CatalogFile=lsi3ware.cat   ; for WHQL certified drivers

[Manufacturer]
%LSI% = LSI, NTx86, NTamd64

[LSI.NTx86]
%DEVICE_DESCRIPTION_LSI% = Install, PCI\VEN_13C1&DEV_1010&SUBSYS_000113C1 ;LSI RAID Controller
%DEVICE_DESCRIPTION_LSI2% = Install, PCI\VEN_13C1&DEV_1010, PCI\VEN_13C1&DEV_1011 ;With compatible ID

[LSI.NTamd64]
%DEVICE_DESCRIPTION_LSI% = Install, PCI\VEN_13C1&DEV_1010&SUBSYS_000113C1 ;LSI RAID Controller
%DEVICE_DESCRIPTION_LSI2% = Install, PCI\VEN_13C1&DEV_1010, PCI\VEN_13C1&DEV_1011 ;With compatible ID

[Strings]
LSI="LSI Corporation"
DEVICE_DESCRIPTION_LSI="LSI 3ware 9750 RAID Controller"
DEVICE_DESCRIPTION_LSI2="LSI 3ware Generic RAID Controller"
"@

$infPath = "$testDir\test.inf"
$testINF | Out-File -FilePath $infPath -Encoding UTF8

Write-Output "Created test INF file: $infPath"
Write-Output "INF content:"
Get-Content $infPath | ForEach-Object { Write-Output "  $_" }

# Import module and test
Import-Module ".\Module\PSWindowsImageTools\PSWindowsImageTools.psd1" -Force

Write-Output "`nTesting hardware ID parsing..."
$drivers = Get-INFDriverList -Path $testDir -ParseINF

foreach ($driver in $drivers) {
    Write-Output "`nDriver: $($driver.INFFile.Name)"
    if ($driver.ParsedInfo) {
        Write-Output "  Provider: $($driver.ParsedInfo.Provider)"
        Write-Output "  Class: $($driver.ParsedInfo.Class)"
        Write-Output "  Driver Name: $($driver.ParsedInfo.DriverName)"
        Write-Output "  Architectures: $($driver.ParsedInfo.SupportedArchitectures.Count) - $($driver.ParsedInfo.SupportedArchitectures -join ', ')"
        Write-Output "  Hardware IDs: $($driver.ParsedInfo.HardwareIds.Count)"
        
        if ($driver.ParsedInfo.HardwareIds.Count -gt 0) {
            $driver.ParsedInfo.HardwareIds | ForEach-Object {
                Write-Output "    - $_"
            }
        } else {
            Write-Output "    (No hardware IDs found)"
        }
        
        if ($driver.ParsedInfo.CompatibleIds.Count -gt 0) {
            Write-Output "  Compatible IDs: $($driver.ParsedInfo.CompatibleIds.Count)"
            $driver.ParsedInfo.CompatibleIds | ForEach-Object {
                Write-Output "    - $_"
            }
        } else {
            Write-Output "  Compatible IDs: None"
        }

        if ($driver.ParsedInfo.CatalogFile) {
            Write-Output "  Catalog File: $($driver.ParsedInfo.CatalogFile.FullName)"
            Write-Output "  Catalog Exists: $($driver.ParsedInfo.CatalogFile.Exists)"
            Write-Output "  Is Signed: $($driver.ParsedInfo.IsSigned)"
        } else {
            Write-Output "  Catalog File: None specified"
        }

        if ($driver.ParsedInfo.ParseErrors.Count -gt 0) {
            Write-Output "  Parse Errors:"
            $driver.ParsedInfo.ParseErrors | ForEach-Object {
                Write-Output "    - $_"
            }
        }
    } else {
        Write-Output "  No parsed info available"
    }
}

# Cleanup
Remove-Item -Path $testDir -Recurse -Force -ErrorAction SilentlyContinue

Write-Output "`nDebug test completed!"
