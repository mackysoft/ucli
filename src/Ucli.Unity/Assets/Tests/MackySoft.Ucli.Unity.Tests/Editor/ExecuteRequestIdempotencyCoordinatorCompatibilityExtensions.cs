using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Execution.RequestIdempotency
{
    internal static class ExecuteRequestIdempotencyCoordinatorCompatibilityExtensions
    {
        public static async Task<IpcResponse> Execute (
            this ExecuteRequestIdempotencyCoordinator coordinator,
            string requestId,
            string requestFingerprint,
            Func<CancellationToken, Task<IpcResponse>> executeRequest,
            Func<IpcResponse> createConflictResponse,
            CancellationToken cancellationToken = default)
        {
            if (coordinator == null)
            {
                throw new ArgumentNullException(nameof(coordinator));
            }

            if (executeRequest == null)
            {
                throw new ArgumentNullException(nameof(executeRequest));
            }

            if (createConflictResponse == null)
            {
                throw new ArgumentNullException(nameof(createConflictResponse));
            }

            cancellationToken.ThrowIfCancellationRequested();
            ExecuteRequestIdempotencyStoreDecision decision = coordinator.Acquire(requestId, requestFingerprint);

            switch (decision.Kind)
            {
                case ExecuteRequestIdempotencyStoreDecision.DecisionKind.ExecuteOwner:
                    try
                    {
                        IpcResponse response = await executeRequest(cancellationToken).ConfigureAwait(false);

                        if (response == null)
                        {
                            throw new InvalidOperationException("Execute delegate returned null response.");
                        }

                        coordinator.CompleteSuccess(requestId, requestFingerprint, response);
                        return response;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        coordinator.CompleteCanceled(requestId);
                        throw;
                    }
                    catch (Exception exception)
                    {
                        coordinator.CompleteFailed(requestId, exception);
                        throw;
                    }

                case ExecuteRequestIdempotencyStoreDecision.DecisionKind.ReplayCompleted:
                    if (decision.Response == null)
                    {
                        throw new InvalidOperationException("Replay decision did not contain a response.");
                    }

                    return decision.Response;

                case ExecuteRequestIdempotencyStoreDecision.DecisionKind.WaitInFlight:
                    if (decision.SharedResponseTask == null)
                    {
                        throw new InvalidOperationException("Wait decision did not contain a shared response task.");
                    }

                    return await decision.SharedResponseTask.ConfigureAwait(false);

                case ExecuteRequestIdempotencyStoreDecision.DecisionKind.Conflict:
                    return createConflictResponse();

                default:
                    throw new InvalidOperationException($"Unsupported decision kind: {decision.Kind}");
            }
        }
    }
}
