# Native Progress Callback System

## Overview

PSWindowsImageTools now uses native DISM and WIM API progress callbacks for real-time, accurate progress reporting instead of imposed progress calculations. This provides users with genuine progress information directly from the underlying Windows APIs.

## Architecture

### Native APIs Used

1. **DISM Native API** (`DismNativeApi`)
   - `DismMountImage()` with `ProgressCallback` delegate
   - `DismUnmountImage()` with `ProgressCallback` delegate
   - Real-time progress from actual DISM operations

2. **WIM Native API** (`WimNativeApi`)
   - `WIMExportImage()` with `WimCallback` delegate
   - Real-time progress from WIM export operations

### Progress Callback Infrastructure

#### ProgressService Methods
- `CreateMountProgressCallback()` - For mount/unmount operations
- `CreateProgressCallback()` - For general file operations
- `CreateInstallProgressCallback()` - For update installation operations

#### NativeDismService
- `MountImage()` - Native mount with progress callbacks
- `UnmountImage()` - Native unmount with progress callbacks
- Automatic progress callback wrapper conversion

## Implementation Pattern

### Before (Imposed Progress)
```csharp
// ❌ Old imposed progress calculation
var progress = (int)((double)(i + 1) / totalCount * 100);
LoggingService.WriteProgress(this, "Activity", $"Item {i+1}", $"Operation ({progress}%)", progress);

// ❌ Old Microsoft.Dism without progress
Microsoft.Dism.DismApi.MountImage(imagePath, mountPath, imageIndex);
```

### After (Native Progress)
```csharp
// ✅ New native progress callback
var progressCallback = ProgressService.CreateMountProgressCallback(
    this, "Activity", imageName, mountPath, currentIndex, totalCount);

// ✅ New native DISM with real progress
using var nativeDismService = new NativeDismService();
var success = nativeDismService.MountImage(
    imagePath, mountPath, imageIndex, 
    progressCallback: progressCallback, cmdlet: this);
```

## Updated Cmdlets

### ✅ Fully Updated
- **GetWindowsImageListCmdlet** - Uses native progress for advanced info mounting
- **MountWindowsImageListCmdlet** - Uses native progress for all mount operations
- **DismountWindowsImageListCmdlet** - Uses native progress for all dismount operations
- **AdvancedImageInfoService** - Uses native DISM service with progress callbacks
- **InvokeMediaDynamicUpdateCmdlet** - Uses native DISM for internal mount/dismount operations

### ✅ Already Using Native Progress
- **ConvertESDToWindowsImageCmdlet** - Already uses proper progress callbacks
- **WimExportService** - Already uses native WIM API with progress callbacks
- **ESDConversionService** - Already uses native progress through WimExportService

## Benefits

### For Users
1. **Real-time Progress** - Actual progress from Windows APIs, not estimates
2. **Accurate Percentages** - True completion percentages from DISM/WIM operations
3. **Detailed Status** - Real operation status messages from native APIs
4. **Consistent Experience** - Unified progress reporting across all cmdlets

### For Developers
1. **No More Imposed Calculations** - Eliminates guesswork in progress reporting
2. **Native API Integration** - Direct access to Windows API progress events
3. **Consistent Patterns** - Standardized progress callback creation and usage
4. **Better Error Handling** - Native API error reporting with HRESULT codes

## Technical Details

### Progress Callback Conversion
```csharp
// Native DISM callback signature: (uint current, uint total, IntPtr userData)
// Converted to: Action<int, string> for PowerShell integration

nativeCallback = (current, total, userData) =>
{
    if (total > 0)
    {
        var percentage = (int)((current * 100) / total);
        progressCallback(percentage, $"Operation: {percentage}%");
    }
    else
    {
        progressCallback(-1, "Operation in progress...");
    }
};
```

### Service Integration
- **NativeDismService** - Handles all DISM operations with progress
- **WimExportService** - Handles WIM operations with progress
- **ProgressService** - Creates standardized progress callbacks
- **AdvancedImageInfoService** - Orchestrates operations with progress

## Migration Guide

### For New Cmdlets
1. Use `NativeDismService` instead of `Microsoft.Dism.DismApi`
2. Create progress callbacks with `ProgressService.CreateMountProgressCallback()`
3. Pass callbacks to native service methods
4. Handle success/failure return values

### For Existing Code
1. Replace imposed progress calculations with native callbacks
2. Update `Microsoft.Dism.DismApi` calls to use `NativeDismService`
3. Remove manual progress percentage calculations
4. Use native service return values for error handling

## Future Enhancements

1. **Package Operations** - Add progress callbacks to package install/remove operations
2. **Feature Operations** - Add progress callbacks to Windows feature operations
3. **Driver Operations** - Add progress callbacks to driver add/remove operations
4. **Cleanup Operations** - Add progress callbacks to component cleanup operations

This unified system ensures that all PSWindowsImageTools operations provide accurate, real-time progress information directly from the Windows APIs.
