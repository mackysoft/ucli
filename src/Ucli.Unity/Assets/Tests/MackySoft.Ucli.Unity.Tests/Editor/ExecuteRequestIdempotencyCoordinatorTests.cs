using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.RequestIdempotency;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class ExecuteRequestIdempotencyCoordinatorTests
    {
        [Test]
        [Category("Size.Small")]
        public void Execute_WhenSameRequestIdAndSameDigestAfterCompletion_ReusesCachedResponse ()
        {
            var coordinator = new ExecuteRequestIdempotencyCoordinator();
            var executeCount = 0;
            var requestId = "req-1";
            var firstResponse = coordinator.Execute(
                requestId: requestId,
                requestDigest: "digest-1",
                executeRequest: _ =>
                {
                    executeCount++;
                    return Task.FromResult(CreateSuccessResponse(requestId, "first"));
                },
                createConflictResponse: () => CreateConflictResponse(requestId))
                .GetAwaiter()
                .GetResult();

            var secondResponse = coordinator.Execute(
                requestId: requestId,
                requestDigest: "digest-1",
                executeRequest: _ =>
                {
                    executeCount++;
                    return Task.FromResult(CreateSuccessResponse(requestId, "second"));
                },
                createConflictResponse: () => CreateConflictResponse(requestId))
                .GetAwaiter()
                .GetResult();

            Assert.That(executeCount, Is.EqualTo(1));
            Assert.That(firstResponse.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(secondResponse.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(GetMarker(secondResponse), Is.EqualTo("first"));
        }

        [Test]
        [Category("Size.Small")]
        public void Execute_WhenSameRequestIdAndDifferentDigestAfterCompletion_ReturnsConflict ()
        {
            var coordinator = new ExecuteRequestIdempotencyCoordinator();
            var executeCount = 0;
            var conflictCount = 0;
            var requestId = "req-1";
            _ = coordinator.Execute(
                requestId: requestId,
                requestDigest: "digest-1",
                executeRequest: _ =>
                {
                    executeCount++;
                    return Task.FromResult(CreateSuccessResponse(requestId, "first"));
                },
                createConflictResponse: () =>
                {
                    conflictCount++;
                    return CreateConflictResponse(requestId);
                })
                .GetAwaiter()
                .GetResult();

            var conflictResponse = coordinator.Execute(
                requestId: requestId,
                requestDigest: "digest-2",
                executeRequest: _ =>
                {
                    executeCount++;
                    return Task.FromResult(CreateSuccessResponse(requestId, "second"));
                },
                createConflictResponse: () =>
                {
                    conflictCount++;
                    return CreateConflictResponse(requestId);
                })
                .GetAwaiter()
                .GetResult();

            Assert.That(executeCount, Is.EqualTo(1));
            Assert.That(conflictCount, Is.EqualTo(1));
            Assert.That(conflictResponse.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(conflictResponse.Errors.Count, Is.EqualTo(1));
            Assert.That(conflictResponse.Errors[0].Code, Is.EqualTo(IpcErrorCodes.RequestIdConflict));
        }

        [Test]
        [Category("Size.Small")]
        public void Execute_WhenSameRequestIdAndSameDigestInFlight_WaitsForOwnerResponse ()
        {
            var coordinator = new ExecuteRequestIdempotencyCoordinator();
            var requestId = "req-1";
            var ownerExecutionCount = 0;
            var waiterExecutionCount = 0;
            var ownerStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var ownerRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var ownerTask = coordinator.Execute(
                requestId: requestId,
                requestDigest: "digest-1",
                executeRequest: async _ =>
                {
                    Interlocked.Increment(ref ownerExecutionCount);
                    ownerStarted.TrySetResult(true);
                    await ownerRelease.Task.ConfigureAwait(false);
                    return CreateSuccessResponse(requestId, "owner");
                },
                createConflictResponse: () => CreateConflictResponse(requestId));

            ownerStarted.Task.GetAwaiter().GetResult();

            var waiterTask = coordinator.Execute(
                requestId: requestId,
                requestDigest: "digest-1",
                executeRequest: _ =>
                {
                    Interlocked.Increment(ref waiterExecutionCount);
                    return Task.FromResult(CreateSuccessResponse(requestId, "waiter"));
                },
                createConflictResponse: () => CreateConflictResponse(requestId));

            Assert.That(waiterTask.IsCompleted, Is.False);
            ownerRelease.TrySetResult(true);

            var ownerResponse = ownerTask.GetAwaiter().GetResult();
            var waiterResponse = waiterTask.GetAwaiter().GetResult();

            Assert.That(ownerExecutionCount, Is.EqualTo(1));
            Assert.That(waiterExecutionCount, Is.EqualTo(0));
            Assert.That(GetMarker(ownerResponse), Is.EqualTo("owner"));
            Assert.That(GetMarker(waiterResponse), Is.EqualTo("owner"));
        }

        [Test]
        [Category("Size.Small")]
        public void Execute_WhenSameRequestIdAndDifferentDigestInFlight_ReturnsConflictWithoutExecuting ()
        {
            var coordinator = new ExecuteRequestIdempotencyCoordinator();
            var requestId = "req-1";
            var ownerStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var ownerRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var conflictExecutionCount = 0;

            var ownerTask = coordinator.Execute(
                requestId: requestId,
                requestDigest: "digest-1",
                executeRequest: async _ =>
                {
                    ownerStarted.TrySetResult(true);
                    await ownerRelease.Task.ConfigureAwait(false);
                    return CreateSuccessResponse(requestId, "owner");
                },
                createConflictResponse: () => CreateConflictResponse(requestId));
            ownerStarted.Task.GetAwaiter().GetResult();

            var conflictResponse = coordinator.Execute(
                requestId: requestId,
                requestDigest: "digest-2",
                executeRequest: _ =>
                {
                    conflictExecutionCount++;
                    return Task.FromResult(CreateSuccessResponse(requestId, "conflict-path"));
                },
                createConflictResponse: () => CreateConflictResponse(requestId))
                .GetAwaiter()
                .GetResult();

            Assert.That(conflictExecutionCount, Is.EqualTo(0));
            Assert.That(conflictResponse.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(conflictResponse.Errors.Count, Is.EqualTo(1));
            Assert.That(conflictResponse.Errors[0].Code, Is.EqualTo(IpcErrorCodes.RequestIdConflict));

            ownerRelease.TrySetResult(true);
            _ = ownerTask.GetAwaiter().GetResult();
        }

        [Test]
        [Category("Size.Small")]
        public void Execute_WhenEntryExpires_ReexecutesRequest ()
        {
            var nowUtc = new DateTimeOffset(2026, 3, 3, 0, 0, 0, TimeSpan.Zero);
            var coordinator = new ExecuteRequestIdempotencyCoordinator(
                cacheTtl: TimeSpan.FromHours(24),
                maxEntries: 10_000,
                utcNowProvider: () => nowUtc);
            var executeCount = 0;
            var requestId = "req-1";

            var firstResponse = coordinator.Execute(
                requestId: requestId,
                requestDigest: "digest-1",
                executeRequest: _ =>
                {
                    executeCount++;
                    return Task.FromResult(CreateSuccessResponse(requestId, "first"));
                },
                createConflictResponse: () => CreateConflictResponse(requestId))
                .GetAwaiter()
                .GetResult();

            nowUtc = nowUtc.AddHours(25);
            var secondResponse = coordinator.Execute(
                requestId: requestId,
                requestDigest: "digest-1",
                executeRequest: _ =>
                {
                    executeCount++;
                    return Task.FromResult(CreateSuccessResponse(requestId, "second"));
                },
                createConflictResponse: () => CreateConflictResponse(requestId))
                .GetAwaiter()
                .GetResult();

            Assert.That(executeCount, Is.EqualTo(2));
            Assert.That(GetMarker(firstResponse), Is.EqualTo("first"));
            Assert.That(GetMarker(secondResponse), Is.EqualTo("second"));
        }

        [Test]
        [Category("Size.Small")]
        public void Execute_WhenCacheExceedsMaxEntries_EvictsOldestEntry ()
        {
            var nowUtc = new DateTimeOffset(2026, 3, 3, 0, 0, 0, TimeSpan.Zero);
            var coordinator = new ExecuteRequestIdempotencyCoordinator(
                cacheTtl: TimeSpan.FromHours(24),
                maxEntries: 2,
                utcNowProvider: () => nowUtc);
            var executeCount = 0;

            Execute("req-1", "digest-1", "first-1");
            nowUtc = nowUtc.AddMinutes(1);
            Execute("req-2", "digest-2", "second-1");
            nowUtc = nowUtc.AddMinutes(1);
            Execute("req-3", "digest-3", "third-1");

            // req-1 should be evicted because max entries is 2.
            nowUtc = nowUtc.AddMinutes(1);
            var req1ResponseAfterEviction = Execute("req-1", "digest-1", "first-2");

            // req-2 should still be cached.
            var req2ResponseFromCache = Execute("req-2", "digest-2", "second-2");

            Assert.That(executeCount, Is.EqualTo(4));
            Assert.That(GetMarker(req1ResponseAfterEviction), Is.EqualTo("first-2"));
            Assert.That(GetMarker(req2ResponseFromCache), Is.EqualTo("second-1"));

            IpcResponse Execute (string requestId, string requestDigest, string marker)
            {
                return coordinator.Execute(
                    requestId: requestId,
                    requestDigest: requestDigest,
                    executeRequest: _ =>
                    {
                        executeCount++;
                        return Task.FromResult(CreateSuccessResponse(requestId, marker));
                    },
                    createConflictResponse: () => CreateConflictResponse(requestId))
                    .GetAwaiter()
                    .GetResult();
            }
        }

        [Test]
        [Category("Size.Small")]
        public void DigestCalculator_WhenArgumentsPropertyOrderDiffers_ReturnsSameDigest ()
        {
            using var firstDocument = JsonDocument.Parse("{\"ops\":[{\"id\":\"op-1\",\"op\":\"ucli.resolve\",\"args\":{\"b\":2,\"a\":1}}],\"protocolVersion\":1,\"requestId\":\"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62\"}");
            using var secondDocument = JsonDocument.Parse("{\"requestId\":\"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62\",\"protocolVersion\":1,\"ops\":[{\"args\":{\"a\":1,\"b\":2},\"op\":\"ucli.resolve\",\"id\":\"op-1\"}]}");
            var firstRequest = new IpcExecuteRequest(IpcExecuteCommandNames.Call, firstDocument.RootElement.Clone());
            var secondRequest = new IpcExecuteRequest(IpcExecuteCommandNames.Call, secondDocument.RootElement.Clone());

            var firstDigest = ExecuteRequestIdempotencyDigestCalculator.ComputeDigest(firstRequest);
            var secondDigest = ExecuteRequestIdempotencyDigestCalculator.ComputeDigest(secondRequest);

            Assert.That(firstDigest, Is.EqualTo(secondDigest));
        }

        [Test]
        [Category("Size.Small")]
        public void DigestCalculator_WhenPlanTokenDiffers_ReturnsDifferentDigest ()
        {
            using var document = JsonDocument.Parse("{\"protocolVersion\":1,\"requestId\":\"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62\",\"ops\":[{\"id\":\"op-1\",\"op\":\"ucli.resolve\",\"args\":{}}]}");
            var firstRequest = new IpcExecuteRequest(IpcExecuteCommandNames.Call, document.RootElement.Clone())
            {
                PlanToken = "token-1",
            };
            var secondRequest = new IpcExecuteRequest(IpcExecuteCommandNames.Call, document.RootElement.Clone())
            {
                PlanToken = "token-2",
            };

            var firstDigest = ExecuteRequestIdempotencyDigestCalculator.ComputeDigest(firstRequest);
            var secondDigest = ExecuteRequestIdempotencyDigestCalculator.ComputeDigest(secondRequest);

            Assert.That(firstDigest, Is.Not.EqualTo(secondDigest));
        }

        [Test]
        [Category("Size.Small")]
        public void DigestCalculator_WhenPlanTokenOnlyDiffersByOuterWhitespace_ReturnsSameDigest ()
        {
            using var document = JsonDocument.Parse("{\"protocolVersion\":1,\"requestId\":\"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62\",\"ops\":[{\"id\":\"op-1\",\"op\":\"ucli.resolve\",\"args\":{}}]}");
            var firstRequest = new IpcExecuteRequest(IpcExecuteCommandNames.Call, document.RootElement.Clone())
            {
                PlanToken = "token-1",
            };
            var secondRequest = new IpcExecuteRequest(IpcExecuteCommandNames.Call, document.RootElement.Clone())
            {
                PlanToken = " token-1 ",
            };

            var firstDigest = ExecuteRequestIdempotencyDigestCalculator.ComputeDigest(firstRequest);
            var secondDigest = ExecuteRequestIdempotencyDigestCalculator.ComputeDigest(secondRequest);

            Assert.That(firstDigest, Is.EqualTo(secondDigest));
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
