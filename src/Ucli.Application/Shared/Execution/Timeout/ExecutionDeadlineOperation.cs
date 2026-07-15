using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Shared.Execution.Timeout;

/// <summary> Executes one independently safe asynchronous operation within an existing execution deadline. </summary>
internal static class ExecutionDeadlineOperation
{
    /// <summary> Executes an operation with the remaining deadline budget. </summary>
    /// <typeparam name="T"> The operation result type. </typeparam>
    /// <param name="deadline"> The shared execution deadline. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <param name="beforeTimeoutMessage"> The timeout message used when no budget remains before execution. </param>
    /// <param name="operationTimeoutMessage"> The timeout message used when execution exhausts the budget. </param>
    /// <param name="operation">
    /// The operation to execute. Its late completion must be safe because a non-cooperative operation remains owned and observed after timeout.
    /// </param>
    /// <returns> The operation value or a structured timeout error. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when one required argument is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when one timeout message is empty or whitespace. </exception>
    public static async ValueTask<ExecutionDeadlineOperationResult<T>> ExecuteAsync<T> (
        ExecutionDeadline deadline,
        CancellationToken cancellationToken,
        string beforeTimeoutMessage,
        string operationTimeoutMessage,
        Func<CancellationToken, ValueTask<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(deadline);
        ArgumentException.ThrowIfNullOrWhiteSpace(beforeTimeoutMessage);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationTimeoutMessage);
        ArgumentNullException.ThrowIfNull(operation);
        cancellationToken.ThrowIfCancellationRequested();

        if (!deadline.TryGetRemainingTimeout(out var operationTimeout))
        {
            return ExecutionDeadlineOperationResult<T>.Failure(ExecutionError.Timeout(beforeTimeoutMessage));
        }

        using var deadlineDelayCancellationTokenSource = new CancellationTokenSource();
        var operationCancellationTokenSource = new CancellationTokenSource();
        var disposeOperationCancellationTokenSource = true;

        try
        {
            var completionSource = new TaskCompletionSource<CompletionKind>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using var callerCancellationRegistration = cancellationToken.CanBeCanceled
                ? cancellationToken.UnsafeRegister(
                    static state => ((TaskCompletionSource<CompletionKind>)state!).TrySetResult(CompletionKind.CallerCancellation),
                    completionSource)
                : default;
            var deadlineTask = Task.Delay(
                operationTimeout,
                deadline.Clock,
                deadlineDelayCancellationTokenSource.Token);
            _ = deadlineTask.ContinueWith(
                static (completedTask, state) =>
                {
                    if (completedTask.Status == TaskStatus.RanToCompletion)
                    {
                        ((TaskCompletionSource<CompletionKind>)state!).TrySetResult(CompletionKind.Deadline);
                    }
                },
                completionSource,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            var operationTask = Task.Run(
                async () =>
                {
                    try
                    {
                        return await operation(operationCancellationTokenSource.Token).ConfigureAwait(false);
                    }
                    finally
                    {
                        completionSource.TrySetResult(CompletionKind.Operation);
                    }
                },
                CancellationToken.None);

            // Each contender records its completion at the source. Await scheduling cannot reorder a
            // caller cancellation, deadline, or operation that completed while this continuation was delayed.
            var completionKind = await completionSource.Task.ConfigureAwait(false);
            if (completionKind == CompletionKind.CallerCancellation)
            {
                var cancellationRequestTask = operationCancellationTokenSource.CancelAsync();
                disposeOperationCancellationTokenSource = false;
                ObserveAndDisposeAfterCompletion(
                    operationTask,
                    cancellationRequestTask,
                    operationCancellationTokenSource);
                throw new OperationCanceledException(cancellationToken);
            }

            if (completionKind == CompletionKind.Deadline)
            {
                var cancellationRequestTask = operationCancellationTokenSource.CancelAsync();
                disposeOperationCancellationTokenSource = false;
                ObserveAndDisposeAfterCompletion(
                    operationTask,
                    cancellationRequestTask,
                    operationCancellationTokenSource);
                return ExecutionDeadlineOperationResult<T>.Failure(ExecutionError.Timeout(operationTimeoutMessage));
            }

            var value = await operationTask.ConfigureAwait(false);
            return ExecutionDeadlineOperationResult<T>.Success(value);
        }
        finally
        {
            deadlineDelayCancellationTokenSource.Cancel();

            if (disposeOperationCancellationTokenSource)
            {
                operationCancellationTokenSource.Dispose();
            }
        }
    }

    private enum CompletionKind
    {
        Operation,
        Deadline,
        CallerCancellation,
    }

    private static void ObserveAndDisposeAfterCompletion<T> (
        Task<T> operationTask,
        Task cancellationRequestTask,
        CancellationTokenSource operationCancellationTokenSource)
    {
        _ = Task.WhenAll(operationTask, cancellationRequestTask).ContinueWith(
            static (completedTask, state) =>
            {
                _ = completedTask.Exception;
                ((CancellationTokenSource)state!).Dispose();
            },
            operationCancellationTokenSource,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}
