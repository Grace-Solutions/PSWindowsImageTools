using System;
using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json;

namespace PSWindowsImageTools.Models
{
    /// <summary>
    /// Represents a Windows Autopilot configuration matching Microsoft's official structure
    /// </summary>
    public class AutopilotConfiguration
    {
        /// <summary>
        /// Azure AD tenant ID
        /// </summary>
        [JsonProperty("CloudAssignedTenantId")]
        public string CloudAssignedTenantId { get; set; } = string.Empty;

        /// <summary>
        /// Device name template (e.g., "XYL-%SERIAL%")
        /// </summary>
        [JsonProperty("CloudAssignedDeviceName")]
        public string CloudAssignedDeviceName { get; set; } = string.Empty;

        /// <summary>
        /// Autopilot update timeout in milliseconds
        /// </summary>
        [JsonProperty("CloudAssignedAutopilotUpdateTimeout")]
        public int CloudAssignedAutopilotUpdateTimeout { get; set; } = 1800000;

        /// <summary>
        /// Whether Autopilot updates are disabled (1 = disabled, 0 = enabled)
        /// </summary>
        [JsonProperty("CloudAssignedAutopilotUpdateDisabled")]
        public int CloudAssignedAutopilotUpdateDisabled { get; set; } = 0;

        /// <summary>
        /// Whether forced enrollment is enabled (1 = enabled, 0 = disabled)
        /// </summary>
        [JsonProperty("CloudAssignedForcedEnrollment")]
        public int CloudAssignedForcedEnrollment { get; set; } = 1;

        /// <summary>
        /// Configuration version number
        /// </summary>
        [JsonProperty("Version")]
        public int Version { get; set; } = 2049;

        /// <summary>
        /// Comment or description of the profile
        /// </summary>
        [JsonProperty("Comment_File")]
        public string CommentFile { get; set; } = "Profile Standard Autopilot Deployment Profile";

        /// <summary>
        /// Azure AD server data as JSON string
        /// </summary>
        [JsonProperty("CloudAssignedAadServerData")]
        public string CloudAssignedAadServerData { get; set; } = string.Empty;

        /// <summary>
        /// OOBE configuration flags
        /// </summary>
        [JsonProperty("CloudAssignedOobeConfig")]
        public int CloudAssignedOobeConfig { get; set; } = 286;

        /// <summary>
        /// Domain join method (0 = Azure AD join, 1 = Hybrid Azure AD join)
        /// </summary>
        [JsonProperty("CloudAssignedDomainJoinMethod")]
        public int CloudAssignedDomainJoinMethod { get; set; } = 0;

        /// <summary>
        /// Zero Touch Deployment correlation ID
        /// </summary>
        [JsonProperty("ZtdCorrelationId")]
        public string ZtdCorrelationId { get; set; } = string.Empty;

        /// <summary>
        /// Cloud assigned tenant domain (can be null)
        /// </summary>
        [JsonProperty("CloudAssignedTenantDomain")]
        public string? CloudAssignedTenantDomain { get; set; } = null;

        /// <summary>
        /// Additional configuration properties for extensibility
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, object> AdditionalProperties { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Source file path (if loaded from file)
        /// </summary>
        [JsonIgnore]
        public string SourceFilePath { get; set; } = string.Empty;

        /// <summary>
        /// Indicates if the configuration has been modified
        /// </summary>
        [JsonIgnore]
        public bool IsModified { get; set; } = false;

        /// <summary>
        /// Validates the Autopilot configuration
        /// </summary>
        /// <returns>List of validation errors</returns>
        public List<string> Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrEmpty(CloudAssignedTenantId))
                errors.Add("CloudAssignedTenantId is required");

            if (CloudAssignedForcedEnrollment < 0 || CloudAssignedForcedEnrollment > 1)
                errors.Add("CloudAssignedForcedEnrollment must be 0 or 1");

            if (CloudAssignedAutopilotUpdateDisabled < 0 || CloudAssignedAutopilotUpdateDisabled > 1)
                errors.Add("CloudAssignedAutopilotUpdateDisabled must be 0 or 1");

            if (CloudAssignedAutopilotUpdateTimeout < 0)
                errors.Add("CloudAssignedAutopilotUpdateTimeout must be positive");

            if (CloudAssignedDomainJoinMethod < 0 || CloudAssignedDomainJoinMethod > 1)
                errors.Add("CloudAssignedDomainJoinMethod must be 0 (Azure AD) or 1 (Hybrid Azure AD)");

            if (string.IsNullOrEmpty(ZtdCorrelationId))
                errors.Add("ZtdCorrelationId is required");

            return errors;
        }

        /// <summary>
        /// Generates the CloudAssignedAadServerData JSON string
        /// </summary>
        public void GenerateAadServerData(string tenantUpn = "", string tenantDomain = "")
        {
            var aadData = new
            {
                ZeroTouchConfig = new
                {
                    CloudAssignedTenantUpn = tenantUpn,
                    ForcedEnrollment = CloudAssignedForcedEnrollment,
                    CloudAssignedTenantDomain = string.IsNullOrEmpty(tenantDomain) ? (object?)null : tenantDomain
                }
            };

            CloudAssignedAadServerData = JsonConvert.SerializeObject(aadData, Formatting.None);
        }

        /// <summary>
        /// Generates a new ZTD correlation ID
        /// </summary>
        public void GenerateZtdCorrelationId()
        {
            ZtdCorrelationId = Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Creates a copy of the configuration
        /// </summary>
        public AutopilotConfiguration Clone()
        {
            var json = JsonConvert.SerializeObject(this);
            var clone = JsonConvert.DeserializeObject<AutopilotConfiguration>(json);
            clone!.SourceFilePath = this.SourceFilePath;
            clone.IsModified = false;
            return clone;
        }

        /// <summary>
        /// Converts to JSON string
        /// </summary>
        public string ToJson(bool indented = true)
        {
            return JsonConvert.SerializeObject(this, indented ? Formatting.Indented : Formatting.None);
        }

        /// <summary>
        /// Creates from JSON string
        /// </summary>
        public static AutopilotConfiguration FromJson(string json)
        {
            return JsonConvert.DeserializeObject<AutopilotConfiguration>(json) ?? new AutopilotConfiguration();
        }
    }

    /// <summary>
    /// Result of applying Autopilot configuration to mounted images
    /// </summary>
    public class AutopilotApplicationResult
    {
        /// <summary>
        /// The mounted image that was processed
        /// </summary>
        public MountedWindowsImage MountedImage { get; set; } = new MountedWindowsImage();

        /// <summary>
        /// Whether the application was successful
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// Error message if application failed
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Path where the Autopilot configuration was applied
        /// </summary>
        public string AppliedPath { get; set; } = string.Empty;

        /// <summary>
        /// The configuration that was applied
        /// </summary>
        public AutopilotConfiguration Configuration { get; set; } = new AutopilotConfiguration();
    }
}
