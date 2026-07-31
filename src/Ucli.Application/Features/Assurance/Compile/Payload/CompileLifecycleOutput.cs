using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;

/// <summary> Represents final editor lifecycle evidence grouped under <c>payload.compile.lifecycle</c>. </summary>
internal sealed record CompileLifecycleOutput (
    string? ServerVersion,
    string? UnityVersion,
    UnityEditorMode? EditorMode,
    UnityEditorLifecycleState? LifecycleState,
    UnityEditorBlockingReason? BlockingReason,
    UnityEditorCompileState? CompileState,
    UnityEditorGenerationSnapshot? Generations,
    bool CanAcceptExecutionRequests,
    DateTimeOffset? ObservedAtUtc,
    UnityEditorActionRequired? ActionRequired,
    CompilePrimaryDiagnosticOutput? PrimaryDiagnostic);
