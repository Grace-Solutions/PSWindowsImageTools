using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using PSWindowsImageTools.Models;

namespace PSWindowsImageTools.Cmdlets
{
    /// <summary>
    /// Gets Microsoft Patch Tuesday dates
    /// </summary>
    [Cmdlet(VerbsCommon.Get, "PatchTuesday", DefaultParameterSetName = "Next")]
    [OutputType(typeof(PatchTuesday[]))]
    public class GetPatchTuesdayCmdlet : PSCmdlet
    {
        /// <summary>
        /// Date to calculate Patch Tuesdays after (defaults to current date)
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "Next")]
        [Parameter(Mandatory = false, ParameterSetName = "All")]
        [Parameter(Mandatory = false, ParameterSetName = "Remaining")]
        public DateTime After { get; set; } = DateTime.Now;

        /// <summary>
        /// Get all Patch Tuesdays for the year (derived from After date)
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "All")]
        public SwitchParameter All { get; set; }

        /// <summary>
        /// Get remaining Patch Tuesdays for the year (after the After date)
        /// </summary>
        [Parameter(Mandatory = false, ParameterSetName = "Remaining")]
        public SwitchParameter Remaining { get; set; }

        protected override void ProcessRecord()
        {
            try
            {
                var targetYear = After.Year;
                var patchTuesdays = new List<PatchTuesday>();

                if (All.IsPresent)
                {
                    // Get all Patch Tuesdays for the year (derived from After date)
                    patchTuesdays = GetAllPatchTuesdaysForYear(targetYear);
                }
                else if (Remaining.IsPresent)
                {
                    // Get remaining Patch Tuesdays for the year (after the After date)
                    patchTuesdays = GetRemainingPatchTuesdaysForYear(targetYear, After);
                }
                else
                {
                    // Default: Get next Patch Tuesday after the After date
                    var nextPatchTuesday = GetNextPatchTuesday(After);
                    patchTuesdays.Add(nextPatchTuesday);
                }

                // Output results
                foreach (var patchTuesday in patchTuesdays)
                {
                    WriteObject(patchTuesday);
                }
            }
            catch (Exception ex)
            {
                WriteError(new ErrorRecord(ex, "PSWindowsImageTools.Error", ErrorCategory.InvalidOperation, null));
            }
        }

        /// <summary>
        /// Gets all Patch Tuesdays for a given year
        /// </summary>
        private List<PatchTuesday> GetAllPatchTuesdaysForYear(int year)
        {
            var patchTuesdays = new List<PatchTuesday>();
            var now = DateTime.Now;

            for (int month = 1; month <= 12; month++)
            {
                var patchTuesday = CalculatePatchTuesday(year, month);
                patchTuesdays.Add(new PatchTuesday
                {
                    Date = patchTuesday,
                    Month = month,
                    Year = year,
                    HasOccurred = patchTuesday < now
                });
            }

            return patchTuesdays;
        }

        /// <summary>
        /// Gets remaining Patch Tuesdays for a year after the specified date
        /// </summary>
        private List<PatchTuesday> GetRemainingPatchTuesdaysForYear(int year, DateTime fromDate)
        {
            var allPatchTuesdays = GetAllPatchTuesdaysForYear(year);
            return allPatchTuesdays.Where(pt => pt.Date >= fromDate.Date).ToList();
        }

        /// <summary>
        /// Gets the next Patch Tuesday after the specified date
        /// </summary>
        private PatchTuesday GetNextPatchTuesday(DateTime fromDate)
        {
            // Check current month first
            var currentPatchTuesday = CalculatePatchTuesday(fromDate.Year, fromDate.Month);

            if (currentPatchTuesday >= fromDate.Date)
            {
                return new PatchTuesday
                {
                    Date = currentPatchTuesday,
                    Month = fromDate.Month,
                    Year = fromDate.Year,
                    HasOccurred = currentPatchTuesday < DateTime.Now
                };
            }

            // If current month's Patch Tuesday has passed, get next month's
            var nextMonth = fromDate.AddMonths(1);
            var nextPatchTuesday = CalculatePatchTuesday(nextMonth.Year, nextMonth.Month);

            return new PatchTuesday
            {
                Date = nextPatchTuesday,
                Month = nextMonth.Month,
                Year = nextMonth.Year,
                HasOccurred = nextPatchTuesday < DateTime.Now
            };
        }

        /// <summary>
        /// Calculates the Patch Tuesday (second Tuesday) for a given year and month
        /// </summary>
        private DateTime CalculatePatchTuesday(int year, int month)
        {
            // Start with the first day of the month
            var firstDay = new DateTime(year, month, 1);
            
            // Find the first Tuesday
            var daysUntilTuesday = ((int)DayOfWeek.Tuesday - (int)firstDay.DayOfWeek + 7) % 7;
            var firstTuesday = firstDay.AddDays(daysUntilTuesday);
            
            // The second Tuesday is 7 days later
            var secondTuesday = firstTuesday.AddDays(7);
            
            return secondTuesday;
        }
    }
}
