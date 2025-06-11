# Changelog

All notable changes to PSWindowsImageTools will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html) with a custom format of `yyyy.MM.dd.HHmm`.

## [Unreleased]

### Added
- Initial project structure and foundation
- Core data models for WindowsImageInfo, BuildRecipe, and database entities
- DatabaseService for SQLite operations with schema initialization
- LoggingService with centralized UTC timestamped logging
- ConfigurationService for module configuration management
- DismService for Windows image operations using ManagedDism
- Set-WindowsImageDatabaseConfiguration cmdlet for database configuration
- New-WindowsImageDatabase cmdlet for explicit database initialization
- Clear-WindowsImageDatabase cmdlet for database cleanup with confirmation
- Get-WindowsImageList cmdlet for image inventory with optional advanced metadata

### Technical Details
- .NET 6.0 target framework
- SQLite database with 4 core tables (Builds, Updates, Downloads, BuildProcessingEvents)
- Centralized configuration with environment variable expansion
- GUID-based mount directory management with auto-cleanup
- SHA256 file hashing for integrity verification
- Comprehensive error handling and logging throughout

### Dependencies
- Microsoft.PowerShell.SDK 7.4.0
- System.Data.SQLite.Core 1.0.118
- Microsoft.Dism 3.1.0
- Newtonsoft.Json 13.0.3
- System.Security.Cryptography.Algorithms 4.3.1

### Directory Structure
```
PSWindowsImageTools/
├── Artifacts/                    # Build artifacts
├── Module/
│   └── PSWindowsImageTools/
│       ├── PSWindowsImageTools.psd1
│       └── bin/                  # DLLs loaded from here
├── Releases/
├── docs/
│   └── CHANGELOG.md
├── src/
│   ├── Cmdlets/
│   ├── Services/
│   ├── Models/
│   └── PSWindowsImageTools.csproj
```

### Still To Implement
- Convert-ESDToWindowsImage cmdlet
- New-WindowsImageBuildRecipe cmdlet
- New-WindowsImageBuild cmdlet
- Search-WindowsImageUpdateCatalog cmdlet
- Invoke-WindowsImageDatabaseQuery cmdlet
- Recipe processing engine with all section handlers
- Windows Update Catalog search service
- ISO file mounting and processing
- Complete database CRUD operations
- Unit and integration tests
- Help documentation generation

## Notes

This is the initial development phase focusing on establishing the core architecture and foundational components according to the detailed specification provided.
