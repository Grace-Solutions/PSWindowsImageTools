# Test script to find and test catalog file parsing
[CmdletBinding()]
param()

Import-Module ".\Module\PSWindowsImageTools\PSWindowsImageTools.psd1" -Force

Write-Output "Testing catalog file parsing..."

# Find INF files that have catalog files specified
$infFiles = Get-ChildItem "C:\Windows\INF\*.inf" | ForEach-Object {
    try {
        $content = Get-Content $_.FullName -ErrorAction SilentlyContinue
        if ($content -match "CatalogFile\s*=\s*\S+") {
            return $_
        }
    } catch {
        # Ignore files we can't read
    }
} | Select-Object -First 3

Write-Output "Found $($infFiles.Count) INF files with catalog files specified"

foreach ($infFile in $infFiles) {
    Write-Output "`n=== Testing: $($infFile.Name) ==="
    
    try {
        $drivers = Get-INFDriverList -Path $infFile.Directory.FullName -ParseINF | Where-Object { $_.INFFile.Name -eq $infFile.Name }
        
        if ($drivers) {
            $driver = $drivers[0]
            Write-Output "Provider: $($driver.ParsedInfo.Provider)"
            Write-Output "Class: $($driver.ParsedInfo.Class)"
            
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
        } else {
            Write-Output "Failed to parse driver"
        }
    } catch {
        Write-Output "Error testing $($infFile.Name): $($_.Exception.Message)"
    }
}

Write-Output "`nCatalog file parsing test completed!"
