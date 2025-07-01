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

This reference covers all major cmdlets and common usage patterns for PSWindowsImageTools.
