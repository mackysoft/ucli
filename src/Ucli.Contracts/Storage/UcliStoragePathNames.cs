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

    /// <summary> Gets the project-scoped storage directory name under <c>.ucli/local</c>. </summary>
    public const string ProjectsDirectoryName = "projects";

    /// <summary> Gets the build-run storage directory name under <c>.ucli/local</c>. </summary>
    public const string BuildRunsDirectoryName = "build-runs";

    /// <summary> Gets the artifacts directory name under one project or build-run storage scope. </summary>
    public const string ArtifactsDirectoryName = "artifacts";

    /// <summary> Gets the work directory name under one project or build-run storage scope. </summary>
    public const string WorkDirectoryName = "work";

    /// <summary> Gets the read-index directory name under one project-scoped directory. </summary>
    public const string IndexDirectoryName = "index";

    /// <summary> Gets the read-index writer lock file name under one read-index directory. </summary>
    public const string ReadIndexWriteLockFileName = "write.lock";

    /// <summary> Gets the current read-index generation pointer file name. </summary>
    public const string ReadIndexCurrentGenerationFileName = "current";

    /// <summary> Gets the immutable read-index generation directory name. </summary>
    public const string ReadIndexGenerationsDirectoryName = "g";

    /// <summary> Gets the unpublished read-index staging directory name. </summary>
    public const string ReadIndexStagingDirectoryName = "s";

    /// <summary> Gets the read-index generation-retention marker directory name. </summary>
    public const string ReadIndexRetentionDirectoryName = "r";

    /// <summary> Gets the catalogs directory name under one read-index directory. </summary>
    public const string CatalogsDirectoryName = "catalogs";

    /// <summary> Gets the read-index types catalog file name. </summary>
    public const string TypesCatalogFileName = "types.catalog.json";

    /// <summary> Gets the read-index schemas catalog file name. </summary>
    public const string SchemasCatalogFileName = "schemas.catalog.json";

    /// <summary> Gets the read-index ops catalog file name. </summary>
    public const string OpsCatalogFileName = "ops.catalog.json";

    /// <summary> Gets the operation detail directory name under one read-index directory. </summary>
    public const string ReadIndexOpsDirectoryName = "ops";

    /// <summary> Gets the read-index ops describe artifact file extension. </summary>
    public const string OpsDescribeFileExtension = ".json";

    /// <summary> Gets the read-index asset-search lookup file name. </summary>
    public const string AssetSearchLookupFileName = "asset-search.lookup.json";

    /// <summary> Gets the read-index GUID-path lookup file name. </summary>
    public const string GuidPathLookupFileName = "guid-path.lookup.json";

    /// <summary> Gets the scene lookup directory name under one read-index directory. </summary>
    public const string ReadIndexScenesDirectoryName = "scenes";

    /// <summary> Gets the read-index scene-tree-lite lookup file extension. </summary>
    public const string SceneTreeLiteLookupFileExtension = ".json";

    /// <summary> Gets the read-index inputs manifest file name. </summary>
    public const string IndexInputsManifestFileName = "manifest.json";

    /// <summary> Gets the test-artifacts directory name under one project-scoped artifacts directory. </summary>
    public const string TestArtifactsDirectoryName = "test";

    /// <summary> Gets the Unity test results XML file name under one test-run artifacts directory. </summary>
    public const string TestResultsXmlFileName = "results.xml";

    /// <summary> Gets the Unity editor log file name under one test-run artifacts directory. </summary>
    public const string TestEditorLogFileName = "editor.log";

    /// <summary> Gets the screenshot directory name under project-scoped artifact and work roots. </summary>
    public const string ScreenshotDirectoryName = "screenshot";

    /// <summary> Gets the final screenshot PNG file name. </summary>
    public const string ScreenshotPngFileName = "screenshot.png";

    /// <summary> Gets the normalized raw screenshot staging file name. </summary>
    public const string ScreenshotRawStagingFileName = "capture.rgba";

    /// <summary> Gets the GameView recording directory name under project-scoped artifact and work roots. </summary>
    public const string GameViewRecordingsDirectoryName = "game-view-recordings";

    /// <summary> Gets the directory containing runtime-scoped GameView recording admission locks. </summary>
    public const string GameViewRecordingAdmissionLocksDirectoryName = "game-view-recording-admissions";

    /// <summary> Gets the extension for one runtime-scoped GameView recording admission lock. </summary>
    public const string GameViewRecordingAdmissionLockFileExtension = ".lock";

    /// <summary> Gets the normalized recording request artifact file name. </summary>
    public const string GameViewRecordingRequestFileName = "recording-request.json";

    /// <summary> Gets the recording manifest artifact file name. </summary>
    public const string GameViewRecordingManifestFileName = "recording-manifest.json";

    /// <summary> Gets the finalized GameView recording video file name. </summary>
    public const string GameViewRecordingVideoFileName = "game-view.mp4";

    /// <summary> Gets the recording cleanup artifact file name. </summary>
    public const string GameViewRecordingCleanupFileName = "recording-cleanup.json";

    /// <summary> Gets the recording terminal artifact file name. </summary>
    public const string GameViewRecordingTerminalFileName = "recording-terminal.json";

    /// <summary> Gets the recording diagnostics directory name. </summary>
    public const string GameViewRecordingDiagnosticsDirectoryName = "diagnostics";

    /// <summary> Gets the recovered partial recording artifact file name. </summary>
    public const string GameViewRecordingPartialOutputFileName = "partial-game-view.mp4";

    /// <summary> Gets the provider-private work directory name under one recording scope. </summary>
    public const string GameViewRecordingProviderWorkDirectoryName = "provider";

    /// <summary> Gets the durable execution-state file name under one recording work scope. </summary>
    public const string GameViewRecordingExecutionStateFileName = "execution-state.json";

    /// <summary> Gets the execution-state mutation lock file name under one recording work scope. </summary>
    public const string GameViewRecordingExecutionStateLockFileName = "execution-state.lock";

    /// <summary> Gets the terminal-publication ownership lock file name under one recording work scope. </summary>
    public const string GameViewRecordingTerminalPublicationLockFileName = "terminal-publication.lock";

    /// <summary> Gets the Recorder provider staging output file name. </summary>
    public const string GameViewRecordingProviderOutputFileName = "game-view.mp4";

    /// <summary> Gets the oneshot bootstrap-envelope directory name under one project-scoped directory. </summary>
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

    /// <summary> Gets the shared config file name under <c>.ucli</c>. </summary>
    public const string ConfigFileName = "config.json";

    /// <summary> Gets the daemon session file name under one project-scoped directory. </summary>
    public const string SessionFileName = "session.json";

    /// <summary> Gets the daemon session-generation lock file name under one project-scoped directory. </summary>
    public const string DaemonSessionLockFileName = "session.lock";

    /// <summary> Gets the daemon diagnosis file name under one project-scoped directory. </summary>
    public const string DaemonDiagnosisFileName = "daemon-diagnosis.json";

    /// <summary> Gets the daemon lifecycle observation file name under one project-scoped directory. </summary>
    public const string DaemonLifecycleFileName = "daemon-lifecycle.json";

    /// <summary> Gets the GUI supervisor manifest file name under one project-scoped directory. </summary>
    public const string GuiSupervisorManifestFileName = "gui-supervisor.json";

    /// <summary> Gets the GUI supervisor manifest lock file name under one project-scoped directory. </summary>
    public const string GuiSupervisorManifestLockFileName = "gui-supervisor.lock";

    /// <summary> Gets the launch-attempts directory name under one project-scoped directory. </summary>
    public const string LaunchAttemptsDirectoryName = "launch-attempts";

    /// <summary> Gets the launch-attempt startup diagnosis file name under one launch-attempt directory. </summary>
    public const string StartupDiagnosisFileName = "startup-diagnosis.json";

    /// <summary> Gets the uCLI Unity plugin marker cache file name under one project-scoped directory. </summary>
    public const string UnityUcliPluginMarkerCacheFileName = "ucli-plugin-marker-cache.json";

    /// <summary> Gets the mutation read-postcondition file name under one project-scoped directory. </summary>
    public const string MutationReadPostconditionFileName = "mutation-read-postcondition.json";

    /// <summary> Gets the Unity batchmode log file name under one project-scoped directory. </summary>
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

    /// <summary> Gets the plan-token signing key file name under one project-scoped directory. </summary>
    public const string PlanTokenKeyFileName = "plan-token.key";
}
