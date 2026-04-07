using MackySoft.Ucli.Foundation;

namespace MackySoft.Ucli.Daemon;

/// <summary> Classifies daemon startup failures from one Unity batchmode startup log segment. </summary>
internal static class DaemonStartupFailureLogClassifier
{
    /// <summary> Extracts the latest startup log segment from one Unity batchmode log text. </summary>
    /// <param name="logText"> The complete Unity batchmode log text. </param>
    /// <returns> The latest startup log segment, or the original text when no startup marker exists. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="logText" /> is <see langword="null" />. </exception>
    public static string GetLatestStartupLogText (string logText)
    {
        ArgumentNullException.ThrowIfNull(logText);
        const string startupMarker = "COMMAND LINE ARGUMENTS:";
        var markerIndex = logText.LastIndexOf(startupMarker, StringComparison.Ordinal);
        return markerIndex >= 0
            ? logText[markerIndex..]
            : logText;
    }

    /// <summary> Tries to classify one daemon startup failure from one startup log segment. </summary>
    /// <param name="startupLogText"> The latest Unity startup log segment. </param>
    /// <param name="error"> The structured startup failure when classification succeeds. </param>
    /// <returns> <see langword="true" /> when one known startup failure was classified; otherwise, <see langword="false" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="startupLogText" /> is <see langword="null" />. </exception>
    public static bool TryClassify (
        string startupLogText,
        out ExecutionError? error)
    {
        ArgumentNullException.ThrowIfNull(startupLogText);

        if (TryGetCompilerErrorSummary(startupLogText, out var compilerErrorSummary))
        {
            error = ExecutionError.InternalError(
                $"Unity daemon startup failed because scripts have compiler errors. {compilerErrorSummary}");
            return true;
        }

        if (TryGetPackageResolutionErrorSummary(startupLogText, out var packageErrorSummary))
        {
            error = ExecutionError.InternalError(
                $"Unity daemon startup failed because package resolution failed. {packageErrorSummary}");
            return true;
        }

        error = null;
        return false;
    }

    private static bool TryGetCompilerErrorSummary (
        string logText,
        out string summary)
    {
        const string compilerErrorsMarker = "Scripts have compiler errors";
        summary = string.Empty;

        var lines = logText.Split('\n');
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.Length == 0)
            {
                continue;
            }

            if (trimmedLine.Contains("error CS", StringComparison.OrdinalIgnoreCase))
            {
                summary = $"FirstError={trimmedLine}";
                return true;
            }

            if (trimmedLine.Contains(compilerErrorsMarker, StringComparison.OrdinalIgnoreCase))
            {
                summary = $"Marker={trimmedLine}";
                return true;
            }
        }

        return false;
    }

    private static bool TryGetPackageResolutionErrorSummary (
        string logText,
        out string summary)
    {
        const string packageFailureMarker = "An error occurred while resolving packages:";
        summary = string.Empty;

        var lines = logText.Split('\n');
        var markerFound = false;
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.Length == 0)
            {
                continue;
            }

            if (!markerFound)
            {
                if (trimmedLine.Contains(packageFailureMarker, StringComparison.OrdinalIgnoreCase))
                {
                    markerFound = true;
                    summary = $"Marker={trimmedLine}";
                }

                continue;
            }

            if (trimmedLine.StartsWith("Project has invalid dependencies:", StringComparison.OrdinalIgnoreCase))
            {
                summary = $"Marker={trimmedLine}";
                continue;
            }

            summary = $"FirstError={trimmedLine}";
            return true;
        }

        return markerFound;
    }
}