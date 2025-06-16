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
        /// Formats a TimeSpan into intelligent human-readable duration text
        /// Examples: "2 milliseconds", "648 seconds", "2 minutes", "3 hours", "2 days"
        /// </summary>
        /// <param name="duration">The duration to format</param>
        /// <returns>Human-readable duration string with intelligent unit selection</returns>
        public static string FormatDuration(TimeSpan duration)
        {
            // For very short durations, show milliseconds
            if (duration.TotalMilliseconds < 1000)
            {
                var ms = (int)duration.TotalMilliseconds;
                return $"{ms} millisecond{(ms == 1 ? "" : "s")}";
            }

            // For durations under 1 minute, show seconds (including fractional)
            if (duration.TotalMinutes < 1)
            {
                var totalSeconds = (int)duration.TotalSeconds;
                return $"{totalSeconds} second{(totalSeconds == 1 ? "" : "s")}";
            }

            // For durations under 1 hour, show minutes and seconds
            if (duration.TotalHours < 1)
            {
                var minutes = duration.Minutes;
                var seconds = duration.Seconds;

                if (seconds == 0)
                {
                    return $"{minutes} minute{(minutes == 1 ? "" : "s")}";
                }
                else
                {
                    return $"{minutes} minute{(minutes == 1 ? "" : "s")}, {seconds} second{(seconds == 1 ? "" : "s")}";
                }
            }

            // For durations under 1 day, show hours and minutes
            if (duration.TotalDays < 1)
            {
                var hours = duration.Hours;
                var minutes = duration.Minutes;

                if (minutes == 0)
                {
                    return $"{hours} hour{(hours == 1 ? "" : "s")}";
                }
                else
                {
                    return $"{hours} hour{(hours == 1 ? "" : "s")}, {minutes} minute{(minutes == 1 ? "" : "s")}";
                }
            }

            // For very long durations, show days and hours
            var days = duration.Days;
            var remainingHours = duration.Hours;

            if (remainingHours == 0)
            {
                return $"{days} day{(days == 1 ? "" : "s")}";
            }
            else
            {
                return $"{days} day{(days == 1 ? "" : "s")}, {remainingHours} hour{(remainingHours == 1 ? "" : "s")}";
            }
        }
        /// <summary>
        /// Writes a verbose message with UTC timestamp
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for writing verbose output</param>
        /// <param name="message">The message to log</param>
        public static void WriteVerbose(PSCmdlet? cmdlet, string message)
        {
            if (cmdlet == null) return;

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
        public static void WriteVerbose(PSCmdlet? cmdlet, string component, string message)
        {
            if (cmdlet == null) return;

            var timestamp = DateTime.UtcNow.ToString(TimestampFormat);
            var formattedMessage = $"[{timestamp}] - [{component}] - {message}";
            cmdlet.WriteVerbose(formattedMessage);
        }

        /// <summary>
        /// Writes a warning message with UTC timestamp
        /// </summary>
        /// <param name="cmdlet">The cmdlet instance for writing warning output</param>
        /// <param name="message">The warning message to log</param>
        public static void WriteWarning(PSCmdlet? cmdlet, string message)
        {
            if (cmdlet == null) return;

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
        public static void WriteWarning(PSCmdlet? cmdlet, string component, string message)
        {
            if (cmdlet == null) return;

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
        public static void WriteError(PSCmdlet? cmdlet, string message, Exception? exception = null)
        {
            if (cmdlet == null) return;

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
        public static void WriteError(PSCmdlet? cmdlet, string component, string message, Exception? exception = null)
        {
            if (cmdlet == null) return;

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
        public static void WriteProgress(PSCmdlet? cmdlet, string activity, string status, int percentComplete = -1)
        {
            if (cmdlet == null) return;

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
        public static void WriteProgress(PSCmdlet? cmdlet, string activity, string status, string currentOperation, int percentComplete = -1)
        {
            if (cmdlet == null) return;

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
        public static void CompleteProgress(PSCmdlet? cmdlet, string activity)
        {
            if (cmdlet == null) return;

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
        public static void LogOperationStart(PSCmdlet? cmdlet, string operation, string? details = null)
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
        public static void LogOperationComplete(PSCmdlet? cmdlet, string operation, TimeSpan duration, string? details = null)
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
        public static void LogOperationFailure(PSCmdlet? cmdlet, string operation, Exception exception, string? details = null)
        {
            var message = $"Operation failed: {operation}";
            if (!string.IsNullOrEmpty(details))
            {
                message += $" - {details}";
            }
            WriteError(cmdlet, message, exception);
        }

        /// <summary>
        /// Formats a timestamp in human-readable format with day name
        /// Example: "Wednesday, June 15, 2025 @ 7:48 AM"
        /// </summary>
        /// <param name="timestamp">Timestamp to format</param>
        /// <returns>Human-readable timestamp string</returns>
        public static string FormatTimestamp(DateTime timestamp)
        {
            return timestamp.ToString("dddd, MMMM dd, yyyy @ h:mm tt");
        }

        /// <summary>
        /// Formats a timestamp in short human-readable format
        /// Example: "Wed @ 7:48 AM"
        /// </summary>
        /// <param name="timestamp">Timestamp to format</param>
        /// <returns>Short human-readable timestamp string</returns>
        public static string FormatTimestampShort(DateTime timestamp)
        {
            return timestamp.ToString("ddd @ h:mm tt");
        }

        /// <summary>
        /// Formats duration in a compact format for progress messages
        /// Examples: "2ms", "30s", "5m", "2h", "1d"
        /// </summary>
        /// <param name="duration">Duration to format</param>
        /// <returns>Compact duration string</returns>
        public static string FormatDurationCompact(TimeSpan duration)
        {
            if (duration.TotalMilliseconds < 1000)
            {
                return $"{(int)duration.TotalMilliseconds}ms";
            }
            else if (duration.TotalMinutes < 1)
            {
                return $"{(int)duration.TotalSeconds}s";
            }
            else if (duration.TotalHours < 1)
            {
                return $"{(int)duration.TotalMinutes}m";
            }
            else if (duration.TotalDays < 1)
            {
                return $"{(int)duration.TotalHours}h";
            }
            else
            {
                return $"{(int)duration.TotalDays}d";
            }
        }

        /// <summary>
        /// Logs operation start with human-readable timestamp
        /// </summary>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <param name="serviceName">Name of the service</param>
        /// <param name="operationName">Name of the operation</param>
        /// <param name="details">Additional operation details</param>
        /// <returns>Start timestamp for duration calculation</returns>
        public static DateTime LogOperationStartWithTimestamp(PSCmdlet? cmdlet, string serviceName, string operationName, string? details = null)
        {
            var startTime = DateTime.UtcNow;
            var localTime = startTime.ToLocalTime();
            var message = string.IsNullOrEmpty(details)
                ? $"Starting {operationName} at {FormatTimestamp(localTime)}"
                : $"Starting {operationName} at {FormatTimestamp(localTime)} - {details}";

            WriteVerbose(cmdlet, serviceName, message);
            return startTime;
        }

        /// <summary>
        /// Logs operation completion with human-readable timestamp and intelligent duration
        /// Example: "Completed WIM Export at Wednesday, June 15, 2025 @ 7:48 AM (Duration: 2 minutes, 30 seconds)"
        /// </summary>
        /// <param name="cmdlet">PowerShell cmdlet for logging</param>
        /// <param name="serviceName">Name of the service</param>
        /// <param name="operationName">Name of the operation</param>
        /// <param name="startTime">Start timestamp from LogOperationStartWithTimestamp</param>
        /// <param name="details">Additional operation details</param>
        public static void LogOperationCompleteWithTimestamp(PSCmdlet? cmdlet, string serviceName, string operationName, DateTime startTime, string? details = null)
        {
            var endTime = DateTime.UtcNow;
            var localEndTime = endTime.ToLocalTime();
            var duration = endTime - startTime;
            var intelligentDuration = FormatDuration(duration);

            var message = string.IsNullOrEmpty(details)
                ? $"Completed {operationName} at {FormatTimestamp(localEndTime)} (Duration: {intelligentDuration})"
                : $"Completed {operationName} at {FormatTimestamp(localEndTime)} (Duration: {intelligentDuration}) - {details}";

            WriteVerbose(cmdlet, serviceName, message);
        }
    }
}
