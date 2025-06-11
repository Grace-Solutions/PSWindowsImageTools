using System;
using System.Management.Automation;
using System.Text;

namespace PSWindowsImageTools.Services
{
    /// <summary>
    /// Centralized logging service for the module
    /// </summary>
    public static class LoggingService
    {
        private const string TimestampFormat = "yyyy/MM/dd HH:mm:ss.FFF";

        /// <summary>
        /// Formats a TimeSpan into human-readable duration text
        /// </summary>
        /// <param name="duration">The duration to format</param>
        /// <returns>Human-readable duration string</returns>
        public static string FormatDuration(TimeSpan duration)
        {
            var parts = new StringBuilder();

            if (duration.Days > 0)
            {
                parts.Append($"{duration.Days} day{(duration.Days == 1 ? "" : "s")}");
            }

            if (duration.Hours > 0)
            {
                if (parts.Length > 0) parts.Append(", ");
                parts.Append($"{duration.Hours} hour{(duration.Hours == 1 ? "" : "s")}");
            }

            if (duration.Minutes > 0)
            {
                if (parts.Length > 0) parts.Append(", ");
                parts.Append($"{duration.Minutes} minute{(duration.Minutes == 1 ? "" : "s")}");
            }

            if (duration.Seconds > 0 || parts.Length == 0)
            {
                if (parts.Length > 0) parts.Append(", ");
                parts.Append($"{duration.Seconds} second{(duration.Seconds == 1 ? "" : "s")}");
            }

            return parts.ToString();
        }
        /// <summary>
        /// Writes a verbose message with UTC timestamp
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for writing verbose output</param>
        /// <param name="message">The message to log</param>
        public static void WriteVerbose(PSCmdlet cmdlet, string message)
        {
            var timestamp = DateTime.UtcNow.ToString("yyyy/MM/dd HH:mm:ss.FFF");
            var formattedMessage = $"[{timestamp}] - [General] - {message}";
            cmdlet.WriteVerbose(formattedMessage);
        }

        /// <summary>
        /// Writes a verbose message with UTC timestamp and component context
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for writing verbose output</param>
        /// <param name="component">The component performing the operation</param>
        /// <param name="message">The message to log</param>
        public static void WriteVerbose(PSCmdlet cmdlet, string component, string message)
        {
            var timestamp = DateTime.UtcNow.ToString(TimestampFormat);
            var formattedMessage = $"[{timestamp}] - [{component}] - {message}";
            cmdlet.WriteVerbose(formattedMessage);
        }

        /// <summary>
        /// Writes a warning message with UTC timestamp
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for writing warning output</param>
        /// <param name="message">The warning message to log</param>
        public static void WriteWarning(PSCmdlet cmdlet, string message)
        {
            var timestamp = DateTime.UtcNow.ToString(TimestampFormat);
            var formattedMessage = $"[{timestamp}] - [General] - {message}";
            cmdlet.WriteWarning(formattedMessage);
        }

        /// <summary>
        /// Writes a warning message with UTC timestamp and component context
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for writing warning output</param>
        /// <param name="component">The component performing the operation</param>
        /// <param name="message">The warning message to log</param>
        public static void WriteWarning(PSCmdlet cmdlet, string component, string message)
        {
            var timestamp = DateTime.UtcNow.ToString(TimestampFormat);
            var formattedMessage = $"[{timestamp}] - [{component}] - {message}";
            cmdlet.WriteWarning(formattedMessage);
        }

        /// <summary>
        /// Writes an error message with UTC timestamp
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for writing error output</param>
        /// <param name="message">The error message to log</param>
        /// <param name="exception">Optional exception to include</param>
        public static void WriteError(PSCmdlet cmdlet, string message, Exception? exception = null)
        {
            var timestamp = DateTime.UtcNow.ToString(TimestampFormat);
            var formattedMessage = $"[{timestamp}] - [General] - {message}";

            if (exception != null)
            {
                formattedMessage += $" Exception: {exception.Message}";
            }

            var errorRecord = new ErrorRecord(
                exception ?? new InvalidOperationException(message),
                "PSWindowsImageTools.Error",
                ErrorCategory.NotSpecified,
                null);

            errorRecord.ErrorDetails = new ErrorDetails(formattedMessage);
            cmdlet.WriteError(errorRecord);
        }

        /// <summary>
        /// Writes an error message with UTC timestamp and component context
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for writing error output</param>
        /// <param name="component">The component performing the operation</param>
        /// <param name="message">The error message to log</param>
        /// <param name="exception">Optional exception to include</param>
        public static void WriteError(PSCmdlet cmdlet, string component, string message, Exception? exception = null)
        {
            var timestamp = DateTime.UtcNow.ToString(TimestampFormat);
            var formattedMessage = $"[{timestamp}] - [{component}] - {message}";

            if (exception != null)
            {
                formattedMessage += $" Exception: {exception.Message}";
            }

            var errorRecord = new ErrorRecord(
                exception ?? new InvalidOperationException(message),
                "PSWindowsImageTools.Error",
                ErrorCategory.NotSpecified,
                null);

            errorRecord.ErrorDetails = new ErrorDetails(formattedMessage);
            cmdlet.WriteError(errorRecord);
        }

        /// <summary>
        /// Writes a progress message
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for writing progress output</param>
        /// <param name="activity">The activity being performed</param>
        /// <param name="status">The current status</param>
        /// <param name="percentComplete">Percentage complete (0-100)</param>
        public static void WriteProgress(PSCmdlet cmdlet, string activity, string status, int percentComplete = -1)
        {
            var progressRecord = new ProgressRecord(0, activity, status);
            
            if (percentComplete >= 0 && percentComplete <= 100)
            {
                progressRecord.PercentComplete = percentComplete;
            }

            cmdlet.WriteProgress(progressRecord);
        }

        /// <summary>
        /// Writes a progress message with current item information
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for writing progress output</param>
        /// <param name="activity">The activity being performed</param>
        /// <param name="status">The current status</param>
        /// <param name="currentOperation">The current operation</param>
        /// <param name="percentComplete">Percentage complete (0-100)</param>
        public static void WriteProgress(PSCmdlet cmdlet, string activity, string status, string currentOperation, int percentComplete = -1)
        {
            var progressRecord = new ProgressRecord(0, activity, status)
            {
                CurrentOperation = currentOperation
            };
            
            if (percentComplete >= 0 && percentComplete <= 100)
            {
                progressRecord.PercentComplete = percentComplete;
            }

            cmdlet.WriteProgress(progressRecord);
        }

        /// <summary>
        /// Completes a progress operation
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for writing progress output</param>
        /// <param name="activity">The activity that was being performed</param>
        public static void CompleteProgress(PSCmdlet cmdlet, string activity)
        {
            var progressRecord = new ProgressRecord(0, activity, "Completed")
            {
                RecordType = ProgressRecordType.Completed
            };

            cmdlet.WriteProgress(progressRecord);
        }

        /// <summary>
        /// Logs the start of an operation
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for writing verbose output</param>
        /// <param name="operation">The operation being started</param>
        /// <param name="details">Additional details about the operation</param>
        public static void LogOperationStart(PSCmdlet cmdlet, string operation, string? details = null)
        {
            var message = $"Starting operation: {operation}";
            if (!string.IsNullOrEmpty(details))
            {
                message += $" - {details}";
            }
            WriteVerbose(cmdlet, message);
        }

        /// <summary>
        /// Logs the completion of an operation
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for writing verbose output</param>
        /// <param name="operation">The operation that completed</param>
        /// <param name="duration">Duration of the operation</param>
        /// <param name="details">Additional details about the operation</param>
        public static void LogOperationComplete(PSCmdlet cmdlet, string operation, TimeSpan duration, string? details = null)
        {
            var durationText = FormatDuration(duration);
            var message = $"Completed operation: {operation} (Duration: {durationText})";
            if (!string.IsNullOrEmpty(details))
            {
                message += $" - {details}";
            }
            WriteVerbose(cmdlet, message);
        }

        /// <summary>
        /// Logs an operation failure
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for writing error output</param>
        /// <param name="operation">The operation that failed</param>
        /// <param name="exception">The exception that occurred</param>
        /// <param name="details">Additional details about the failure</param>
        public static void LogOperationFailure(PSCmdlet cmdlet, string operation, Exception exception, string? details = null)
        {
            var message = $"Operation failed: {operation}";
            if (!string.IsNullOrEmpty(details))
            {
                message += $" - {details}";
            }
            WriteError(cmdlet, message, exception);
        }
    }
}
