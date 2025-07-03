# PSWindowsImageTools Cmdlet Reference

Complete reference for all cmdlets in the PSWindowsImageTools module.

## Core Image Management

### Get-WindowsImageList
Get detailed information about Windows images in WIM/ESD files.

```powershell
Get-WindowsImageList -ImagePath "install.wim" [-Advanced] [-InclusionFilter {scriptblock}] [-ExclusionFilter {scriptblock}]
```

**Parameters:**
- `ImagePath`: Path to WIM/ESD file
- `Advanced`: Include detailed metadata (slower)
- `InclusionFilter`: PowerShell scriptblock to select images
- `ExclusionFilter`: PowerShell scriptblock to exclude images

### Mount-WindowsImageList
Mount Windows images with GUID-based organization.

```powershell
Mount-WindowsImageList -ImagePath "install.wim" -Index 1 -MountPath "C:\Mount" [-ReadWrite] [-InclusionFilter {scriptblock}] [-ExclusionFilter {scriptblock}]
```

**Parameters:**
- `ImagePath`: Path to WIM/ESD file
- `Index`: Image index to mount (optional with filters)
- `MountPath`: Base mount directory
- `ReadWrite`: Mount as read-write (default: read-only)
- `InclusionFilter`: PowerShell scriptblock to select images
- `ExclusionFilter`: PowerShell scriptblock to exclude images

### Dismount-WindowsImageList
Dismount mounted Windows images with cleanup options.

```powershell
Dismount-WindowsImageList -MountPath "C:\Mount\{guid}\1" [-Save] [-CleanupDirectory]
```

**Parameters:**
- `MountPath`: Path to mounted image
- `Save`: Save changes to the image
- `CleanupDirectory`: Remove mount directory after dismount

## Windows Update Integration

### Search-WindowsUpdateCatalog
Search the Windows Update Catalog for updates.

```powershell
Search-WindowsUpdateCatalog -Query "Windows 11" [-Architecture x64] [-Products @("Windows 11")] [-MaxResults 10]
```

**Parameters:**
- `Query`: Search query string
- `Architecture`: Filter by architecture (x86, x64, arm64)
- `Products`: Filter by product names
- `MaxResults`: Maximum number of results to return

### Get-WindowsUpdateDownloadUrl
Extract download URLs from catalog search results.

```powershell
$catalogResults | Get-WindowsUpdateDownloadUrl
```

**Pipeline Input:** WindowsUpdateCatalogResult objects

### Save-WindowsUpdateCatalogResult
Download Windows updates with resume capability.

```powershell
$catalogResults | Save-WindowsUpdateCatalogResult -DestinationPath "C:\Updates" [-Verify] [-Resume]
```

**Parameters:**
- `DestinationPath`: Directory to save downloaded files
- `Verify`: Verify file integrity after download
- `Resume`: Resume interrupted downloads

### Install-WindowsUpdateFile
Install CAB/MSU files into mounted Windows images.

```powershell
Install-WindowsUpdateFile -UpdatePath "C:\Updates\KB5000001.msu" -ImagePath "C:\Mount\Image1" [-ValidateImage] [-IgnoreCheck] [-PreventPending] [-ContinueOnError]
```

**Parameters:**
- `UpdatePath`: Path to update file(s) or directory
- `ImagePath`: Path to mounted Windows image
- `ValidateImage`: Validate image before installation
- `IgnoreCheck`: Skip DISM applicability checks
- `PreventPending`: Prevent prerequisite installation
- `ContinueOnError`: Continue on individual failures

### Install-WindowsImageUpdate
Install updates from pipeline into mounted images.

```powershell
$mountedImages | Install-WindowsImageUpdate -InputObject $updatePackages
```

**Parameters:**
- `MountedImages`: MountedWindowsImage objects from pipeline
- `InputObject`: WindowsUpdatePackage objects to install

## Image Customization

### Add-SetupCompleteAction
Add custom actions to SetupComplete.cmd for first boot execution.

```powershell
Add-SetupCompleteAction -ImagePath "C:\Mount\Image1" -Command "echo Hello" [-Description "Test"] [-Priority 100] [-ContinueOnError] [-ScriptFile "script.cmd"] [-CopyFiles @("file1", "file2")] [-CopyDestination "Temp\Custom"] [-Backup]
```

**Parameters:**
- `ImagePath`: Path to mounted Windows image
- `Command`: Command(s) to execute
- `Description`: Action description for documentation
- `Priority`: Execution order (1-999, default: 100)
- `ContinueOnError`: Continue if action fails
- `ScriptFile`: Script file to copy and execute
- `CopyFiles`: Files/directories to copy to image
- `CopyDestination`: Destination path for copied files
- `Backup`: Create backup of existing SetupComplete.cmd

## Database Operations

### Set-WindowsImageDatabaseConfiguration
Configure database connection for operation tracking.

```powershell
Set-WindowsImageDatabaseConfiguration -Path "C:\Database\images.db" [-Disable]
```

**Parameters:**
- `Path`: Path to SQLite database file
- `Disable`: Disable database usage for current session

### New-WindowsImageDatabase
Create and initialize the database schema.

```powershell
New-WindowsImageDatabase [-Force]
```

**Parameters:**
- `Force`: Overwrite existing database

### Clear-WindowsImageDatabase
Clear all data from the database with confirmation.

```powershell
Clear-WindowsImageDatabase [-Force]
```

**Parameters:**
- `Force`: Skip confirmation prompt

### Search-WindowsImageDatabase
Search database records for builds, updates, and events.

```powershell
Search-WindowsImageDatabase [-BuildId "guid"] [-UpdateId "guid"] [-EventType "Download"] [-After (Get-Date).AddDays(-7)]
```

**Parameters:**
- `BuildId`: Filter by build GUID
- `UpdateId`: Filter by update GUID
- `EventType`: Filter by event type
- `After`: Filter events after date

## Format Conversion

### Convert-ESDToWindowsImage
Convert ESD files to WIM format with filtering.

```powershell
Convert-ESDToWindowsImage -ESDPath "install.esd" -OutputPath "install.wim" [-InclusionFilter {scriptblock}] [-ExclusionFilter {scriptblock}]
```

**Parameters:**
- `ESDPath`: Path to source ESD file
- `OutputPath`: Path for output WIM file
- `InclusionFilter`: PowerShell scriptblock to select images
- `ExclusionFilter`: PowerShell scriptblock to exclude images

## Utility Cmdlets

### Get-PatchTuesday
Calculate Patch Tuesday dates for planning update deployments.

```powershell
Get-PatchTuesday [-Year 2025] [-Month 6] [-After (Get-Date)] [-Count 12]
```

**Parameters:**
- `Year`: Specific year (derived from After if not specified)
- `Month`: Specific month (returns all months if not specified)
- `After`: Return dates after this date (default: today)
- `Count`: Maximum number of dates to return

### Get-WindowsReleaseInfo
Fetch comprehensive Windows release information from Microsoft sources.

```powershell
Get-WindowsReleaseInfo [-OperatingSystem "Windows 11"] [-ReleaseId "22H2"] [-BuildNumber 22621] [-KBArticle "KB5000001"] [-Version "10.0.22621"] [-LTSCOnly] [-ClientOnly] [-ServerOnly] [-Latest] [-WithKBOnly] [-After (Get-Date "2023-01-01")] [-Before (Get-Date "2024-01-01")] [-Detailed] [-ContinueOnError]
```

**Parameters:**
- `OperatingSystem`: Filter by OS (Windows 10, Windows 11, Windows Server 2019, etc.)
- `ReleaseId`: Filter by release ID (21H2, 22H2, 23H2, 24H2, etc.)
- `BuildNumber`: Filter by build number (19041, 22621, etc.)
- `KBArticle`: Filter by KB article number (KB5000001, etc.)
- `Version`: Filter by version string (10.0.22621.2428, etc.)
- `LTSCOnly`: Include only LTSC/LTSB releases
- `ClientOnly`: Include only Client operating systems
- `ServerOnly`: Include only Server operating systems
- `Latest`: Get latest release for each OS/release ID combination
- `WithKBOnly`: Return only releases that have KB articles
- `After`: Filter releases after this date
- `Before`: Filter releases before this date
- `Detailed`: Include detailed release information
- `ContinueOnError`: Continue processing on errors

## Common Patterns

### Windows Release Information Workflows

```powershell
# Find latest KB for Windows 11 22H2
$latestWin11 = Get-WindowsReleaseInfo -OperatingSystem "Windows 11" -ReleaseId "22H2" -Latest
$latestKB = $latestWin11.LatestKBArticle

# Search for that KB in Windows Update Catalog and download
$catalogResults = Search-WindowsUpdateCatalog -Query $latestKB
$catalogResults | Get-WindowsUpdateDownloadUrl | Save-WindowsUpdateCatalogResult -DestinationPath "C:\Updates"

# Correlate version to release information
$version = "10.0.22621.2428"
$releaseInfo = Get-WindowsReleaseInfo -Version $version
Write-Host "Version $version belongs to: $($releaseInfo.OperatingSystem) $($releaseInfo.ReleaseId)"

# Find all LTSC releases with KB articles
$ltscReleases = Get-WindowsReleaseInfo -LTSCOnly -WithKBOnly
$ltscReleases | ForEach-Object {
    Write-Host "$($_.OperatingSystem) $($_.ReleaseId) - Latest KB: $($_.LatestKBArticle)"
}

# Get release history for specific build
$buildReleases = Get-WindowsReleaseInfo -BuildNumber 22621 -Detailed
$buildReleases.Releases | Sort-Object AvailabilityDate | ForEach-Object {
    Write-Host "$($_.AvailabilityDate.ToString('yyyy-MM-dd')) - $($_.Version) ($($_.KBArticle))"
}
```

### Complete Image Customization Workflow

```powershell
# 1. Configure database
Set-WindowsImageDatabaseConfiguration -Path "C:\Database\images.db"
New-WindowsImageDatabase

# 2. Get and mount image
$images = Get-WindowsImageList -ImagePath "install.wim" -InclusionFilter { $_.ImageName -like "*Pro*" }
$mounted = $images | Mount-WindowsImageList -MountPath "C:\Mount" -ReadWrite

# 3. Get latest updates using release info
$latestWin11 = Get-WindowsReleaseInfo -OperatingSystem "Windows 11" -Latest -WithKBOnly
$updates = $latestWin11 | ForEach-Object {
    Search-WindowsUpdateCatalog -Query $_.LatestKBArticle -Architecture x64
} | Get-WindowsUpdateDownloadUrl | Save-WindowsUpdateCatalogResult -DestinationPath "C:\Updates"

# 4. Install updates
$updates | ForEach-Object { Install-WindowsUpdateFile -UpdatePath $_.LocalFile -ImagePath $mounted[0].MountPath }

# 5. Add custom setup actions
Add-SetupCompleteAction -ImagePath $mounted[0].MountPath -ScriptFile "setup.cmd" -Priority 50
Add-SetupCompleteAction -ImagePath $mounted[0].MountPath -Command "reg add..." -Priority 100

# 6. Dismount and save
$mounted | Dismount-WindowsImageList -Save -CleanupDirectory
```

### Batch Processing with Error Handling

```powershell
# Process multiple images with error handling
$images = Get-WindowsImageList -ImagePath "install.wim"
$mounted = $images | Mount-WindowsImageList -MountPath "C:\Mount" -ReadWrite

foreach ($image in $mounted) {
    try {
        # Install updates with error continuation
        Install-WindowsUpdateFile -UpdatePath "C:\Updates\" -ImagePath $image.MountPath -ContinueOnError
        
        # Add setup actions with error handling
        Add-SetupCompleteAction -ImagePath $image.MountPath -Command "echo Processed" -ContinueOnError
        
        # Save changes
        Dismount-WindowsImageList -MountPath $image.MountPath -Save
    }
    catch {
        Write-Warning "Failed to process $($image.ImageName): $($_.Exception.Message)"
        # Dismount without saving on error
        Dismount-WindowsImageList -MountPath $image.MountPath
    }
}
```

### Database Querying

```powershell
# Find recent downloads
$recentDownloads = Search-WindowsImageDatabase -EventType "Download" -After (Get-Date).AddDays(-7)

# Find builds with specific updates
$buildsWithUpdate = Search-WindowsImageDatabase -UpdateId "ea1c079c-952f-41cb-9a15-79799249e300"

# Clear old data
Clear-WindowsImageDatabase -Force
```

## ADK and Optional Component Management

### Get-ADKInstallation
Detect installed Windows Assessment and Deployment Kit (ADK) installations.

```powershell
Get-ADKInstallation [-Latest] [-MinimumVersion <Version>] [-RequireWinPE] [-RequireDeploymentTools] [-RequiredArchitecture <String>]
```

**Parameters:**
- `Latest`: Return only the latest version if multiple installations found
- `MinimumVersion`: Minimum required ADK version
- `RequireWinPE`: Require WinPE add-on to be installed
- `RequireDeploymentTools`: Require Deployment Tools to be installed
- `RequiredArchitecture`: Specific architecture support required (x86, amd64, arm64)

### Get-WinPEOptionalComponent
Get available WinPE Optional Components from ADK installation.

```powershell
Get-WinPEOptionalComponent [-ADKInstallation <ADKInfo>] [-Architecture <String>] [-IncludeLanguagePacks] [-Category <String[]>] [-Name <String[]>]
```

**Parameters:**
- `ADKInstallation`: ADK installation to scan (auto-detected if not specified)
- `Architecture`: Target architecture (x86, amd64, arm64) - default: amd64
- `IncludeLanguagePacks`: Include language pack components
- `Category`: Filter by category (Networking, Storage, Scripting, etc.)
- `Name`: Filter by name pattern (supports wildcards)

### Add-WinPEOptionalComponent
Install WinPE Optional Components into mounted boot images.

```powershell
Add-WinPEOptionalComponent -MountedImages <MountedWindowsImage[]> -Components <WinPEOptionalComponent[]> [-ContinueOnError]
```

**Parameters:**
- `MountedImages`: Mounted boot images from Mount-WindowsImageList
- `Components`: Optional components from Get-WinPEOptionalComponent
- `ContinueOnError`: Continue if individual components fail

### Install-ADK
Download and install the latest Windows ADK silently with automatic patch detection.

```powershell
Install-ADK [-InstallPath <String>] [-IncludeWinPE] [-IncludeDeploymentTools] [-Force]
```

**Parameters:**
- `InstallPath`: Custom installation path for ADK
- `IncludeWinPE`: Include WinPE add-on in the installation (default: true)
- `IncludeDeploymentTools`: Include Deployment Tools in the installation (default: true)
- `Force`: Force installation even if ADK is already installed (default: skip if present)

**Features:**
- Automatically parses Microsoft's ADK download page for latest version
- Downloads and installs both ADK and WinPE add-on
- Detects and applies available patches (ZIP files with MSP files)
- Enhanced process monitoring with command line display and timeouts

### Uninstall-ADK
Uninstall Windows ADK and WinPE add-on silently.

```powershell
Uninstall-ADK [-All] [-Force]
```

**Parameters:**
- `All`: Remove all ADK installations found on the system
- `Force`: Force uninstallation without confirmation prompts

## ADK and Optional Component Examples

### ADK Installation and Management

```powershell
# Install latest ADK with WinPE and Deployment Tools
$adk = Install-ADK -IncludeWinPE -IncludeDeploymentTools

# Install to custom path
$adk = Install-ADK -InstallPath "C:\CustomADK" -IncludeWinPE

# Skip installation if already present (default behavior)
$adk = Install-ADK

# Uninstall latest ADK
Uninstall-ADK

# Uninstall all ADK installations
Uninstall-ADK -All -Force
```

### Basic ADK Detection and Component Installation

```powershell
# 1. Detect or install ADK
$adk = Get-ADKInstallation -Latest -RequireWinPE
if (-not $adk) {
    $adk = Install-ADK -IncludeWinPE -IncludeDeploymentTools
}

# 2. Get available components
$components = Get-WinPEOptionalComponent -ADKInstallation $adk -Category Scripting

# 3. Mount boot image
$mounted = Mount-WindowsImageList -ImagePath "boot.wim" -Index 2 -MountPath "C:\Mount" -ReadWrite

# 4. Install components
$results = Add-WinPEOptionalComponent -MountedImages $mounted -Components $components

# 5. Dismount and save
Dismount-WindowsImageList -MountPath $mounted[0].MountPath -Save
```

### Advanced Component Management

```powershell
# Find PowerShell and .NET components
$scriptingComponents = Get-ADKInstallation -Latest -RequireWinPE |
    Get-WinPEOptionalComponent -Name "*PowerShell*","*NetFx*" -Architecture amd64

# Install with comprehensive error handling
$installResults = Add-WinPEOptionalComponent -MountedImages $mounted -Components $scriptingComponents -ContinueOnError

# Review results
foreach ($result in $installResults) {
    Write-Host "Image: $($result.MountedImage.ImageName)"
    Write-Host "  Success: $($result.SuccessfulComponents.Count)"
    Write-Host "  Failed: $($result.FailedComponents.Count)"
    Write-Host "  Success Rate: $($result.SuccessRate.ToString('F1'))%"
}
```

### Component Discovery and Filtering

```powershell
# Get all components with size information
$allComponents = Get-WinPEOptionalComponent -IncludeLanguagePacks |
    Sort-Object Category, Name

# Filter by category
$networkingComponents = Get-WinPEOptionalComponent -Category Networking

# Find components by pattern
$storageComponents = Get-WinPEOptionalComponent -Name "*Storage*","*WMI*"

# Show component details
$allComponents | Format-Table Name, Category, Architecture, SizeFormatted, IsLanguagePack
```

This reference covers all major cmdlets and common usage patterns for PSWindowsImageTools.
