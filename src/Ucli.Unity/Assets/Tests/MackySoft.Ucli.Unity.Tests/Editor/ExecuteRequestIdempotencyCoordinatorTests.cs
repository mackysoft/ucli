using System;
using System.Collections;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.RequestIdempotency;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class ExecuteRequestIdempotencyCoordinatorTests
    {
        private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenSameRequestIdAndSameFingerprintAfterCompletion_ReusesCachedResponse () => UniTask.ToCoroutine(async () =>
        {
            var coordinator = new ExecuteRequestIdempotencyCoordinator();
            var executeCount = 0;
            var requestId = "req-1";
            var firstResponse = await coordinator.Execute(
                requestId: requestId,
                requestFingerprint: "fingerprint-1",
                executeRequest: _ =>
                {
                    executeCount++;
                    return Task.FromResult(CreateSuccessResponse(requestId, "first"));
                },
                createConflictResponse: () => CreateConflictResponse(requestId));

            var secondResponse = await coordinator.Execute(
                requestId: requestId,
                requestFingerprint: "fingerprint-1",
                executeRequest: _ =>
                {
                    executeCount++;
                    return Task.FromResult(CreateSuccessResponse(requestId, "second"));
                },
                createConflictResponse: () => CreateConflictResponse(requestId));

            Assert.That(executeCount, Is.EqualTo(1));
            Assert.That(firstResponse.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(secondResponse.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(GetMarker(secondResponse), Is.EqualTo("first"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenSameRequestIdAndDifferentFingerprintAfterCompletion_ReturnsConflict () => UniTask.ToCoroutine(async () =>
        {
            var coordinator = new ExecuteRequestIdempotencyCoordinator();
            var executeCount = 0;
            var conflictCount = 0;
            var requestId = "req-1";
            _ = await coordinator.Execute(
                requestId: requestId,
                requestFingerprint: "fingerprint-1",
                executeRequest: _ =>
                {
                    executeCount++;
                    return Task.FromResult(CreateSuccessResponse(requestId, "first"));
                },
                createConflictResponse: () =>
                {
                    conflictCount++;
                    return CreateConflictResponse(requestId);
                });

            var conflictResponse = await coordinator.Execute(
                requestId: requestId,
                requestFingerprint: "fingerprint-2",
                executeRequest: _ =>
                {
                    executeCount++;
                    return Task.FromResult(CreateSuccessResponse(requestId, "second"));
                },
                createConflictResponse: () =>
                {
                    conflictCount++;
                    return CreateConflictResponse(requestId);
                });

            Assert.That(executeCount, Is.EqualTo(1));
            Assert.That(conflictCount, Is.EqualTo(1));
            Assert.That(conflictResponse.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(conflictResponse.Errors.Count, Is.EqualTo(1));
            Assert.That(conflictResponse.Errors[0].Code, Is.EqualTo(IpcErrorCodes.RequestIdConflict));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenSameRequestIdAndSameFingerprintInFlight_WaitsForOwnerResponse () => UniTask.ToCoroutine(async () =>
        {
            var coordinator = new ExecuteRequestIdempotencyCoordinator();
            var requestId = "req-1";
            var ownerExecutionCount = 0;
            var waiterExecutionCount = 0;
            var ownerStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var ownerRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var ownerTask = coordinator.Execute(
                requestId: requestId,
                requestFingerprint: "fingerprint-1",
                executeRequest: async _ =>
                {
                    Interlocked.Increment(ref ownerExecutionCount);
                    ownerStarted.TrySetResult(true);
                    await ownerRelease.Task.ConfigureAwait(false);
                    return CreateSuccessResponse(requestId, "owner");
                },
                createConflictResponse: () => CreateConflictResponse(requestId));

            await TestAwaiter.WaitAsync(ownerStarted.Task, "Owner request start", SignalWaitTimeout);

            var waiterTask = coordinator.Execute(
                requestId: requestId,
                requestFingerprint: "fingerprint-1",
                executeRequest: _ =>
                {
                    Interlocked.Increment(ref waiterExecutionCount);
                    return Task.FromResult(CreateSuccessResponse(requestId, "waiter"));
                },
                createConflictResponse: () => CreateConflictResponse(requestId));

            try
            {
                Assert.That(waiterTask.IsCompleted, Is.False);
                ownerRelease.TrySetResult(true);

                var ownerResponse = await TestAwaiter.WaitAsync(ownerTask, "Owner request result", SignalWaitTimeout);
                var waiterResponse = await TestAwaiter.WaitAsync(waiterTask, "Waiter replayed result", SignalWaitTimeout);

                Assert.That(ownerExecutionCount, Is.EqualTo(1));
                Assert.That(waiterExecutionCount, Is.EqualTo(0));
                Assert.That(GetMarker(ownerResponse), Is.EqualTo("owner"));
                Assert.That(GetMarker(waiterResponse), Is.EqualTo("owner"));
            }
            finally
            {
                ownerRelease.TrySetResult(true);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenWaiterCancellationRequestedDuringInFlight_ThrowsWithoutCancelingOwner () => UniTask.ToCoroutine(async () =>
        {
            var coordinator = new ExecuteRequestIdempotencyCoordinator();
            var requestId = "req-1";
            var ownerExecutionCount = 0;
            var waiterExecutionCount = 0;
            var ownerStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var ownerRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var ownerTask = coordinator.Execute(
                requestId: requestId,
                requestFingerprint: "fingerprint-1",
                executeRequest: async _ =>
                {
                    Interlocked.Increment(ref ownerExecutionCount);
                    ownerStarted.TrySetResult(true);
                    await ownerRelease.Task.ConfigureAwait(false);
                    return CreateSuccessResponse(requestId, "owner");
                },
                createConflictResponse: () => CreateConflictResponse(requestId));

            await TestAwaiter.WaitAsync(ownerStarted.Task, "Owner request start", SignalWaitTimeout);

            using var waiterCancellationTokenSource = new CancellationTokenSource();
            var waiterTask = coordinator.Execute(
                requestId: requestId,
                requestFingerprint: "fingerprint-1",
                executeRequest: _ =>
                {
                    Interlocked.Increment(ref waiterExecutionCount);
                    return Task.FromResult(CreateSuccessResponse(requestId, "waiter"));
                },
                createConflictResponse: () => CreateConflictResponse(requestId),
                cancellationToken: waiterCancellationTokenSource.Token);

            Assert.That(waiterTask.IsCompleted, Is.False);

            waiterCancellationTokenSource.Cancel();

            try
            {
                Assert.CatchAsync<OperationCanceledException>(async () =>
                    await TestAwaiter.WaitAsync(waiterTask, "Waiter cancellation result", SignalWaitTimeout));
                Assert.That(ownerTask.IsCompleted, Is.False);
                ownerRelease.TrySetResult(true);

                var ownerResponse = await TestAwaiter.WaitAsync(ownerTask, "Owner request result after waiter cancellation", SignalWaitTimeout);
                Assert.That(ownerExecutionCount, Is.EqualTo(1));
                Assert.That(waiterExecutionCount, Is.EqualTo(0));
                Assert.That(GetMarker(ownerResponse), Is.EqualTo("owner"));
            }
            finally
            {
                ownerRelease.TrySetResult(true);
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenSameRequestIdAndDifferentFingerprintInFlight_ReturnsConflictWithoutExecuting () => UniTask.ToCoroutine(async () =>
        {
            var coordinator = new ExecuteRequestIdempotencyCoordinator();
            var requestId = "req-1";
            var ownerStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var ownerRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var conflictExecutionCount = 0;

            var ownerTask = coordinator.Execute(
                requestId: requestId,
                requestFingerprint: "fingerprint-1",
                executeRequest: async _ =>
                {
                    ownerStarted.TrySetResult(true);
                    await ownerRelease.Task.ConfigureAwait(false);
                    return CreateSuccessResponse(requestId, "owner");
                },
                createConflictResponse: () => CreateConflictResponse(requestId));
            await TestAwaiter.WaitAsync(ownerStarted.Task, "Owner request start", SignalWaitTimeout);

            var conflictResponse = await coordinator.Execute(
                requestId: requestId,
                requestFingerprint: "fingerprint-2",
                executeRequest: _ =>
                {
                    conflictExecutionCount++;
                    return Task.FromResult(CreateSuccessResponse(requestId, "conflict-path"));
                },
                createConflictResponse: () => CreateConflictResponse(requestId));

            try
            {
                Assert.That(conflictExecutionCount, Is.EqualTo(0));
                Assert.That(conflictResponse.Status, Is.EqualTo(IpcProtocol.StatusError));
                Assert.That(conflictResponse.Errors.Count, Is.EqualTo(1));
                Assert.That(conflictResponse.Errors[0].Code, Is.EqualTo(IpcErrorCodes.RequestIdConflict));
            }
            finally
            {
                ownerRelease.TrySetResult(true);
            }

            _ = await TestAwaiter.WaitAsync(ownerTask, "Owner request completion after conflict response", SignalWaitTimeout);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenEntryExpires_ReexecutesRequest () => UniTask.ToCoroutine(async () =>
        {
            var nowUtc = new DateTimeOffset(2026, 3, 3, 0, 0, 0, TimeSpan.Zero);
            var coordinator = new ExecuteRequestIdempotencyCoordinator(
                cacheTtl: TimeSpan.FromHours(24),
                maxEntries: 10_000,
                utcNowProvider: () => nowUtc);
            var executeCount = 0;
            var requestId = "req-1";

            var firstResponse = await coordinator.Execute(
                requestId: requestId,
                requestFingerprint: "fingerprint-1",
                executeRequest: _ =>
                {
                    executeCount++;
                    return Task.FromResult(CreateSuccessResponse(requestId, "first"));
                },
                createConflictResponse: () => CreateConflictResponse(requestId));

            nowUtc = nowUtc.AddHours(25);
            var secondResponse = await coordinator.Execute(
                requestId: requestId,
                requestFingerprint: "fingerprint-1",
                executeRequest: _ =>
                {
                    executeCount++;
                    return Task.FromResult(CreateSuccessResponse(requestId, "second"));
                },
                createConflictResponse: () => CreateConflictResponse(requestId));

            Assert.That(executeCount, Is.EqualTo(2));
            Assert.That(GetMarker(firstResponse), Is.EqualTo("first"));
            Assert.That(GetMarker(secondResponse), Is.EqualTo("second"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenCacheExceedsMaxEntries_EvictsOldestEntry () => UniTask.ToCoroutine(async () =>
        {
            var nowUtc = new DateTimeOffset(2026, 3, 3, 0, 0, 0, TimeSpan.Zero);
            var coordinator = new ExecuteRequestIdempotencyCoordinator(
                cacheTtl: TimeSpan.FromHours(24),
                maxEntries: 2,
                utcNowProvider: () => nowUtc);
            var executeCount = 0;

            await Execute("req-1", "fingerprint-1", "first-1");
            nowUtc = nowUtc.AddMinutes(1);
            await Execute("req-2", "fingerprint-2", "second-1");
            nowUtc = nowUtc.AddMinutes(1);
            await Execute("req-3", "fingerprint-3", "third-1");

            // req-1 should be evicted because max entries is 2.
            nowUtc = nowUtc.AddMinutes(1);
            var req1ResponseAfterEviction = await Execute("req-1", "fingerprint-1", "first-2");

            // req-2 should still be cached.
            var req2ResponseFromCache = await Execute("req-2", "fingerprint-2", "second-2");

            Assert.That(executeCount, Is.EqualTo(5));
            Assert.That(GetMarker(req1ResponseAfterEviction), Is.EqualTo("first-2"));
            Assert.That(GetMarker(req2ResponseFromCache), Is.EqualTo("second-2"));

            async Task<IpcResponse> Execute (string requestId, string requestFingerprint, string marker)
            {
                return await coordinator.Execute(
                    requestId: requestId,
                    requestFingerprint: requestFingerprint,
                    executeRequest: _ =>
                    {
                        executeCount++;
                        return Task.FromResult(CreateSuccessResponse(requestId, marker));
                    },
                    createConflictResponse: () => CreateConflictResponse(requestId));
            }
        });

        [Test]
        [Category("Size.Small")]
        public void FingerprintCalculator_WhenArgumentsPropertyOrderDiffers_ReturnsSameFingerprint ()
        {
            using var firstDocument = JsonDocument.Parse(
                "{\"ops\":[{\"id\":\"op-1\",\"op\":\"__RESOLVE_OP__\",\"args\":{\"b\":2,\"a\":1}}],\"protocolVersion\":1,\"requestId\":\"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62\"}"
                    .Replace("__RESOLVE_OP__", UcliPrimitiveOperationNames.Resolve, StringComparison.Ordinal));
            using var secondDocument = JsonDocument.Parse(
                "{\"requestId\":\"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62\",\"protocolVersion\":1,\"ops\":[{\"args\":{\"a\":1,\"b\":2},\"op\":\"__RESOLVE_OP__\",\"id\":\"op-1\"}]}"
                    .Replace("__RESOLVE_OP__", UcliPrimitiveOperationNames.Resolve, StringComparison.Ordinal));
            var firstRequest = new IpcExecuteRequest(UcliCommandIds.Call, firstDocument.RootElement.Clone());
            var secondRequest = new IpcExecuteRequest(UcliCommandIds.Call, secondDocument.RootElement.Clone());

            var firstFingerprint = ExecuteRequestFingerprintCalculator.Create(firstRequest);
            var secondFingerprint = ExecuteRequestFingerprintCalculator.Create(secondRequest);

            Assert.That(firstFingerprint, Is.EqualTo(secondFingerprint));
        }

        [Test]
        [Category("Size.Small")]
        public void FingerprintCalculator_WhenPlanTokenDiffers_ReturnsDifferentFingerprint ()
        {
            using var document = JsonDocument.Parse(
                "{\"protocolVersion\":1,\"requestId\":\"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62\",\"ops\":[{\"id\":\"op-1\",\"op\":\"__RESOLVE_OP__\",\"args\":{}}]}"
                    .Replace("__RESOLVE_OP__", UcliPrimitiveOperationNames.Resolve, StringComparison.Ordinal));
            var firstRequest = new IpcExecuteRequest(UcliCommandIds.Call, document.RootElement.Clone())
            {
                PlanToken = "token-1",
            };
            var secondRequest = new IpcExecuteRequest(UcliCommandIds.Call, document.RootElement.Clone())
            {
                PlanToken = "token-2",
            };

            var firstFingerprint = ExecuteRequestFingerprintCalculator.Create(firstRequest);
            var secondFingerprint = ExecuteRequestFingerprintCalculator.Create(secondRequest);

            Assert.That(firstFingerprint, Is.Not.EqualTo(secondFingerprint));
        }

        [Test]
        [Category("Size.Small")]
        public void FingerprintCalculator_WhenPlanTokenOnlyDiffersByOuterWhitespace_ReturnsSameFingerprint ()
        {
            using var document = JsonDocument.Parse(
                "{\"protocolVersion\":1,\"requestId\":\"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62\",\"ops\":[{\"id\":\"op-1\",\"op\":\"__RESOLVE_OP__\",\"args\":{}}]}"
                    .Replace("__RESOLVE_OP__", UcliPrimitiveOperationNames.Resolve, StringComparison.Ordinal));
            var firstRequest = new IpcExecuteRequest(UcliCommandIds.Call, document.RootElement.Clone())
            {
                PlanToken = "token-1",
            };
            var secondRequest = new IpcExecuteRequest(UcliCommandIds.Call, document.RootElement.Clone())
            {
                PlanToken = " token-1 ",
            };

            var firstFingerprint = ExecuteRequestFingerprintCalculator.Create(firstRequest);
            var secondFingerprint = ExecuteRequestFingerprintCalculator.Create(secondRequest);

            Assert.That(firstFingerprint, Is.EqualTo(secondFingerprint));
        }

        [Test]
        [Category("Size.Small")]
        public void FingerprintCalculator_WhenFailFastDiffers_ReturnsDifferentFingerprint ()
        {
            using var document = JsonDocument.Parse(
                "{\"protocolVersion\":1,\"requestId\":\"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62\",\"ops\":[{\"id\":\"op-1\",\"op\":\"__RESOLVE_OP__\",\"args\":{}}]}"
                    .Replace("__RESOLVE_OP__", UcliPrimitiveOperationNames.Resolve, StringComparison.Ordinal));
            var firstRequest = new IpcExecuteRequest(UcliCommandIds.Call, document.RootElement.Clone())
            {
                FailFast = false,
            };
            var secondRequest = new IpcExecuteRequest(UcliCommandIds.Call, document.RootElement.Clone())
            {
                FailFast = true,
            };

            var firstFingerprint = ExecuteRequestFingerprintCalculator.Create(firstRequest);
            var secondFingerprint = ExecuteRequestFingerprintCalculator.Create(secondRequest);

            Assert.That(firstFingerprint, Is.Not.EqualTo(secondFingerprint));
        }

        private static IpcResponse CreateSuccessResponse (
            string requestId,
            string marker)
        {
            return new IpcResponse(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: requestId,
                Status: IpcProtocol.StatusOk,
                Payload: JsonSerializer.SerializeToElement(new { marker }),
                Errors: Array.Empty<IpcError>());
        }

        private static IpcResponse CreateConflictResponse (string requestId)
        {
            return new IpcResponse(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: requestId,
                Status: IpcProtocol.StatusError,
                Payload: JsonSerializer.SerializeToElement(new { opResults = Array.Empty<object>() }),
                Errors: new[]
                {
                    new IpcError(IpcErrorCodes.RequestIdConflict, "request id conflict", null),
                });
        }

        private static string GetMarker (IpcResponse response)
        {
            return response.Payload.GetProperty("marker").GetString() ?? string.Empty;
        }
    }
}
