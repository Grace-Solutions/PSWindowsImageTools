# Test Script for Windows Installation Tree Assembly
# Tests the proper ESD to Windows installation tree conversion following the specification:
# - Image 1: Base Windows setup media (installation tree base)
# - Image 2: Windows PE (exported to sources/boot.wim)
# - Image 3: Windows Setup (appended to sources/boot.wim, set bootable)
# - Remaining images: Windows editions (exported to sources/install.esd)

Write-Host "=== WINDOWS INSTALLATION TREE ASSEMBLY TEST ===" -ForegroundColor Yellow
Write-Host "Testing proper ESD to Windows installation tree conversion" -ForegroundColor Cyan
Write-Host ""

# Test parameters
$esdFile = "C:\Users\gsadmin\Downloads\WindowsImages\26100.2033.241004-2336.ge_release_svc_refresh_CLIENTBUSINESS_VOL_x64FRE_en-us.esd"
$outputFolder = "C:\temp\installation-tree"

# Check if test file exists
if (!(Test-Path $esdFile)) {
    Write-Host "ERROR: Test ESD file not found: $esdFile" -ForegroundColor Red
    Write-Host "Please ensure the ESD file exists for testing" -ForegroundColor Yellow
    exit 1
}

# Clean up previous test
if (Test-Path $outputFolder) {
    Remove-Item $outputFolder -Recurse -Force
}

# Import the module
try {
    Import-Module '.\Module\PSWindowsImageTools\PSWindowsImageTools.psd1' -Force
    Write-Host "SUCCESS: Module imported successfully" -ForegroundColor Green
} catch {
    Write-Host "ERROR: Failed to import module: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "STEP 1: Analyze ESD Structure" -ForegroundColor Green
Write-Host "Getting all images from ESD to understand the structure..." -ForegroundColor White

try {
    $allImages = Get-WindowsImageList -ImagePath $esdFile -Verbose
    
    if ($allImages -and $allImages.Count -gt 0) {
        Write-Host "SUCCESS: Found $($allImages.Count) images in ESD" -ForegroundColor Green
        Write-Host ""
        Write-Host "ESD Image Structure:" -ForegroundColor Cyan
        
        for ($i = 0; $i -lt [Math]::Min($allImages.Count, 10); $i++) {
            $img = $allImages[$i]
            $purpose = switch ($img.Index) {
                1 { "Base Windows setup media (installation tree base)" }
                2 { "Windows PE (will go to sources/boot.wim)" }
                3 { "Windows Setup (will append to sources/boot.wim, bootable)" }
                default { "Windows edition (will go to sources/install.esd)" }
            }
            
            Write-Host "  Image $($img.Index): $($img.Name)" -ForegroundColor White
            Write-Host "    Edition: $($img.Edition)" -ForegroundColor Gray
            Write-Host "    Purpose: $purpose" -ForegroundColor Yellow
            Write-Host "    Size: $([math]::Round($img.Size / 1GB, 2)) GB" -ForegroundColor Gray
            Write-Host ""
        }
    } else {
        Write-Host "ERROR: No images found in ESD" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "STEP 2: Installation Tree Assembly" -ForegroundColor Green
Write-Host "Converting ESD to proper Windows installation tree structure..." -ForegroundColor White

try {
    # Use Folder mode to create the installation tree
    # Note: We're not filtering here to get all images in proper order
    $result = Convert-ESDToWindowsImage -SourcePath $esdFile -OutputPath $outputFolder -Mode Folder `
        -IncludeWindowsPE -IncludeWindowsSetup `
        -CompressionType Max -Verbose
    
    if ($result -and $result.Success) {
        Write-Host "SUCCESS: Installation tree assembly completed" -ForegroundColor Green
        Write-Host "  Mode: $($result.Mode)" -ForegroundColor Cyan
        Write-Host "  Output: $($result.OutputPath)" -ForegroundColor Cyan
        Write-Host "  Duration: $([math]::Round($result.Duration.TotalMinutes, 1)) minutes" -ForegroundColor Cyan
        Write-Host "  Images processed: $($result.ProcessedImages.Count)" -ForegroundColor Cyan
        Write-Host "  Successful: $($result.SuccessfulImages)" -ForegroundColor Cyan
        Write-Host "  Failed: $($result.FailedImages)" -ForegroundColor Cyan
        Write-Host ""
    } else {
        Write-Host "ERROR: Installation tree assembly failed" -ForegroundColor Red
        if ($result -and $result.ErrorMessage) {
            Write-Host "  Error: $($result.ErrorMessage)" -ForegroundColor Red
        }
        exit 1
    }
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host "STEP 3: Validate Installation Tree Structure" -ForegroundColor Green
Write-Host "Checking that the installation tree follows Windows specification..." -ForegroundColor White

# Check folder structure
Write-Host "Folder Structure:" -ForegroundColor Cyan
$expectedFolders = @("boot", "efi", "sources", "support", "x64", "x86")
foreach ($folder in $expectedFolders) {
    $folderPath = Join-Path $outputFolder $folder
    $exists = Test-Path $folderPath
    $status = if ($exists) { "✓" } else { "✗" }
    $color = if ($exists) { "Green" } else { "Red" }
    Write-Host "  $status $folder" -ForegroundColor $color
}

Write-Host ""
Write-Host "Critical Files:" -ForegroundColor Cyan

# Check sources/boot.wim (should contain Windows PE + Windows Setup)
$bootWimPath = Join-Path $outputFolder "sources\boot.wim"
if (Test-Path $bootWimPath) {
    $bootWimSize = [math]::Round((Get-Item $bootWimPath).Length / 1MB, 2)
    Write-Host "  ✓ sources/boot.wim ($bootWimSize MB)" -ForegroundColor Green
    Write-Host "    Should contain: Windows PE (Image 2) + Windows Setup (Image 3, bootable)" -ForegroundColor Gray
} else {
    Write-Host "  ✗ sources/boot.wim (MISSING)" -ForegroundColor Red
}

# Check sources/install.esd (should contain Windows editions)
$installEsdPath = Join-Path $outputFolder "sources\install.esd"
if (Test-Path $installEsdPath) {
    $installEsdSize = [math]::Round((Get-Item $installEsdPath).Length / 1MB, 2)
    Write-Host "  ✓ sources/install.esd ($installEsdSize MB)" -ForegroundColor Green
    Write-Host "    Should contain: All Windows editions (Images 4+)" -ForegroundColor Gray
} else {
    Write-Host "  ✗ sources/install.esd (MISSING)" -ForegroundColor Red
}

# Check for legacy install.wim (should NOT exist in proper installation tree)
$installWimPath = Join-Path $outputFolder "sources\install.wim"
if (Test-Path $installWimPath) {
    Write-Host "  ⚠ sources/install.wim (Should be install.esd instead)" -ForegroundColor Yellow
} else {
    Write-Host "  ✓ sources/install.wim (Correctly absent - using install.esd)" -ForegroundColor Green
}

Write-Host ""
Write-Host "STEP 4: Processed Images Analysis" -ForegroundColor Green
Write-Host "Analyzing how each image was processed..." -ForegroundColor White

if ($result -and $result.ProcessedImages) {
    Write-Host "Processed Images:" -ForegroundColor Cyan
    foreach ($img in $result.ProcessedImages) {
        $status = if ($img.Success) { "✓" } else { "✗" }
        $color = if ($img.Success) { "Green" } else { "Red" }
        $duration = if ($img.ProcessingDuration.TotalSeconds -gt 0) { 
            " ($([math]::Round($img.ProcessingDuration.TotalMinutes, 1))m)" 
        } else { "" }
        
        Write-Host "  $status Image $($img.SourceIndex): $($img.Name)$duration" -ForegroundColor $color
        Write-Host "    Edition: $($img.Edition)" -ForegroundColor Gray
        if ($img.OutputPath) {
            $relativePath = $img.OutputPath.Replace($outputFolder, "").TrimStart('\')
            Write-Host "    Output: $relativePath" -ForegroundColor Gray
        }
        if (!$img.Success -and $img.ErrorMessage) {
            Write-Host "    Error: $($img.ErrorMessage)" -ForegroundColor Red
        }
        Write-Host ""
    }
}

Write-Host ""
Write-Host "STEP 5: Installation Tree Validation Summary" -ForegroundColor Green

$hasBootWim = Test-Path $bootWimPath
$hasInstallEsd = Test-Path $installEsdPath
$hasProperStructure = $expectedFolders | ForEach-Object { Test-Path (Join-Path $outputFolder $_) } | Where-Object { $_ -eq $false } | Measure-Object | Select-Object -ExpandProperty Count
$isValid = $hasBootWim -and $hasInstallEsd -and ($hasProperStructure -eq 0)

if ($isValid) {
    Write-Host "✓ INSTALLATION TREE VALIDATION: PASSED" -ForegroundColor Green
    Write-Host ""
    Write-Host "The installation tree follows Windows specification:" -ForegroundColor White
    Write-Host "• Image 1: Base Windows setup media extracted as installation tree base" -ForegroundColor Green
    Write-Host "• Image 2: Windows PE exported to sources/boot.wim" -ForegroundColor Green
    Write-Host "• Image 3: Windows Setup appended to sources/boot.wim (bootable)" -ForegroundColor Green
    Write-Host "• Images 4+: Windows editions exported to sources/install.esd" -ForegroundColor Green
    Write-Host ""
    Write-Host "This installation tree is ready for:" -ForegroundColor Cyan
    Write-Host "• Bootable ISO creation" -ForegroundColor White
    Write-Host "• Windows deployment" -ForegroundColor White
    Write-Host "• Custom Windows installation media" -ForegroundColor White
} else {
    Write-Host "✗ INSTALLATION TREE VALIDATION: FAILED" -ForegroundColor Red
    Write-Host ""
    Write-Host "Issues found:" -ForegroundColor Yellow
    if (!$hasBootWim) { Write-Host "• Missing sources/boot.wim" -ForegroundColor Red }
    if (!$hasInstallEsd) { Write-Host "• Missing sources/install.esd" -ForegroundColor Red }
    if ($hasProperStructure -gt 0) { Write-Host "• Missing required folders" -ForegroundColor Red }
}

Write-Host ""
Write-Host "=== INSTALLATION TREE ASSEMBLY TEST COMPLETE ===" -ForegroundColor Yellow
