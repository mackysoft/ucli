namespace MackySoft.Ucli.Daemon;

/// <summary> Resolves daemon-specific local storage paths under <c>.ucli/local/fingerprints</c>. </summary>
internal static class DaemonStoragePathResolver
{
    private const string UcliDirectoryName = ".ucli";

    private const string LocalDirectoryName = "local";

    private const string FingerprintsDirectoryName = "fingerprints";

    private const string SessionFileName = "session.json";

    private const string DaemonLogFileName = "daemon.log";

    /// <summary> Resolves the fingerprint directory path for daemon artifacts. </summary>
    /// <param name="storageRoot"> The storage root path. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. Must not be <see langword="null" />, empty, or whitespace. </param>
    /// <returns> The absolute fingerprint directory path. </returns>
    /// <exception cref="ArgumentException"> Thrown when one argument is <see langword="null" />, empty, or whitespace. </exception>
    public static string ResolveFingerprintDirectory (
        string storageRoot,
        string projectFingerprint)
    {
        if (string.IsNullOrWhiteSpace(storageRoot))
        {
            throw new ArgumentException("Storage root must not be empty.", nameof(storageRoot));
        }

        if (string.IsNullOrWhiteSpace(projectFingerprint))
        {
            throw new ArgumentException("Project fingerprint must not be empty.", nameof(projectFingerprint));
        }

        return Path.Combine(
            Path.GetFullPath(storageRoot),
            UcliDirectoryName,
            LocalDirectoryName,
            FingerprintsDirectoryName,
            projectFingerprint.Trim());
    }

    /// <summary> Resolves the daemon session JSON file path. </summary>
    /// <param name="storageRoot"> The storage root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <returns> The absolute daemon session file path. </returns>
    public static string ResolveSessionPath (
        string storageRoot,
        string projectFingerprint)
    {
        return Path.Combine(
            ResolveFingerprintDirectory(storageRoot, projectFingerprint),
            SessionFileName);
    }

    /// <summary> Resolves the daemon log file path. </summary>
    /// <param name="storageRoot"> The storage root path. </param>
    /// <param name="projectFingerprint"> The project fingerprint value. </param>
    /// <returns> The absolute daemon log file path. </returns>
    public static string ResolveDaemonLogPath (
        string storageRoot,
        string projectFingerprint)
    {
        return Path.Combine(
            ResolveFingerprintDirectory(storageRoot, projectFingerprint),
            DaemonLogFileName);
    }
}