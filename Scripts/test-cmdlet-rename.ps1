# Test script to verify cmdlet rename
[CmdletBinding()]
param()

Write-Output "=== Testing Cmdlet Rename ==="

try {
    Import-Module ".\Module\PSWindowsImageTools\PSWindowsImageTools.psd1" -Force
    Write-Output "Module imported successfully"

    # Check for registry cmdlets
    $registryCmdlets = Get-Command -Module PSWindowsImageTools | Where-Object { $_.Name -like '*Registry*' }
    
    Write-Output "`nRegistry cmdlets found:"
    foreach ($cmdlet in $registryCmdlets) {
        Write-Output "  - $($cmdlet.Name)"
    }

    # Verify the old cmdlet name is gone
    $oldCmdlet = Get-Command -Module PSWindowsImageTools | Where-Object { $_.Name -eq 'Invoke-RegistryOperationList' }
    if ($oldCmdlet) {
        Write-Warning "Old cmdlet 'Invoke-RegistryOperationList' still exists!"
    } else {
        Write-Output "`nPASS: Old cmdlet 'Invoke-RegistryOperationList' successfully removed"
    }

    # Verify the new cmdlet name exists
    $newCmdlet = Get-Command -Module PSWindowsImageTools | Where-Object { $_.Name -eq 'Write-RegistryOperationList' }
    if ($newCmdlet) {
        Write-Output "PASS: New cmdlet 'Write-RegistryOperationList' successfully added"
        Write-Output "  Verb: $($newCmdlet.Verb)"
        Write-Output "  Noun: $($newCmdlet.Noun)"
    } else {
        Write-Warning "New cmdlet 'Write-RegistryOperationList' not found!"
    }

    # Test that both cmdlets work
    Write-Output "`n=== Testing Cmdlet Functionality ==="
    
    # Test Get-RegistryOperationList
    try {
        $help = Get-Help Get-RegistryOperationList -ErrorAction Stop
        Write-Output "PASS: Get-RegistryOperationList help available"
    } catch {
        Write-Warning "FAIL: Get-RegistryOperationList help not available: $($_.Exception.Message)"
    }

    # Test Write-RegistryOperationList
    try {
        $help = Get-Help Write-RegistryOperationList -ErrorAction Stop
        Write-Output "PASS: Write-RegistryOperationList help available"
    } catch {
        Write-Warning "FAIL: Write-RegistryOperationList help not available: $($_.Exception.Message)"
    }

} catch {
    Write-Error "Error during testing: $($_.Exception.Message)"
}

Write-Output "`n=== Cmdlet Rename Test Completed ==="
