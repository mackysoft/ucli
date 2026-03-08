namespace MackySoft.Ucli.Execution;

/// <summary> Converts process execution timeouts to whole-second values accepted by external process runners. </summary>
internal static class ProcessTimeoutConverter
{
    /// <summary> Converts one positive timeout to ceil-rounded whole seconds with a minimum of one second. </summary>
    /// <param name="timeout"> The timeout to convert. </param>
    /// <returns> The converted timeout in seconds. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="timeout" /> is zero or negative. </exception>
    public static int ConvertToSeconds (TimeSpan timeout)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var timeoutSeconds = (int)Math.Ceiling(timeout.TotalSeconds);
        if (timeoutSeconds < 1)
        {
            return 1;
        }

        return timeoutSeconds;
    }
}