using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Clients;

/// <summary>
/// Observes the durable-start and action-dispatch boundaries needed to stop only one caller's wait.
/// </summary>
internal sealed class LifecycleExecutionDispatchObservation
{
    private readonly TaskCompletionSource<LifecycleExecutionStartBinding> startSource =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int actionDispatched;

    /// <summary> Gets the provider-confirmed durable start. </summary>
    public Task<LifecycleExecutionStartBinding> Start => startSource.Task;

    /// <summary> Gets whether the action request began after the durable start was confirmed. </summary>
    public bool ActionDispatched => Volatile.Read(ref actionDispatched) != 0;

    /// <summary> Reports that the durable start record was confirmed. </summary>
    public void ReportStarted (LifecycleExecutionStartBinding start)
    {
        ArgumentNullException.ThrowIfNull(start);
        if (startSource.Task.IsCompletedSuccessfully
            && !HasSameIdentity(startSource.Task.Result, start))
        {
            throw new InvalidOperationException(
                "Lifecycle Execution dispatch reported a different durable start identity.");
        }

        startSource.TrySetResult(start);
    }

    /// <summary> Reports that the action request is about to enter its provider transport. </summary>
    public void ReportActionDispatched ()
    {
        if (!startSource.Task.IsCompletedSuccessfully)
        {
            throw new InvalidOperationException(
                "Lifecycle Execution action dispatch requires a confirmed durable start.");
        }

        Interlocked.Exchange(ref actionDispatched, 1);
    }

    private static bool HasSameIdentity (
        LifecycleExecutionStartBinding established,
        LifecycleExecutionStartBinding candidate)
    {
        return established.LifecycleExecutionRef.Kind
                == candidate.LifecycleExecutionRef.Kind
            && established.LifecycleExecutionRef.Id
                == candidate.LifecycleExecutionRef.Id
            && established.LifecycleExecutionRef.DefinitionDigest
                == candidate.LifecycleExecutionRef.DefinitionDigest;
    }
}
