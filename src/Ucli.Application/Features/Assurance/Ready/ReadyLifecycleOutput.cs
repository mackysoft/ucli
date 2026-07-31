using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Represents normalized Unity lifecycle evidence emitted by ready. </summary>
internal sealed record ReadyLifecycleOutput (
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
    ReadyPrimaryDiagnosticOutput? PrimaryDiagnostic,
    UnityEditorPlayModeSnapshot? PlayMode);
