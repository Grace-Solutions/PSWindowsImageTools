# Test script for the new native Registry service
param(
    [switch]$Verbose
)

if ($Verbose) { $VerbosePreference = 'Continue' }

try {
    Write-Output "Testing native Registry service..."
    
    # Import the module
    $ModulePath = "C:\Users\augment\Documents\PSWindowsImageTools\Module\PSWindowsImageTools\PSWindowsImageTools.psd1"
    Write-Output "Importing module from: $ModulePath"
    
    if (Test-Path $ModulePath) {
        Import-Module $ModulePath -Force -Verbose:$false
        Write-Output "Module imported successfully"
    } else {
        Write-Error "Module not found at: $ModulePath"
        exit 1
    }
    
    # Test ADK detection
    Write-Output "`nTesting ADK detection..."
    $adkInstallations = Get-ADKInstallationPath -Verbose:$Verbose
    
    if ($adkInstallations) {
        Write-Output "Found $($adkInstallations.Count) ADK installation(s):"
        foreach ($adk in $adkInstallations) {
            Write-Output "  - $($adk.DisplayName) at $($adk.InstallationPath)"
        }
    } else {
        Write-Output "No ADK installations found"
    }
    
    Write-Output "`nRegistry service test completed successfully!"
    
} catch {
    Write-Error "Test failed: $($_.Exception.Message)"
    Write-Error "Stack trace: $($_.ScriptStackTrace)"
    exit 1
}
