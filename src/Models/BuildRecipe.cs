using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace PSWindowsImageTools.Models
{
    /// <summary>
    /// Represents a complete Windows image build recipe
    /// </summary>
    public class BuildRecipe
    {
        /// <summary>
        /// Recipe metadata
        /// </summary>
        [JsonProperty("metadata")]
        public RecipeMetadata Metadata { get; set; } = new RecipeMetadata();

        /// <summary>
        /// Image filtering configuration
        /// </summary>
        [JsonProperty("imageFilter")]
        public ImageFilterSection ImageFilter { get; set; } = new ImageFilterSection();

        /// <summary>
        /// AppX package removal configuration
        /// </summary>
        [JsonProperty("removeAppxPackages")]
        public RemoveAppxPackagesSection RemoveAppxPackages { get; set; } = new RemoveAppxPackagesSection();

        /// <summary>
        /// File copy operations
        /// </summary>
        [JsonProperty("copyFiles")]
        public CopyFilesSection CopyFiles { get; set; } = new CopyFilesSection();

        /// <summary>
        /// Wallpaper and lock screen configuration
        /// </summary>
        [JsonProperty("setWallpapers")]
        public SetWallpapersSection SetWallpapers { get; set; } = new SetWallpapersSection();

        /// <summary>
        /// Windows features to enable
        /// </summary>
        [JsonProperty("enableFeatures")]
        public EnableFeaturesSection EnableFeatures { get; set; } = new EnableFeaturesSection();

        /// <summary>
        /// Driver integration
        /// </summary>
        [JsonProperty("integrateDrivers")]
        public IntegrateDriversSection IntegrateDrivers { get; set; } = new IntegrateDriversSection();

        /// <summary>
        /// Update integration
        /// </summary>
        [JsonProperty("integrateUpdates")]
        public IntegrateUpdatesSection IntegrateUpdates { get; set; } = new IntegrateUpdatesSection();

        /// <summary>
        /// Features on Demand integration
        /// </summary>
        [JsonProperty("integrateFeaturesOnDemand")]
        public IntegrateFeaturesOnDemandSection IntegrateFeaturesOnDemand { get; set; } = new IntegrateFeaturesOnDemandSection();

        /// <summary>
        /// Registry modifications
        /// </summary>
        [JsonProperty("registryModifications")]
        public RegistryModificationsSection RegistryModifications { get; set; } = new RegistryModificationsSection();
    }

    /// <summary>
    /// Recipe metadata
    /// </summary>
    public class RecipeMetadata
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;

        [JsonProperty("version")]
        public string Version { get; set; } = "1.0.0";

        [JsonProperty("author")]
        public string Author { get; set; } = string.Empty;

        [JsonProperty("createdUtc")]
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        [JsonProperty("modifiedUtc")]
        public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Base class for recipe sections
    /// </summary>
    public abstract class RecipeSection
    {
        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = false;
    }

    /// <summary>
    /// Image filtering section
    /// </summary>
    public class ImageFilterSection : RecipeSection
    {
        [JsonProperty("inclusionExpression")]
        public string InclusionExpression { get; set; } = string.Empty;

        [JsonProperty("exclusionExpression")]
        public string ExclusionExpression { get; set; } = string.Empty;
    }

    /// <summary>
    /// AppX package removal section
    /// </summary>
    public class RemoveAppxPackagesSection : RecipeSection
    {
        [JsonProperty("patterns")]
        public List<string> Patterns { get; set; } = new List<string>();
    }

    /// <summary>
    /// File copy section
    /// </summary>
    public class CopyFilesSection : RecipeSection
    {
        [JsonProperty("items")]
        public List<CopyFileItem> Items { get; set; } = new List<CopyFileItem>();
    }

    /// <summary>
    /// Individual file copy item
    /// </summary>
    public class CopyFileItem
    {
        [JsonProperty("source")]
        public string Source { get; set; } = string.Empty;

        [JsonProperty("destination")]
        public string Destination { get; set; } = string.Empty;

        [JsonProperty("overwrite")]
        public bool Overwrite { get; set; } = true;
    }

    /// <summary>
    /// Wallpaper configuration section
    /// </summary>
    public class SetWallpapersSection : RecipeSection
    {
        [JsonProperty("wallpaper")]
        public string Wallpaper { get; set; } = string.Empty;

        [JsonProperty("lockScreen")]
        public string LockScreen { get; set; } = string.Empty;
    }

    /// <summary>
    /// Features enablement section
    /// </summary>
    public class EnableFeaturesSection : RecipeSection
    {
        [JsonProperty("patterns")]
        public List<string> Patterns { get; set; } = new List<string>();
    }

    /// <summary>
    /// Driver integration section
    /// </summary>
    public class IntegrateDriversSection : RecipeSection
    {
        [JsonProperty("paths")]
        public List<string> Paths { get; set; } = new List<string>();
    }

    /// <summary>
    /// Update integration section
    /// </summary>
    public class IntegrateUpdatesSection : RecipeSection
    {
        [JsonProperty("paths")]
        public List<string> Paths { get; set; } = new List<string>();
    }

    /// <summary>
    /// Features on Demand integration section
    /// </summary>
    public class IntegrateFeaturesOnDemandSection : RecipeSection
    {
        [JsonProperty("paths")]
        public List<string> Paths { get; set; } = new List<string>();
    }

    /// <summary>
    /// Registry modifications section
    /// </summary>
    public class RegistryModificationsSection : RecipeSection
    {
        [JsonProperty("modifications")]
        public List<RegistryModification> Modifications { get; set; } = new List<RegistryModification>();
    }

    /// <summary>
    /// Individual registry modification
    /// </summary>
    public class RegistryModification
    {
        [JsonProperty("hive")]
        public string Hive { get; set; } = string.Empty;

        [JsonProperty("key")]
        public string Key { get; set; } = string.Empty;

        [JsonProperty("valueName")]
        public string ValueName { get; set; } = string.Empty;

        [JsonProperty("valueData")]
        public object ValueData { get; set; } = string.Empty;

        [JsonProperty("valueType")]
        public string ValueType { get; set; } = "String";
    }
}
