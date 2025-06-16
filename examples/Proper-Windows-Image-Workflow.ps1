# ============================================================================
# PROPER WINDOWS IMAGE PROCESSING WORKFLOW
# ============================================================================
# This example demonstrates the correct architectural approach for processing
# Windows images using the PSWindowsImageTools module.
#
# The key principle: Use Get-WindowsImageList → Mount-WindowsImageList → 
# Process (Install Updates, Add Drivers, etc.) → Dismount-WindowsImageList
# ============================================================================

# Import the module
Import-Module PSWindowsImageTools

# ============================================================================
# STEP 1: GET IMAGES FROM ANY SOURCE (WIM/ESD/ISO)
# ============================================================================

Write-Host "=== STEP 1: Getting Windows Images ===" -ForegroundColor Cyan

# Get images from a WIM file
$wimImages = Get-WindowsImageList -ImagePath "C:\Images\install.wim"
Write-Host "Found $($wimImages.Count) images in WIM file" -ForegroundColor Green

# Get images from an ESD file  
$esdImages = Get-WindowsImageList -ImagePath "C:\Images\install.esd"
Write-Host "Found $($esdImages.Count) images in ESD file" -ForegroundColor Green

# Get images from an ISO file
$isoImages = Get-WindowsImageList -ImagePath "C:\Images\Windows11.iso"
Write-Host "Found $($isoImages.Count) images in ISO file" -ForegroundColor Green

# Filter images using scriptblocks (proper PowerShell way)
$proImages = $wimImages | Where-Object { $_.Name -like "*Pro*" }
$enterpriseImages = $wimImages | Where-Object { $_.Edition -eq "Enterprise" }

Write-Host "Filtered to $($proImages.Count) Pro images and $($enterpriseImages.Count) Enterprise images" -ForegroundColor Green

# ============================================================================
# STEP 2: MOUNT THE IMAGES YOU WANT TO WORK WITH
# ============================================================================

Write-Host "`n=== STEP 2: Mounting Windows Images ===" -ForegroundColor Cyan

# Mount specific images (filtering was done in Get-WindowsImageList)
$mountedImages = $proImages | Mount-WindowsImageList -ReadWrite
Write-Host "Mounted $($mountedImages.Count) Pro images for processing" -ForegroundColor Green

# Alternative: Mount Enterprise images
$mountedEnterpriseImages = $enterpriseImages | Mount-WindowsImageList -ReadWrite
Write-Host "Mounted $($mountedEnterpriseImages.Count) Enterprise images" -ForegroundColor Green

# ============================================================================
# STEP 3: SEARCH AND DOWNLOAD WINDOWS UPDATES
# ============================================================================

Write-Host "`n=== STEP 3: Searching and Downloading Windows Updates ===" -ForegroundColor Cyan

# Option A: Search the Windows Update Catalog (online)
$catalogResults = Search-WindowsUpdateCatalog -Query "Windows 11" -Architecture "AMD64" -MaxResults 10 -IncludeDownloadUrls
Write-Host "Found $($catalogResults.Count) catalog results from online catalog" -ForegroundColor Green

# Option B: Search local database for previously downloaded updates (offline)
$databaseResults = Search-WindowsImageDatabase -ObjectType 'Updates' -Architecture "AMD64" -DownloadedOnly
Write-Host "Found $($databaseResults.Rows.Count) previously downloaded updates in database" -ForegroundColor Green

# Download new catalog results to get packages
$updatePackages = $catalogResults | Save-WindowsUpdateCatalogResult -DestinationPath "C:\Updates" -Verify
Write-Host "Downloaded $($updatePackages.Count) update packages" -ForegroundColor Green

# ============================================================================
# STEP 4: INSTALL UPDATE PACKAGES ON MOUNTED IMAGES
# ============================================================================

Write-Host "`n=== STEP 4: Installing Update Packages on Mounted Images ===" -ForegroundColor Cyan

# Install update packages on mounted images (PROPER WAY - no shell-outs!)
$updatedImages = $mountedImages | Install-WindowsImageUpdate -Updates $updatePackages
Write-Host "Installed update packages on $($updatedImages.Count) images" -ForegroundColor Green

# Check results
$successfulUpdates = $updatedImages | Where-Object { $_.LastUpdateResult.Success }
$failedUpdates = $updatedImages | Where-Object { $_.LastUpdateResult.Success -eq $false }

Write-Host "Successful: $($successfulUpdates.Count), Failed: $($failedUpdates.Count)" -ForegroundColor Green

# ============================================================================
# STEP 5: ADDITIONAL PROCESSING ON THE SAME MOUNTED IMAGES
# ============================================================================

Write-Host "`n=== STEP 5: Additional Processing ===" -ForegroundColor Cyan

# Add drivers to the same mounted images
# $updatedImages | Add-WindowsImageDriver -DriverPath "C:\Drivers\Network"

# Install/Remove AppX packages
# $updatedImages | Add-WindowsImageAppxPackage -PackagePath "C:\Apps\MyApp.appx"
# $updatedImages | Remove-WindowsImageAppxPackage -PackageName "Microsoft.BingWeather"

# Enable/Disable Windows Features
# $updatedImages | Enable-WindowsImageFeature -FeatureName "IIS-WebServer"
# $updatedImages | Disable-WindowsImageFeature -FeatureName "WindowsMediaPlayer"

# Add Features on Demand
# $updatedImages | Add-WindowsImageCapability -CapabilityName "Language.Basic~~~en-US~0.0.1.0"

# Copy files to images
# $updatedImages | Copy-ItemToWindowsImage -SourcePath "C:\CustomFiles" -DestinationPath "C:\Windows\Setup\Scripts"

Write-Host "Additional processing would happen here..." -ForegroundColor Yellow

# ============================================================================
# STEP 6: COMMIT AND DISMOUNT IMAGES
# ============================================================================

Write-Host "`n=== STEP 6: Committing and Dismounting Images ===" -ForegroundColor Cyan

# Commit changes and dismount
$dismountedImages = $updatedImages | Dismount-WindowsImageList -Save
Write-Host "Dismounted $($dismountedImages.Count) images with changes saved" -ForegroundColor Green

# Alternative: Dismount without saving changes
# $discardedImages = $mountedImages | Dismount-WindowsImageList
# Write-Host "Dismounted images without saving changes" -ForegroundColor Yellow

# ============================================================================
# STEP 7: EXPORT OR CONVERT RESULTS
# ============================================================================

Write-Host "`n=== STEP 7: Export Results ===" -ForegroundColor Cyan

# Export processed images to a new WIM
# $finalImages = Get-WindowsImageList -ImagePath "C:\Images\install.wim"
# $finalImages | Export-WindowsImageList -DestinationPath "C:\Output\processed.wim" -CompressionType "Maximum"

# Convert to ISO for deployment
# Convert-ESDToWindowsImage -SourcePath "C:\Images\install.esd" -OutputPath "C:\Output\CustomWindows11.iso" -Mode "ISO"

Write-Host "Export operations would happen here..." -ForegroundColor Yellow

# ============================================================================
# SUMMARY OF PROPER ARCHITECTURE
# ============================================================================

Write-Host "`n=== ARCHITECTURAL PRINCIPLES ===" -ForegroundColor Magenta
Write-Host "✅ Use Get-WindowsImageList to get image information from any source" -ForegroundColor Green
Write-Host "✅ Use Mount-WindowsImageList to mount images with proper management" -ForegroundColor Green  
Write-Host "✅ Use MountedWindowsImage objects in the pipeline for all processing" -ForegroundColor Green
Write-Host "✅ Use proper DISM API calls instead of shell-outs" -ForegroundColor Green
Write-Host "✅ Use Dismount-WindowsImageList to properly clean up mounts" -ForegroundColor Green
Write-Host "✅ All cmdlets work together through the pipeline" -ForegroundColor Green

Write-Host "`n❌ AVOID THESE ANTI-PATTERNS:" -ForegroundColor Red
Write-Host "❌ Don't mount images in individual cmdlets" -ForegroundColor Red
Write-Host "❌ Don't use shell-out DISM commands" -ForegroundColor Red
Write-Host "❌ Don't create hardcoded mount paths" -ForegroundColor Red
Write-Host "❌ Don't duplicate mount/dismount logic across cmdlets" -ForegroundColor Red
Write-Host "❌ Don't chain DISM commands with && operators" -ForegroundColor Red
Write-Host "❌ Don't put filters on Mount-WindowsImageList (use Get-WindowsImageList filters)" -ForegroundColor Red
Write-Host "❌ Don't use string paths (use DirectoryInfo/FileInfo)" -ForegroundColor Red

Write-Host "`n🎯 RESULT: Clean, maintainable, PowerShell-native workflow!" -ForegroundColor Cyan

Write-Host "`n=== NEW UPDATE WORKFLOW EXAMPLES ===" -ForegroundColor Magenta

Write-Host "`n✅ PIPELINE WORKFLOW:" -ForegroundColor Green
Write-Host @"
# Search → Download → Install in one pipeline
"Windows 11", "KB5000001" | Search-WindowsUpdateCatalog -Architecture "AMD64" | Save-WindowsUpdateCatalogResult -DestinationPath "C:\Updates" | Install-WindowsImageUpdate -MountedImages `$mountedImages
"@ -ForegroundColor White

Write-Host "`n✅ PARAMETER WORKFLOW:" -ForegroundColor Green
Write-Host @"
# Step-by-step with parameters
`$catalogResults = Search-WindowsUpdateCatalog -Query @("Windows 11", "Security Update") -Architecture "AMD64"
`$packages = Save-WindowsUpdateCatalogResult -CatalogResults `$catalogResults -DestinationPath "C:\Updates" -Verify
`$updatedImages = Install-WindowsImageUpdate -MountedImages `$mountedImages -Updates `$packages
"@ -ForegroundColor White

Write-Host "`n✅ MIXED WORKFLOW:" -ForegroundColor Green
Write-Host @"
# Search with parameters, download and install via pipeline
`$catalogResults = Search-WindowsUpdateCatalog -Query "Windows 11" -MaxResults 20
`$updatedImages = `$catalogResults | Save-WindowsUpdateCatalogResult | Install-WindowsImageUpdate -MountedImages `$mountedImages
"@ -ForegroundColor White

Write-Host "`n=== DATABASE SEARCH EXAMPLES ===" -ForegroundColor Magenta

Write-Host "`n✅ SEARCH DOWNLOADED UPDATES:" -ForegroundColor Green
Write-Host @"
# Find all downloaded updates
`$updates = Search-WindowsImageDatabase -ObjectType 'Updates' -DownloadedOnly

# Find specific KB updates
`$kb5000001 = Search-WindowsImageDatabase -ObjectType 'Updates' -KBNumber 'KB5000001'

# Find security updates for AMD64
`$securityUpdates = Search-WindowsImageDatabase -ObjectType 'Updates' -Architecture 'AMD64' -Classification 'Security'
"@ -ForegroundColor White

Write-Host "`n✅ SEARCH OTHER OBJECT TYPES:" -ForegroundColor Green
Write-Host @"
# Find processed images
`$images = Search-WindowsImageDatabase -ObjectType 'Images' -DateFrom (Get-Date).AddDays(-7)

# Find recent operations
`$operations = Search-WindowsImageDatabase -ObjectType 'Operations' -DateFrom (Get-Date).AddHours(-24)

# Custom query for advanced searches
`$customResults = Search-WindowsImageDatabase -CustomQuery "SELECT * FROM Updates WHERE SizeInBytes > 100000000"
"@ -ForegroundColor White
