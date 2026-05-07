using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.Shared.Unity.ProjectLock;

/// <summary> Classifies Unity editor logs that report an already-open project. </summary>
internal static class UnityProjectAlreadyOpenLogClassifier
{
    private const string AlreadyOpenMarker = "It looks like another Unity instance is running with this project open.";

    /// <summary> Determines whether the Unity editor log contains Unity's already-open project marker. </summary>
    /// <param name="editorLogPath"> The Unity editor log path. </param>
    /// <returns> <see langword="true" /> when the marker exists; otherwise <see langword="false" />. </returns>
    public static bool ContainsAlreadyOpenMarker (string editorLogPath)
    {
        if (string.IsNullOrWhiteSpace(editorLogPath))
        {
            return false;
        }

        try
        {
            foreach (var line in File.ReadLines(editorLogPath))
            {
                if (line.Contains(AlreadyOpenMarker, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception)
                                         || exception is IOException
                                         || exception is UnauthorizedAccessException)
        {
            return false;
        }

        return false;
    }

    /// <summary> Determines whether the supplied Unity editor log text contains Unity's already-open project marker. </summary>
    /// <param name="logText"> The Unity editor log text to inspect. </param>
    /// <returns> <see langword="true" /> when the marker exists; otherwise <see langword="false" />. </returns>
    public static bool ContainsAlreadyOpenMarkerInText (string logText)
    {
        return !string.IsNullOrWhiteSpace(logText)
            && logText.Contains(AlreadyOpenMarker, StringComparison.Ordinal);
    }
}
