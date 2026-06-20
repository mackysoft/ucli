using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution.UnityRequest;

/// <summary> Represents a host-executed Unity request without owning the IPC wire envelope. </summary>
internal abstract record UnityRequestPayload
{
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
        string BuildTarget,
        string UnityBuildTarget,
        string SceneSource,
        IReadOnlyList<string> ScenePaths,
        bool Development,
        string OutputPath,
        IpcBuildOutputLayout OutputLayout,
        string BuildReportPath,
        string BuildLogPath) : UnityRequestPayload;

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
