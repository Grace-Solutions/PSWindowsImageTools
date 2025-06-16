using System;
using System.Collections.Generic;
using System.Linq;

namespace PSWindowsImageTools.Models
{
    /// <summary>
    /// Represents the result of an ESD to Windows Image conversion operation
    /// </summary>
    public class ConversionResult
    {
        /// <summary>
        /// Whether the conversion was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Conversion mode used (WIM or Folder)
        /// </summary>
        public string Mode { get; set; } = string.Empty;

        /// <summary>
        /// Path to the source ESD file
        /// </summary>
        public string SourcePath { get; set; } = string.Empty;

        /// <summary>
        /// Output path (WIM file or folder)
        /// </summary>
        public string OutputPath { get; set; } = string.Empty;

        /// <summary>
        /// Start time of the conversion
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// End time of the conversion
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// Duration of the conversion
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// List of processed images
        /// </summary>
        public List<ProcessedImageInfo> ProcessedImages { get; set; } = new List<ProcessedImageInfo>();

        /// <summary>
        /// Error message if conversion failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Total size of converted images in bytes
        /// </summary>
        public long TotalSize { get; set; }

        /// <summary>
        /// Number of images successfully converted
        /// </summary>
        public int SuccessfulImages => ProcessedImages.Count(p => p.Success);

        /// <summary>
        /// Number of images that failed to convert
        /// </summary>
        public int FailedImages => ProcessedImages.Count(p => !p.Success);
    }

    /// <summary>
    /// Information about a processed image during conversion
    /// </summary>
    public class ProcessedImageInfo
    {
        /// <summary>
        /// Source image index
        /// </summary>
        public int SourceIndex { get; set; }

        /// <summary>
        /// Image name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Image edition
        /// </summary>
        public string Edition { get; set; } = string.Empty;

        /// <summary>
        /// Whether this image was processed successfully
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if processing failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Size of the processed image in bytes
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// Duration of processing this image
        /// </summary>
        public TimeSpan ProcessingDuration { get; set; }

        /// <summary>
        /// Output path for this image (for folder mode)
        /// </summary>
        public string? OutputPath { get; set; }
    }
}
