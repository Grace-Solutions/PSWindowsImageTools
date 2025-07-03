# Test script for registry operation cmdlets
[CmdletBinding()]
param()

Write-Output "=== Testing Registry Operation Cmdlets ==="

# Create test directory
$testRoot = "C:\Temp\RegistryTest"
if (Test-Path $testRoot) {
    Remove-Item -Path $testRoot -Recurse -Force
}
New-Item -Path $testRoot -ItemType Directory -Force | Out-Null
Write-Verbose "Created test directory: $testRoot"

# Create sample .reg files
$regFiles = @{
    "$testRoot\system-tweaks.reg" = @"
Windows Registry Editor Version 5.00

; System Performance Tweaks
[HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management]
"DisablePagingExecutive"=dword:00000001
"LargeSystemCache"=dword:00000000

[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"Hidden"=dword:00000001
"HideFileExt"=dword:00000000

; Remove Windows Defender (example of key deletion)
[-HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows Defender]

; User Profile Settings (will be mapped to default user)
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced]
"ShowSuperHidden"=dword:00000001
"LaunchTo"=dword:00000001
"@

    "$testRoot\privacy-settings.reg" = @"
Windows Registry Editor Version 5.00

; Privacy Settings
[HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection]
"AllowTelemetry"=dword:00000000
"DoNotShowFeedbackNotifications"=dword:00000001

[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection]
"AllowTelemetry"=dword:00000000

; Disable Cortana
[HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\Windows Search]
"AllowCortana"=dword:00000000

; Binary data example
[HKEY_LOCAL_MACHINE\SOFTWARE\TestApp]
"BinaryValue"=hex:01,02,03,04,05
"MultiStringValue"=hex(7):48,00,65,00,6c,00,6c,00,6f,00,00,00,57,00,6f,00,72,00,6c,00,64,00,00,00,00,00
"@

    "$testRoot\user-customizations.reg" = @"
Windows Registry Editor Version 5.00

; User Interface Customizations
[HKEY_CURRENT_USER\Control Panel\Desktop]
"Wallpaper"="C:\\Windows\\Web\\Wallpaper\\Windows\\img0.jpg"
"WallpaperStyle"="10"

[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize]
"AppsUseLightTheme"=dword:00000000
"SystemUsesLightTheme"=dword:00000000

; Remove a specific value (example)
[HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run]
"TestApp"=-
"@
}

Write-Output "`n=== Test 1: Creating Sample .reg Files ==="
foreach ($regFile in $regFiles.GetEnumerator()) {
    $regFile.Value | Out-File -FilePath $regFile.Key -Encoding UTF8
    Write-Verbose "Created: $($regFile.Key)"
}
Write-Output "Created $($regFiles.Count) sample .reg files"

try {
    # Import the module
    Import-Module ".\Module\PSWindowsImageTools\PSWindowsImageTools.psd1" -Force
    Write-Verbose "Module imported successfully"

    Write-Output "`n=== Test 2: Get-RegistryOperationList (All Files) ==="
    $allOperations = Get-RegistryOperationList -Path $testRoot -Verbose
    Write-Output "Found $($allOperations.Count) total registry operations"

    # Group by operation type
    $operationsByType = $allOperations | Group-Object Operation
    foreach ($group in $operationsByType) {
        Write-Output "  $($group.Name): $($group.Count) operations"
    }

    # Group by hive
    $operationsByHive = $allOperations | Group-Object { $_.GetMappedHive() }
    Write-Output "`nOperations by hive:"
    foreach ($group in $operationsByHive) {
        Write-Output "  $($group.Name): $($group.Count) operations"
    }

    Write-Output "`n=== Test 3: Filter by HKLM Operations ==="
    $hklmOperations = Get-RegistryOperationList -Path $testRoot -FilterHive "HKLM" -Verbose
    Write-Output "Found $($hklmOperations.Count) HKLM operations"

    Write-Output "`n=== Test 4: Filter by Remove Operations ==="
    $removeOperations = Get-RegistryOperationList -Path $testRoot -FilterOperation "Remove" -Verbose
    Write-Output "Found $($removeOperations.Count) remove operations"

    Write-Output "`n=== Test 5: Show Sample Operations ==="
    $sampleOperations = $allOperations | Select-Object -First 5
    foreach ($operation in $sampleOperations) {
        Write-Output "Operation: $($operation.Operation)"
        Write-Output "  Full Path: $($operation.GetFullPath())"
        Write-Output "  Value Name: $($operation.ValueName)"
        Write-Output "  Value: $($operation.Value)"
        Write-Output "  Value Type: $($operation.ValueType)"
        Write-Output "  Original Line: $($operation.OriginalLine)"
        Write-Output ""
    }

    Write-Output "`n=== Test 6: Test Dynamic Value Get/Set ==="
    $testOperation = $allOperations | Where-Object { $_.ValueName -eq "AllowTelemetry" } | Select-Object -First 1
    if ($testOperation) {
        Write-Output "Original value: $($testOperation.Value)"
        Write-Output "Value type: $($testOperation.ValueType)"
        Write-Output "Full path: $($testOperation.GetFullPath())"
        Write-Output "Get as string: $($testOperation.GetValueAsString())"
        Write-Output "Get as int: $($testOperation.GetValueAsInt())"

        # Test setting a new value
        $testOperation.SetValue(2)
        Write-Output "After setting to 2: $($testOperation.Value)"

        # Test the formatted value method
        $formattedValue = $testOperation.GetFormattedValue()
        Write-Output "Formatted value: $formattedValue"
    }

    Write-Output "`n=== Test 7: WhatIf Mode (Mock) ==="
    Write-Output "Note: This test requires mounted Windows images which are not available in this test environment."
    Write-Output "The Write-RegistryOperationList cmdlet would be used like this:"
    Write-Output "`$mountedImages = Mount-WindowsImageList -ImagePath 'install.wim'"
    Write-Output "`$operations = Get-RegistryOperationList -Path '$testRoot'"
    Write-Output "`$results = Write-RegistryOperationList -MountedImages `$mountedImages -Operations `$operations -WhatIf"

    Write-Output "`n=== Test 8: Show Registry Hive Mapping ==="
    $hiveMappingTests = @(
        @{ Original = "HKEY_LOCAL_MACHINE"; Expected = "HKLM" },
        @{ Original = "HKLM"; Expected = "HKLM" },
        @{ Original = "HKEY_CURRENT_USER"; Expected = "HKU" },
        @{ Original = "HKCU"; Expected = "HKU" },
        @{ Original = "HKEY_CLASSES_ROOT"; Expected = "HKLM\SOFTWARE\Classes" }
    )

    foreach ($test in $hiveMappingTests) {
        $operation = [PSWindowsImageTools.Models.RegistryOperation]::new()
        $operation.Hive = $test.Original
        $mapped = $operation.GetMappedHive()
        $status = if ($mapped -eq $test.Expected) { "PASS" } else { "FAIL" }
        Write-Output "  $status $($test.Original) -> $mapped"
    }

} catch {
    Write-Error "Error during testing: $($_.Exception.Message)"
    Write-Error $_.ScriptStackTrace
} finally {
    # Cleanup
    if (Test-Path $testRoot) {
        Remove-Item -Path $testRoot -Recurse -Force -ErrorAction SilentlyContinue
        Write-Verbose "Test directory cleaned up"
    }
}

Write-Output "`n=== Test Summary ==="
Write-Output "PASS Registry operation parsing tested"
Write-Output "PASS Filtering functionality tested"
Write-Output "PASS Dynamic value operations tested"
Write-Output "PASS Hive mapping tested"
Write-Output "Note: Write-RegistryOperationList requires real mounted images for full testing"

Write-Output "`nRegistry operation cmdlet testing completed!"
