using System.Globalization;

namespace HanumanInstitute.SynthMultiViewer.Helpers;

/// <summary>
/// Formats durations for display in the job windows.
/// </summary>
public static class TimeSpanExtensions
{
    /// <summary>
    /// Formats a duration as h:mm:ss once it reaches an hour, otherwise m:ss.
    /// Negative durations are treated as zero.
    /// </summary>
    public static string FormatDuration(this TimeSpan time)
    {
        if (time < TimeSpan.Zero)
        {
            time = TimeSpan.Zero;
        }

        return time.TotalHours >= 1
            ? ((long)time.TotalHours).ToString(CultureInfo.InvariantCulture) + time.ToString(@"\:mm\:ss", CultureInfo.InvariantCulture)
            : time.ToString(@"m\:ss", CultureInfo.InvariantCulture);
    }
}
