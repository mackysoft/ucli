using System.Collections.ObjectModel;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution.UnityRequest;

/// <summary> Represents a host-executed Unity request without owning the IPC wire envelope. </summary>
internal abstract record UnityRequestPayload
{
    private static readonly IReadOnlyDictionary<string, string> EmptyStringMap =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary> Represents a request whose method and payload are already owned by a host adapter. </summary>
    internal sealed record Raw (
        string Method,
        JsonElement Payload) : UnityRequestPayload;

    /// <summary> Represents a lifecycle ping request prepared by application orchestration. </summary>
    internal sealed record Ping (
        string ClientVersion,
        bool FailFast = false) : UnityRequestPayload;

    /// <summary> Represents a compile assurance request prepared by application orchestration. </summary>
    internal sealed record Compile (
        string RunId) : UnityRequestPayload;

    /// <summary> Represents a build assurance request prepared by application orchestration. </summary>
    internal sealed record BuildRun (
        string RunId,
        string InputKind,
        string? BuildTarget,
        string? UnityBuildTarget,
        string? SceneSource,
        IReadOnlyList<string> ScenePaths,
        bool Development,
        string OutputPath,
        IpcBuildOutputLayout? OutputLayout,
        string BuildReportPath,
        string BuildLogPath,
        IReadOnlyList<string> AllowedEditorModes,
        string ProjectMutationMode,
        string RunnerKind) : UnityRequestPayload
    {
        /// <summary> Gets the Unity Build Profile asset input when Unity resolves build inputs. </summary>
        public IpcUnityBuildProfileInput? UnityBuildProfile { get; init; }

        /// <summary> Gets the resolved build profile path used for runner context construction. </summary>
        public string? ProfilePath { get; init; }

        /// <summary> Gets the canonical build profile digest used for runner context construction. </summary>
        public string? ProfileDigest { get; init; }

        /// <summary> Gets the resolved executeMethod runner method identity. </summary>
        public string? RunnerMethod { get; init; }

        /// <summary> Gets the substitution-resolved non-secret runner arguments. </summary>
        public IReadOnlyDictionary<string, string> RunnerArguments { get; init; } = EmptyStringMap;

        /// <summary> Gets the requested non-secret runner environment variable names. </summary>
        public IReadOnlyList<string> RunnerEnvironmentVariables { get; init; } = Array.Empty<string>();

        /// <summary> Gets the requested secret runner environment names. </summary>
        public IReadOnlyList<string> RunnerEnvironmentSecrets { get; init; } = Array.Empty<string>();

        /// <summary> Gets non-secret environment values resolved by the uCLI runtime for IPC delivery only. </summary>
        public IReadOnlyDictionary<string, string> RunnerEnvironmentVariableValues { get; init; } = EmptyStringMap;

        /// <summary> Gets secret environment values resolved by the uCLI runtime for IPC delivery only. </summary>
        public IReadOnlyDictionary<string, string> RunnerEnvironmentSecretValues { get; init; } = EmptyStringMap;
    }

    /// <summary> Represents a Unity Test Framework run request prepared by application orchestration. </summary>
    internal sealed record TestRun (
        string TestPlatform,
        string? TestFilter,
        string[] TestCategories,
        string[] AssemblyNames,
        string? TestSettingsPath,
        string ResultsXmlPath,
        string EditorLogPath,
        bool FailFast,
        string RunId) : UnityRequestPayload;

    /// <summary> Represents a Play Mode status request prepared by application orchestration. </summary>
    internal sealed record PlayStatus : UnityRequestPayload;

    /// <summary> Represents a screenshot capture request prepared by application orchestration. </summary>
    /// <param name="Target"> The screenshot target literal. </param>
    /// <param name="RequestedWidth"> The requested GameView width, or <see langword="null" /> for the current surface size. </param>
    /// <param name="RequestedHeight"> The requested GameView height, or <see langword="null" /> for the current surface size. </param>
    /// <param name="StagingPath"> The host-owned absolute raw-image staging path. </param>
    /// <param name="TimeoutMilliseconds"> The effective server-side capture timeout in milliseconds. </param>
    internal sealed record ScreenshotCapture (
        string Target,
        int? RequestedWidth,
        int? RequestedHeight,
        string StagingPath,
        int TimeoutMilliseconds) : UnityRequestPayload;

    /// <summary> Represents a Play Mode enter request prepared by application orchestration. </summary>
    internal sealed record PlayEnter (
        int TimeoutMilliseconds) : UnityRequestPayload;

    /// <summary> Represents a Play Mode exit request prepared by application orchestration. </summary>
    internal sealed record PlayExit (
        int TimeoutMilliseconds) : UnityRequestPayload;

    /// <summary> Represents an execute request whose execute-arguments JSON was already prepared. </summary>
    internal sealed record ExecuteJson (
        UcliCommand Command,
        JsonElement ExecuteArguments,
        bool FailFast,
        bool AllowDangerous = false,
        string? PlanToken = null,
        bool AllowPlayMode = false) : UnityRequestPayload;

    /// <summary> Represents a single-operation execute request prepared by application orchestration. </summary>
    internal sealed record ExecuteOperation (
        UcliCommand Command,
        string RequestId,
        string OperationId,
        string OperationName,
        JsonElement Args,
        bool FailFast,
        bool AllowDangerous = false,
        string? PlanToken = null) : UnityRequestPayload;
}
