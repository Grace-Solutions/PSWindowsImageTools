# PSWindowsImageTools

A comprehensive PowerShell module for Windows image customization and management, providing enterprise-grade tools for Windows Update integration, image customization, and automated deployment.

## Features

- **🔍 Image Management**: Get detailed information about Windows images with advanced metadata
- **📦 Update Integration**: Search, download, and install Windows updates with resume capability
- **🛠️ Image Customization**: Install updates and add custom SetupComplete actions
- **🗄️ Database Tracking**: SQLite-based tracking of operations with comprehensive logging
- **🔧 Mount Management**: Automated mounting with GUID-based organization and cleanup
- **📊 Progress Tracking**: Real-time progress with intelligent size formatting and statistics
- **🎯 Format Conversion**: Convert ESD files to WIM format with filtering capabilities

## Quick Start

```powershell
# Import the module
Import-Module PSWindowsImageTools

# Configure database (optional)
Set-WindowsImageDatabaseConfiguration -Path "C:\Database\images.db"
New-WindowsImageDatabase

# Get image information
$images = Get-WindowsImageList -ImagePath "C:\Images\install.wim"

# Download Windows updates
$updates = Search-WindowsUpdateCatalog -Query "Windows 11 Cumulative" -Architecture x64 |
    Get-WindowsUpdateDownloadUrl |
    Save-WindowsUpdateCatalogResult -DestinationPath "C:\Updates"

# Mount image and install updates
$mounted = $images | Mount-WindowsImageList -MountPath "C:\Mount" -ReadWrite
$updates | ForEach-Object { Install-WindowsUpdateFile -UpdatePath $_.LocalFile -ImagePath $mounted[0].MountPath }

# Add custom setup actions
Add-SetupCompleteAction -ImagePath $mounted[0].MountPath -Command "echo Custom setup complete" -Description "Post-install message"

# Dismount and save
$mounted | Dismount-WindowsImageList -Save
```

## Documentation

- **[Cmdlet Reference](docs/CmdletReference.md)** - Complete cmdlet documentation and examples
- **[Windows Update Catalog](docs/WindowsUpdateCatalog.md)** - Update search, download, and installation
- **[Image Customization](docs/ImageCustomization.md)** - Update installation and SetupComplete automation
- **[Changelog](docs/CHANGELOG.md)** - Version history and changes

## Key Cmdlets

### Core Image Management
- `Get-WindowsImageList` - Get detailed Windows image information
- `Mount-WindowsImageList` - Mount images with GUID-based organization
- `Dismount-WindowsImageList` - Dismount with save and cleanup options

### Windows Update Integration
- `Search-WindowsUpdateCatalog` - Search Windows Update Catalog
- `Get-WindowsUpdateDownloadUrl` - Extract download URLs from results
- `Save-WindowsUpdateCatalogResult` - Download with resume capability
- `Install-WindowsUpdateFile` - Install CAB/MSU files into mounted images

### Image Customization
- `Add-SetupCompleteAction` - Add custom first-boot actions
- `Convert-ESDToWindowsImage` - Convert ESD to WIM format

### Database Operations
- `Set-WindowsImageDatabaseConfiguration` - Configure operation tracking
- `New-WindowsImageDatabase` - Initialize database schema
- `Search-WindowsImageDatabase` - Query operation history

## Examples

### Complete Image Customization
```powershell
# Download latest Windows 11 updates
$updates = Search-WindowsUpdateCatalog -Query "Windows 11 Cumulative" -Architecture x64 -MaxResults 3 |
    Get-WindowsUpdateDownloadUrl |
    Save-WindowsUpdateCatalogResult -DestinationPath "C:\Updates"

# Mount Windows image
$mounted = Mount-WindowsImageList -ImagePath "install.wim" -Index 1 -MountPath "C:\Mount" -ReadWrite

# Install updates
$updates | ForEach-Object { Install-WindowsUpdateFile -UpdatePath $_.LocalFile -ImagePath $mounted[0].MountPath -ValidateImage }

# Add enterprise configuration
Add-SetupCompleteAction -ImagePath $mounted[0].MountPath -ScriptFile "enterprise-setup.cmd" -Priority 50
Add-SetupCompleteAction -ImagePath $mounted[0].MountPath -Command "reg add HKLM\Software\..." -Priority 100

# Save and dismount
Dismount-WindowsImageList -MountPath $mounted[0].MountPath -Save
```

### Batch Processing with Database Tracking
```powershell
# Configure database
Set-WindowsImageDatabaseConfiguration -Path "C:\Database\operations.db"
New-WindowsImageDatabase

# Process multiple images
Get-WindowsImageList -ImagePath "install.wim" |
    Mount-WindowsImageList -MountPath "C:\Mount" -ReadWrite |
    ForEach-Object {
        Install-WindowsUpdateFile -UpdatePath "C:\Updates\" -ImagePath $_.MountPath -ContinueOnError
        Add-SetupCompleteAction -ImagePath $_.MountPath -Command "echo Processed" -Description "Batch processing"
        Dismount-WindowsImageList -MountPath $_.MountPath -Save
    }

# Query operation history
Search-WindowsImageDatabase -EventType "Download" -After (Get-Date).AddDays(-7)
```

## Requirements

- Windows 10/11 or Windows Server 2016+
- PowerShell 5.1 or PowerShell 7+
- Administrative privileges for image operations
- .NET Framework 4.8 or .NET 6+