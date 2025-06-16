# Test Script for Enhanced ISO Service Capabilities
# Tests the intelligent oscdimg detection and fallback system

Write-Host "=== ISO SERVICE CAPABILITIES TEST ===" -ForegroundColor Yellow
Write-Host "Testing intelligent oscdimg detection and fallback system" -ForegroundColor Cyan
Write-Host ""

# Import the module
try {
    Import-Module '.\Module\PSWindowsImageTools\PSWindowsImageTools.psd1' -Force
    Write-Host "SUCCESS: Module imported successfully" -ForegroundColor Green
} catch {
    Write-Host "ERROR: Failed to import module: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "TEST 1: ISO Creation Capabilities Assessment" -ForegroundColor Green
Write-Host "Checking what ISO creation methods are available on this system..." -ForegroundColor White

try {
    # Load the assembly to access ISOService directly
    Add-Type -Path ".\Module\PSWindowsImageTools\bin\PSWindowsImageTools.dll"
    
    # Get capabilities assessment
    $capabilities = [PSWindowsImageTools.Services.ISOService]::GetISOCreationCapabilities($null)
    
    Write-Host "System ISO Creation Capabilities:" -ForegroundColor Cyan
    Write-Host $capabilities -ForegroundColor White
    Write-Host ""
    
    # Parse capabilities to determine best available method
    if ($capabilities -match "✓ Windows ADK oscdimg") {
        Write-Host "OPTIMAL: Windows ADK oscdimg is available!" -ForegroundColor Green
        Write-Host "• Best quality bootable ISOs" -ForegroundColor White
        Write-Host "• Full UEFI and BIOS support" -ForegroundColor White
        Write-Host "• Official Microsoft tool" -ForegroundColor White
    } elseif ($capabilities -match "✓ mkisofs") {
        Write-Host "GOOD: mkisofs is available as alternative" -ForegroundColor Yellow
        Write-Host "• Cross-platform ISO creation" -ForegroundColor White
        Write-Host "• Basic bootable ISO support" -ForegroundColor White
    } else {
        Write-Host "BASIC: Only PowerShell method available" -ForegroundColor Red
        Write-Host "• Limited ISO creation capabilities" -ForegroundColor White
        Write-Host "• Consider installing Windows ADK" -ForegroundColor White
    }
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "TEST 2: Windows ADK Installation Guidance" -ForegroundColor Green
Write-Host "Displaying guidance for optimal ISO creation setup..." -ForegroundColor White

try {
    # Show Windows ADK guidance
    [PSWindowsImageTools.Services.ISOService]::ShowWindowsADKGuidance($null)
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "TEST 3: oscdimg Detection Test" -ForegroundColor Green
Write-Host "Testing intelligent oscdimg detection..." -ForegroundColor White

try {
    # Create ISO service instance to test detection
    $isoService = New-Object PSWindowsImageTools.Services.ISOService
    
    # Test oscdimg detection (using reflection to access private method)
    $findMethod = $isoService.GetType().GetMethod("FindInstalledOscdimg", [System.Reflection.BindingFlags]::NonPublic -bor [System.Reflection.BindingFlags]::Instance)
    
    if ($findMethod) {
        $oscdimgPath = $findMethod.Invoke($isoService, $null)
        
        if ($oscdimgPath) {
            Write-Host "SUCCESS: Found oscdimg at: $oscdimgPath" -ForegroundColor Green
            
            # Test if the file actually exists and get version info
            if (Test-Path $oscdimgPath -and $oscdimgPath -ne "oscdimg.exe") {
                $fileInfo = Get-Item $oscdimgPath
                Write-Host "  File size: $([math]::Round($fileInfo.Length / 1KB, 2)) KB" -ForegroundColor Cyan
                Write-Host "  Last modified: $($fileInfo.LastWriteTime)" -ForegroundColor Cyan
                
                # Try to get version info
                try {
                    $versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($oscdimgPath)
                    if ($versionInfo.FileVersion) {
                        Write-Host "  Version: $($versionInfo.FileVersion)" -ForegroundColor Cyan
                    }
                } catch {
                    Write-Host "  Version: Unable to determine" -ForegroundColor Gray
                }
            } elseif ($oscdimgPath -eq "oscdimg.exe") {
                Write-Host "  Found in PATH - testing availability..." -ForegroundColor Cyan
                try {
                    $testResult = & oscdimg.exe 2>&1
                    Write-Host "  PATH oscdimg is accessible" -ForegroundColor Green
                } catch {
                    Write-Host "  PATH oscdimg test failed: $($_.Exception.Message)" -ForegroundColor Red
                }
            }
        } else {
            Write-Host "INFO: oscdimg not found in standard locations" -ForegroundColor Yellow
            Write-Host "  Checked locations:" -ForegroundColor Gray
            Write-Host "  • Program Files (x86)\Windows Kits\10\Assessment and Deployment Kit\..." -ForegroundColor Gray
            Write-Host "  • Program Files\Windows Kits\10\Assessment and Deployment Kit\..." -ForegroundColor Gray
            Write-Host "  • PATH environment variable" -ForegroundColor Gray
        }
    } else {
        Write-Host "ERROR: Could not access oscdimg detection method" -ForegroundColor Red
    }
    
    $isoService.Dispose()
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "TEST 4: Alternative ISO Tools Detection" -ForegroundColor Green
Write-Host "Checking for alternative ISO creation tools..." -ForegroundColor White

# Test for mkisofs
try {
    $mkisofsTest = Get-Command mkisofs -ErrorAction SilentlyContinue
    if ($mkisofsTest) {
        Write-Host "✓ mkisofs found: $($mkisofsTest.Source)" -ForegroundColor Green
        try {
            $mkisofsVersion = & mkisofs --version 2>&1 | Select-Object -First 1
            Write-Host "  Version: $mkisofsVersion" -ForegroundColor Cyan
        } catch {
            Write-Host "  Version: Unable to determine" -ForegroundColor Gray
        }
    } else {
        Write-Host "✗ mkisofs not found" -ForegroundColor Red
        Write-Host "  Install via: choco install mkisofs" -ForegroundColor Gray
        Write-Host "  Or download from: http://smithii.com/mkisofs" -ForegroundColor Gray
    }
} catch {
    Write-Host "✗ mkisofs detection failed: $($_.Exception.Message)" -ForegroundColor Red
}

# Test for genisoimage (Linux alternative)
try {
    $genisoimageTest = Get-Command genisoimage -ErrorAction SilentlyContinue
    if ($genisoimageTest) {
        Write-Host "✓ genisoimage found: $($genisoimageTest.Source)" -ForegroundColor Green
    } else {
        Write-Host "✗ genisoimage not found" -ForegroundColor Yellow
    }
} catch {
    Write-Host "✗ genisoimage detection failed" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "TEST 5: Cache Directory Test" -ForegroundColor Green
Write-Host "Testing oscdimg cache directory functionality..." -ForegroundColor White

try {
    $isoService = New-Object PSWindowsImageTools.Services.ISOService
    
    # Test cache directory creation (using reflection)
    $cacheMethod = $isoService.GetType().GetMethod("GetOscdimgCacheDirectory", [System.Reflection.BindingFlags]::NonPublic -bor [System.Reflection.BindingFlags]::Instance)
    
    if ($cacheMethod) {
        $cacheDir = $cacheMethod.Invoke($isoService, $null)
        
        if (Test-Path $cacheDir) {
            Write-Host "SUCCESS: Cache directory created: $cacheDir" -ForegroundColor Green
            
            # Check if cached oscdimg exists
            $cachedOscdimg = Join-Path $cacheDir "oscdimg.exe"
            if (Test-Path $cachedOscdimg) {
                $cachedInfo = Get-Item $cachedOscdimg
                Write-Host "  Cached oscdimg found: $([math]::Round($cachedInfo.Length / 1KB, 2)) KB" -ForegroundColor Cyan
                Write-Host "  Cached on: $($cachedInfo.CreationTime)" -ForegroundColor Cyan
            } else {
                Write-Host "  No cached oscdimg found (will be downloaded if needed)" -ForegroundColor Yellow
            }
        } else {
            Write-Host "ERROR: Failed to create cache directory" -ForegroundColor Red
        }
    } else {
        Write-Host "ERROR: Could not access cache directory method" -ForegroundColor Red
    }
    
    $isoService.Dispose()
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== ISO SERVICE CAPABILITIES SUMMARY ===" -ForegroundColor Yellow
Write-Host ""

# Provide recommendations based on findings
$hasOscdimg = $capabilities -match "✓ Windows ADK oscdimg"
$hasMkisofs = $capabilities -match "✓ mkisofs"

if ($hasOscdimg) {
    Write-Host "🎉 EXCELLENT: Your system is optimally configured for ISO creation!" -ForegroundColor Green
    Write-Host "   Windows ADK oscdimg provides the best bootable ISO experience." -ForegroundColor White
} elseif ($hasMkisofs) {
    Write-Host "👍 GOOD: Your system can create ISOs using mkisofs." -ForegroundColor Yellow
    Write-Host "   For optimal Windows ISO creation, consider installing Windows ADK." -ForegroundColor White
} else {
    Write-Host "⚠️  BASIC: Limited ISO creation capabilities detected." -ForegroundColor Red
    Write-Host "   Strongly recommend installing Windows ADK for best results." -ForegroundColor White
}

Write-Host ""
Write-Host "Intelligent Fallback Chain:" -ForegroundColor Cyan
Write-Host "1. ✓ Installed Windows ADK oscdimg (best quality)" -ForegroundColor White
Write-Host "2. ✓ Cached oscdimg (good quality)" -ForegroundColor White
Write-Host "3. ✓ Download oscdimg from Microsoft (good quality)" -ForegroundColor White
Write-Host "4. ✓ mkisofs alternative (basic quality)" -ForegroundColor White
Write-Host "5. ✓ PowerShell method (minimal quality)" -ForegroundColor White
Write-Host ""
Write-Host "The ISO service will automatically use the best available method!" -ForegroundColor Green
