using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;

/// <summary> Represents final editor lifecycle evidence grouped under <c>payload.compile.lifecycle</c>. </summary>
internal sealed record CompileLifecycleOutput (
    string? ServerVersion,
    string? UnityVersion,
    DaemonEditorMode? EditorMode,
    IpcEditorLifecycleState? LifecycleState,
    IpcEditorBlockingReason? BlockingReason,
    IpcCompileState? CompileState,
    IpcUnityGenerationSnapshot? Generations,
    bool CanAcceptExecutionRequests,
    DateTimeOffset? ObservedAtUtc,
    string? ActionRequired,
    CompilePrimaryDiagnosticOutput? PrimaryDiagnostic);
