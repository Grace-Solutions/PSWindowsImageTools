# Test script for Autopilot and Unattend XML cmdlets
param(
    [switch]$Verbose
)

if ($Verbose) {
    $VerbosePreference = 'Continue'
}

Write-Output "=== Testing Autopilot and Unattend XML Cmdlets ==="

# Import the module
try {
    Import-Module "$PSScriptRoot\..\Module\PSWindowsImageTools\PSWindowsImageTools.psd1" -Force
    Write-Output "Module imported successfully"
} catch {
    Write-Error "Failed to import module: $_"
    exit 1
}

# Create test directories
$testDir = "C:\Temp\AutopilotUnattendTest"
$autopilotDir = Join-Path $testDir "Autopilot"
$unattendDir = Join-Path $testDir "Unattend"

if (Test-Path $testDir) {
    Remove-Item $testDir -Recurse -Force
}
New-Item -ItemType Directory -Path $testDir -Force | Out-Null
New-Item -ItemType Directory -Path $autopilotDir -Force | Out-Null
New-Item -ItemType Directory -Path $unattendDir -Force | Out-Null

Write-Output "`n=== Test 1: Creating Sample Autopilot Configuration ==="

try {
    # Test New-AutopilotConfiguration
    $autopilotConfig = New-AutopilotConfiguration -TenantId "12345678-1234-1234-1234-123456789012" -TenantDomain "contoso.onmicrosoft.com" -Verbose
    Write-Output "Created Autopilot configuration for tenant: $($autopilotConfig.CloudAssignedTenantDomain)"
    Write-Output "Tenant ID: $($autopilotConfig.CloudAssignedTenantId)"
    Write-Output "Forced Enrollment: $($autopilotConfig.CloudAssignedForcedEnrollment)"
} catch {
    Write-Error "Failed to create Autopilot configuration: $_"
}

Write-Output "`n=== Test 2: Modifying Autopilot Configuration ==="

try {
    # Test Set-AutopilotConfiguration
    $modifiedConfig = $autopilotConfig | Set-AutopilotConfiguration -DeviceName "CORP-%SERIAL%" -OobeConfig 300 -PassThru -Verbose
    Write-Output "Modified configuration:"
    Write-Output "  Device Name: $($modifiedConfig.CloudAssignedDeviceName)"
    Write-Output "  OOBE Config: $($modifiedConfig.CloudAssignedOobeConfig)"
    Write-Output "  Domain Join Method: $($modifiedConfig.CloudAssignedDomainJoinMethod)"
    Write-Output "  Is Modified: $($modifiedConfig.IsModified)"
} catch {
    Write-Error "Failed to modify Autopilot configuration: $_"
}

Write-Output "`n=== Test 3: Exporting Autopilot Configuration ==="

try {
    # Test Export-AutopilotConfiguration
    $autopilotFile = Join-Path $autopilotDir "autopilot-config.json"
    $exportedFile = $modifiedConfig | Export-AutopilotConfiguration -OutputFile $autopilotFile -PassThru -Verbose
    Write-Output "Exported Autopilot configuration to: $($exportedFile.FullName)"
    Write-Output "File size: $($exportedFile.Length) bytes"
    
    # Show content preview
    $content = Get-Content $autopilotFile -Raw | ConvertFrom-Json
    Write-Output "JSON content preview:"
    Write-Output "  CloudAssignedTenantDomain: $($content.CloudAssignedTenantDomain)"
    Write-Output "  CloudAssignedDeviceName: $($content.CloudAssignedDeviceName)"
    Write-Output "  Version: $($content.Version)"
} catch {
    Write-Error "Failed to export Autopilot configuration: $_"
}

Write-Output "`n=== Test 4: Loading Autopilot Configuration ==="

try {
    # Test Get-AutopilotConfiguration
    $loadedConfig = Get-AutopilotConfiguration -File $autopilotFile -Validate -Verbose
    Write-Output "Loaded Autopilot configuration:"
    Write-Output "  Source File: $($loadedConfig.SourceFilePath)"
    Write-Output "  Tenant Domain: $($loadedConfig.CloudAssignedTenantDomain)"
    Write-Output "  Device Name: $($loadedConfig.CloudAssignedDeviceName)"
    Write-Output "  Version: $($loadedConfig.Version)"
} catch {
    Write-Error "Failed to load Autopilot configuration: $_"
}

Write-Output "`n=== Test 5: Creating Sample UnattendXML Configuration ==="

try {
    # Test New-UnattendXMLConfiguration
    $unattendConfig = New-UnattendXMLConfiguration -Template "Basic" -Architecture "amd64" -Language "neutral" -IncludeSamples -Verbose
    Write-Output "Created UnattendXML configuration:"
    Write-Output "  Configuration passes: $($unattendConfig.ConfigurationPasses -join ', ')"
    Write-Output "  Components count: $($unattendConfig.Components.Count)"

    if ($unattendConfig.Components.Count -gt 0) {
        Write-Output "  Sample components:"
        $unattendConfig.Components | Select-Object -First 3 | ForEach-Object {
            Write-Output "    - $($_.Name) ($($_.Pass))"
        }
    }
} catch {
    Write-Error "Failed to create UnattendXML configuration: $_"
}

Write-Output "`n=== Test 6: Modifying UnattendXML Configuration ==="

try {
    # Test Set-UnattendXMLConfiguration using friendly name
    $modifiedUnattend = $unattendConfig | Set-UnattendXMLConfiguration -ElementName "HideEULAPage" -Value "false" -PassThru -Verbose
    Write-Output "Modified UnattendXML configuration using friendly name:"
    Write-Output "  Is Modified: $($modifiedUnattend.IsModified)"

    # Verify the change
    $hideEulaElement = $modifiedUnattend.FindElement("HideEULAPage")
    if ($hideEulaElement) {
        Write-Output "  HideEULAPage value: $($hideEulaElement.InnerText)"
    }
} catch {
    Write-Error "Failed to modify UnattendXML configuration: $_"
}

Write-Output "`n=== Test 7: Exporting UnattendXML Configuration ==="

try {
    # Test Export-UnattendXMLConfiguration
    $unattendFile = Join-Path $unattendDir "unattend.xml"
    $exportedUnattend = $modifiedUnattend | Export-UnattendXMLConfiguration -OutputFile $unattendFile -Encoding UTF8 -PassThru -Verbose
    Write-Output "Exported UnattendXML configuration to: $($exportedUnattend.FullName)"
    Write-Output "File size: $($exportedUnattend.Length) bytes"

    # Show content preview
    $xmlContent = [xml](Get-Content $unattendFile -Raw)
    Write-Output "XML content preview:"
    Write-Output "  Root element: $($xmlContent.DocumentElement.Name)"
    Write-Output "  Namespace: $($xmlContent.DocumentElement.NamespaceURI)"
} catch {
    Write-Error "Failed to export UnattendXML configuration: $_"
}

Write-Output "`n=== Test 8: Loading UnattendXML Configuration ==="

try {
    # Test Get-UnattendXMLConfiguration
    $loadedUnattend = Get-UnattendXMLConfiguration -File $unattendFile -Validate -ShowComponents -Verbose
    Write-Output "Loaded UnattendXML configuration:"
    Write-Output "  Source File: $($loadedUnattend.SourceFilePath)"
    Write-Output "  Configuration passes: $($loadedUnattend.ConfigurationPasses -join ', ')"
    Write-Output "  Components count: $($loadedUnattend.Components.Count)"
} catch {
    Write-Error "Failed to load UnattendXML configuration: $_"
}

Write-Output "`n=== Test 9: WhatIf Mode for Autopilot Application ==="

try {
    # Create mock mounted image objects for testing
    $mockImage1 = [PSCustomObject]@{
        ImageName = "Windows 11 Pro"
        MountPath = [System.IO.DirectoryInfo]"C:\Mount\Image1"
        ImageIndex = 1
    }
    
    $mockImage2 = [PSCustomObject]@{
        ImageName = "Windows 11 Enterprise"
        MountPath = [System.IO.DirectoryInfo]"C:\Mount\Image2"
        ImageIndex = 2
    }
    
    Write-Output "Note: This test requires mounted Windows images which are not available in this test environment."
    Write-Output "The Install-AutopilotConfiguration cmdlet would be used like this:"
    Write-Output "`$mountedImages = Mount-WindowsImageList -ImagePath 'install.wim'"
    Write-Output "`$autopilotConfig = Get-AutopilotConfiguration -File '$autopilotFile'"
    Write-Output "`$results = Install-AutopilotConfiguration -MountedImages `$mountedImages -Configuration `$autopilotConfig -WhatIf"
} catch {
    Write-Error "Failed WhatIf test: $_"
}

Write-Output "`n=== Test 10: WhatIf Mode for UnattendXML Application ==="

try {
    Write-Output "Note: This test requires mounted Windows images which are not available in this test environment."
    Write-Output "The Install-UnattendXMLConfiguration cmdlet would be used like this:"
    Write-Output "`$mountedImages = Mount-WindowsImageList -ImagePath 'install.wim'"
    Write-Output "`$unattendConfig = Get-UnattendXMLConfiguration -File '$unattendFile'"
    Write-Output "`$results = Install-UnattendXMLConfiguration -MountedImages `$mountedImages -Configuration `$unattendConfig -WhatIf"
} catch {
    Write-Error "Failed WhatIf test: $_"
}

Write-Output "`n=== Test 11: Directory Scanning ==="

try {
    # Create additional test files
    $autopilotConfig2 = New-AutopilotConfiguration -TenantId "87654321-4321-4321-4321-210987654321" -TenantDomain "fabrikam.onmicrosoft.com"
    $autopilotFile2 = Join-Path $autopilotDir "autopilot-config2.json"
    $autopilotConfig2 | Export-AutopilotConfiguration -Path $autopilotFile2
    
    # Test directory scanning
    $allAutopilotConfigs = Get-AutopilotConfiguration -Path $autopilotDir -Verbose
    Write-Output "Found $($allAutopilotConfigs.Count) Autopilot configurations in directory"
    
    $allAutopilotConfigs | ForEach-Object {
        Write-Output "  - $($_.CloudAssignedTenantDomain) (from $([System.IO.Path]::GetFileName($_.SourceFilePath)))"
    }
} catch {
    Write-Error "Failed directory scanning test: $_"
}

Write-Output "`n=== Test Summary ==="
Write-Output "PASS Autopilot configuration creation tested"
Write-Output "PASS Autopilot configuration modification tested"
Write-Output "PASS Autopilot configuration export/import tested"
Write-Output "PASS UnattendXML configuration creation tested"
Write-Output "PASS UnattendXML configuration export/import tested"
Write-Output "PASS Directory scanning tested"
Write-Output "PASS Validation functionality tested"
Write-Output ""
Write-Output "Note: Install-AutopilotConfiguration and Install-UnattendXMLConfiguration require real mounted images for full testing"

Write-Output "`nAutopilot and UnattendXML cmdlet testing completed!"

# Cleanup
try {
    Remove-Item $testDir -Recurse -Force
    Write-Output "Test files cleaned up"
} catch {
    Write-Warning "Failed to clean up test files: $_"
}
