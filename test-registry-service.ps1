# Test script for RegistryPackageService
param(
    [switch]$Verbose
)

if ($Verbose) { $VerbosePreference = 'Continue' }

try {
    Write-Output "Testing RegistryPackageService with EnterpriseS image..."
    
    # Import the module
    $ModulePath = "./Module/PSWindowsImageTools/PSWindowsImageTools.psd1"
    Write-Output "Importing module from: $ModulePath"
    
    if (Test-Path $ModulePath) {
        Import-Module $ModulePath -Force -Verbose:$false
        Write-Output "Module imported successfully"
    } else {
        Write-Error "Module not found at: $ModulePath"
        exit 1
    }
    
    # Test the image
    $ImagePath = "C:\Users\gsadmin\Downloads\windows_10_enterprise_ltsc_2021_x64\sources\install.wim"
    Write-Output "Testing image: $ImagePath"
    
    if (-not (Test-Path $ImagePath)) {
        Write-Error "Image file not found: $ImagePath"
        exit 1
    }
    
    # Get the EnterpriseS image with advanced info
    Write-Output "Getting EnterpriseS image with advanced registry info..."
    $result = Get-WindowsImageList -ImagePath $ImagePath -IncludeAdvanced -Verbose | Where-Object { $_.Edition -eq 'EnterpriseS' }
    
    if (-not $result) {
        Write-Error "No EnterpriseS edition found in the image"
        exit 1
    }
    
    Write-Output "`nImage Information:"
    Write-Output "Name: $($result.Name)"
    Write-Output "Edition: $($result.Edition)"
    Write-Output "Version: $($result.Version)"
    Write-Output "FullVersion: $($result.FullVersion)"
    
    Write-Output "`nRegistry Info Analysis:"
    if ($result.AdvancedInfo -and $result.AdvancedInfo.RegistryInfo) {
        $regInfo = $result.AdvancedInfo.RegistryInfo
        
        Write-Output "Total registry keys: $($regInfo.Keys.Count)"
        
        # Check VersionInfo keys
        $versionKeys = $regInfo.Keys | Where-Object { $_ -like 'VersionInfo.*' }
        Write-Output "VersionInfo keys: $($versionKeys.Count)"
        if ($versionKeys.Count -gt 0) {
            Write-Output "  Sample VersionInfo keys: $($versionKeys | Select-Object -First 5 | Join-String -Separator ', ')"
        }
        
        # Check Software
        if ($regInfo.ContainsKey('Software')) {
            $software = $regInfo['Software']
            Write-Output "Software entries: $($software.Count)"
            if ($software.Count -gt 0) {
                Write-Output "  Sample software:"
                $software | Select-Object -First 3 | ForEach-Object {
                    Write-Output "    - $($_.DisplayName) ($($_.DisplayVersion)) by $($_.Publisher)"
                    Write-Output "      Registry: $($_.RegistryKeyPath)"
                }
            }
        } else {
            Write-Output "No Software key found in registry info"
        }
        
        # Check WUConfig keys
        $wuKeys = $regInfo.Keys | Where-Object { $_ -like 'WUConfig.*' }
        Write-Output "WUConfig keys: $($wuKeys.Count)"
        if ($wuKeys.Count -gt 0) {
            Write-Output "  WUConfig keys: $($wuKeys | Join-String -Separator ', ')"
        }
    } else {
        Write-Output "No advanced registry info found"
    }
    
    Write-Output "`nTest completed successfully!"
    
} catch {
    Write-Error "Test failed: $($_.Exception.Message)"
    Write-Error $_.ScriptStackTrace
    exit 1
}
