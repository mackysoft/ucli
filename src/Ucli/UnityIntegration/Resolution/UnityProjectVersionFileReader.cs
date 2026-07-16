using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.UnityIntegration.Resolution;

/// <summary> Reads Unity editor version values from <c>ProjectSettings/ProjectVersion.txt</c>. </summary>
internal static class UnityProjectVersionFileReader
{
    private const string ProjectSettingsDirectoryName = "ProjectSettings";
    private const string ProjectVersionFileName = "ProjectVersion.txt";
    private const string EditorVersionPrefix = "m_EditorVersion:";

    /// <summary> Creates the canonical project-version file path under a Unity project root. </summary>
    public static string GetProjectVersionPath (string unityProjectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(unityProjectRoot);

        return Path.Combine(
            unityProjectRoot,
            ProjectSettingsDirectoryName,
            ProjectVersionFileName);
    }

    /// <summary> Reads and extracts the editor version from one project-version file. </summary>
    public static ReadResult ReadEditorVersion (string projectVersionPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectVersionPath);

        try
        {
            using var reader = File.OpenText(projectVersionPath);
            return TryReadEditorVersion(reader, out var unityVersion)
                ? ReadResult.Success(unityVersion)
                : ReadResult.MissingEditorVersion();
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return ReadResult.PathInvalid(exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            return ReadResult.ReadFailure(exception.Message);
        }
        catch (IOException exception)
        {
            return ReadResult.ReadFailure(exception.Message);
        }
    }

    private static bool TryReadEditorVersion (
        TextReader reader,
        out string unityVersion)
    {
        unityVersion = string.Empty;

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (!line.StartsWith(EditorVersionPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var value = line[EditorVersionPrefix.Length..].Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            unityVersion = value;
            return true;
        }

        return false;
    }

    internal sealed class ReadResult
    {
        private ReadResult (
            ReadStatus status,
            string? unityVersion,
            string? errorMessage)
        {
            Status = status;
            UnityVersion = unityVersion;
            ErrorMessage = errorMessage;
        }

        public ReadStatus Status { get; }

        public string? UnityVersion { get; }

        public string? ErrorMessage { get; }

        public static ReadResult Success (string unityVersion)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(unityVersion);

            return new ReadResult(ReadStatus.Success, unityVersion, null);
        }

        public static ReadResult MissingEditorVersion ()
        {
            return new ReadResult(ReadStatus.MissingEditorVersion, null, null);
        }

        public static ReadResult PathInvalid (string errorMessage)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
            return new ReadResult(ReadStatus.PathInvalid, null, errorMessage);
        }

        public static ReadResult ReadFailure (string errorMessage)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
            return new ReadResult(ReadStatus.ReadFailure, null, errorMessage);
        }
    }

    internal enum ReadStatus
    {
        Success,
        MissingEditorVersion,
        PathInvalid,
        ReadFailure,
    }
}
