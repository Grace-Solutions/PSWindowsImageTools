using System;

namespace PSWindowsImageTools.Models
{
    /// <summary>
    /// Represents a mounted Windows image with all necessary information for management
    /// </summary>
    public class MountedWindowsImage
    {
        /// <summary>
        /// Unique identifier for this mount session
        /// </summary>
        public string MountId { get; set; } = string.Empty;

        /// <summary>
        /// Path to the source WIM/ESD file
        /// </summary>
        public string SourceImagePath { get; set; } = string.Empty;

        /// <summary>
        /// Index of the image within the WIM/ESD file
        /// </summary>
        public int ImageIndex { get; set; }

        /// <summary>
        /// Name of the image (e.g., "Windows 11 Pro")
        /// </summary>
        public string ImageName { get; set; } = string.Empty;

        /// <summary>
        /// Edition of the image (e.g., "Professional")
        /// </summary>
        public string Edition { get; set; } = string.Empty;

        /// <summary>
        /// Architecture of the image (e.g., "AMD64")
        /// </summary>
        public string Architecture { get; set; } = string.Empty;

        /// <summary>
        /// Path where the image is mounted
        /// </summary>
        public string MountPath { get; set; } = string.Empty;

        /// <summary>
        /// GUID used for organizing mounts from the same WIM file
        /// </summary>
        public string WimGuid { get; set; } = string.Empty;

        /// <summary>
        /// When the image was mounted
        /// </summary>
        public DateTime MountedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Current status of the mount
        /// </summary>
        public MountStatus Status { get; set; } = MountStatus.Mounted;

        /// <summary>
        /// Whether the image is mounted read-only
        /// </summary>
        public bool IsReadOnly { get; set; } = true;

        /// <summary>
        /// Any error message if mount failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Size of the source image in bytes
        /// </summary>
        public long ImageSize { get; set; }

        /// <summary>
        /// Returns a string representation of the mounted image
        /// </summary>
        public override string ToString()
        {
            return $"[{ImageIndex}] {ImageName} ({Edition}) - {MountPath}";
        }
    }

    /// <summary>
    /// Status of a mounted image
    /// </summary>
    public enum MountStatus
    {
        /// <summary>
        /// Image is currently mounted and accessible
        /// </summary>
        Mounted,

        /// <summary>
        /// Image mount is in progress
        /// </summary>
        Mounting,

        /// <summary>
        /// Image unmount is in progress
        /// </summary>
        Unmounting,

        /// <summary>
        /// Image has been unmounted
        /// </summary>
        Unmounted,

        /// <summary>
        /// Mount operation failed
        /// </summary>
        Failed,

        /// <summary>
        /// Mount is in an unknown or corrupted state
        /// </summary>
        Corrupted
    }
}
