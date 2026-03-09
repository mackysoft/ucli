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

    /// <summary> Gets the artifacts directory name under one fingerprint directory. </summary>
    public const string ArtifactsDirectoryName = "artifacts";

    /// <summary> Gets the read-index directory name under one fingerprint directory. </summary>
    public const string IndexDirectoryName = "index";

    /// <summary> Gets the catalogs directory name under one read-index directory. </summary>
    public const string CatalogsDirectoryName = "catalogs";

    /// <summary> Gets the read-index inputs directory name under one read-index directory. </summary>
    public const string IndexInputsDirectoryName = "inputs";

    /// <summary> Gets the read-index types catalog file name. </summary>
    public const string TypesCatalogFileName = "types.catalog.json";

    /// <summary> Gets the read-index schemas catalog file name. </summary>
    public const string SchemasCatalogFileName = "schemas.catalog.json";

    /// <summary> Gets the read-index ops catalog file name. </summary>
    public const string OpsCatalogFileName = "ops.catalog.json";

    /// <summary> Gets the read-index inputs manifest file name. </summary>
    public const string IndexInputsManifestFileName = "manifest.json";

    /// <summary> Gets the test-artifacts directory name under one fingerprint artifacts directory. </summary>
    public const string TestArtifactsDirectoryName = "test";

    /// <summary> Gets the shared config file name under <c>.ucli</c>. </summary>
    public const string ConfigFileName = "config.json";

    /// <summary> Gets the daemon session file name under one fingerprint directory. </summary>
    public const string SessionFileName = "session.json";

    /// <summary> Gets the Unity batchmode log file name under one fingerprint directory. </summary>
    public const string UnityLogFileName = "unity.log";

    /// <summary> Gets the project lifecycle lock file name under one fingerprint directory. </summary>
    public const string LifecycleLockFileName = "lifecycle.lock";

    /// <summary> Gets the plan-token signing key file name under one fingerprint directory. </summary>
    public const string PlanTokenKeyFileName = "plan-token.key";
}