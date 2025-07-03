# Test script to verify hardware ID cleaning and compatible ID parsing
[CmdletBinding()]
param()

Import-Module ".\Module\PSWindowsImageTools\PSWindowsImageTools.psd1" -Force

Write-Output "Testing hardware ID cleaning and compatible ID parsing..."

# Test with a real Windows driver that has comments
$drivers = Get-INFDriverList -Path "C:\Windows\INF" -ParseINF | Where-Object { $_.ParsedInfo.HardwareIds.Count -gt 0 } | Select-Object -First 3

foreach ($driver in $drivers) {
    Write-Output "`n=== Driver: $($driver.INFFile.Name) ==="
    Write-Output "Provider: $($driver.ParsedInfo.Provider)"
    Write-Output "Class: $($driver.ParsedInfo.Class)"
    Write-Output "Architectures: $($driver.ParsedInfo.SupportedArchitectures -join ', ')"

    if ($driver.ParsedInfo.CatalogFile) {
        Write-Output "Catalog File: $($driver.ParsedInfo.CatalogFile.FullName)"
        Write-Output "Catalog Exists: $($driver.ParsedInfo.CatalogFile.Exists)"
        Write-Output "Is Signed: $($driver.ParsedInfo.IsSigned)"
        if ($driver.ParsedInfo.CatalogFile.Exists) {
            Write-Output "Catalog Size: $($driver.ParsedInfo.CatalogFile.Length) bytes"
            Write-Output "Catalog Modified: $($driver.ParsedInfo.CatalogFile.LastWriteTime)"
        }
    } else {
        Write-Output "Catalog File: None specified"
    }
    
    Write-Output "`nHardware IDs ($($driver.ParsedInfo.HardwareIds.Count)):"
    $driver.ParsedInfo.HardwareIds | Select-Object -First 5 | ForEach-Object {
        Write-Output "  - $_"
    }
    if ($driver.ParsedInfo.HardwareIds.Count -gt 5) {
        Write-Output "  ... and $($driver.ParsedInfo.HardwareIds.Count - 5) more"
    }
    
    if ($driver.ParsedInfo.CompatibleIds.Count -gt 0) {
        Write-Output "`nCompatible IDs ($($driver.ParsedInfo.CompatibleIds.Count)):"
        $driver.ParsedInfo.CompatibleIds | Select-Object -First 3 | ForEach-Object {
            Write-Output "  - $_"
        }
        if ($driver.ParsedInfo.CompatibleIds.Count -gt 3) {
            Write-Output "  ... and $($driver.ParsedInfo.CompatibleIds.Count - 3) more"
        }
    } else {
        Write-Output "`nCompatible IDs: None"
    }
}

Write-Output "`nHardware ID cleaning test completed!"
