# Test script to check hardware ID parsing
[CmdletBinding()]
param()

Import-Module ".\Module\PSWindowsImageTools\PSWindowsImageTools.psd1" -Force

Write-Output "Testing hardware ID parsing..."

# Test with Windows INF directory
$drivers = Get-INFDriverList -Path "C:\Windows\INF" -ParseINF | Select-Object -First 3

foreach ($driver in $drivers) {
    Write-Output "`nDriver: $($driver.INFFile.Name)"
    if ($driver.ParsedInfo) {
        Write-Output "  Provider: $($driver.ParsedInfo.Provider)"
        Write-Output "  Class: $($driver.ParsedInfo.Class)"
        Write-Output "  Architectures: $($driver.ParsedInfo.SupportedArchitectures.Count) - $($driver.ParsedInfo.SupportedArchitectures -join ', ')"
        Write-Output "  Hardware IDs: $($driver.ParsedInfo.HardwareIds.Count)"
        
        if ($driver.ParsedInfo.HardwareIds.Count -gt 0) {
            $driver.ParsedInfo.HardwareIds | Select-Object -First 5 | ForEach-Object {
                Write-Output "    - $_"
            }
            if ($driver.ParsedInfo.HardwareIds.Count -gt 5) {
                Write-Output "    ... and $($driver.ParsedInfo.HardwareIds.Count - 5) more"
            }
        }
        
        if ($driver.ParsedInfo.CompatibleIds.Count -gt 0) {
            Write-Output "  Compatible IDs: $($driver.ParsedInfo.CompatibleIds.Count)"
            $driver.ParsedInfo.CompatibleIds | Select-Object -First 3 | ForEach-Object {
                Write-Output "    - $_"
            }
        }
    }
}

Write-Output "`nHardware ID parsing test completed!"
