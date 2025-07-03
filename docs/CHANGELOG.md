# Changelog

All notable changes to PSWindowsImageTools will be documented in this file.

## [2.0.0] - 2025-01-03

### Added
- **Complete ADK Management Suite**
  - `Get-ADKInstallation` - Detect installed Windows ADK versions
  - `Install-ADK` - Download and install latest ADK with automatic patch detection
  - `Uninstall-ADK` - Remove ADK installations
  - `Get-WinPEOptionalComponent` - Discover available WinPE components
  - `Add-WinPEOptionalComponent` - Install components into boot images

- **Enhanced Windows Update Integration**
  - Dynamic parsing of Microsoft's ADK download pages
  - Automatic patch detection and installation (ZIP files with MSP files)
  - Enhanced process monitoring with command line display and timeouts
  - Robust error handling and fallback mechanisms

- **Advanced Image Customization**
  - `Get-INFDriverList` - Parse INF files and extract driver information
  - `Add-INFDriverList` - Install drivers into mounted images
  - `Remove-AppXProvisionedPackageList` - Remove AppX packages with regex filtering
  - `Get-RegistryOperationList` - Parse registry files
  - `Write-RegistryOperationList` - Apply registry operations
  - `Get-AutopilotConfiguration` - Load Autopilot JSON configuration
  - `Set-AutopilotConfiguration` - Modify Autopilot settings
  - `Export-AutopilotConfiguration` - Save Autopilot configuration
  - `Install-AutopilotConfiguration` - Apply to mounted images
  - `New-AutopilotConfiguration` - Create new configuration

- **Enterprise Features**
  - Windows release information and KB correlation
  - Patch Tuesday automation and scheduling
  - Comprehensive logging and progress reporting
  - SQLite database for operation tracking and inventory

### Changed
- **Consolidated Duplicate Cmdlets**
  - Merged `Install-WindowsUpdateFile` and `Install-WindowsImageUpdate` into unified `Install-WindowsImageUpdate`
  - Enhanced with dual parameter sets for both file-based and pipeline workflows
  - Improved pipeline integration and object flow

- **Enhanced Process Monitoring**
  - Removed async/await patterns for PowerShell compatibility
  - Added command line transparency and runtime tracking
  - Implemented timeout management (60 min ADK, 30 min uninstall, 15 min components)
  - Added graceful process termination on timeout

- **Improved Documentation**
  - Complete rewrite of README.md with comprehensive feature overview
  - Updated cmdlet reference with all new cmdlets and examples
  - Enhanced Windows Update Catalog guide with enterprise workflows
  - New Image Customization guide with advanced techniques

### Removed
- Excessive sample and debug scripts (cleaned up Scripts folder)
- Duplicate cmdlet exports from module manifest
- Legacy async/await patterns that caused PowerShell compatibility issues

### Fixed
- All compilation errors and warnings resolved
- String.Contains overload issues for .NET Standard 2.0 compatibility
- Registry namespace conflicts
- Model property references for proper compilation
- Null reference warnings and unreachable code

## [1.0.0] - Previous Version

### Initial Release
- Basic Windows image management
- Windows Update Catalog integration
- Database operations
- Core image mounting and dismounting functionality

---

## Version Numbering

This project follows [Semantic Versioning](https://semver.org/):
- **MAJOR** version for incompatible API changes
- **MINOR** version for backwards-compatible functionality additions
- **PATCH** version for backwards-compatible bug fixes

## Contributing

See [Contributing Guidelines](../CONTRIBUTING.md) for information on how to contribute to this project.