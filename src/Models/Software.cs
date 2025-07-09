using System;

namespace PSWindowsImageTools.Models
{
    /// <summary>
    /// Represents installed software information from registry
    /// </summary>
    public class Software
    {
        /// <summary>
        /// Software publisher/vendor
        /// </summary>
        public string Publisher { get; set; } = string.Empty;

        /// <summary>
        /// Display name of the software
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Version of the software (parsed using utilities if possible, otherwise original string)
        /// </summary>
        public object? DisplayVersion { get; set; }

        /// <summary>
        /// Install date of the software (parsed from registry if available, otherwise original string)
        /// </summary>
        public object? InstallDate { get; set; }

        /// <summary>
        /// Registry key path where this software entry was found
        /// </summary>
        public string RegistryKeyPath { get; set; } = string.Empty;
    }
}
