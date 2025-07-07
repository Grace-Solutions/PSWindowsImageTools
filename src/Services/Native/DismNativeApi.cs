using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace PSWindowsImageTools.Services.Native
{
    /// <summary>
    /// Complete native DISM and WIM API declarations based on Microsoft's actual implementation
    /// Analyzed from decompiled Microsoft.Dism.PowerShell module
    /// </summary>
    public static class DismNativeApi
    {
        private const string DismApiDll = "dismapi.dll";
        private const string WimApiDll = "wimgapi.dll";

        #region Enums and Structures

        public enum LogLevel
        {
            LogErrors = 0,
            LogWarnings = 1,
            LogInfo = 2
        }

        public enum ProgressOperation
        {
            DismProgressNone,
            DismProgressImageApply,
            DismProgressImageCapture,
            DismProgressImageUnmount,
            DismProgressImageMount,
            DismProgressImageDelete,
            DismProgressImageCommit,
            DismProgressImageExport
        }

        public enum ImageIdentifier : uint
        {
            ImageIndex = 0,
            ImageName = 1,
            ImageNone = 2
        }

        public enum PackageIdentifier : uint
        {
            PackageName = 0,
            PackagePath = 1
        }

        public enum ImageHealthState : uint
        {
            Healthy = 0,
            Repairable = 1,
            NonRepairable = 2
        }

        public enum PackageFeatureState : uint
        {
            NotPresent = 0,
            UninstallPending = 1,
            Staged = 2,
            Removed = 3,
            Installed = 4,
            InstallPending = 5,
            Superseded = 6,
            PartiallyInstalled = 7
        }

        public enum ReleaseType : uint
        {
            CriticalUpdate = 0,
            Driver = 1,
            FeaturePack = 2,
            Hotfix = 3,
            SecurityUpdate = 4,
            ServicePack = 5,
            Tool = 6,
            UpdateRollup = 7,
            Update = 8,
            Unspecified = 9
        }

        public enum Architecture : uint
        {
            X86 = 0,
            MIPS = 1,
            Alpha = 2,
            PowerPC = 3,
            SHX = 4,
            ARM = 5,
            IA64 = 6,
            Alpha64 = 7,
            MSIL = 8,
            X64 = 9,
            IA32_ON_WIN64 = 10,
            Neutral = 11,
            ARM64 = 12,
            ARM32_ON_WIN64 = 13,
            IA32_ON_ARM64 = 14
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 8)]
        public struct DismImageInfo
        {
            public uint ImageType;
            public uint ImageIndex;
            public IntPtr ImageName;
            public IntPtr ImageDescription;
            public ulong ImageSize;
            public Architecture Architecture;
            public IntPtr ProductName;
            public IntPtr EditionId;
            public IntPtr InstallationType;
            public IntPtr Hal;
            public IntPtr ProductType;
            public IntPtr ProductSuite;
            public uint MajorVersion;
            public uint MinorVersion;
            public uint Build;
            public uint SpBuild;
            public IntPtr SpLevel;
            public uint Bootable;
            public IntPtr SystemRoot;
            public uint LanguageCount;
            public IntPtr Languages;
            public IntPtr DefaultLanguage;
            public IntPtr CreatedTime;
            public IntPtr ModifiedTime;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DismFeatureInfo
        {
            public IntPtr FeatureName;
            public PackageFeatureState State;
            public IntPtr DisplayName;
            public IntPtr Description;
            public uint RestartRequired;
            public uint CustomPropertyCount;
            public IntPtr CustomProperty;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DismPackageInfo
        {
            public IntPtr PackageName;
            public PackageFeatureState PackageState;
            public ReleaseType ReleaseType;
            public IntPtr InstallTime;
            public uint Applicable;
            public IntPtr Copyright;
            public IntPtr Company;
            public IntPtr CreationTime;
            public IntPtr DisplayName;
            public IntPtr Description;
            public IntPtr InstallClient;
            public IntPtr InstallPackageName;
            public IntPtr LastUpdateTime;
            public IntPtr ProductName;
            public IntPtr ProductVersion;
            public uint RestartRequired;
            public IntPtr SupportInformation;
            public uint PackageSize;
            public uint FeatureCount;
            public IntPtr Feature;
            public uint CustomPropertyCount;
            public IntPtr CustomProperty;
        }

        #endregion

        #region DISM Constants

        /// <summary>
        /// DISM unmount flags
        /// </summary>
        public static class UnmountFlags
        {
            public const uint DISM_DISCARD_IMAGE = 0x00000000;
            public const uint DISM_COMMIT_IMAGE = 0x00000001;
            public const uint DISM_COMMIT_MASK = 0x00000001;
        }

        #endregion

        // Progress callback delegate - matches the actual DISM API signature
        public delegate void ProgressCallback(
            uint current,
            uint total,
            IntPtr userData);

        #region Core DISM API Functions - Based on Microsoft's actual implementation

        [DllImport(DismApiDll, CharSet = CharSet.Unicode, PreserveSig = false)]
        public static extern void DismInitialize(
            LogLevel logLevel,
            string? scratchDirectory,
            string? logFilePath);

        [DllImport(DismApiDll, CharSet = CharSet.Unicode, PreserveSig = false)]
        public static extern void DismShutdown();

        [DllImport(DismApiDll)]
        public static extern int DismOpenSession(
            [MarshalAs(UnmanagedType.LPWStr)] string imagePath,
            [MarshalAs(UnmanagedType.LPWStr)] string windowsDirectory,
            [MarshalAs(UnmanagedType.LPWStr)] string systemDrive,
            out uint session);

        [DllImport(DismApiDll)]
        public static extern int DismCloseSession(uint session);

        [DllImport(DismApiDll, CharSet = CharSet.Unicode, PreserveSig = false)]
        public static extern void DismMountImage(
            string imagePath,
            string mountPath,
            uint imageIndex,
            string? imageName,
            ImageIdentifier imageIdentifier,
            uint flags,
            IntPtr cancelEvent,
            ProgressCallback? progress,
            IntPtr userData);

        [DllImport(DismApiDll, CharSet = CharSet.Unicode, PreserveSig = false)]
        public static extern void DismUnmountImage(
            string mountPath,
            uint flags,
            IntPtr cancelEvent,
            ProgressCallback? progress,
            IntPtr userData);



        [DllImport(DismApiDll)]
        public static extern int DismGetImageInfo(
            [MarshalAs(UnmanagedType.LPWStr)] string imageFilePath,
            out IntPtr imageInfoBufPtr,
            out uint imageInfoCount);

        [DllImport(DismApiDll)]
        public static extern int DismCleanupMountpoints();

        [DllImport(DismApiDll)]
        public static extern int DismDelete(IntPtr dismStructure);

        #endregion

        #region Package Management Functions - Based on Microsoft's implementation

        [DllImport(DismApiDll)]
        public static extern int DismAddPackage(
            uint session,
            [MarshalAs(UnmanagedType.LPWStr)] string packagePath,
            bool ignoreCheck,
            bool preventPending,
            SafeWaitHandle? cancelHandle,
            ProgressCallback progress,
            IntPtr userData);

        [DllImport(DismApiDll)]
        public static extern int DismRemovePackage(
            uint session,
            [MarshalAs(UnmanagedType.LPWStr)] string identifier,
            PackageIdentifier packageIdentifier,
            SafeWaitHandle? cancelHandle,
            ProgressCallback progress,
            IntPtr userData);

        [DllImport(DismApiDll)]
        public static extern int DismGetPackages(
            uint session,
            out IntPtr packageBufPtr,
            out uint packageCount);

        [DllImport(DismApiDll)]
        public static extern int DismGetPackageInfo(
            uint session,
            [MarshalAs(UnmanagedType.LPWStr)] string identifier,
            PackageIdentifier packageIdentifier,
            out IntPtr packageInfoPtr);

        #endregion

        #region Feature Management Functions - Based on Microsoft's implementation

        [DllImport(DismApiDll)]
        public static extern int DismEnableFeature(
            uint session,
            [MarshalAs(UnmanagedType.LPWStr)] string featureName,
            [MarshalAs(UnmanagedType.LPWStr)] string identifier,
            PackageIdentifier packageIdentifier,
            bool limitAccess,
            [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr, SizeParamIndex = 6)] string[] sourcePaths,
            uint sourcePathCount,
            bool enableAll,
            SafeWaitHandle? cancelHandle,
            ProgressCallback progress,
            IntPtr userData);

        [DllImport(DismApiDll)]
        public static extern int DismDisableFeature(
            uint session,
            [MarshalAs(UnmanagedType.LPWStr)] string featureName,
            [MarshalAs(UnmanagedType.LPWStr)] string packageName,
            bool removePayload,
            SafeWaitHandle? cancelHandle,
            ProgressCallback progress,
            IntPtr userData);

        [DllImport(DismApiDll)]
        public static extern int DismGetFeatureInfo(
            uint session,
            [MarshalAs(UnmanagedType.LPWStr)] string featureName,
            [MarshalAs(UnmanagedType.LPWStr)] string identifier,
            PackageIdentifier packageIdentifier,
            out IntPtr featureInfoPtr);

        #endregion

        #region Helper Methods

        /// <summary>
        /// Converts a native string pointer to a managed string
        /// </summary>
        public static string PtrToStringUni(IntPtr ptr)
        {
            return ptr != IntPtr.Zero ? Marshal.PtrToStringUni(ptr) ?? string.Empty : string.Empty;
        }

        /// <summary>
        /// Gets the last Win32 error as an HRESULT
        /// </summary>
        public static int GetLastErrorAsHResult()
        {
            var lastError = Marshal.GetLastWin32Error();
            return lastError <= 0 ? lastError : (int)(0x80070000 | (uint)lastError);
        }

        #endregion
    }

    /// <summary>
    /// Extension methods for Architecture enum
    /// </summary>
    public static class ArchitectureExtensions
    {
        /// <summary>
        /// Converts Architecture enum value to readable string
        /// </summary>
        /// <param name="architecture">Architecture enum value</param>
        /// <returns>Human-readable architecture string</returns>
        public static string ToDisplayString(this DismNativeApi.Architecture architecture)
        {
            return architecture switch
            {
                DismNativeApi.Architecture.X86 => "x86",
                DismNativeApi.Architecture.X64 => "x64",
                DismNativeApi.Architecture.ARM => "ARM",
                DismNativeApi.Architecture.ARM64 => "ARM64",
                DismNativeApi.Architecture.IA64 => "IA64",
                DismNativeApi.Architecture.MIPS => "MIPS",
                DismNativeApi.Architecture.Alpha => "Alpha",
                DismNativeApi.Architecture.PowerPC => "PowerPC",
                DismNativeApi.Architecture.SHX => "SHX",
                DismNativeApi.Architecture.Alpha64 => "Alpha64",
                DismNativeApi.Architecture.MSIL => "MSIL",
                DismNativeApi.Architecture.IA32_ON_WIN64 => "IA32_on_Win64",
                DismNativeApi.Architecture.Neutral => "Neutral",
                DismNativeApi.Architecture.ARM32_ON_WIN64 => "ARM32_on_Win64",
                DismNativeApi.Architecture.IA32_ON_ARM64 => "IA32_on_ARM64",
                _ => $"Unknown ({(uint)architecture})"
            };
        }

        /// <summary>
        /// Converts uint architecture value to readable string
        /// </summary>
        /// <param name="architectureValue">Raw architecture value (e.g., 9 for x64)</param>
        /// <returns>Human-readable architecture string</returns>
        public static string ArchitectureToString(uint architectureValue)
        {
            if (Enum.IsDefined(typeof(DismNativeApi.Architecture), architectureValue))
            {
                return ((DismNativeApi.Architecture)architectureValue).ToDisplayString();
            }
            return $"Unknown ({architectureValue})";
        }
    }
}
