using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.TestSupport;

internal sealed record LifecycleExecutionHostExitTerminalizationInvocation (
    ResolvedUnityProjectContext Project,
    LifecycleExecutionStartBinding Start,
    ExecutionRef CurrentReference,
    LifecycleExecutionTerminalRecord TerminalRecord)
{
    public LifecycleExecutionTerminalReason TerminalReason =>
        TerminalRecord.TerminalReason;

    public ExecutionApplicationState ApplicationState =>
        TerminalRecord.ApplicationState;

    public DateTimeOffset CompletedAtUtc =>
        TerminalRecord.CompletedAtUtc;
}

internal sealed class RecordingLifecycleExecutionHostExitTerminalizer :
    ILifecycleExecutionHostExitTerminalizer
{
    private readonly List<LifecycleExecutionHostExitTerminalizationInvocation>
        invocations = [];

    public RecordingLifecycleExecutionHostExitTerminalizer (
        LifecycleExecutionHostExitTerminalizationResult result)
    {
        Result = result ?? throw new ArgumentNullException(nameof(result));
    }

    public IReadOnlyList<LifecycleExecutionHostExitTerminalizationInvocation>
        Invocations => invocations;

    private LifecycleExecutionHostExitTerminalizationResult Result { get; }

    public ValueTask<LifecycleExecutionHostExitTerminalizationResult>
        TerminalizeAsync (
            ResolvedUnityProjectContext project,
            LifecycleExecutionStartBinding start,
            ExecutionRef currentReference,
            LifecycleExecutionTerminalFacts observedTerminalFacts,
            LifecycleExecutionHostExitTerminalRecordFactory
                terminalRecordFactory)
    {
        ArgumentNullException.ThrowIfNull(terminalRecordFactory);
        var terminalRecord = terminalRecordFactory(
            start,
            observedTerminalFacts);
        invocations.Add(
            new LifecycleExecutionHostExitTerminalizationInvocation(
                project,
                start,
                currentReference,
                terminalRecord));
        return ValueTask.FromResult(Result);
    }
}
