using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Shared.Execution.Timeout;

/// <summary> Executes one independently safe asynchronous operation within an existing execution deadline. </summary>
internal static class ExecutionDeadlineOperation
{
    private static readonly Task NeverCompletingTask = new TaskCompletionSource(
        TaskCreationOptions.RunContinuationsAsynchronously).Task;

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
            var deadlineTask = Task.Delay(
                operationTimeout,
                deadline.Clock,
                deadlineDelayCancellationTokenSource.Token);
            var operationTask = Task.Run(
                async () => await operation(operationCancellationTokenSource.Token).ConfigureAwait(false),
                CancellationToken.None);
            var callerCancellationSource = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            using var callerCancellationRegistration = cancellationToken.CanBeCanceled
                ? cancellationToken.UnsafeRegister(
                    static state => ((TaskCompletionSource)state!).TrySetResult(),
                    callerCancellationSource)
                : default;
            var callerCancellationTask = cancellationToken.CanBeCanceled
                ? callerCancellationSource.Task
                : NeverCompletingTask;
            var completedTask = await Task.WhenAny(
                    operationTask,
                    deadlineTask,
                    callerCancellationTask)
                .ConfigureAwait(false);
            if (!ReferenceEquals(completedTask, operationTask)
                || cancellationToken.IsCancellationRequested)
            {
                var cancellationRequestTask = operationCancellationTokenSource.CancelAsync();
                disposeOperationCancellationTokenSource = false;
                ObserveAndDisposeAfterCompletion(
                    operationTask,
                    cancellationRequestTask,
                    operationCancellationTokenSource);
                cancellationToken.ThrowIfCancellationRequested();
                return ExecutionDeadlineOperationResult<T>.Failure(ExecutionError.Timeout(operationTimeoutMessage));
            }

            T value;
            try
            {
                value = await operationTask.ConfigureAwait(false);
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested)
            {
                var cancellationRequestTask = operationCancellationTokenSource.CancelAsync();
                disposeOperationCancellationTokenSource = false;
                ObserveAndDisposeAfterCompletion(
                    operationTask,
                    cancellationRequestTask,
                    operationCancellationTokenSource);
                cancellationToken.ThrowIfCancellationRequested();
                throw;
            }

            cancellationToken.ThrowIfCancellationRequested();
            return ExecutionDeadlineOperationResult<T>.Success(value);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        finally
        {
            try
            {
                deadlineDelayCancellationTokenSource.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            if (disposeOperationCancellationTokenSource)
            {
                operationCancellationTokenSource.Dispose();
            }
        }
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
