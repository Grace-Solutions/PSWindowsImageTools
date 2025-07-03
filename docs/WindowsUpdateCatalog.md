# Windows Update Catalog Integration

This document describes how PSWindowsImageTools integrates with the Windows Update Catalog to search, download, and install Windows updates.

## Overview

PSWindowsImageTools provides comprehensive Windows Update Catalog integration through several cmdlets:

- `Search-WindowsUpdateCatalog` - Search for updates in the catalog
- `Get-WindowsUpdateDownloadUrl` - Extract download URLs from catalog results
- `Save-WindowsUpdateCatalogResult` - Download update files with resume capability
- `Install-WindowsImageUpdate` - Unified cmdlet for installing updates (supports both file paths and pipeline objects)

## Windows Update Catalog API Behavior

### Important Limitations

The Windows Update Catalog API has specific behaviors that affect how we implement search and filtering:

1. **No Server-Side Filtering**: The API returns all results without server-side filtering capability
2. **Client-Side Processing Required**: All filtering must be done client-side after parsing results
3. **Large Result Sets**: Searches can return hundreds of results that need local processing

### Search Implementation

```powershell
# Basic search - returns all matching results
Search-WindowsUpdateCatalog -Query 'Windows 11'

# Filtered search - client-side filtering applied
Search-WindowsUpdateCatalog -Query 'Windows 11' -Architecture x64 -MaxResults 10

# Product-specific search
Search-WindowsUpdateCatalog -Query 'Cumulative' -Products 'Windows 11'
```

## Catalog Parsing Specification

The Windows Update Catalog parsing follows the specification documented at:
https://github.com/Grace-Solutions/WindowsUpdateCatalogScraper/blob/main/docs/WindowsUpdateCatalogScrapingGuide.md

### Key Parsing Elements

1. **Update Identification**
   - UpdateId (GUID)
   - KB Number extraction
   - Title parsing

2. **Metadata Extraction**
   - Product information
   - Architecture detection
   - Language support
   - File sizes

3. **Download URL Processing**
   - Multiple download URLs per update
   - URL validation and accessibility
   - File naming conventions

## Download Process

### Resume Capability

The `Save-WindowsUpdateCatalogResult` cmdlet supports download resume:

```powershell
# Download with automatic resume on failure
$packages = Search-WindowsUpdateCatalog -Query 'Windows 11 Cumulative' |
    Get-WindowsUpdateDownloadUrl |
    Save-WindowsUpdateCatalogResult -DestinationPath "C:\Updates"
```

### Progress Tracking

Downloads include comprehensive progress tracking:
- Individual file progress
- Overall batch progress
- Size-aware progress reporting
- Intelligent unit formatting (B, KB, MB, GB, TB)

### Database Integration

When database functionality is enabled, all download operations are automatically tracked:

```powershell
# Configure database
Set-WindowsImageDatabaseConfiguration -Path "C:\Database\updates.db"
New-WindowsImageDatabase

# Downloads are automatically logged to database
$packages = Save-WindowsUpdateCatalogResult -DestinationPath "C:\Updates" $catalogResults
```

## Update Installation

### Direct File Installation

Install CAB/MSU files directly into mounted images:

```powershell
# Install single update
Install-WindowsUpdateFile -UpdatePath "C:\Updates\KB5000001.msu" -ImagePath "C:\Mount\Image1"

# Install multiple updates from directory
Install-WindowsUpdateFile -UpdatePath "C:\Updates\" -ImagePath "C:\Mount\Image1" -ContinueOnError

# Install with validation
Install-WindowsUpdateFile -UpdatePath "C:\Updates\*.cab" -ImagePath "C:\Mount\Image1" -ValidateImage
```

### Pipeline Installation

Install updates from the download pipeline:

```powershell
# Complete workflow: Search → Download → Install
Search-WindowsUpdateCatalog -Query 'Windows 11 Cumulative' -Architecture x64 |
    Get-WindowsUpdateDownloadUrl |
    Save-WindowsUpdateCatalogResult -DestinationPath "C:\Updates" |
    ForEach-Object { Install-WindowsUpdateFile -UpdatePath $_.LocalFile -ImagePath "C:\Mount\Image1" }
```

### Boot Image Updates

Boot images can be updated using the same process:

```powershell
# Mount boot image
Mount-WindowsImageList -ImagePath "boot.wim" -Index 2 -MountPath "C:\Mount\Boot"

# Install cumulative update into boot image
Install-WindowsUpdateFile -UpdatePath "C:\Updates\KB5000001.msu" -ImagePath "C:\Mount\Boot" -ValidateImage

# Dismount and save
Dismount-WindowsImageList -MountPath "C:\Mount\Boot" -Save
```

## Error Handling

### Common Issues

1. **Network Connectivity**: Download failures due to network issues
2. **Disk Space**: Insufficient space for large updates
3. **File Corruption**: Incomplete downloads or corrupted files
4. **DISM Errors**: Update installation failures

### Mitigation Strategies

1. **Resume Downloads**: Automatic resume on network failures
2. **Space Validation**: Pre-download space checking
3. **File Verification**: SHA256 hash validation (when available)
4. **Error Continuation**: ContinueOnError parameter for batch operations

## Performance Considerations

### Large Update Sets

When processing large numbers of updates:

1. Use `-MaxResults` to limit initial search results
2. Enable database tracking for progress monitoring
3. Use `-ContinueOnError` for resilient batch processing
4. Monitor disk space during downloads

### Network Optimization

1. Downloads use HTTP range requests for resume capability
2. Progress callbacks minimize UI blocking
3. Parallel processing where appropriate

## Integration Examples

### Complete Image Customization

```powershell
# 1. Search and download updates
$updates = Search-WindowsUpdateCatalog -Query 'Windows 11 Cumulative' -Architecture x64 -MaxResults 5 |
    Get-WindowsUpdateDownloadUrl |
    Save-WindowsUpdateCatalogResult -DestinationPath "C:\Updates"

# 2. Mount image
$mountedImages = Mount-WindowsImageList -ImagePath "install.wim" -Index 1 -MountPath "C:\Mount"

# 3. Install updates
$updates | ForEach-Object {
    Install-WindowsImageUpdate -UpdatePath $_.LocalFile -ImagePath $mountedImages[0].MountPath -ValidateImage
}

# 4. Add custom setup actions
Add-SetupCompleteAction -ImagePath $mountedImages[0].MountPath -Command "echo Updates installed" -Description "Update confirmation"

# 5. Dismount and save
Dismount-WindowsImageList -MountPath $mountedImages[0].MountPath -Save
```

This workflow provides complete Windows image customization with update integration and custom deployment actions.
