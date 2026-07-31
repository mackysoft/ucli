using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Application.Features.Play.Common.Contracts;

/// <summary> Represents normalized lifecycle snapshot values emitted by Play Mode command payloads. </summary>
internal sealed record PlayLifecycleSnapshotOutput (
    string? ServerVersion,
    UnityEditorMode? EditorMode,
    string? UnityVersion,
    ProjectFingerprint? ProjectFingerprint,
    UnityEditorLifecycleState? LifecycleState,
    UnityEditorBlockingReason? BlockingReason,
    UnityEditorCompileState? CompileState,
    UnityEditorGenerationSnapshot? Generations,
    bool CanAcceptExecutionRequests,
    DateTimeOffset? ObservedAtUtc,
    UnityEditorActionRequired? ActionRequired,
    DaemonPrimaryDiagnosticOutput? PrimaryDiagnostic,
    UnityEditorPlayModeSnapshot PlayMode);
