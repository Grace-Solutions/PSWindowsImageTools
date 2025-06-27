using System;

namespace PSWindowsImageTools.Models
{
    /// <summary>
    /// Represents a Microsoft Patch Tuesday date
    /// </summary>
    public class PatchTuesday
    {
        /// <summary>
        /// The date of Patch Tuesday
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// The month (1-12) of this Patch Tuesday
        /// </summary>
        public int Month { get; set; }

        /// <summary>
        /// The year of this Patch Tuesday
        /// </summary>
        public int Year { get; set; }

        /// <summary>
        /// Whether this Patch Tuesday has already occurred
        /// </summary>
        public bool HasOccurred { get; set; }

        /// <summary>
        /// Gets the month name
        /// </summary>
        public string MonthName => Date.ToString("MMMM");

        /// <summary>
        /// Gets the week number of the year (1-53)
        /// </summary>
        public int WeekOfYear => System.Globalization.CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(
            Date, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Sunday);

        /// <summary>
        /// Gets the week number within the month (1-6)
        /// Calculates which week of the month this date falls in
        /// </summary>
        public int WeekOfMonth
        {
            get
            {
                // Get the first day of the month
                var firstDayOfMonth = new DateTime(Date.Year, Date.Month, 1);

                // Calculate how many days into the month we are
                var dayOfMonth = Date.Day;

                // Calculate how many days from the start of the week the first day falls
                var firstDayOffset = (int)firstDayOfMonth.DayOfWeek;

                // Calculate the week number (1-based)
                return (dayOfMonth + firstDayOffset - 1) / 7 + 1;
            }
        }

        /// <summary>
        /// Gets the quarter of the year (1-4)
        /// </summary>
        public int Quarter => (Month - 1) / 3 + 1;

        /// <summary>
        /// Gets the day of the year (1-366)
        /// </summary>
        public int DayOfYear => Date.DayOfYear;

        /// <summary>
        /// Gets a friendly description of this Patch Tuesday
        /// </summary>
        public string Description => $"Patch Tuesday - {MonthName} {Year}";

        /// <summary>
        /// Gets the number of days until this Patch Tuesday (negative if past)
        /// </summary>
        public int DaysFromNow => (Date - DateTime.Now.Date).Days;

        /// <summary>
        /// Gets whether this Patch Tuesday is in the current month
        /// </summary>
        public bool IsCurrentMonth => Date.Year == DateTime.Now.Year && Date.Month == DateTime.Now.Month;

        /// <summary>
        /// Gets whether this Patch Tuesday is next month
        /// </summary>
        public bool IsNextMonth
        {
            get
            {
                var nextMonth = DateTime.Now.AddMonths(1);
                return Date.Year == nextMonth.Year && Date.Month == nextMonth.Month;
            }
        }

        /// <summary>
        /// Returns a string representation of this Patch Tuesday
        /// </summary>
        public override string ToString()
        {
            var status = HasOccurred ? "Past" : "Upcoming";
            return $"{Date:yyyy-MM-dd} ({MonthName} {Year}) - {status}";
        }
    }
}
