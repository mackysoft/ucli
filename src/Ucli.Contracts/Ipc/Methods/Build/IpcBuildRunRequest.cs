using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a fully validated <c>build.run</c> IPC request payload. </summary>
public sealed record IpcBuildRunRequest
{
    /// <summary> Initializes one build-run request and snapshots all caller-owned collections. </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when the run identifier, input shape, runner shape, or collection contents violate the build-run contract.
    /// </exception>
    /// <exception cref="ArgumentNullException"> Thrown when a required reference value is <see langword="null" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when an enum value is undefined. </exception>
    [JsonConstructor]
    public IpcBuildRunRequest (
        Guid RunId,
        BuildProfileInputsKind InputKind,
        BuildTargetStableName? BuildTarget,
        BuildProfileSceneSource? SceneSource,
        IReadOnlyList<SceneAssetPath> ScenePaths,
        bool Development,
        string OutputPath,
        IpcBuildOutputLayout? OutputLayout,
        string BuildReportPath,
        string BuildLogPath,
        IReadOnlyList<DaemonEditorMode> AllowedEditorModes,
        BuildProfileProjectMutationMode ProjectMutationMode,
        BuildRunnerKind RunnerKind,
        Sha256Digest ProfileDigest,
        IpcUnityBuildProfileInput? UnityBuildProfile,
        string? ProfilePath,
        string? RunnerMethod,
        IReadOnlyDictionary<string, string> RunnerArguments,
        IReadOnlyList<string> RunnerEnvironmentVariables,
        IReadOnlyList<string> RunnerEnvironmentSecrets,
        IReadOnlyDictionary<string, string> RunnerEnvironmentVariableValues,
        IReadOnlyDictionary<string, string> RunnerEnvironmentSecretValues)
    {
        if (RunId == Guid.Empty)
        {
            throw new ArgumentException("Run id must not be empty.", nameof(RunId));
        }

        RequireDefined(InputKind, nameof(InputKind));
        RequireDefined(ProjectMutationMode, nameof(ProjectMutationMode));
        RequireDefined(RunnerKind, nameof(RunnerKind));
        if (BuildTarget.HasValue)
        {
            RequireDefined(BuildTarget.Value, nameof(BuildTarget));
        }

        if (SceneSource.HasValue)
        {
            RequireDefined(SceneSource.Value, nameof(SceneSource));
        }

        var scenePaths = SnapshotDistinctItems(ScenePaths, nameof(ScenePaths));
        var allowedEditorModes = SnapshotAllowedEditorModes(AllowedEditorModes);
        var runnerArguments = SnapshotMap(RunnerArguments, nameof(RunnerArguments));
        var runnerEnvironmentVariables = SnapshotNames(
            RunnerEnvironmentVariables,
            nameof(RunnerEnvironmentVariables));
        var runnerEnvironmentSecrets = SnapshotNames(
            RunnerEnvironmentSecrets,
            nameof(RunnerEnvironmentSecrets));
        var runnerEnvironmentVariableValues = SnapshotMap(
            RunnerEnvironmentVariableValues,
            nameof(RunnerEnvironmentVariableValues));
        var runnerEnvironmentSecretValues = SnapshotMap(
            RunnerEnvironmentSecretValues,
            nameof(RunnerEnvironmentSecretValues));

        ValidateInputShape(
            InputKind,
            BuildTarget,
            SceneSource,
            scenePaths,
            Development,
            OutputLayout,
            RunnerKind,
            UnityBuildProfile);
        ValidateRunnerShape(
            RunnerKind,
            OutputLayout,
            ProfilePath,
            RunnerMethod,
            runnerArguments,
            runnerEnvironmentVariables,
            runnerEnvironmentSecrets,
            runnerEnvironmentVariableValues,
            runnerEnvironmentSecretValues);

        this.RunId = RunId;
        this.InputKind = InputKind;
        this.BuildTarget = BuildTarget;
        this.SceneSource = SceneSource;
        this.ScenePaths = scenePaths;
        this.Development = Development;
        this.OutputPath = ContractArgumentGuard.RequireValue(OutputPath, nameof(OutputPath));
        this.OutputLayout = OutputLayout;
        this.BuildReportPath = ContractArgumentGuard.RequireValue(BuildReportPath, nameof(BuildReportPath));
        this.BuildLogPath = ContractArgumentGuard.RequireValue(BuildLogPath, nameof(BuildLogPath));
        this.AllowedEditorModes = allowedEditorModes;
        this.ProjectMutationMode = ProjectMutationMode;
        this.RunnerKind = RunnerKind;
        this.ProfileDigest = ProfileDigest ?? throw new ArgumentNullException(nameof(ProfileDigest));
        this.UnityBuildProfile = UnityBuildProfile;
        this.ProfilePath = ProfilePath;
        this.RunnerMethod = RunnerMethod;
        this.RunnerArguments = runnerArguments;
        this.RunnerEnvironmentVariables = runnerEnvironmentVariables;
        this.RunnerEnvironmentSecrets = runnerEnvironmentSecrets;
        this.RunnerEnvironmentVariableValues = runnerEnvironmentVariableValues;
        this.RunnerEnvironmentSecretValues = runnerEnvironmentSecretValues;
    }

    public Guid RunId { get; }

    public BuildProfileInputsKind InputKind { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BuildTargetStableName? BuildTarget { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BuildProfileSceneSource? SceneSource { get; }

    /// <summary> Gets an immutable snapshot of the distinct explicit scene paths. </summary>
    public IReadOnlyList<SceneAssetPath> ScenePaths { get; }

    public bool Development { get; }

    public string OutputPath { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IpcBuildOutputLayout? OutputLayout { get; }

    public string BuildReportPath { get; }

    public string BuildLogPath { get; }

    /// <summary> Gets an immutable, non-empty set of allowed editor modes in caller-specified order. </summary>
    public IReadOnlyList<DaemonEditorMode> AllowedEditorModes { get; }

    public BuildProfileProjectMutationMode ProjectMutationMode { get; }

    public BuildRunnerKind RunnerKind { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IpcUnityBuildProfileInput? UnityBuildProfile { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProfilePath { get; }

    public Sha256Digest ProfileDigest { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RunnerMethod { get; }

    public IReadOnlyDictionary<string, string> RunnerArguments { get; }

    public IReadOnlyList<string> RunnerEnvironmentVariables { get; }

    public IReadOnlyList<string> RunnerEnvironmentSecrets { get; }

    public IReadOnlyDictionary<string, string> RunnerEnvironmentVariableValues { get; }

    public IReadOnlyDictionary<string, string> RunnerEnvironmentSecretValues { get; }

    private static void ValidateInputShape (
        BuildProfileInputsKind inputKind,
        BuildTargetStableName? buildTarget,
        BuildProfileSceneSource? sceneSource,
        IReadOnlyList<SceneAssetPath> scenePaths,
        bool development,
        IpcBuildOutputLayout? outputLayout,
        BuildRunnerKind runnerKind,
        IpcUnityBuildProfileInput? unityBuildProfile)
    {
        if (inputKind == BuildProfileInputsKind.Explicit)
        {
            if (!buildTarget.HasValue || !sceneSource.HasValue)
            {
                throw new ArgumentException("Explicit build input requires a build target and scene source.", nameof(BuildTarget));
            }

            if (sceneSource == BuildProfileSceneSource.UnityBuildProfile)
            {
                throw new ArgumentException("Explicit build input cannot use the Unity Build Profile scene source.", nameof(SceneSource));
            }

            if (sceneSource == BuildProfileSceneSource.Explicit && scenePaths.Count == 0)
            {
                throw new ArgumentException("Explicit scene input requires at least one scene path.", nameof(ScenePaths));
            }

            if (sceneSource == BuildProfileSceneSource.EditorBuildSettings && scenePaths.Count != 0)
            {
                throw new ArgumentException("Editor Build Settings scene input must not include explicit scene paths.", nameof(ScenePaths));
            }

            if (unityBuildProfile != null)
            {
                throw new ArgumentException("Explicit build input must not include a Unity Build Profile.", nameof(UnityBuildProfile));
            }

            if (runnerKind == BuildRunnerKind.BuildPipeline && outputLayout == null)
            {
                throw new ArgumentNullException(nameof(OutputLayout));
            }

            return;
        }

        if (buildTarget.HasValue
            || sceneSource.HasValue
            || scenePaths.Count != 0
            || development
            || outputLayout != null)
        {
            throw new ArgumentException(
                "Unity Build Profile input must not include pre-resolved target, scene, option, or output layout values.",
                nameof(InputKind));
        }

        if (runnerKind != BuildRunnerKind.BuildPipeline)
        {
            throw new ArgumentException("Unity Build Profile input requires the BuildPipeline runner.", nameof(RunnerKind));
        }

        if (unityBuildProfile == null)
        {
            throw new ArgumentNullException(nameof(UnityBuildProfile));
        }

        if (unityBuildProfile.Digest != null || unityBuildProfile.ApplyAudit != null)
        {
            throw new ArgumentException(
                "A Unity Build Profile request may contain only the profile path.",
                nameof(UnityBuildProfile));
        }
    }

    private static void ValidateRunnerShape (
        BuildRunnerKind runnerKind,
        IpcBuildOutputLayout? outputLayout,
        string? profilePath,
        string? runnerMethod,
        IReadOnlyDictionary<string, string> runnerArguments,
        IReadOnlyList<string> runnerEnvironmentVariables,
        IReadOnlyList<string> runnerEnvironmentSecrets,
        IReadOnlyDictionary<string, string> runnerEnvironmentVariableValues,
        IReadOnlyDictionary<string, string> runnerEnvironmentSecretValues)
    {
        if (runnerKind == BuildRunnerKind.BuildPipeline)
        {
            if (profilePath != null
                || runnerMethod != null
                || runnerArguments.Count != 0
                || runnerEnvironmentVariables.Count != 0
                || runnerEnvironmentSecrets.Count != 0
                || runnerEnvironmentVariableValues.Count != 0
                || runnerEnvironmentSecretValues.Count != 0)
            {
                throw new ArgumentException(
                    "BuildPipeline runner must not include executeMethod invocation values.",
                    nameof(RunnerKind));
            }

            return;
        }

        ContractArgumentGuard.RequireValue(profilePath, nameof(ProfilePath));
        ContractArgumentGuard.RequireValue(runnerMethod, nameof(RunnerMethod));
        if (outputLayout != null)
        {
            throw new ArgumentException("ExecuteMethod runner must not include a BuildPipeline output layout.", nameof(OutputLayout));
        }

        RequireMatchingEnvironmentValues(
            runnerEnvironmentVariables,
            runnerEnvironmentVariableValues,
            nameof(RunnerEnvironmentVariableValues));
        RequireMatchingEnvironmentValues(
            runnerEnvironmentSecrets,
            runnerEnvironmentSecretValues,
            nameof(RunnerEnvironmentSecretValues));

        var names = new HashSet<string>(runnerEnvironmentVariables, StringComparer.Ordinal);
        for (var index = 0; index < runnerEnvironmentSecrets.Count; index++)
        {
            if (!names.Add(runnerEnvironmentSecrets[index]))
            {
                throw new ArgumentException(
                    $"Runner environment name '{runnerEnvironmentSecrets[index]}' cannot be both a variable and a secret.",
                    nameof(RunnerEnvironmentSecrets));
            }
        }
    }

    private static IReadOnlyList<T> SnapshotDistinctItems<T> (
        IReadOnlyList<T>? items,
        string parameterName)
        where T : class
    {
        var snapshot = ContractArgumentGuard.RequireItems(items, parameterName);
        var seen = new HashSet<T>();
        for (var index = 0; index < snapshot.Count; index++)
        {
            if (!seen.Add(snapshot[index]))
            {
                throw new ArgumentException($"Collection contains duplicate value '{snapshot[index]}'.", parameterName);
            }
        }

        return snapshot;
    }

    private static IReadOnlyList<DaemonEditorMode> SnapshotAllowedEditorModes (
        IReadOnlyList<DaemonEditorMode>? allowedEditorModes)
    {
        if (allowedEditorModes == null)
        {
            throw new ArgumentNullException(nameof(AllowedEditorModes));
        }

        if (allowedEditorModes.Count == 0)
        {
            throw new ArgumentException("At least one allowed editor mode is required.", nameof(AllowedEditorModes));
        }

        var snapshot = new DaemonEditorMode[allowedEditorModes.Count];
        var seen = new HashSet<DaemonEditorMode>();
        for (var index = 0; index < allowedEditorModes.Count; index++)
        {
            var editorMode = allowedEditorModes[index];
            RequireDefined(editorMode, nameof(AllowedEditorModes));
            if (!seen.Add(editorMode))
            {
                throw new ArgumentException($"Allowed editor modes contain duplicate value '{editorMode}'.", nameof(AllowedEditorModes));
            }

            snapshot[index] = editorMode;
        }

        return Array.AsReadOnly(snapshot);
    }

    private static IReadOnlyList<string> SnapshotNames (
        IReadOnlyList<string>? names,
        string parameterName)
    {
        if (names == null)
        {
            throw new ArgumentNullException(parameterName);
        }

        if (names.Count == 0)
        {
            return Array.Empty<string>();
        }

        var snapshot = new string[names.Count];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < names.Count; index++)
        {
            var name = ContractArgumentGuard.RequireValue(names[index], parameterName);
            if (!seen.Add(name))
            {
                throw new ArgumentException($"Collection contains duplicate value '{name}'.", parameterName);
            }

            snapshot[index] = name;
        }

        return Array.AsReadOnly(snapshot);
    }

    private static IReadOnlyDictionary<string, string> SnapshotMap (
        IReadOnlyDictionary<string, string>? values,
        string parameterName)
    {
        if (values == null)
        {
            throw new ArgumentNullException(parameterName);
        }

        var snapshot = new Dictionary<string, string>(values.Count, StringComparer.Ordinal);
        foreach (var pair in values)
        {
            var key = ContractArgumentGuard.RequireValue(pair.Key, parameterName);
            if (pair.Value == null)
            {
                throw new ArgumentException($"Map value for key '{key}' must not be null.", parameterName);
            }

            if (!snapshot.TryAdd(key, pair.Value))
            {
                throw new ArgumentException($"Map contains duplicate key '{key}'.", parameterName);
            }
        }

        return new ReadOnlyDictionary<string, string>(snapshot);
    }

    private static void RequireMatchingEnvironmentValues (
        IReadOnlyList<string> names,
        IReadOnlyDictionary<string, string> values,
        string parameterName)
    {
        if (names.Count != values.Count)
        {
            throw new ArgumentException("Runner environment names and values must contain the same keys.", parameterName);
        }

        for (var index = 0; index < names.Count; index++)
        {
            if (!values.ContainsKey(names[index]))
            {
                throw new ArgumentException("Runner environment names and values must contain the same keys.", parameterName);
            }
        }
    }

    private static void RequireDefined<TEnum> (
        TEnum value,
        string parameterName)
        where TEnum : struct, Enum
    {
        if (!TextVocabulary.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, $"{typeof(TEnum).Name} must be specified.");
        }
    }
}
