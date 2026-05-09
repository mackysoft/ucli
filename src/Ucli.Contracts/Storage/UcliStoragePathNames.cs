namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Defines the stable directory and file names used under <c>.ucli</c> storage. </summary>
public static class UcliStoragePathNames
{
    /// <summary> Gets the repository marker name used for root detection. </summary>
    public const string GitMarkerName = ".git";

    /// <summary> Gets the root directory name used by uCLI shared storage. </summary>
    public const string UcliDirectoryName = ".ucli";

    /// <summary> Gets the git-ignore file name under <c>.ucli</c>. </summary>
    public const string GitIgnoreFileName = ".gitignore";

    /// <summary> Gets the local-state directory name under <c>.ucli</c>. </summary>
    public const string LocalDirectoryName = "local";

    /// <summary> Gets the supervisor runtime-state directory name under <c>.ucli/local</c>. </summary>
    public const string SupervisorDirectoryName = "supervisor";

    /// <summary> Gets the project-fingerprint directory name under <c>.ucli/local</c>. </summary>
    public const string FingerprintsDirectoryName = "fingerprints";

    /// <summary> Gets the artifacts directory name under one fingerprint directory. </summary>
    public const string ArtifactsDirectoryName = "artifacts";

    /// <summary> Gets the read-index directory name under one fingerprint directory. </summary>
    public const string IndexDirectoryName = "index";

    /// <summary> Gets the catalogs directory name under one read-index directory. </summary>
    public const string CatalogsDirectoryName = "catalogs";

    /// <summary> Gets the lookups directory name under one read-index directory. </summary>
    public const string LookupsDirectoryName = "lookups";

    /// <summary> Gets the read-index inputs directory name under one read-index directory. </summary>
    public const string IndexInputsDirectoryName = "inputs";

    /// <summary> Gets the read-index types catalog file name. </summary>
    public const string TypesCatalogFileName = "types.catalog.json";

    /// <summary> Gets the read-index schemas catalog file name. </summary>
    public const string SchemasCatalogFileName = "schemas.catalog.json";

    /// <summary> Gets the read-index ops catalog file name. </summary>
    public const string OpsCatalogFileName = "ops.catalog.json";

    /// <summary> Gets the read-index ops describe artifact directory name. </summary>
    public const string OpsDescribeDirectoryName = "ops.describe";

    /// <summary> Gets the read-index ops describe artifact file extension. </summary>
    public const string OpsDescribeFileExtension = ".json";

    /// <summary> Gets the read-index asset-search lookup file name. </summary>
    public const string AssetSearchLookupFileName = "asset-search.lookup.json";

    /// <summary> Gets the read-index GUID-path lookup file name. </summary>
    public const string GuidPathLookupFileName = "guid-path.lookup.json";

    /// <summary> Gets the read-index scene-tree-lite lookup directory name. </summary>
    public const string SceneTreeLiteLookupDirectoryName = "scene-tree-lite";

    /// <summary> Gets the read-index scene-tree-lite lookup file extension. </summary>
    public const string SceneTreeLiteLookupFileExtension = ".lookup.json";

    /// <summary> Gets the read-index inputs manifest file name. </summary>
    public const string IndexInputsManifestFileName = "manifest.json";

    /// <summary> Gets the test-artifacts directory name under one fingerprint artifacts directory. </summary>
    public const string TestArtifactsDirectoryName = "test";

    /// <summary> Gets the shared config file name under <c>.ucli</c>. </summary>
    public const string ConfigFileName = "config.json";

    /// <summary> Gets the daemon session file name under one fingerprint directory. </summary>
    public const string SessionFileName = "session.json";

    /// <summary> Gets the daemon diagnosis file name under one fingerprint directory. </summary>
    public const string DaemonDiagnosisFileName = "daemon-diagnosis.json";

    /// <summary> Gets the daemon lifecycle observation file name under one fingerprint directory. </summary>
    public const string DaemonLifecycleFileName = "daemon-lifecycle.json";

    /// <summary> Gets the uCLI Unity plugin marker cache file name under one fingerprint directory. </summary>
    public const string UnityUcliPluginMarkerCacheFileName = "ucli-plugin-marker-cache.json";

    /// <summary> Gets the mutation read-postcondition file name under one fingerprint directory. </summary>
    public const string MutationReadPostconditionFileName = "mutation-read-postcondition.json";

    /// <summary> Gets the Unity batchmode log file name under one fingerprint directory. </summary>
    public const string UnityLogFileName = "unity.log";

    /// <summary> Gets the supervisor manifest file name under <c>.ucli/local/supervisor</c>. </summary>
    public const string SupervisorManifestFileName = "manifest.json";

    /// <summary> Gets the supervisor bootstrap lock file name under <c>.ucli/local/supervisor</c>. </summary>
    public const string SupervisorBootstrapLockFileName = "bootstrap.lock";

    /// <summary> Gets the supervisor log file name under <c>.ucli/local/supervisor</c>. </summary>
    public const string SupervisorLogFileName = "supervisor.log";

    /// <summary> Gets the launch-agent plist file name under <c>.ucli/local/supervisor</c>. </summary>
    public const string SupervisorLaunchAgentPlistFileName = "launch.agent.plist";

    /// <summary> Gets the plan-token signing key file name under one fingerprint directory. </summary>
    public const string PlanTokenKeyFileName = "plan-token.key";
}
