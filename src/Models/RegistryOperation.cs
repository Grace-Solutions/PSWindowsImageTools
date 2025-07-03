using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace PSWindowsImageTools.Models
{
    /// <summary>
    /// Represents a registry operation to be performed on a Windows image
    /// </summary>
    public class RegistryOperation
    {
        /// <summary>
        /// The operation to perform (Create, Remove, Modify)
        /// </summary>
        public RegistryOperationType Operation { get; set; }

        /// <summary>
        /// The registry hive (HKLM, HKCU, HKU, etc.)
        /// </summary>
        public string Hive { get; set; } = string.Empty;

        /// <summary>
        /// The registry key path
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// The value name (empty for default value)
        /// </summary>
        public string ValueName { get; set; } = string.Empty;

        /// <summary>
        /// The value data
        /// </summary>
        public object? Value { get; set; }

        /// <summary>
        /// The registry value type
        /// </summary>
        public RegistryValueKind ValueType { get; set; } = RegistryValueKind.String;

        /// <summary>
        /// Original line from .reg file for reference
        /// </summary>
        public string OriginalLine { get; set; } = string.Empty;

        /// <summary>
        /// Line number in the .reg file
        /// </summary>
        public int LineNumber { get; set; }

        /// <summary>
        /// Whether this operation was successfully applied
        /// </summary>
        public bool IsApplied { get; set; }

        /// <summary>
        /// Error message if operation failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets the mapped hive for Windows image operations
        /// HKCU and HKU are mapped to the default user hive
        /// </summary>
        public string GetMappedHive()
        {
            var upperHive = Hive.ToUpperInvariant();

            if (upperHive == "HKEY_CURRENT_USER" || upperHive == "HKCU")
                return "HKU";
            if (upperHive == "HKEY_USERS" || upperHive == "HKU")
                return "HKU";
            if (upperHive == "HKEY_LOCAL_MACHINE" || upperHive == "HKLM")
                return "HKLM";
            if (upperHive == "HKEY_CLASSES_ROOT" || upperHive == "HKCR")
                return "HKLM\\SOFTWARE\\Classes";

            return Hive;
        }

        /// <summary>
        /// Gets the full registry path for the operation
        /// </summary>
        public string GetFullPath()
        {
            var mappedHive = GetMappedHive();
            return string.IsNullOrEmpty(Key) ? mappedHive : $"{mappedHive}\\{Key}";
        }

        /// <summary>
        /// Sets the value with automatic type conversion
        /// </summary>
        public void SetValue(object? value)
        {
            Value = ConvertValueToType(value, ValueType);
        }

        /// <summary>
        /// Gets the value with type conversion
        /// </summary>
        public T GetValue<T>()
        {
            if (Value == null) return default(T)!;

            try
            {
                return (T)Convert.ChangeType(Value, typeof(T));
            }
            catch
            {
                return default(T)!;
            }
        }

        /// <summary>
        /// Gets the value as a string (PowerShell-friendly)
        /// </summary>
        public string GetValueAsString()
        {
            return Value?.ToString() ?? string.Empty;
        }

        /// <summary>
        /// Gets the value as an integer (PowerShell-friendly)
        /// </summary>
        public int GetValueAsInt()
        {
            if (Value == null) return 0;

            try
            {
                return Convert.ToInt32(Value);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Gets a formatted representation of the value
        /// </summary>
        public string GetFormattedValue()
        {
            if (Value == null) return "(null)";

            switch (ValueType)
            {
                case RegistryValueKind.DWord:
                    return $"0x{Convert.ToUInt32(Value):X8} ({Value})";
                case RegistryValueKind.QWord:
                    return $"0x{Convert.ToUInt64(Value):X16} ({Value})";
                case RegistryValueKind.Binary:
                    return Value is byte[] bytes ? BitConverter.ToString(bytes) : Value.ToString() ?? "";
                case RegistryValueKind.MultiString:
                    return Value is string[] strings ? string.Join(", ", strings) : Value.ToString() ?? "";
                default:
                    return Value.ToString() ?? "";
            }
        }

        /// <summary>
        /// Converts a value to the specified registry type
        /// </summary>
        private object? ConvertValueToType(object? value, RegistryValueKind valueType)
        {
            if (value == null) return null;

            return valueType switch
            {
                RegistryValueKind.String => value.ToString(),
                RegistryValueKind.DWord => Convert.ToUInt32(value),
                RegistryValueKind.QWord => Convert.ToUInt64(value),
                RegistryValueKind.Binary => value is byte[] ? value : Convert.FromBase64String(value.ToString() ?? ""),
                RegistryValueKind.MultiString => value is string[] ? value : value.ToString()?.Split(new char[] { '\0' }, StringSplitOptions.RemoveEmptyEntries),
                RegistryValueKind.ExpandString => value.ToString(),
                _ => value
            };
        }

        /// <summary>
        /// Returns a string representation of the registry operation
        /// </summary>
        public override string ToString()
        {
            var operation = Operation switch
            {
                RegistryOperationType.Create => "CREATE",
                RegistryOperationType.Remove => "REMOVE",
                RegistryOperationType.Modify => "MODIFY",
                RegistryOperationType.RemoveKey => "REMOVE_KEY",
                _ => "UNKNOWN"
            };

            if (Operation == RegistryOperationType.RemoveKey)
            {
                return $"{operation}: {GetFullPath()}";
            }

            var valueName = string.IsNullOrEmpty(ValueName) ? "(Default)" : ValueName;
            return $"{operation}: {GetFullPath()}\\{valueName} = {Value} ({ValueType})";
        }
    }

    /// <summary>
    /// Types of registry operations
    /// </summary>
    public enum RegistryOperationType
    {
        /// <summary>
        /// Create a new registry value
        /// </summary>
        Create,

        /// <summary>
        /// Modify an existing registry value
        /// </summary>
        Modify,

        /// <summary>
        /// Remove a registry value
        /// </summary>
        Remove,

        /// <summary>
        /// Remove an entire registry key
        /// </summary>
        RemoveKey
    }

    /// <summary>
    /// Result of applying registry operations to a Windows image
    /// </summary>
    public class RegistryOperationResult
    {
        /// <summary>
        /// The mounted Windows image that operations were applied to
        /// </summary>
        public MountedWindowsImage MountedImage { get; set; } = new MountedWindowsImage();

        /// <summary>
        /// List of operations that were successfully applied
        /// </summary>
        public List<RegistryOperation> SuccessfulOperations { get; set; } = new List<RegistryOperation>();

        /// <summary>
        /// List of operations that failed to apply
        /// </summary>
        public List<RegistryOperation> FailedOperations { get; set; } = new List<RegistryOperation>();

        /// <summary>
        /// Error messages for failed operations
        /// </summary>
        public Dictionary<string, string> ErrorMessages { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Total number of operations processed
        /// </summary>
        public int TotalOperations => SuccessfulOperations.Count + FailedOperations.Count;

        /// <summary>
        /// Number of successful operations
        /// </summary>
        public int SuccessCount => SuccessfulOperations.Count;

        /// <summary>
        /// Number of failed operations
        /// </summary>
        public int FailureCount => FailedOperations.Count;

        /// <summary>
        /// Success percentage
        /// </summary>
        public double SuccessPercentage => TotalOperations > 0 ? (double)SuccessCount / TotalOperations * 100 : 0;

        /// <summary>
        /// Whether all operations were applied successfully
        /// </summary>
        public bool IsCompletelySuccessful => FailureCount == 0 && SuccessCount > 0;

        /// <summary>
        /// Returns a string representation of the operation result
        /// </summary>
        public override string ToString()
        {
            return $"{MountedImage.ImageName}: {SuccessCount} successful, {FailureCount} failed ({SuccessPercentage:F1}%)";
        }
    }
}
