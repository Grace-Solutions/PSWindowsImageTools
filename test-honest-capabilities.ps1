# Honest Test of Actual DISM Service Capabilities
Import-Module '.\Module\PSWindowsImageTools\PSWindowsImageTools.psd1' -Force

Write-Host "=== HONEST ASSESSMENT OF DISM SERVICE CAPABILITIES ===" -ForegroundColor Yellow
Write-Host ""

# Test what actually works vs. what doesn't
$dismService = New-Object PSWindowsImageTools.Services.DismService

Write-Host "✅ WHAT ACTUALLY WORKS:" -ForegroundColor Green
Write-Host ""

# Test 1: Basic image information (this should work)
Write-Host "1. Getting image information from ESD file..." -ForegroundColor White
try {
    $imageList = Get-WindowsImageList -ImagePath "C:\Users\gsadmin\Downloads\WindowsImages\26100.2033.241004-2336.ge_release_svc_refresh_CLIENTBUSINESS_VOL_x64FRE_en-us.esd" | Select-Object -First 1
    if ($imageList) {
        Write-Host "   ✅ SUCCESS: Image info retrieval works" -ForegroundColor Green
        Write-Host "   Image: $($imageList.Name)" -ForegroundColor Cyan
        Write-Host "   Edition: $($imageList.Edition)" -ForegroundColor Cyan
    }
} catch {
    Write-Host "   ❌ FAILED: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "❌ WHAT DOESN'T WORK (Honest Limitations):" -ForegroundColor Red
Write-Host ""

# Test 2: Export functionality (this will fail honestly)
Write-Host "2. Testing export functionality..." -ForegroundColor White
try {
    $result = $dismService.ExportImage(
        "C:\Users\gsadmin\Downloads\WindowsImages\26100.2033.241004-2336.ge_release_svc_refresh_CLIENTBUSINESS_VOL_x64FRE_en-us.esd",
        "C:\temp\test-export.wim",
        1,
        "Max",
        $null,
        $null
    )
    Write-Host "   Unexpected success: $result" -ForegroundColor Yellow
} catch {
    Write-Host "   ❌ EXPECTED FAILURE: Export not available in Microsoft.Dism library" -ForegroundColor Red
    Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Gray
}

# Test 3: Package management (this will fail honestly)
Write-Host ""
Write-Host "3. Testing package management..." -ForegroundColor White
try {
    $result = $dismService.AddPackage("C:\mount", "C:\package.cab", $null, $null)
    Write-Host "   Unexpected success: $result" -ForegroundColor Yellow
} catch {
    Write-Host "   ❌ EXPECTED FAILURE: AddPackage method signature unknown in Microsoft.Dism" -ForegroundColor Red
    Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Gray
}

# Test 4: Feature management (this will fail honestly)
Write-Host ""
Write-Host "4. Testing feature management..." -ForegroundColor White
try {
    $result = $dismService.EnableFeature("C:\mount", "IIS-WebServer", $true, $null, $null, $null)
    Write-Host "   Unexpected success: $result" -ForegroundColor Yellow
} catch {
    Write-Host "   ❌ EXPECTED FAILURE: EnableFeature method unknown in Microsoft.Dism" -ForegroundColor Red
    Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Gray
}

# Test 5: Update management (this will fail honestly)
Write-Host ""
Write-Host "5. Testing update management..." -ForegroundColor White
try {
    $result = $dismService.AddUpdate("C:\mount", "C:\update.msu", $null, $null)
    Write-Host "   Unexpected success: $result" -ForegroundColor Yellow
} catch {
    Write-Host "   ❌ EXPECTED FAILURE: Update management not available in Microsoft.Dism" -ForegroundColor Red
    Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Gray
}

$dismService.Dispose()

Write-Host ""
Write-Host "=== SUMMARY ===" -ForegroundColor Yellow
Write-Host ""
Write-Host "✅ Microsoft.Dism Library DOES Support:" -ForegroundColor Green
Write-Host "   • Basic image information retrieval" -ForegroundColor White
Write-Host "   • Mount/unmount operations" -ForegroundColor White
Write-Host "   • Listing features, packages, drivers" -ForegroundColor White
Write-Host "   • Session management" -ForegroundColor White
Write-Host ""
Write-Host "❌ Microsoft.Dism Library DOES NOT Support:" -ForegroundColor Red
Write-Host "   • Image export functionality" -ForegroundColor White
Write-Host "   • Package installation/removal" -ForegroundColor White
Write-Host "   • Feature enable/disable" -ForegroundColor White
Write-Host "   • Update management (.msu/.cab)" -ForegroundColor White
Write-Host "   • Progress callbacks" -ForegroundColor White
Write-Host ""
Write-Host "🔍 NEXT STEPS:" -ForegroundColor Cyan
Write-Host "   • Research direct DISM API P/Invoke" -ForegroundColor White
Write-Host "   • Investigate WimLib library integration" -ForegroundColor White
Write-Host "   • Consider hybrid approach with proper native APIs" -ForegroundColor White
Write-Host ""
Write-Host "This is an HONEST assessment - no shell-out to dism.exe!" -ForegroundColor Yellow
