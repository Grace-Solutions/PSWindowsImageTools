# Test script for ADK and WinPE Optional Component cmdlets
# This script tests Get-ADKInstallation, Get-WinPEOptionalComponent, and Add-WinPEOptionalComponent

[CmdletBinding()]
param(
    [string]$TestBootImagePath = "",
    [switch]$SkipInstallation,
    [switch]$Detailed
)

Write-Output "=== Testing ADK and WinPE Optional Component Cmdlets ==="
Write-Output "This script tests the new ADK detection and Optional Component management cmdlets"
Write-Output ""

# Import the module
try {
    Import-Module ".\Module\PSWindowsImageTools\PSWindowsImageTools.psd1" -Force
    Write-Output "✓ Module imported successfully"
} catch {
    Write-Error "✗ Failed to import module: $($_.Exception.Message)"
    exit 1
}

# Test 1: ADK Detection
Write-Output "`n=== Test 1: ADK Installation Detection ==="

try {
    Write-Output "Testing Get-ADKInstallation cmdlet..."
    
    # Basic ADK detection
    $adkInstallations = Get-ADKInstallation -Verbose:$Detailed
    
    if ($adkInstallations) {
        Write-Output "✓ Found $($adkInstallations.Count) ADK installation(s)"
        
        foreach ($adk in $adkInstallations) {
            Write-Output "  - $($adk.DisplayName)"
            Write-Output "    Version: $($adk.Version)"
            Write-Output "    Path: $($adk.InstallationPath.FullName)"
            Write-Output "    WinPE Add-on: $($adk.HasWinPEAddon)"
            Write-Output "    Deployment Tools: $($adk.HasDeploymentTools)"
            Write-Output "    Supported Architectures: $($adk.SupportedArchitectures -join ', ')"
            
            if ($adk.WinPEOptionalComponentsPath) {
                Write-Output "    WinPE OC Path: $($adk.WinPEOptionalComponentsPath.FullName)"
            }
            Write-Output ""
        }
        
        # Test filtering
        Write-Output "Testing ADK filtering options..."
        
        $latestADK = Get-ADKInstallation -Latest -Verbose:$Detailed
        if ($latestADK) {
            Write-Output "✓ Latest ADK: $($latestADK.DisplayName) v$($latestADK.Version)"
        }
        
        $winpeADK = Get-ADKInstallation -RequireWinPE -Verbose:$Detailed
        if ($winpeADK) {
            Write-Output "✓ ADK with WinPE: $($winpeADK.Count) found"
        }
        
        $amd64ADK = Get-ADKInstallation -RequiredArchitecture amd64 -Verbose:$Detailed
        if ($amd64ADK) {
            Write-Output "✓ ADK with amd64 support: $($amd64ADK.Count) found"
        }
        
    } else {
        Write-Warning "⚠ No ADK installations found on this system"
        Write-Output "Please install Windows ADK to test Optional Component functionality"
        exit 0
    }
} catch {
    Write-Error "✗ ADK detection failed: $($_.Exception.Message)"
    if ($Detailed) { Write-Error $_.Exception.StackTrace }
}

# Test 2: Optional Component Discovery
Write-Output "`n=== Test 2: WinPE Optional Component Discovery ==="

try {
    # Get the best ADK for testing
    $testADK = Get-ADKInstallation -Latest -RequireWinPE
    
    if (-not $testADK) {
        Write-Warning "⚠ No suitable ADK found for Optional Component testing"
        exit 0
    }
    
    Write-Output "Using ADK: $($testADK.DisplayName) v$($testADK.Version)"
    
    # Test basic component discovery
    Write-Output "`nTesting Get-WinPEOptionalComponent cmdlet..."
    
    $allComponents = Get-WinPEOptionalComponent -ADKInstallation $testADK -Verbose:$Detailed
    
    if ($allComponents) {
        Write-Output "✓ Found $($allComponents.Count) optional components"
        
        # Show category breakdown
        $categoryBreakdown = $allComponents | Group-Object Category | Sort-Object Name
        Write-Output "`nCategory breakdown:"
        foreach ($category in $categoryBreakdown) {
            Write-Output "  - $($category.Name): $($category.Count) components"
        }
        
        # Show largest components
        $largestComponents = $allComponents | Sort-Object SizeInBytes -Descending | Select-Object -First 5
        Write-Output "`nLargest components:"
        foreach ($component in $largestComponents) {
            Write-Output "  - $($component.Name): $($component.SizeFormatted)"
        }
        
        # Test filtering
        Write-Output "`nTesting component filtering..."
        
        $scriptingComponents = Get-WinPEOptionalComponent -ADKInstallation $testADK -Category Scripting -Verbose:$Detailed
        if ($scriptingComponents) {
            Write-Output "✓ Scripting components: $($scriptingComponents.Count) found"
            foreach ($comp in $scriptingComponents) {
                Write-Output "    - $($comp.Name) ($($comp.SizeFormatted))"
            }
        }
        
        $powerShellComponents = Get-WinPEOptionalComponent -ADKInstallation $testADK -Name "*PowerShell*" -Verbose:$Detailed
        if ($powerShellComponents) {
            Write-Output "✓ PowerShell components: $($powerShellComponents.Count) found"
        }
        
        $smallComponents = Get-WinPEOptionalComponent -ADKInstallation $testADK -MaximumSizeMB 10 -Verbose:$Detailed
        if ($smallComponents) {
            Write-Output "✓ Small components (<10MB): $($smallComponents.Count) found"
        }
        
        # Test with language packs
        $withLanguagePacks = Get-WinPEOptionalComponent -ADKInstallation $testADK -IncludeLanguagePacks -Verbose:$Detailed
        if ($withLanguagePacks) {
            $languagePacks = $withLanguagePacks | Where-Object { $_.IsLanguagePack }
            Write-Output "✓ Language packs found: $($languagePacks.Count)"
        }
        
    } else {
        Write-Warning "⚠ No optional components found"
    }
} catch {
    Write-Error "✗ Optional component discovery failed: $($_.Exception.Message)"
    if ($Detailed) { Write-Error $_.Exception.StackTrace }
}

# Test 3: Component Installation (if boot image provided and not skipped)
if (-not $SkipInstallation -and $TestBootImagePath -and (Test-Path $TestBootImagePath)) {
    Write-Output "`n=== Test 3: Optional Component Installation ==="
    
    try {
        Write-Output "Testing Add-WinPEOptionalComponent cmdlet..."
        Write-Output "Boot image: $TestBootImagePath"
        
        # Mount the boot image
        Write-Output "Mounting boot image for testing..."
        $mountPath = "C:\Temp\WinPEMount_$(Get-Random)"
        
        $mountedImages = Mount-WindowsImageList -ImagePath $TestBootImagePath -Index 2 -MountPath $mountPath -ReadWrite
        
        if ($mountedImages) {
            Write-Output "✓ Successfully mounted boot image"
            
            try {
                # Get some safe components to install (WMI is usually safe)
                $testComponents = Get-WinPEOptionalComponent -ADKInstallation $testADK -Name "WMI" -Verbose:$Detailed
                
                if ($testComponents) {
                    Write-Output "Installing test components: $($testComponents.Count) components"
                    
                    # Install components
                    $installResults = Add-WinPEOptionalComponent -MountedImages $mountedImages -Components $testComponents -ContinueOnError -ValidateInstallation -Verbose:$Detailed
                    
                    if ($installResults) {
                        Write-Output "✓ Component installation completed"
                        
                        foreach ($result in $installResults) {
                            Write-Output "  Image: $($result.MountedImage.ImageName)"
                            Write-Output "    Successful: $($result.SuccessfulComponents.Count)"
                            Write-Output "    Failed: $($result.FailedComponents.Count)"
                            Write-Output "    Skipped: $($result.SkippedComponents.Count)"
                            Write-Output "    Success Rate: $($result.SuccessRate.ToString('F1'))%"
                            Write-Output "    Duration: $($result.Duration.TotalSeconds.ToString('F1')) seconds"
                            
                            if ($result.Errors.Count -gt 0) {
                                Write-Output "    Errors:"
                                foreach ($error in $result.Errors) {
                                    Write-Output "      - $error"
                                }
                            }
                        }
                    }
                } else {
                    Write-Warning "⚠ No suitable test components found"
                }
            }
            finally {
                # Always dismount the image
                Write-Output "Dismounting boot image..."
                try {
                    Dismount-WindowsImageList -MountPath $mountedImages[0].MountPath -Save
                    Write-Output "✓ Boot image dismounted and saved"
                } catch {
                    Write-Warning "⚠ Failed to dismount cleanly, attempting discard..."
                    try {
                        Dismount-WindowsImageList -MountPath $mountedImages[0].MountPath -Discard
                        Write-Output "✓ Boot image dismounted (changes discarded)"
                    } catch {
                        Write-Error "✗ Failed to dismount boot image: $($_.Exception.Message)"
                    }
                }
            }
        } else {
            Write-Error "✗ Failed to mount boot image"
        }
    } catch {
        Write-Error "✗ Component installation test failed: $($_.Exception.Message)"
        if ($Detailed) { Write-Error $_.Exception.StackTrace }
    }
} else {
    Write-Output "`n=== Test 3: Optional Component Installation (Skipped) ==="
    if ($SkipInstallation) {
        Write-Output "Installation test skipped by user request"
    } else {
        Write-Output "Installation test skipped - no boot image path provided"
        Write-Output "To test installation, provide a boot.wim path:"
        Write-Output "  .\test-adk-optional-components.ps1 -TestBootImagePath 'C:\path\to\boot.wim'"
    }
}

# Test 4: Pipeline Usage Examples
Write-Output "`n=== Test 4: Pipeline Usage Examples ==="

try {
    Write-Output "Demonstrating pipeline usage patterns..."
    
    # Example 1: Get latest ADK and find PowerShell components
    Write-Output "`nExample 1: Latest ADK → PowerShell components"
    $pipelineResult1 = Get-ADKInstallation -Latest -RequireWinPE | Get-WinPEOptionalComponent -Name "*PowerShell*" -Verbose:$Detailed
    if ($pipelineResult1) {
        Write-Output "✓ Found $($pipelineResult1.Count) PowerShell components via pipeline"
    }
    
    # Example 2: Filter by category and size
    Write-Output "`nExample 2: Scripting components under 50MB"
    $pipelineResult2 = Get-ADKInstallation -Latest -RequireWinPE | Get-WinPEOptionalComponent -Category Scripting -MaximumSizeMB 50 -Verbose:$Detailed
    if ($pipelineResult2) {
        Write-Output "✓ Found $($pipelineResult2.Count) scripting components under 50MB"
    }
    
    # Example 3: Architecture-specific components
    Write-Output "`nExample 3: x86 components"
    $x86ADK = Get-ADKInstallation -RequiredArchitecture x86 -Latest
    if ($x86ADK) {
        $pipelineResult3 = $x86ADK | Get-WinPEOptionalComponent -Architecture x86 -Verbose:$Detailed
        if ($pipelineResult3) {
            Write-Output "✓ Found $($pipelineResult3.Count) x86 components"
        }
    } else {
        Write-Output "⚠ No ADK with x86 support found"
    }
    
} catch {
    Write-Error "✗ Pipeline usage test failed: $($_.Exception.Message)"
    if ($Detailed) { Write-Error $_.Exception.StackTrace }
}

# Summary
Write-Output "`n=== Test Summary ==="
Write-Output "✓ ADK Detection: Get-ADKInstallation cmdlet tested"
Write-Output "✓ Component Discovery: Get-WinPEOptionalComponent cmdlet tested"
if (-not $SkipInstallation -and $TestBootImagePath -and (Test-Path $TestBootImagePath)) {
    Write-Output "✓ Component Installation: Add-WinPEOptionalComponent cmdlet tested"
} else {
    Write-Output "⚠ Component Installation: Skipped (no boot image provided)"
}
Write-Output "✓ Pipeline Usage: Demonstrated pipeline patterns"
Write-Output ""
Write-Output "All available cmdlets:"
Write-Output "  - Get-ADKInstallation: Detect Windows ADK installations"
Write-Output "  - Get-WinPEOptionalComponent: Discover available WinPE Optional Components"
Write-Output "  - Add-WinPEOptionalComponent: Install components into mounted boot images"
Write-Output ""
Write-Output "For detailed help on any cmdlet, use: Get-Help <CmdletName> -Detailed"
