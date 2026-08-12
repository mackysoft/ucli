using MackySoft.Ucli.Contracts;

namespace MackySoft.Ucli.Application.Features.Programs.Persistence;

/// <summary> Owns the closed Program Run state machine and its common execution-reference projection. </summary>
internal static class ProgramRunStateSemantics
{
    public static ExecutionLifecycle ToExecutionLifecycle (ProgramRunState state)
    {
        return state switch
        {
            ProgramRunState.Created or ProgramRunState.Running or ProgramRunState.WaitingForRuntime => ExecutionLifecycle.Active,
            ProgramRunState.Cancelling => ExecutionLifecycle.Recovery,
            ProgramRunState.Completed or ProgramRunState.Failed or ProgramRunState.Cancelled or ProgramRunState.Interrupted => ExecutionLifecycle.Terminal,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Program Run state must be defined."),
        };
    }

    public static bool IsTerminal (ProgramRunState state) =>
        ToExecutionLifecycle(state) == ExecutionLifecycle.Terminal;

    public static bool IsTerminal (ProgramStepState state)
    {
        return state is ProgramStepState.Completed
            or ProgramStepState.Failed
            or ProgramStepState.Cancelled
            or ProgramStepState.Interrupted;
    }

    public static bool IsOngoing (ProgramStepState state) =>
        state is ProgramStepState.Planning
            or ProgramStepState.Running
            or ProgramStepState.WaitingForRuntime
            or ProgramStepState.Cancelling;

    public static bool CanTransitionTo (ProgramRunState from, ProgramRunState to)
    {
        if (IsTerminal(from))
        {
            return false;
        }

        return from switch
        {
            ProgramRunState.Created => to is ProgramRunState.Running
                or ProgramRunState.Completed
                or ProgramRunState.Cancelling
                or ProgramRunState.Failed
                or ProgramRunState.Interrupted,
            ProgramRunState.Running => to is ProgramRunState.WaitingForRuntime
                or ProgramRunState.Cancelling
                or ProgramRunState.Completed
                or ProgramRunState.Failed
                or ProgramRunState.Interrupted,
            ProgramRunState.WaitingForRuntime => to is ProgramRunState.Running
                or ProgramRunState.Cancelling
                or ProgramRunState.Failed
                or ProgramRunState.Interrupted,
            ProgramRunState.Cancelling => to is ProgramRunState.Cancelled
                or ProgramRunState.Failed
                or ProgramRunState.Interrupted,
            _ => false,
        };
    }

    public static bool CanTransitionTo (ProgramStepState from, ProgramStepState to)
    {
        if (!TextVocabulary.IsDefined(from) || !TextVocabulary.IsDefined(to) || IsTerminal(from))
        {
            return false;
        }
        if (from == to)
        {
            return true;
        }
        return from switch
        {
            ProgramStepState.Deferred => to is ProgramStepState.Planning or ProgramStepState.Cancelling or ProgramStepState.Failed or ProgramStepState.Cancelled or ProgramStepState.Interrupted,
            ProgramStepState.Planning => to is ProgramStepState.Running or ProgramStepState.WaitingForRuntime or ProgramStepState.Cancelling or ProgramStepState.Failed or ProgramStepState.Cancelled or ProgramStepState.Interrupted,
            ProgramStepState.Running => to is ProgramStepState.WaitingForRuntime or ProgramStepState.Cancelling or ProgramStepState.Completed or ProgramStepState.Failed or ProgramStepState.Cancelled or ProgramStepState.Interrupted,
            ProgramStepState.WaitingForRuntime => to is ProgramStepState.Running or ProgramStepState.Cancelling or ProgramStepState.Completed or ProgramStepState.Failed or ProgramStepState.Cancelled or ProgramStepState.Interrupted,
            ProgramStepState.Cancelling => to is ProgramStepState.Cancelled or ProgramStepState.Failed or ProgramStepState.Interrupted,
            _ => false,
        };
    }

    public static Verdict? AggregateVerdict (
        ProgramRunState state,
        IEnumerable<Verdict?> verdicts)
    {
        _ = ToExecutionLifecycle(state);
        return VerdictAggregation.Aggregate(verdicts);
    }
}
