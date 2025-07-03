@{
    # Script module or binary module file associated with this manifest.
    RootModule = 'bin\PSWindowsImageTools.dll'

    # Version number of this module.
    ModuleVersion = '2025.06.18.1'

    # Supported PSEditions
    CompatiblePSEditions = @('Desktop', 'Core')

    # ID used to uniquely identify this module
    GUID = 'a1b2c3d4-e5f6-7890-abcd-ef1234567890'

    # Author of this module
    Author = 'PSWindowsImageTools'

    # Company or vendor of this module
    CompanyName = 'PSWindowsImageTools'

    # Copyright statement for this module
    Copyright = 'Copyright (c) 2025 PSWindowsImageTools. All rights reserved.'

    # Description of the functionality provided by this module
    Description = 'PowerShell module for Windows image customization and Windows Update management. Provides comprehensive tools for working with ISO, WIM, and ESD files, native DISM operations, Windows Update catalog integration, and database-driven workflow management.'

    # Minimum version of the PowerShell engine required by this module
    PowerShellVersion = '5.1'

    # Minimum version of Microsoft .NET Framework required by this module
    DotNetFrameworkVersion = '4.8'

    # Minimum version of the common language runtime (CLR) required by this module
    CLRVersion = '4.0'

    # Assemblies that must be loaded prior to importing this module
    RequiredAssemblies = @(
        'bin\PSWindowsImageTools.dll',
        'bin\Microsoft.Dism.dll',
        'bin\Registry.dll',
        'bin\System.Data.SQLite.dll',
        'bin\Newtonsoft.Json.dll',
        'bin\HtmlAgilityPack.dll'
    )

    # Functions to export from this module, for best performance, do not use wildcards and do not delete the entry, use an empty array if there are no functions to export.
    FunctionsToExport = @()

    # Cmdlets to export from this module, for best performance, do not use wildcards and do not delete the entry, use an empty array if there are no cmdlets to export.
    CmdletsToExport = @(
        # Core Windows Image Management
        'Get-WindowsImageList',
        'Mount-WindowsImageList',
        'Dismount-WindowsImageList',

        # ESD/ISO Conversion
        'Convert-ESDToWindowsImage',

        # Windows Update Workflow
        'Search-WindowsUpdateCatalog',
        'Get-WindowsUpdateDownloadUrl',
        'Get-PatchTuesday',
        'Save-WindowsUpdateCatalogResult',
        'Install-WindowsImageUpdate',

        # Windows Release Information
        'Get-WindowsReleaseInfo',

        # Image Customization
        'Add-SetupCompleteAction',
        'Reset-WindowsImageBase',

        # Driver Management
        'Get-INFDriverList',
        'Add-INFDriverList',

        # ADK and Optional Component Management
        'Get-ADKInstallation',
        'Get-WinPEOptionalComponent',
        'Add-WinPEOptionalComponent',
        'Install-ADK',
        'Uninstall-ADK',

        # AppX Package Management
        'Remove-AppXProvisionedPackageList',

        # Database Operations
        'Search-WindowsImageDatabase',
        'Set-WindowsImageDatabaseConfiguration',
        'New-WindowsImageDatabase',
        'Clear-WindowsImageDatabase',

        # Registry Operations
        'Get-RegistryOperationList',
        'Write-RegistryOperationList',

        # Autopilot Configuration Management
        'Get-AutopilotConfiguration',
        'Set-AutopilotConfiguration',
        'Export-AutopilotConfiguration',
        'Install-AutopilotConfiguration',
        'New-AutopilotConfiguration',

        # Unattend XML Configuration Management
        'Get-UnattendXMLConfiguration',
        'Set-UnattendXMLConfiguration',
        'Export-UnattendXMLConfiguration',
        'Install-UnattendXMLConfiguration',
        'New-UnattendXMLConfiguration'
    )

    # Variables to export from this module
    VariablesToExport = @()

    # Aliases to export from this module, for best performance, do not use wildcards and do not delete the entry, use an empty array if there are no aliases to export.
    AliasesToExport = @()

    # Private data to pass to the module specified in RootModule/ModuleToProcess. This may also contain a PSData hashtable with additional module metadata used by PowerShell.
    PrivateData = @{
        PSData = @{
            # Tags applied to this module. These help with module discovery in online galleries.
            Tags = @('Windows', 'Image', 'WIM', 'ESD', 'ISO', 'DISM', 'Customization', 'Updates', 'WindowsUpdate', 'Catalog', 'Database', 'PowerShell')

            # A URL to the license for this module.
            LicenseUri = 'https://www.gnu.org/licenses/gpl-3.0.html'

            # A URL to the main website for this project.
            ProjectUri = 'https://github.com/Grace-Solutions/PSWindowsUpdateTools'

            # ReleaseNotes of this module
            ReleaseNotes = @'
Major architectural improvements and new Windows Update workflow:

NEW FEATURES:
• Complete Windows Update architecture redesign with proper object models
• Search-WindowsUpdateCatalog: Search Microsoft Update Catalog with filters
• Save-WindowsUpdateCatalogResult: Download catalog results to packages
• Search-WindowsImageDatabase: Search local database for cached updates
• Install-WindowsImageUpdate: Install packages on mounted images (redesigned)
• Full pipeline support: Search → Download → Install workflow
• Database integration with ObjectType queries (Updates, Images, Operations, Inventory)

IMPROVEMENTS:
• Migrated to .NET Standard 2.0 for better PowerShell compatibility
• Reduced bin folder from 200+ files to 8 essential DLLs
• Fixed all build warnings and security vulnerabilities
• Consistent InputObject parameter naming for pipeline support
• All path parameters use DirectoryInfo/FileInfo types
• Removed shell-outs, pure DISM API usage throughout
• Enhanced logging with human-readable timestamps and durations

ARCHITECTURE:
• Clean separation between catalog results and downloaded packages
• Type-safe object models with proper PowerShell patterns
• Offline database searches complement online catalog searches
• Comprehensive examples and workflow documentation
'@
        }
    }
}
