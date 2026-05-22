using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UnityIpcMethodDispatcherTests
    {
        private static readonly TimeSpan AsyncWaitTimeout = TimeSpan.FromSeconds(5);

        [Test]
        [Category("Size.Small")]
        public void Constructor_WhenHandlersAreEmpty_ThrowsArgumentException ()
        {
            var exception = Assert.Throws<ArgumentException>(() =>
            {
                _ = new UnityIpcMethodDispatcher(Array.Empty<IUnityIpcMethodHandler>());
            });

            Assert.That(exception, Is.Not.Null);
            Assert.That(exception.ParamName, Is.EqualTo("methodHandlers"));
        }

        [Test]
        [Category("Size.Small")]
        public void Constructor_WhenHandlerEntryIsNull_ThrowsArgumentException ()
        {
            var exception = Assert.Throws<ArgumentException>(() =>
            {
                _ = new UnityIpcMethodDispatcher(
                    new IUnityIpcMethodHandler[]
                    {
                        null!,
                    });
            });

            Assert.That(exception, Is.Not.Null);
            Assert.That(exception.ParamName, Is.EqualTo("methodHandlers"));
            Assert.That(exception.Message, Does.Contain("must not be null"));
        }

        [Test]
        [Category("Size.Small")]
        public void Constructor_WhenMethodNameIsDuplicated_ThrowsArgumentException ()
        {
            var exception = Assert.Throws<ArgumentException>(() =>
            {
                _ = new UnityIpcMethodDispatcher(
                    new IUnityIpcMethodHandler[]
                    {
                        new StubMethodHandler(IpcMethodNames.Ping, static (_, _) =>
                            new ValueTask<IpcResponse>(CreateSuccessResponse("req-1"))),
                        new StubMethodHandler(IpcMethodNames.Ping, static (_, _) =>
                            new ValueTask<IpcResponse>(CreateSuccessResponse("req-1"))),
                    });
            });

            Assert.That(exception, Is.Not.Null);
            Assert.That(exception.ParamName, Is.EqualTo("methodHandlers"));
            Assert.That(exception.Message, Does.Contain("Duplicate IPC method handler"));
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenMethodIsNotRegistered_ReturnsMethodNotSupportedError () => UniTask.ToCoroutine(async () =>
        {
            var dispatcher = new UnityIpcMethodDispatcher(
                new IUnityIpcMethodHandler[]
                {
                    new StubMethodHandler(IpcMethodNames.Ping, static (_, _) =>
                        new ValueTask<IpcResponse>(CreateSuccessResponse("req-unsupported"))),
                });
            var request = CreateRequest("req-unsupported", method: "unknown.method", payload: 0);

            var response = await TestAwaiter.WaitAsync(
                dispatcher.DispatchAsync(request, CancellationToken.None).AsUniTask(),
                "Unsupported IPC method dispatch",
                AsyncWaitTimeout);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcProtocolErrorCodes.IpcMethodNotSupported));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenHandlerThrows_ReturnsInternalError () => UniTask.ToCoroutine(async () =>
        {
            var dispatcher = new UnityIpcMethodDispatcher(
                new IUnityIpcMethodHandler[]
                {
                    new StubMethodHandler(IpcMethodNames.Ping, static (_, _) =>
                        throw new InvalidOperationException("dispatcher-test")),
                });
            var request = CreateRequest("req-throw", IpcMethodNames.Ping, new IpcPingRequest("tests"));

            var response = await TestAwaiter.WaitAsync(
                dispatcher.DispatchAsync(request, CancellationToken.None).AsUniTask(),
                "Throwing IPC method dispatch",
                AsyncWaitTimeout);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InternalError));
            Assert.That(response.Errors[0].Message, Does.Contain("dispatcher-test"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenMethodIsRegistered_DelegatesToHandler () => UniTask.ToCoroutine(async () =>
        {
            var handler = new StubMethodHandler(IpcMethodNames.Ping, static (request, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new ValueTask<IpcResponse>(CreateSuccessResponse(request.RequestId));
            });
            var dispatcher = new UnityIpcMethodDispatcher(new IUnityIpcMethodHandler[] { handler });
            var request = CreateRequest("req-ok", IpcMethodNames.Ping, new IpcPingRequest("tests"));

            var response = await TestAwaiter.WaitAsync(
                dispatcher.DispatchAsync(request, CancellationToken.None).AsUniTask(),
                "Successful IPC method dispatch",
                AsyncWaitTimeout);

            Assert.That(handler.CallCount, Is.EqualTo(1));
            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.Errors, Is.Empty);
            Assert.That(response.RequestId, Is.EqualTo("req-ok"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenRecoverableRecordIsCompleted_ReturnsStoredResponseWithoutCallingHandler () => UniTask.ToCoroutine(async () =>
        {
            var completedResponse = CreateSuccessResponse("req-recovered-completed");
            var store = new StubRecoverableIpcOperationStore
            {
                ReadRecord = new RecoverableIpcOperationRecord
                {
                    State = RecoverableIpcOperationStateNames.Completed,
                    Response = completedResponse,
                },
            };
            var handler = new StubRecoverableMethodHandler(IpcMethodNames.PlayEnter, static (_, _, _) =>
                new ValueTask<IpcResponse>(CreateSuccessResponse("unexpected")));
            var dispatcher = new UnityIpcMethodDispatcher(new IUnityIpcMethodHandler[] { handler }, store);
            var request = CreateRequest("req-recovered-completed", IpcMethodNames.PlayEnter, new IpcPlayEnterRequest { TimeoutMilliseconds = 1000 });

            var response = await TestAwaiter.WaitAsync(
                dispatcher.DispatchAsync(request, CancellationToken.None).AsUniTask(),
                "Completed recoverable IPC method dispatch",
                AsyncWaitTimeout);

            Assert.That(response, Is.SameAs(completedResponse));
            Assert.That(handler.RecoverableCallCount, Is.EqualTo(0));
            Assert.That(store.PurgeCallCount, Is.EqualTo(1));
            Assert.That(store.LastReadRequestPayloadHash, Is.EqualTo(handler.RecoverableRequestPayloadHash));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenRecoverableRecordPayloadHashDoesNotMatch_ReturnsInternalErrorWithoutCallingHandler () => UniTask.ToCoroutine(async () =>
        {
            var store = new StubRecoverableIpcOperationStore
            {
                ExpectedReadRequestPayloadHash = "other-hash",
            };
            var handler = new StubRecoverableMethodHandler(IpcMethodNames.PlayEnter, static (_, _, _) =>
                new ValueTask<IpcResponse>(CreateSuccessResponse("unexpected")));
            var dispatcher = new UnityIpcMethodDispatcher(new IUnityIpcMethodHandler[] { handler }, store);
            var request = CreateRequest("req-recovered-hash-mismatch", IpcMethodNames.PlayEnter, new IpcPlayEnterRequest { TimeoutMilliseconds = 1000 });

            var response = await TestAwaiter.WaitAsync(
                dispatcher.DispatchAsync(request, CancellationToken.None).AsUniTask(),
                "Hash-mismatched recoverable IPC method dispatch",
                AsyncWaitTimeout);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InternalError));
            Assert.That(handler.RecoverableCallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenRecoverableStoreExistsAndHandlerDoesNotSupportRecovery_DelegatesNormally () => UniTask.ToCoroutine(async () =>
        {
            var store = new StubRecoverableIpcOperationStore();
            var handler = new StubMethodHandler(IpcMethodNames.PlayEnter, static (request, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return new ValueTask<IpcResponse>(CreateSuccessResponse(request.RequestId));
            });
            var dispatcher = new UnityIpcMethodDispatcher(new IUnityIpcMethodHandler[] { handler }, store);
            var request = CreateRequest("req-plain-handler-with-store", IpcMethodNames.PlayEnter, new IpcPlayEnterRequest { TimeoutMilliseconds = 1000 });

            var response = await TestAwaiter.WaitAsync(
                dispatcher.DispatchAsync(request, CancellationToken.None).AsUniTask(),
                "Plain method handler with recoverable store dispatch",
                AsyncWaitTimeout);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.RequestId, Is.EqualTo("req-plain-handler-with-store"));
            Assert.That(handler.CallCount, Is.EqualTo(1));
            Assert.That(store.ReadCallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenRecoverableHandlerCreatesPendingRecord_WritesCompletedResponse () => UniTask.ToCoroutine(async () =>
        {
            var store = new StubRecoverableIpcOperationStore();
            var handler = new StubRecoverableMethodHandler(IpcMethodNames.PlayEnter, static (request, context, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                Assert.That(context.TryMarkPending(new { checkpoint = "before" }, out var errorMessage), Is.True, errorMessage);
                return new ValueTask<IpcResponse>(CreateSuccessResponse(request.RequestId));
            });
            var dispatcher = new UnityIpcMethodDispatcher(new IUnityIpcMethodHandler[] { handler }, store);
            var request = CreateRequest("req-recovered-pending", IpcMethodNames.PlayEnter, new IpcPlayEnterRequest { TimeoutMilliseconds = 1000 });

            var response = await TestAwaiter.WaitAsync(
                dispatcher.DispatchAsync(request, CancellationToken.None).AsUniTask(),
                "Pending recoverable IPC method dispatch",
                AsyncWaitTimeout);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(handler.RecoverableCallCount, Is.EqualTo(1));
            Assert.That(store.PendingWriteCallCount, Is.EqualTo(1));
            Assert.That(store.CompletedWriteCallCount, Is.EqualTo(1));
            Assert.That(store.CompletedResponse, Is.SameAs(response));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenRecoverableRecordIsInvalid_ReturnsInternalErrorWithoutCallingHandler () => UniTask.ToCoroutine(async () =>
        {
            var store = new StubRecoverableIpcOperationStore
            {
                ReadErrorMessage = "invalid record",
            };
            var handler = new StubRecoverableMethodHandler(IpcMethodNames.PlayEnter, static (_, _, _) =>
                new ValueTask<IpcResponse>(CreateSuccessResponse("unexpected")));
            var dispatcher = new UnityIpcMethodDispatcher(new IUnityIpcMethodHandler[] { handler }, store);
            var request = CreateRequest("req-invalid-record", IpcMethodNames.PlayEnter, new IpcPlayEnterRequest { TimeoutMilliseconds = 1000 });

            var response = await TestAwaiter.WaitAsync(
                dispatcher.DispatchAsync(request, CancellationToken.None).AsUniTask(),
                "Invalid recoverable IPC record dispatch",
                AsyncWaitTimeout);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InternalError));
            Assert.That(handler.RecoverableCallCount, Is.EqualTo(0));
            Assert.That(store.CompletedWriteCallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenCancellationIsRequested_ThrowsOperationCanceledException () => UniTask.ToCoroutine(async () =>
        {
            var handler = new StubMethodHandler(IpcMethodNames.Ping, static (_, _) =>
                new ValueTask<IpcResponse>(CreateSuccessResponse("req-canceled")));
            var dispatcher = new UnityIpcMethodDispatcher(new IUnityIpcMethodHandler[] { handler });
            var request = CreateRequest("req-canceled", IpcMethodNames.Ping, new IpcPingRequest("tests"));
            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await dispatcher.DispatchAsync(request, cancellationTokenSource.Token).AsUniTask();
            }, "Canceled IPC method dispatch", AsyncWaitTimeout);
            Assert.That(handler.CallCount, Is.EqualTo(0));
        });

        private static IpcRequest CreateRequest (
            string requestId,
            string method,
            object payload)
        {
            return new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: requestId,
                SessionToken: "session-token",
                Method: method,
                Payload: IpcPayloadCodec.SerializeToElement(payload));
        }

        private static IpcResponse CreateSuccessResponse (string requestId)
        {
            return new IpcResponse(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: requestId,
                Status: IpcProtocol.StatusOk,
                Payload: IpcPayloadCodec.SerializeToElement(new { ok = true }),
                Errors: Array.Empty<IpcError>());
        }

        private sealed class StubMethodHandler : IUnityIpcMethodHandler
        {
            private readonly Func<IpcRequest, CancellationToken, ValueTask<IpcResponse>> handle;

            public StubMethodHandler (
                string method,
                Func<IpcRequest, CancellationToken, ValueTask<IpcResponse>> handle)
            {
                Method = method;
                this.handle = handle;
            }

            public int CallCount { get; private set; }

            public string Method { get; }

            public ValueTask<IpcResponse> HandleAsync (
                IpcRequest request,
                CancellationToken cancellationToken)
            {
                CallCount++;
                return handle(request, cancellationToken);
            }
        }

        private sealed class StubRecoverableMethodHandler : IRecoverableUnityIpcMethodHandler
        {
            private readonly Func<IpcRequest, RecoverableIpcOperationContext, CancellationToken, ValueTask<IpcResponse>> handle;

            public StubRecoverableMethodHandler (
                string method,
                Func<IpcRequest, RecoverableIpcOperationContext, CancellationToken, ValueTask<IpcResponse>> handle)
            {
                Method = method;
                this.handle = handle;
            }

            public int PlainCallCount { get; private set; }

            public int RecoverableCallCount { get; private set; }

            public string Method { get; }

            public string RecoverableRequestPayloadHash { get; set; } = "stub-recoverable-hash";

            public bool TryCreateRecoverableRequestPayloadHash (
                IpcRequest request,
                out string requestPayloadHash,
                out IpcResponse errorResponse)
            {
                requestPayloadHash = RecoverableRequestPayloadHash;
                errorResponse = null;
                return true;
            }

            public ValueTask<IpcResponse> HandleAsync (
                IpcRequest request,
                CancellationToken cancellationToken)
            {
                PlainCallCount++;
                return handle(request, null, cancellationToken);
            }

            public ValueTask<IpcResponse> HandleRecoverableAsync (
                IpcRequest request,
                RecoverableIpcOperationContext context,
                CancellationToken cancellationToken)
            {
                RecoverableCallCount++;
                return handle(request, context, cancellationToken);
            }
        }

        private sealed class StubRecoverableIpcOperationStore : IRecoverableIpcOperationStore
        {
            public RecoverableIpcOperationRecord ReadRecord { get; set; }

            public string ReadErrorMessage { get; set; }

            public string ExpectedReadRequestPayloadHash { get; set; }

            public string LastReadRequestPayloadHash { get; private set; }

            public int PurgeCallCount { get; private set; }

            public int ReadCallCount { get; private set; }

            public int PendingWriteCallCount { get; private set; }

            public int CompletedWriteCallCount { get; private set; }

            public IpcResponse CompletedResponse { get; private set; }

            public bool TryRead (
                string method,
                string requestId,
                string requestPayloadHash,
                out RecoverableIpcOperationRecord record,
                out string errorMessage)
            {
                ReadCallCount++;
                LastReadRequestPayloadHash = requestPayloadHash;
                if (!string.IsNullOrWhiteSpace(ExpectedReadRequestPayloadHash)
                    && !string.Equals(ExpectedReadRequestPayloadHash, requestPayloadHash, StringComparison.Ordinal))
                {
                    record = null;
                    errorMessage = "identity mismatch";
                    return false;
                }

                record = ReadRecord;
                errorMessage = ReadErrorMessage;
                return record != null;
            }

            public bool TryWritePending (
                string method,
                string requestId,
                string requestPayloadHash,
                DateTimeOffset startedAtUtc,
                System.Text.Json.JsonElement recoveryPayload,
                out string errorMessage)
            {
                PendingWriteCallCount++;
                errorMessage = null;
                return true;
            }

            public bool TryWriteCompleted (
                string method,
                string requestId,
                string requestPayloadHash,
                DateTimeOffset startedAtUtc,
                DateTimeOffset completedAtUtc,
                System.Text.Json.JsonElement recoveryPayload,
                IpcResponse response,
                out string errorMessage)
            {
                CompletedWriteCallCount++;
                CompletedResponse = response;
                errorMessage = null;
                return true;
            }

            public bool TryPurgeExpiredRecords (
                DateTimeOffset nowUtc,
                out string errorMessage)
            {
                PurgeCallCount++;
                errorMessage = null;
                return true;
            }
        }
    }
}
