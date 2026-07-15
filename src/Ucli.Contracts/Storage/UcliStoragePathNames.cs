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

    /// <summary> Gets the work directory name under one fingerprint directory. </summary>
    public const string WorkDirectoryName = "work";

    /// <summary> Gets the read-index directory name under one fingerprint directory. </summary>
    public const string IndexDirectoryName = "index";

    /// <summary> Gets the read-index writer lock file name under one read-index directory. </summary>
    public const string ReadIndexWriteLockFileName = "write.lock";

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

    /// <summary> Gets the Unity test results XML file name under one test-run artifacts directory. </summary>
    public const string TestResultsXmlFileName = "results.xml";

    /// <summary> Gets the Unity editor log file name under one test-run artifacts directory. </summary>
    public const string TestEditorLogFileName = "editor.log";

    /// <summary> Gets the compile-artifacts directory name under one fingerprint artifacts directory. </summary>
    public const string CompileArtifactsDirectoryName = "compile";

    /// <summary> Gets the build-artifacts directory name under one fingerprint artifacts directory. </summary>
    public const string BuildArtifactsDirectoryName = "build";

    /// <summary> Gets the screenshot directory name under fingerprint artifact and work roots. </summary>
    public const string ScreenshotDirectoryName = "screenshot";

    /// <summary> Gets the final screenshot PNG file name. </summary>
    public const string ScreenshotPngFileName = "screenshot.png";

    /// <summary> Gets the normalized raw screenshot staging file name. </summary>
    public const string ScreenshotRawStagingFileName = "capture.rgba";

    /// <summary> Gets the build-work directory name under one fingerprint work directory. </summary>
    public const string BuildWorkDirectoryName = "build";

    /// <summary> Gets the recoverable IPC operation directory name under one fingerprint directory. </summary>
    public const string IpcOperationsDirectoryName = "ipc-operations";

    /// <summary> Gets the oneshot bootstrap-envelope directory name under one fingerprint directory. </summary>
    public const string OneshotBootstrapDirectoryName = "oneshot-bootstrap";

    /// <summary> Gets the file extension for one oneshot bootstrap envelope. </summary>
    public const string OneshotBootstrapFileExtension = ".json";

    /// <summary> Gets the build-run metadata artifact file name. </summary>
    public const string BuildMetadataFileName = "build.json";

    /// <summary> Gets the normalized Unity BuildReport artifact file name. </summary>
    public const string BuildReportFileName = "build-report.json";

    /// <summary> Gets the build-run Unity log artifact file name. </summary>
    public const string BuildLogFileName = "build.log";

    /// <summary> Gets the build output manifest artifact file name. </summary>
    public const string BuildOutputManifestFileName = "output-manifest.json";

    /// <summary> Gets the build output directory name under one build run artifact directory. </summary>
    public const string BuildOutputDirectoryName = "output";

    /// <summary> Gets the compile-run request artifact file name. </summary>
    public const string CompileRequestFileName = "request.json";

    /// <summary> Gets the compile-run summary artifact file name. </summary>
    public const string CompileSummaryFileName = "summary.json";

    /// <summary> Gets the compile-run diagnostics artifact file name. </summary>
    public const string CompileDiagnosticsFileName = "diagnostics.json";

    /// <summary> Gets the shared config file name under <c>.ucli</c>. </summary>
    public const string ConfigFileName = "config.json";

    /// <summary> Gets the daemon session file name under one fingerprint directory. </summary>
    public const string SessionFileName = "session.json";

    /// <summary> Gets the daemon session-generation lock file name under one fingerprint directory. </summary>
    public const string DaemonSessionLockFileName = "session.lock";

    /// <summary> Gets the daemon diagnosis file name under one fingerprint directory. </summary>
    public const string DaemonDiagnosisFileName = "daemon-diagnosis.json";

    /// <summary> Gets the daemon lifecycle observation file name under one fingerprint directory. </summary>
    public const string DaemonLifecycleFileName = "daemon-lifecycle.json";

    /// <summary> Gets the GUI supervisor manifest file name under one fingerprint directory. </summary>
    public const string GuiSupervisorManifestFileName = "gui-supervisor.json";

    /// <summary> Gets the GUI supervisor manifest lock file name under one fingerprint directory. </summary>
    public const string GuiSupervisorManifestLockFileName = "gui-supervisor.lock";

    /// <summary> Gets the launch-attempts directory name under one fingerprint directory. </summary>
    public const string LaunchAttemptsDirectoryName = "launch-attempts";

    /// <summary> Gets the launch-attempt startup diagnosis file name under one launch-attempt directory. </summary>
    public const string StartupDiagnosisFileName = "startup-diagnosis.json";

    /// <summary> Gets the uCLI Unity plugin marker cache file name under one fingerprint directory. </summary>
    public const string UnityUcliPluginMarkerCacheFileName = "ucli-plugin-marker-cache.json";

    /// <summary> Gets the mutation read-postcondition file name under one fingerprint directory. </summary>
    public const string MutationReadPostconditionFileName = "mutation-read-postcondition.json";

    /// <summary> Gets the Unity batchmode log file name under one fingerprint directory. </summary>
    public const string UnityLogFileName = "unity.log";

    /// <summary> Gets the supervisor manifest file name under <c>.ucli/local/supervisor</c>. </summary>
    public const string SupervisorManifestFileName = "manifest.json";

    /// <summary> Gets the supervisor manifest mutation lock file name under <c>.ucli/local/supervisor</c>. </summary>
    public const string SupervisorManifestLockFileName = "manifest.lock";

    /// <summary> Gets the supervisor bootstrap lock file name under <c>.ucli/local/supervisor</c>. </summary>
    public const string SupervisorBootstrapLockFileName = "bootstrap.lock";

    /// <summary> Gets the supervisor runtime ownership lock file name under <c>.ucli/local/supervisor</c>. </summary>
    public const string SupervisorRuntimeOwnershipLockFileName = "runtime-ownership.lock";

    /// <summary> Gets the supervisor log file name under <c>.ucli/local/supervisor</c>. </summary>
    public const string SupervisorLogFileName = "supervisor.log";

    /// <summary> Gets the launch-agent plist file name under <c>.ucli/local/supervisor</c>. </summary>
    public const string SupervisorLaunchAgentPlistFileName = "launch.agent.plist";

    /// <summary> Gets the plan-token signing key file name under one fingerprint directory. </summary>
    public const string PlanTokenKeyFileName = "plan-token.key";
}
