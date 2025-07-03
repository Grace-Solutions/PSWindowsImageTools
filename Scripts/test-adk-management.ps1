# Test script for ADK management cmdlets
# This script tests Install-ADK and Uninstall-ADK cmdlets

[CmdletBinding()]
param(
    [switch]$TestInstallation,
    [switch]$TestUninstallation,
    [switch]$Detailed,
    [string]$CustomInstallPath = ""
)

Write-Output "=== Testing ADK Management Cmdlets ==="
Write-Output "This script tests the ADK installation and uninstallation cmdlets"
Write-Output ""

# Check for administrator privileges
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")
if (-not $isAdmin) {
    Write-Error "Administrator privileges are required to test ADK management cmdlets"
    Write-Output "Please run this script as Administrator"
    exit 1
}

# Import the module
try {
    Import-Module ".\Module\PSWindowsImageTools\PSWindowsImageTools.psd1" -Force
    Write-Output "✓ Module imported successfully"
} catch {
    Write-Error "✗ Failed to import module: $($_.Exception.Message)"
    exit 1
}

# Test 1: Initial ADK Detection
Write-Output "`n=== Test 1: Initial ADK Detection ==="

try {
    Write-Output "Detecting existing ADK installations..."
    
    $initialADKs = Get-ADKInstallation -Verbose:$Detailed
    
    if ($initialADKs) {
        Write-Output "✓ Found $($initialADKs.Count) existing ADK installation(s)"
        
        foreach ($adk in $initialADKs) {
            Write-Output "  - $($adk.DisplayName) v$($adk.Version)"
            Write-Output "    Path: $($adk.InstallationPath.FullName)"
            Write-Output "    WinPE: $($adk.HasWinPEAddon), Deployment Tools: $($adk.HasDeploymentTools)"
        }
    } else {
        Write-Output "⚠ No existing ADK installations found"
    }
} catch {
    Write-Error "✗ Initial ADK detection failed: $($_.Exception.Message)"
    if ($Detailed) { Write-Error $_.Exception.StackTrace }
}

# Test 2: ADK Installation (if requested)
if ($TestInstallation) {
    Write-Output "`n=== Test 2: ADK Installation ==="
    
    try {
        Write-Output "Testing Install-ADK cmdlet..."
        
        # Test 2a: Check if installation is needed (default behavior)
        Write-Output "`nTest 2a: Default skip-if-present functionality"
        $skipResult = Install-ADK -Verbose:$Detailed

        if ($skipResult) {
            Write-Output "✓ Install-ADK with default behavior completed"
            Write-Output "  Result: $($skipResult.DisplayName) v$($skipResult.Version)"
        }
        
        # Test 2b: Force installation (if no existing ADK or user wants to test)
        if (-not $initialADKs -or $PSCmdlet.ShouldContinue("Force install ADK for testing?", "ADK Installation Test")) {
            Write-Output "`nTest 2b: Force installation"
            
            $installParams = @{
                IncludeWinPE = $true
                IncludeDeploymentTools = $true
                Force = $true
                Verbose = $Detailed
            }
            
            if ($CustomInstallPath) {
                $installParams.InstallPath = $CustomInstallPath
                Write-Output "Using custom install path: $CustomInstallPath"
            }
            
            Write-Output "Starting ADK installation (this may take several minutes)..."
            $installResult = Install-ADK @installParams
            
            if ($installResult) {
                Write-Output "✓ ADK installation completed successfully"
                Write-Output "  Installed: $($installResult.DisplayName) v$($installResult.Version)"
                Write-Output "  Path: $($installResult.InstallationPath.FullName)"
                Write-Output "  WinPE: $($installResult.HasWinPEAddon)"
                Write-Output "  Deployment Tools: $($installResult.HasDeploymentTools)"
                Write-Output "  Architectures: $($installResult.SupportedArchitectures -join ', ')"
            } else {
                Write-Error "✗ ADK installation failed - no result returned"
            }
        } else {
            Write-Output "⚠ Force installation skipped by user"
        }
        
    } catch {
        Write-Error "✗ ADK installation test failed: $($_.Exception.Message)"
        if ($Detailed) { Write-Error $_.Exception.StackTrace }
    }
} else {
    Write-Output "`n=== Test 2: ADK Installation (Skipped) ==="
    Write-Output "Installation test skipped by user request"
    Write-Output "To test installation, use: -TestInstallation"
    Write-Warning "Note: ADK installation requires internet connection and may take 10+ minutes"
}

# Test 3: Post-Installation Verification
Write-Output "`n=== Test 3: Post-Installation Verification ==="

try {
    Write-Output "Verifying ADK installations after potential changes..."
    
    $currentADKs = Get-ADKInstallation -Verbose:$Detailed
    
    if ($currentADKs) {
        Write-Output "✓ Found $($currentADKs.Count) ADK installation(s)"
        
        # Test component discovery
        $latestADK = $currentADKs | Sort-Object Version -Descending | Select-Object -First 1
        Write-Output "`nTesting component discovery with latest ADK..."
        
        $components = Get-WinPEOptionalComponent -ADKInstallation $latestADK -Verbose:$Detailed
        
        if ($components) {
            Write-Output "✓ Found $($components.Count) optional components"
            
            # Show category breakdown
            $categories = $components | Group-Object Category | Sort-Object Name
            Write-Output "  Categories available:"
            foreach ($category in $categories) {
                Write-Output "    - $($category.Name): $($category.Count) components"
            }
            
            # Test specific component types
            $scriptingComponents = $components | Where-Object { $_.Category -eq 'Scripting' }
            if ($scriptingComponents) {
                Write-Output "  ✓ Scripting components: $($scriptingComponents.Count) found"
            }
            
            $networkingComponents = $components | Where-Object { $_.Category -eq 'Networking' }
            if ($networkingComponents) {
                Write-Output "  ✓ Networking components: $($networkingComponents.Count) found"
            }
        } else {
            Write-Warning "⚠ No optional components found"
        }
    } else {
        Write-Warning "⚠ No ADK installations found after verification"
    }
} catch {
    Write-Error "✗ Post-installation verification failed: $($_.Exception.Message)"
    if ($Detailed) { Write-Error $_.Exception.StackTrace }
}

# Test 4: ADK Uninstallation (if requested)
if ($TestUninstallation) {
    Write-Output "`n=== Test 4: ADK Uninstallation ==="
    
    try {
        Write-Output "Testing Uninstall-ADK cmdlet..."
        
        $adksToTest = Get-ADKInstallation
        
        if (-not $adksToTest) {
            Write-Output "⚠ No ADK installations found to uninstall"
        } else {
            Write-Output "Found $($adksToTest.Count) ADK installation(s) available for uninstallation"
            
            # Test 4a: Uninstall latest (with confirmation)
            if ($PSCmdlet.ShouldContinue("Uninstall latest ADK installation for testing?", "ADK Uninstallation Test")) {
                Write-Output "`nTest 4a: Uninstall latest ADK"
                
                $uninstallResult = Uninstall-ADK -Verbose:$Detailed
                
                if ($uninstallResult) {
                    Write-Output "✓ ADK uninstallation completed successfully"
                    
                    # Verify uninstallation
                    $remainingADKs = Get-ADKInstallation
                    $expectedCount = $adksToTest.Count - 1
                    
                    if ($remainingADKs.Count -eq $expectedCount) {
                        Write-Output "✓ Uninstallation verified: $($remainingADKs.Count) installations remaining"
                    } else {
                        Write-Warning "⚠ Uninstallation verification: Expected $expectedCount, found $($remainingADKs.Count)"
                    }
                } else {
                    Write-Error "✗ ADK uninstallation failed"
                }
            } else {
                Write-Output "⚠ Uninstallation test skipped by user"
            }
            
            # Test 4b: Uninstall all (if multiple installations and user confirms)
            $remainingAfterTest = Get-ADKInstallation
            if ($remainingAfterTest.Count -gt 1) {
                if ($PSCmdlet.ShouldContinue("Uninstall all remaining ADK installations?", "Complete ADK Removal Test")) {
                    Write-Output "`nTest 4b: Uninstall all ADK installations"
                    
                    $uninstallAllResult = Uninstall-ADK -All -Force -Verbose:$Detailed
                    
                    if ($uninstallAllResult) {
                        Write-Output "✓ All ADK uninstallations completed"
                        
                        # Final verification
                        $finalADKs = Get-ADKInstallation
                        if ($finalADKs.Count -eq 0) {
                            Write-Output "✓ Complete removal verified: No ADK installations remaining"
                        } else {
                            Write-Warning "⚠ Complete removal verification: $($finalADKs.Count) installations still found"
                        }
                    } else {
                        Write-Error "✗ Complete ADK uninstallation failed"
                    }
                } else {
                    Write-Output "⚠ Complete uninstallation test skipped by user"
                }
            }
        }
    } catch {
        Write-Error "✗ ADK uninstallation test failed: $($_.Exception.Message)"
        if ($Detailed) { Write-Error $_.Exception.StackTrace }
    }
} else {
    Write-Output "`n=== Test 4: ADK Uninstallation (Skipped) ==="
    Write-Output "Uninstallation test skipped by user request"
    Write-Output "To test uninstallation, use: -TestUninstallation"
    Write-Warning "Note: This will remove ADK installations from your system"
}

# Test 5: Pipeline Integration
Write-Output "`n=== Test 5: Pipeline Integration ==="

try {
    Write-Output "Testing pipeline integration between cmdlets..."
    
    # Test pipeline: Install → Detect → Get Components
    Write-Output "`nPipeline test: ADK detection → Component discovery"
    
    $pipelineADK = Get-ADKInstallation -Latest -RequireWinPE
    if ($pipelineADK) {
        $pipelineComponents = $pipelineADK | Get-WinPEOptionalComponent -Category Scripting
        
        if ($pipelineComponents) {
            Write-Output "✓ Pipeline successful: $($pipelineComponents.Count) scripting components found"
        } else {
            Write-Output "⚠ Pipeline completed but no scripting components found"
        }
    } else {
        Write-Output "⚠ No suitable ADK found for pipeline test"
    }
    
} catch {
    Write-Error "✗ Pipeline integration test failed: $($_.Exception.Message)"
    if ($Detailed) { Write-Error $_.Exception.StackTrace }
}

# Summary
Write-Output "`n=== Test Summary ==="
Write-Output "✓ ADK Detection: Get-ADKInstallation cmdlet tested"

if ($TestInstallation) {
    Write-Output "✓ ADK Installation: Install-ADK cmdlet tested"
} else {
    Write-Output "⚠ ADK Installation: Skipped (use -TestInstallation to test)"
}

if ($TestUninstallation) {
    Write-Output "✓ ADK Uninstallation: Uninstall-ADK cmdlet tested"
} else {
    Write-Output "⚠ ADK Uninstallation: Skipped (use -TestUninstallation to test)"
}

Write-Output "✓ Component Discovery: Get-WinPEOptionalComponent integration tested"
Write-Output "✓ Pipeline Integration: Cmdlet chaining tested"
Write-Output ""
Write-Output "Available ADK management cmdlets:"
Write-Output "  - Get-ADKInstallation: Detect Windows ADK installations"
Write-Output "  - Install-ADK: Download and install latest ADK silently"
Write-Output "  - Uninstall-ADK: Remove ADK installations silently"
Write-Output "  - Get-WinPEOptionalComponent: Discover available components"
Write-Output "  - Add-WinPEOptionalComponent: Install components into boot images"
Write-Output ""
Write-Output "For detailed help on any cmdlet, use: Get-Help <CmdletName> -Detailed"
