# PSWindowsImageTools

A comprehensive PowerShell module for Windows image management, ADK automation, and deployment workflows. Streamline your Windows deployment process with powerful cmdlets for image customization, update management, and component installation.

## 🚀 Key Features

### 🖼️ **Windows Image Management**
- Mount, modify, and manage Windows WIM/ESD images
- Full pipeline support for batch operations
- Automatic cleanup and error handling
- Database tracking for operation history

### 📦 **Windows Update Integration**
- Search Microsoft Update Catalog with advanced filtering
- Download updates with resume capability and integrity verification
- Install updates into mounted images with progress tracking
- Support for both file-based and pipeline workflows

### 🛠️ **ADK Management & Automation**
- Automatic detection and installation of latest Windows ADK
- Dynamic parsing of Microsoft's download pages
- WinPE Optional Component management with validation
- Enhanced process monitoring with command-line transparency

### 🔧 **Advanced Image Customization**
- Registry operations with hive mounting/unmounting
- Driver integration with INF parsing and hardware ID extraction
- Autopilot configuration management
- Unattend.xml creation and modification
- AppX package removal with regex filtering
- Custom setup actions and first-boot scripts

### 📊 **Enterprise Features**
- SQLite database for operation tracking and inventory
- Windows release information and KB correlation
- Patch Tuesday automation and scheduling
- Comprehensive logging and progress reporting

## 🏃‍♂️ Quick Start

```powershell
# Import the module
Import-Module PSWindowsImageTools

# Install latest ADK automatically
Install-ADK -IncludeWinPE -IncludeDeploymentTools

# Get image information
$images = Get-WindowsImageList -ImagePath "C:\Images\install.wim"

# Search and download latest updates
$updates = Search-WindowsUpdateCatalog -Query "Windows 11 Cumulative" -Architecture x64 |
    Get-WindowsUpdateDownloadUrl |
    Save-WindowsUpdateCatalogResult -DestinationPath "C:\Updates"

# Mount, customize, and update image
$mounted = $images | Mount-WindowsImageList -MountPath "C:\Mount" -ReadWrite
$mounted | Install-WindowsImageUpdate -UpdatePackages $updates
$mounted | Dismount-WindowsImageList -Save
```

## 📋 Complete Cmdlet Reference

### **ADK & Component Management**
| Cmdlet | Description |
|--------|-------------|
| `Get-ADKInstallation` | Detect installed Windows ADK versions |
| `Install-ADK` | Download and install latest ADK with patches |
| `Uninstall-ADK` | Remove ADK installations |
| `Get-WinPEOptionalComponent` | Discover available WinPE components |
| `Add-WinPEOptionalComponent` | Install components into boot images |

### **Image Management**
| Cmdlet | Description |
|--------|-------------|
| `Get-WindowsImageList` | Enumerate images in WIM/ESD files |
| `Mount-WindowsImageList` | Mount images for modification |
| `Dismount-WindowsImageList` | Unmount and save changes |
| `Convert-ESDToWindowsImage` | Convert ESD to WIM format |
| `Reset-WindowsImageBase` | Reset image base and cleanup |

### **Windows Update Workflow**
| Cmdlet | Description |
|--------|-------------|
| `Search-WindowsUpdateCatalog` | Search Microsoft Update Catalog |
| `Get-WindowsUpdateDownloadUrl` | Extract download URLs |
| `Save-WindowsUpdateCatalogResult` | Download with resume capability |
| `Install-WindowsImageUpdate` | Install updates into mounted images |
| `Get-PatchTuesday` | Calculate Patch Tuesday dates |

### **Image Customization**
| Cmdlet | Description |
|--------|-------------|
| `Get-INFDriverList` | Parse INF files and extract driver info |
| `Add-INFDriverList` | Install drivers into mounted images |
| `Remove-AppXProvisionedPackageList` | Remove AppX packages with filtering |
| `Get-RegistryOperationList` | Parse registry files |
| `Write-RegistryOperationList` | Apply registry operations |
| `Add-SetupCompleteAction` | Add custom first-boot actions |

### **Autopilot & Configuration**
| Cmdlet | Description |
|--------|-------------|
| `Get-AutopilotConfiguration` | Load Autopilot JSON configuration |
| `Set-AutopilotConfiguration` | Modify Autopilot settings |
| `Export-AutopilotConfiguration` | Save Autopilot configuration |
| `Install-AutopilotConfiguration` | Apply to mounted images |
| `New-AutopilotConfiguration` | Create new configuration |

### **Database & Tracking**
| Cmdlet | Description |
|--------|-------------|
| `Set-WindowsImageDatabaseConfiguration` | Configure database settings |
| `New-WindowsImageDatabase` | Initialize database schema |
| `Search-WindowsImageDatabase` | Query operation history |
| `Clear-WindowsImageDatabase` | Reset database |

### **Release Information**
| Cmdlet | Description |
|--------|-------------|
| `Get-WindowsReleaseInfo` | Get Windows release history and KB info |

## 💡 Usage Examples

### **Enterprise Deployment Workflow**
```powershell
# 1. Setup environment
Install-ADK -Force
Set-WindowsImageDatabaseConfiguration -Path "C:\Deployment\tracking.db"
New-WindowsImageDatabase

# 2. Get latest Windows 11 updates
$latestRelease = Get-WindowsReleaseInfo -OperatingSystem "Windows 11" -Latest
$updates = Search-WindowsUpdateCatalog -Query $latestRelease.LatestKBArticle -Architecture x64 |
    Get-WindowsUpdateDownloadUrl |
    Save-WindowsUpdateCatalogResult -DestinationPath "C:\Updates"

# 3. Customize image with drivers and updates
$images = Get-WindowsImageList -ImagePath "install.wim" | Where-Object { $_.ImageName -like "*Enterprise*" }
$mounted = $images | Mount-WindowsImageList -MountPath "C:\Mount" -ReadWrite

# Install drivers
$drivers = Get-INFDriverList -Path "C:\Drivers" -Recurse
$mounted | Add-INFDriverList -Drivers $drivers

# Install updates
$mounted | Install-WindowsImageUpdate -UpdatePackages $updates

# Configure Autopilot
$autopilot = New-AutopilotConfiguration -TenantId "your-tenant-id" -DeviceName "%SERIAL%"
$mounted | Install-AutopilotConfiguration -Configuration $autopilot

# Remove unwanted AppX packages
$mounted | Remove-AppXProvisionedPackageList -InclusionFilter "Xbox|Candy|Solitaire" -ExclusionFilter "Store|Calculator"

# Save and cleanup
$mounted | Dismount-WindowsImageList -Save
```

### **Automated Patch Tuesday Updates**
```powershell
# Calculate next Patch Tuesday
$nextPatchTuesday = Get-PatchTuesday -Next

# Setup automated download for that date
$updates = Search-WindowsUpdateCatalog -Query "Cumulative" -Architecture x64 |
    Where-Object { $_.LastModified.Date -eq $nextPatchTuesday.Date } |
    Get-WindowsUpdateDownloadUrl |
    Save-WindowsUpdateCatalogResult -DestinationPath "C:\PatchTuesday\$($nextPatchTuesday.Date.ToString('yyyy-MM'))"

Write-Output "Downloaded $($updates.Count) updates for Patch Tuesday: $($nextPatchTuesday.Date.ToString('MMMM dd, yyyy'))"
```

### **WinPE Customization**
```powershell
# Install ADK with WinPE
Install-ADK -IncludeWinPE -IncludeDeploymentTools

# Get available components
$adk = Get-ADKInstallation -Latest
$components = Get-WinPEOptionalComponent -ADKInstallation $adk -Category "Scripting","Networking"

# Mount WinPE image and add components
$winpe = Get-WindowsImageList -ImagePath "C:\WinPE\boot.wim"
$mounted = $winpe | Mount-WindowsImageList -MountPath "C:\WinPE\Mount" -ReadWrite

# Add PowerShell and networking support
$mounted | Add-WinPEOptionalComponent -Components ($components | Where-Object { $_.Name -like "*PowerShell*" -or $_.Name -like "*WMI*" })

$mounted | Dismount-WindowsImageList -Save
```

## 🔧 Installation & Requirements

### **Prerequisites**
- Windows 10/11 or Windows Server 2019/2022
- PowerShell 5.1 or PowerShell 7+
- Administrator privileges for image operations
- DISM tools (included with Windows)

### **Installation**
```powershell
# Clone repository
git clone https://github.com/Grace-Solutions/PSWindowsImageTools.git
cd PSWindowsImageTools

# Import module
Import-Module .\Module\PSWindowsImageTools\PSWindowsImageTools.psd1

# Verify installation
Get-Command -Module PSWindowsImageTools
```

## 📚 Documentation

- **[Complete Cmdlet Reference](docs/CmdletReference.md)** - Detailed documentation for all cmdlets
- **[Windows Update Catalog Guide](docs/WindowsUpdateCatalog.md)** - Update management workflows
- **[Image Customization Guide](docs/ImageCustomization.md)** - Advanced customization techniques

## 🤝 Contributing

We welcome contributions! Please:
1. Fork the repository
2. Create a feature branch
3. Submit a pull request with detailed description

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

**PSWindowsImageTools** - Streamlining Windows deployment automation with PowerShell excellence.
