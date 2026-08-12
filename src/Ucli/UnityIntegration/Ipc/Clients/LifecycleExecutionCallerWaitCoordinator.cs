using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityRequest;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
using MackySoft.Ucli.UnityIntegration.Ipc.Failures;
using MackySoft.Ucli.UnityIntegration.Ipc.Recovery;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Clients;

/// <summary>
/// Separates one caller's wait cancellation from an already-started Lifecycle Execution dispatch.
/// </summary>
internal static class LifecycleExecutionCallerWaitCoordinator
{
    /// <summary>
    /// Waits for provider dispatch completion until the caller leaves, retaining the authoritative
    /// durable start and dispatch fact when the execution continues independently.
    /// </summary>
    /// <param name="unityProject"> The resolved Unity project that owns the durable start. </param>
    /// <param name="dispatchRequest"> The typed IPC dispatch request. </param>
    /// <param name="deadline"> The current caller's IPC wait deadline. </param>
    /// <param name="dispatch"> Starts the provider transport dispatch and accepts lifecycle boundary observations. </param>
    /// <param name="cancellationToken"> Stops only the current caller's wait. </param>
    /// <returns>
    /// The provider result when dispatch completes first; otherwise a caller-wait failure retaining
    /// the authoritative start and the observed action-dispatch fact.
    /// </returns>
    public static async ValueTask<UnityRequestExecutionResult> WaitAsync (
        ResolvedUnityProjectContext unityProject,
        UnityIpcDispatchRequest dispatchRequest,
        ExecutionDeadline deadline,
        Func<LifecycleExecutionDispatchObservation?, ValueTask<UnityRequestExecutionResult>> dispatch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(dispatchRequest);
        ArgumentNullException.ThrowIfNull(deadline);
        ArgumentNullException.ThrowIfNull(dispatch);
        if (dispatchRequest.Registration == null || !cancellationToken.CanBeCanceled)
        {
            var result = await dispatch(null).ConfigureAwait(false);
            return await RetainAuthoritativeStartAsync(
                    unityProject,
                    dispatchRequest,
                    result)
                .ConfigureAwait(false);
        }

        var dispatchObservation = new LifecycleExecutionDispatchObservation();
        var callerCanceledSource =
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var callerCancellationRegistration = cancellationToken.Register(
            static state => ((TaskCompletionSource)state!).TrySetResult(),
            callerCanceledSource);
        var dispatchTask = dispatch(dispatchObservation)
            .AsTask();

        var firstCompleted = await Task.WhenAny(
                dispatchTask,
                callerCanceledSource.Task)
            .ConfigureAwait(false);
        if (ReferenceEquals(firstCompleted, dispatchTask))
        {
            return await RetainAuthoritativeStartAsync(
                    unityProject,
                    dispatchRequest,
                    await dispatchTask.ConfigureAwait(false))
                .ConfigureAwait(false);
        }

        using var startObservationCancellation =
            new CancellationTokenSource();
        // The caller's deadline remains immutable. Recovery alone receives one bounded delivery
        // grace because the first provider write may already have persisted a Start Record.
        var startObservationDeadline = dispatchRequest.BeginsLifecycleExecution
            ? deadline.CreateCompletionDeadline(LifecycleExecutionTiming.ResponseDeliveryGrace)
            : deadline;
        var persistedStartTask =
            LifecycleExecutionStartRecordRecovery.WaitUntilAvailableAsync(
                unityProject,
                dispatchRequest,
                startObservationDeadline,
                startObservationCancellation.Token);
        try
        {
            _ = await Task.WhenAny(
                    dispatchTask,
                    dispatchObservation.Start,
                    persistedStartTask)
                .ConfigureAwait(false);
            if (dispatchTask.IsCompleted)
            {
                return await RetainAuthoritativeStartAsync(
                        unityProject,
                        dispatchRequest,
                        await dispatchTask.ConfigureAwait(false))
                    .ConfigureAwait(false);
            }

            var observedStart = dispatchObservation.Start.IsCompleted
                ? await dispatchObservation.Start.ConfigureAwait(false)
                : await persistedStartTask.ConfigureAwait(false);
            ObserveFault(dispatchTask);
            if (observedStart is null)
            {
                return CreateStartObservationTimeoutResult(deadline.Timeout);
            }

            var observerResult = await dispatchRequest
                .ObserveLifecycleStartAsync(observedStart)
                .ConfigureAwait(false);
            if (observerResult
                is LifecycleExecutionStartObservation.Rejected rejected)
            {
                return UnityRequestExecutionResult.Failure(
                    UnityIpcFailureClassifier.FromCodeAndMessage(
                        rejected.Failure.Code,
                        rejected.Failure.Message),
                    observedStart);
            }

            dispatchObservation.ReportStarted(observedStart);

            return CreateCallerCanceledResult(
                observedStart,
                dispatchObservation.ActionDispatched);
        }
        finally
        {
            startObservationCancellation.Cancel();
            ObserveFault(persistedStartTask);
        }
    }

    private static async ValueTask<UnityRequestExecutionResult> RetainAuthoritativeStartAsync (
        ResolvedUnityProjectContext unityProject,
        UnityIpcDispatchRequest dispatchRequest,
        UnityRequestExecutionResult result)
    {
        if (dispatchRequest.Registration is null)
        {
            return result;
        }

        var authoritativeStart = result.LifecycleExecutionStart
            ?? await LifecycleExecutionStartRecordRecovery.TryReadAsync(
                    unityProject,
                    dispatchRequest)
                .ConfigureAwait(false);
        if (authoritativeStart is null)
        {
            return result;
        }

        var observation = await dispatchRequest
            .ObserveLifecycleStartAsync(authoritativeStart)
            .ConfigureAwait(false);
        if (observation is LifecycleExecutionStartObservation.Rejected rejected)
        {
            if (result.LifecycleActionDispatched)
            {
                throw new InvalidOperationException(
                    "A Lifecycle Execution action was dispatched before its durable start observer completed.");
            }

            return UnityRequestExecutionResult.Failure(
                UnityIpcFailureClassifier.FromCodeAndMessage(
                    rejected.Failure.Code,
                    rejected.Failure.Message),
                authoritativeStart);
        }

        return result.WithLifecycleExecutionStart(authoritativeStart);
    }

    private static UnityRequestExecutionResult CreateCallerCanceledResult (
        LifecycleExecutionStartBinding start,
        bool lifecycleActionDispatched)
    {
        ArgumentNullException.ThrowIfNull(start);
        return UnityRequestExecutionResult.Failure(
            UnityIpcFailureClassifier.FromCodeAndMessage(
                ExecutionErrorCodes.Canceled,
                "Waiting for the Lifecycle Execution response was canceled. "
                + "The execution continues and can be reconnected through its ExecutionRef."),
            start,
            lifecycleActionDispatched);
    }

    private static UnityRequestExecutionResult
        CreateStartObservationTimeoutResult (TimeSpan timeout)
    {
        return UnityRequestExecutionResult.Failure(
            UnityIpcFailureClassifier.Timeout(
                "Lifecycle Execution Start Record was not observable before "
                + $"the Unity IPC request deadline of {timeout.TotalMilliseconds:0} milliseconds."));
    }

    private static void ObserveFault (Task task)
    {
        _ = task.ContinueWith(
            static completedTask => _ = completedTask.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted
                | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}
