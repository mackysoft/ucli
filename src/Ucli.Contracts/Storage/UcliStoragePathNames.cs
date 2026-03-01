namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Defines the stable directory and file names used under <c>.ucli</c> storage. </summary>
public static class UcliStoragePathNames
{
    /// <summary> Gets the repository marker name used for root detection. </summary>
    public const string GitMarkerName = ".git";

    /// <summary> Gets the root directory name used by uCLI shared storage. </summary>
    public const string UcliDirectoryName = ".ucli";

    /// <summary> Gets the local-state directory name under <c>.ucli</c>. </summary>
    public const string LocalDirectoryName = "local";

    /// <summary> Gets the project-fingerprint directory name under <c>.ucli/local</c>. </summary>
    public const string FingerprintsDirectoryName = "fingerprints";

    /// <summary> Gets the shared config file name under <c>.ucli</c>. </summary>
    public const string ConfigFileName = "config.json";

    /// <summary> Gets the daemon session file name under one fingerprint directory. </summary>
    public const string SessionFileName = "session.json";

    /// <summary> Gets the daemon log file name under one fingerprint directory. </summary>
    public const string DaemonLogFileName = "daemon.log";

    /// <summary> Gets the daemon lifecycle lock file name under one fingerprint directory. </summary>
    public const string LifecycleLockFileName = "lifecycle.lock";

    /// <summary> Gets the plan-token signing key file name under one fingerprint directory. </summary>
    public const string PlanTokenKeyFileName = "plan-token.key";
}