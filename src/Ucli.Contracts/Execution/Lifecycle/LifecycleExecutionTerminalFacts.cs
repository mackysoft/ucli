using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Contracts.Execution.Lifecycle;

/// <summary>
/// Carries terminal facts resolved independently of an action's typed result and evidence.
/// </summary>
internal readonly record struct LifecycleExecutionTerminalFacts (
    LifecycleExecutionTerminalReason TerminalReason,
    ExecutionApplicationState ApplicationState,
    UnityEditorGenerationSnapshot? TerminalGeneration,
    DateTimeOffset CompletedAtUtc);
