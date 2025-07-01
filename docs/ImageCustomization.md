# Windows Image Customization

This document describes the image customization capabilities of PSWindowsImageTools, including update installation and SetupComplete automation.

## Overview

PSWindowsImageTools provides comprehensive Windows image customization through:

- **Update Installation**: Direct installation of CAB/MSU files into mounted images
- **SetupComplete Automation**: Custom actions during Windows first boot
- **File Deployment**: Copying files and scripts into images
- **Configuration Management**: Automated system configuration

## Update Installation

### Install-WindowsUpdateFile Cmdlet

Install Windows updates (CAB/MSU files) directly into mounted Windows images.

#### Basic Usage

```powershell
# Install single update file
Install-WindowsUpdateFile -UpdatePath "C:\Updates\KB5000001.msu" -ImagePath "C:\Mount\Image1"

# Install multiple updates from directory
Install-WindowsUpdateFile -UpdatePath "C:\Updates\" -ImagePath "C:\Mount\Image1"

# Install with error handling
Install-WindowsUpdateFile -UpdatePath "C:\Updates\*.cab" -ImagePath "C:\Mount\Image1" -ContinueOnError
```

#### Advanced Options

```powershell
# Install with validation and DISM options
Install-WindowsUpdateFile -UpdatePath "C:\Updates\" -ImagePath "C:\Mount\Image1" `
    -ValidateImage `
    -IgnoreCheck `
    -PreventPending `
    -ContinueOnError
```

#### Parameters

- **UpdatePath**: Path to update file(s) or directory containing updates
- **ImagePath**: Path to mounted Windows image directory
- **IgnoreCheck**: Prevents DISM from checking package applicability
- **PreventPending**: Prevents automatic installation of prerequisite packages
- **ContinueOnError**: Continues processing other updates if one fails
- **ValidateImage**: Validates that the image is suitable for update integration

#### Pipeline Integration

```powershell
# Complete workflow: Search → Download → Install
Search-WindowsUpdateCatalog -Query 'Windows 11 Cumulative' -Architecture x64 |
    Get-WindowsUpdateDownloadUrl |
    Save-WindowsUpdateCatalogResult -DestinationPath "C:\Updates" |
    ForEach-Object { Install-WindowsUpdateFile -UpdatePath $_.LocalFile -ImagePath "C:\Mount\Image1" }
```

### Boot Image Support

Boot images can be updated using the same process:

```powershell
# Mount boot image
Mount-WindowsImageList -ImagePath "boot.wim" -Index 2 -MountPath "C:\Mount\Boot"

# Install updates
Install-WindowsUpdateFile -UpdatePath "C:\Updates\KB5000001.msu" -ImagePath "C:\Mount\Boot"

# Dismount and save
Dismount-WindowsImageList -MountPath "C:\Mount\Boot" -Save
```

## SetupComplete Automation

### Add-SetupCompleteAction Cmdlet

Add custom actions to SetupComplete.cmd that execute during Windows first boot.

#### Basic Usage

```powershell
# Add simple command
Add-SetupCompleteAction -ImagePath "C:\Mount\Image1" `
    -Command "reg add HKLM\Software\MyApp /v Installed /t REG_SZ /d Yes" `
    -Description "Register application"

# Add multiple commands
Add-SetupCompleteAction -ImagePath "C:\Mount\Image1" `
    -Command @("net stop Themes", "reg add HKLM\...", "net start Themes") `
    -Description "System configuration" `
    -Priority 100
```

#### Script Execution

```powershell
# Execute external script
Add-SetupCompleteAction -ImagePath "C:\Mount\Image1" `
    -ScriptFile "C:\Scripts\post-install.cmd" `
    -Description "Run post-installation script" `
    -Priority 50 `
    -ContinueOnError
```

#### File Deployment

```powershell
# Copy files and execute commands
Add-SetupCompleteAction -ImagePath "C:\Mount\Image1" `
    -CopyFiles "C:\LocalFiles\config.xml", "C:\Tools\*" `
    -CopyDestination "Temp\Deployment" `
    -Command "copy C:\Temp\Deployment\config.xml C:\Program Files\MyApp\" `
    -Description "Deploy configuration files" `
    -Backup
```

#### Parameters

- **ImagePath**: Path to mounted Windows image directory
- **Command**: Command(s) to execute during SetupComplete phase
- **Description**: Description of the action for documentation
- **Priority**: Execution order (lower numbers execute first, default: 100)
- **ContinueOnError**: Continue execution if this action fails
- **ScriptFile**: Path to script file to copy and execute
- **CopyFiles**: Files/directories to copy to the image
- **CopyDestination**: Destination path in image for copied files (relative to C:\)
- **Backup**: Create backup of existing SetupComplete.cmd

### Priority System

Actions execute in priority order during Windows first boot:

```powershell
# Early initialization (Priority 10-50)
Add-SetupCompleteAction -ImagePath "C:\Mount\Image1" -Command "echo Starting setup..." -Priority 10

# Application installation (Priority 50-100)
Add-SetupCompleteAction -ImagePath "C:\Mount\Image1" -ScriptFile "install-apps.cmd" -Priority 50

# System configuration (Priority 100-150)
Add-SetupCompleteAction -ImagePath "C:\Mount\Image1" -Command "reg add..." -Priority 100

# Final cleanup (Priority 200+)
Add-SetupCompleteAction -ImagePath "C:\Mount\Image1" -Command "del /q C:\Temp\*.tmp" -Priority 200
```

### Error Handling

```powershell
# Continue on error with logging
Add-SetupCompleteAction -ImagePath "C:\Mount\Image1" `
    -Command "net start MyService" `
    -Description "Start custom service" `
    -ContinueOnError `
    -Priority 150
```

This generates: `net start MyService || echo Warning: Command failed but continuing...`

## Complete Customization Workflow

### Enterprise Image Preparation

```powershell
# 1. Mount Windows Image
$mountedImages = Mount-WindowsImageList -ImagePath "install.wim" -Index 1 -MountPath "C:\Mount\Enterprise"
$imagePath = $mountedImages[0].MountPath

# 2. Install Latest Updates
Search-WindowsUpdateCatalog -Query 'Windows 11 Cumulative' -Architecture x64 -MaxResults 3 |
    Get-WindowsUpdateDownloadUrl |
    Save-WindowsUpdateCatalogResult -DestinationPath "C:\Updates" |
    ForEach-Object { Install-WindowsUpdateFile -UpdatePath $_.LocalFile -ImagePath $imagePath -ValidateImage }

# 3. Deploy Tools and Scripts
Add-SetupCompleteAction -ImagePath $imagePath `
    -CopyFiles "C:\DeploymentTools\*" `
    -CopyDestination "Tools" `
    -Command "echo Deployment tools copied" `
    -Description "Deploy enterprise tools" `
    -Priority 10

# 4. Install Applications
Add-SetupCompleteAction -ImagePath $imagePath `
    -ScriptFile "C:\Scripts\install-enterprise-apps.cmd" `
    -Description "Install enterprise applications" `
    -Priority 50 `
    -ContinueOnError

# 5. Configure System Settings
Add-SetupCompleteAction -ImagePath $imagePath `
    -Command @(
        "reg add HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate /v DoNotConnectToWindowsUpdateInternetLocations /t REG_DWORD /d 1",
        "reg add HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate /v DisableWindowsUpdateAccess /t REG_DWORD /d 1",
        "powershell -File C:\Tools\configure-enterprise.ps1"
    ) `
    -Description "Apply enterprise policies" `
    -Priority 100

# 6. Final Configuration
Add-SetupCompleteAction -ImagePath $imagePath `
    -Command "shutdown /r /t 60 /c 'Enterprise configuration complete. Restarting in 60 seconds.'" `
    -Description "Restart after configuration" `
    -Priority 200

# 7. Dismount and Save
Dismount-WindowsImageList -MountPath $imagePath -Save
```

### Generated SetupComplete.cmd

The above workflow generates a SetupComplete.cmd like:

```cmd
REM === SetupComplete Action - Deploy enterprise tools (Priority: 10) ===
REM Added on: 2025-06-27 16:41:21
echo Deployment tools copied
REM === End SetupComplete Action ===

REM === SetupComplete Action - Install enterprise applications (Priority: 50) ===
REM Added on: 2025-06-27 16:41:21
call "C:\Tools/install-enterprise-apps.cmd" || echo Warning: Script failed but continuing...
REM === End SetupComplete Action ===

REM === SetupComplete Action - Apply enterprise policies (Priority: 100) ===
REM Added on: 2025-06-27 16:41:21
reg add HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate /v DoNotConnectToWindowsUpdateInternetLocations /t REG_DWORD /d 1
reg add HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate /v DisableWindowsUpdateAccess /t REG_DWORD /d 1
powershell -File C:\Tools\configure-enterprise.ps1
REM === End SetupComplete Action ===

REM === SetupComplete Action - Restart after configuration (Priority: 200) ===
REM Added on: 2025-06-27 16:41:21
shutdown /r /t 60 /c "Enterprise configuration complete. Restarting in 60 seconds."
REM === End SetupComplete Action ===
```

## Best Practices

### Update Installation

1. **Always validate images** before installing updates
2. **Use ContinueOnError** for batch operations
3. **Install updates before** adding SetupComplete actions
4. **Test with boot images** if they need updates

### SetupComplete Actions

1. **Use priority ordering** to control execution sequence
2. **Include error handling** with ContinueOnError for non-critical actions
3. **Document actions** with meaningful descriptions
4. **Create backups** when modifying existing SetupComplete.cmd
5. **Test scripts** before adding them to images

### File Management

1. **Copy files to appropriate locations** (avoid system directories)
2. **Use relative paths** in commands when possible
3. **Clean up temporary files** with high-priority cleanup actions
4. **Validate file permissions** after deployment

This comprehensive customization system enables enterprise-grade Windows image preparation with automated deployment and configuration.
