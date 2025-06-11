# PSWindowsImageTools

A comprehensive PowerShell module for Windows image customization, providing tools for working with ISO, WIM, and ESD files, applying customizations through recipes, and managing Windows Update integration.

## Features

- **Image Inventory**: Get detailed information about Windows images with optional advanced metadata
- **Format Conversion**: Convert ESD files to WIM format with filtering capabilities
- **Recipe-Based Customization**: Apply comprehensive customizations using JSON recipes
- **Update Integration**: Search Windows Update Catalog and integrate updates
- **Database Tracking**: SQLite-based tracking of builds, updates, and processing events
- **Mount Management**: Automated mounting with GUID-based cleanup

## Quick Start

```powershell
# Import the module
Import-Module PSWindowsImageTools

# Get image information
$images = Get-WindowsImageList -ImagePath "C:\Images\install.wim"

# Create a customization recipe
New-WindowsImageBuildRecipe -OutputPath "C:\Recipes\custom.json" -IncludeDefaults

# Apply customizations
$recipe = Get-Content "C:\Recipes\custom.json" | ConvertFrom-Json
New-WindowsImageBuild -InputObject $images[0] -Recipe $recipe
```

## Requirements

- Windows 10/11 or Windows Server 2016+
- PowerShell 5.1 or PowerShell 7+
- Administrative privileges for image operations
- .NET Framework 4.8 or .NET 6+