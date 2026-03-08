using System;
using System.Collections;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.Requests;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;
using UnityEngine.TestTools;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class ExecuteRequestDispatcherTests
    {
        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenCommandIsPlan_DelegatesToPhaseExecutor () => UniTask.ToCoroutine(async () =>
        {
            await AssertDelegatesToPhaseExecutor(UcliCommandIds.Plan, PhaseExecutionCommand.Plan);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenCommandIsCall_DelegatesToPhaseExecutor () => UniTask.ToCoroutine(async () =>
        {
            await AssertDelegatesToPhaseExecutor(UcliCommandIds.Call, PhaseExecutionCommand.Call);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenPlanTraceContainsPlanToken_MapsTokenToPayload () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedRequest();
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(PhaseExecutionTrace.Success(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: "req-1",
                operationTraces: System.Array.Empty<OperationPhaseTrace>(),
                planToken: "issued-token"));
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var request = CreateExecuteRequest(UcliCommandIds.Plan);

            var response = await dispatcher.Dispatch(request, context, CancellationToken.None).AsUniTask();

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.Payload.TryGetProperty("planToken", out var planToken), Is.True);
            Assert.That(planToken.GetString(), Is.EqualTo("issued-token"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenOperationTraceContainsResult_MapsResultToPayload () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedRequest();
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var operationTrace = new OperationPhaseTrace(
                "op-1",
                "ucli.go.describe",
                OperationPhase.Plan,
                false,
                false,
                System.Array.Empty<OperationTouch>(),
                null)
            {
                Result = JsonSerializer.SerializeToElement(new
                {
                    name = "Root",
                }),
            };
            var phaseExecutor = new SpyOperationPhaseExecutor(PhaseExecutionTrace.Success(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: "req-1",
                operationTraces: new[]
                {
                    operationTrace,
                }));
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var request = CreateExecuteRequest(UcliCommandIds.Plan, operationName: "ucli.go.describe");

            var response = await dispatcher.Dispatch(request, context, CancellationToken.None).AsUniTask();

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            var opResult = GetSingleArrayElement(response.Payload.GetProperty("opResults"));
            Assert.That(opResult.TryGetProperty("result", out var result), Is.True);
            Assert.That(result.GetProperty("name").GetString(), Is.EqualTo("Root"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenSameRequestIdAndSamePayload_ReusesCompletedResponse () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedRequest();
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(PhaseExecutionTrace.Success(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: "req-1",
                operationTraces: new[]
                {
                    new OperationPhaseTrace(
                        "op-1",
                        "ucli.resolve",
                        OperationPhase.Plan,
                        false,
                        false,
                        System.Array.Empty<OperationTouch>(),
                        null),
                }));
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var request = CreateExecuteRequest(UcliCommandIds.Plan);

            var firstResponse = await dispatcher.Dispatch(request, context, CancellationToken.None).AsUniTask();
            var secondResponse = await dispatcher.Dispatch(request, context, CancellationToken.None).AsUniTask();

            Assert.That(firstResponse.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(secondResponse.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(phaseExecutor.CallCount, Is.EqualTo(1));
            Assert.That(secondResponse.Payload.TryGetProperty("opResults", out var opResults), Is.True);
            Assert.That(opResults.GetArrayLength(), Is.EqualTo(1));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenSameRequestIdAndDifferentPayload_ReturnsRequestIdConflict () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedRequest();
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(PhaseExecutionTrace.Success(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: "req-1",
                operationTraces: System.Array.Empty<OperationPhaseTrace>()));
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var firstRequest = CreateExecuteRequest(
                commandName: UcliCommandIds.Plan,
                operationId: "op-1",
                operationName: "ucli.resolve");
            var secondRequest = CreateExecuteRequest(
                commandName: UcliCommandIds.Plan,
                operationId: "op-2",
                operationName: "ucli.scene.open");

            _ = await dispatcher.Dispatch(firstRequest, context, CancellationToken.None).AsUniTask();
            var secondResponse = await dispatcher.Dispatch(secondRequest, context, CancellationToken.None).AsUniTask();

            Assert.That(secondResponse.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(secondResponse.Errors.Count, Is.EqualTo(1));
            Assert.That(secondResponse.Errors[0].Code, Is.EqualTo(IpcErrorCodes.RequestIdConflict));
            Assert.That(phaseExecutor.CallCount, Is.EqualTo(1));
            AssertEmptyOpResultsPayload(secondResponse.Payload);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenSameRequestIdAndDifferentPlanToken_ReturnsRequestIdConflict () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedRequest();
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(PhaseExecutionTrace.Success(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: "req-1",
                operationTraces: System.Array.Empty<OperationPhaseTrace>()));
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var firstRequest = CreateExecuteRequest(
                commandName: UcliCommandIds.Call,
                operationId: "op-1",
                operationName: "ucli.resolve",
                planToken: "token-1");
            var secondRequest = CreateExecuteRequest(
                commandName: UcliCommandIds.Call,
                operationId: "op-1",
                operationName: "ucli.resolve",
                planToken: "token-2");

            _ = await dispatcher.Dispatch(firstRequest, context, CancellationToken.None).AsUniTask();
            var secondResponse = await dispatcher.Dispatch(secondRequest, context, CancellationToken.None).AsUniTask();

            Assert.That(secondResponse.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(secondResponse.Errors.Count, Is.EqualTo(1));
            Assert.That(secondResponse.Errors[0].Code, Is.EqualTo(IpcErrorCodes.RequestIdConflict));
            Assert.That(phaseExecutor.CallCount, Is.EqualTo(1));
            AssertEmptyOpResultsPayload(secondResponse.Payload);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenCommandIsResolve_ReturnsCommandNotImplementedError () => UniTask.ToCoroutine(async () =>
        {
            await AssertReturnsCommandNotImplementedError(UcliCommandIds.Resolve);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenCommandIsQuery_ReturnsCommandNotImplementedError () => UniTask.ToCoroutine(async () =>
        {
            await AssertReturnsCommandNotImplementedError(UcliCommandIds.Query);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenCommandIsRefresh_ReturnsCommandNotImplementedError () => UniTask.ToCoroutine(async () =>
        {
            await AssertReturnsCommandNotImplementedError(UcliCommandIds.Refresh);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenNormalizationFails_ReturnsNormalizationError () => UniTask.ToCoroutine(async () =>
        {
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Failure(
                new ExecuteRequestNormalizationError(IpcErrorCodes.InvalidArgument, "invalid request", "op-1")));
            var phaseExecutor = new SpyOperationPhaseExecutor(PhaseExecutionTrace.Success(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: "req-1",
                operationTraces: System.Array.Empty<OperationPhaseTrace>()));
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var request = CreateExecuteRequest(UcliCommandIds.Plan);

            var response = await dispatcher.Dispatch(request, context, CancellationToken.None).AsUniTask();

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcErrorCodes.InvalidArgument));
            Assert.That(response.Errors[0].OpId, Is.EqualTo("op-1"));
            AssertEmptyOpResultsPayload(response.Payload);
            Assert.That(phaseExecutor.CallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenArgumentsIsNotObject_ReturnsInvalidArgumentWithoutExecuting () => UniTask.ToCoroutine(async () =>
        {
            var normalizer = new SpyExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Failure(
                new ExecuteRequestNormalizationError(IpcErrorCodes.InvalidArgument, "normalizer should not run", null)));
            var phaseExecutor = new SpyOperationPhaseExecutor(PhaseExecutionTrace.Success(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: "req-1",
                operationTraces: System.Array.Empty<OperationPhaseTrace>()));
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var request = new IpcExecuteRequest(UcliCommandIds.Plan, default);

            var response = await dispatcher.Dispatch(request, context, CancellationToken.None).AsUniTask();

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcErrorCodes.InvalidArgument));
            Assert.That(response.Errors[0].Message, Is.EqualTo("Request arguments must be a JSON object."));
            AssertEmptyOpResultsPayload(response.Payload);
            Assert.That(normalizer.CallCount, Is.EqualTo(0));
            Assert.That(phaseExecutor.CallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenPhaseExecutionFails_ReturnsOpResultsAndErrors () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedRequest();
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(PhaseExecutionTrace.Failure(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: "req-1",
                operationTraces: new[]
                {
                    new OperationPhaseTrace(
                        "op-1",
                        "ucli.resolve",
                        OperationPhase.Call,
                        true,
                        true,
                        new[]
                        {
                            new OperationTouch(OperationTouchKind.Scene, "Assets/Scenes/Main.unity", "11111111111111111111111111111111"),
                        },
                        new OperationFailure(IpcErrorCodes.InvalidArgument, "call failed", "op-1")),
                    new OperationPhaseTrace(
                        "op-2",
                        "ucli.scene.open",
                        OperationPhase.Skipped,
                        false,
                        false,
                        System.Array.Empty<OperationTouch>(),
                        null),
                },
                errors: new[]
                {
                    new OperationFailure(IpcErrorCodes.InvalidArgument, "call failed", "op-1"),
                }));
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var request = CreateExecuteRequest(UcliCommandIds.Call);

            var response = await dispatcher.Dispatch(request, context, CancellationToken.None).AsUniTask();

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcErrorCodes.InvalidArgument));
            Assert.That(response.Errors[0].OpId, Is.EqualTo("op-1"));
            Assert.That(response.Payload.TryGetProperty("opResults", out var opResults), Is.True);
            Assert.That(opResults.GetArrayLength(), Is.EqualTo(2));
            Assert.That(response.Payload.TryGetProperty("operationTraces", out _), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenCancellationRequested_ThrowsOperationCanceledException () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedRequest();
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(PhaseExecutionTrace.Success(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: "req-1",
                operationTraces: System.Array.Empty<OperationPhaseTrace>()));
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var request = CreateExecuteRequest(UcliCommandIds.Plan);
            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await dispatcher.Dispatch(request, context, cancellationTokenSource.Token).AsUniTask();
            });
        });

        private static async UniTask AssertDelegatesToPhaseExecutor (
            string commandName,
            PhaseExecutionCommand expectedCommand)
        {
            var normalizedRequest = CreateNormalizedRequest();
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(PhaseExecutionTrace.Success(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: "req-1",
                operationTraces: new[]
                {
                    new OperationPhaseTrace(
                        "op-1",
                        "ucli.resolve",
                        OperationPhase.Plan,
                        false,
                        true,
                        new[]
                        {
                            new OperationTouch(OperationTouchKind.Scene, "Assets/Scenes/Main.unity", "11111111111111111111111111111111"),
                        },
                        null),
                }));
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var request = CreateExecuteRequest(commandName);

            var response = await dispatcher.Dispatch(request, context, CancellationToken.None).AsUniTask();

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.Errors.Count, Is.EqualTo(0));
            Assert.That(phaseExecutor.ReceivedCommand, Is.EqualTo(expectedCommand));
            Assert.That(phaseExecutor.CallCount, Is.EqualTo(1));
            Assert.That(response.Payload.TryGetProperty("opResults", out var opResults), Is.True);
            Assert.That(opResults.GetArrayLength(), Is.EqualTo(1));
            Assert.That(response.Payload.TryGetProperty("operationTraces", out _), Is.False);

            var opResult = GetSingleArrayElement(opResults);
            Assert.That(opResult.GetProperty("opId").GetString(), Is.EqualTo("op-1"));
            Assert.That(opResult.GetProperty("op").GetString(), Is.EqualTo("ucli.resolve"));
            Assert.That(opResult.GetProperty("phase").GetString(), Is.EqualTo(IpcExecuteOperationPhaseNames.Plan));
            Assert.That(opResult.GetProperty("applied").GetBoolean(), Is.False);
            Assert.That(opResult.GetProperty("changed").GetBoolean(), Is.True);

            var touched = opResult.GetProperty("touched");
            Assert.That(touched.GetArrayLength(), Is.EqualTo(1));
            var touchedElement = GetSingleArrayElement(touched);
            Assert.That(touchedElement.GetProperty("kind").GetString(), Is.EqualTo(IpcExecuteTouchedResourceKindNames.Scene));
            Assert.That(touchedElement.GetProperty("path").GetString(), Is.EqualTo("Assets/Scenes/Main.unity"));
            Assert.That(touchedElement.GetProperty("guid").GetString(), Is.EqualTo("11111111111111111111111111111111"));
        }

        private static async UniTask AssertReturnsCommandNotImplementedError (string commandName)
        {
            var normalizer = new SpyExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Failure(
                new ExecuteRequestNormalizationError(IpcErrorCodes.InvalidArgument, "normalizer should not run", null)));
            var phaseExecutor = new SpyOperationPhaseExecutor(PhaseExecutionTrace.Success(
                protocolVersion: IpcProtocol.CurrentVersion,
                requestId: "req-1",
                operationTraces: System.Array.Empty<OperationPhaseTrace>()));
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var request = CreateExecuteRequest(commandName);

            var response = await dispatcher.Dispatch(request, context, CancellationToken.None).AsUniTask();

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcErrorCodes.CommandNotImplemented));
            AssertEmptyOpResultsPayload(response.Payload);
            Assert.That(normalizer.CallCount, Is.EqualTo(0));
            Assert.That(phaseExecutor.CallCount, Is.EqualTo(0));
        }

        private static void AssertEmptyOpResultsPayload (JsonElement payload)
        {
            Assert.That(payload.ValueKind, Is.EqualTo(JsonValueKind.Object));
            Assert.That(payload.TryGetProperty("opResults", out var opResults), Is.True);
            Assert.That(opResults.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(opResults.GetArrayLength(), Is.EqualTo(0));
            Assert.That(payload.TryGetProperty("operationTraces", out _), Is.False);
        }

        private static JsonElement GetSingleArrayElement (JsonElement arrayElement)
        {
            var enumerator = arrayElement.EnumerateArray();
            Assert.That(enumerator.MoveNext(), Is.True);
            var first = enumerator.Current;
            Assert.That(enumerator.MoveNext(), Is.False);
            return first;
        }

        private static IpcExecuteRequest CreateExecuteRequest (
            string commandName,
            string operationId = "op-1",
            string operationName = "ucli.resolve",
            string? planToken = null)
        {
            return new IpcExecuteRequest(
                commandName,
                JsonSerializer.SerializeToElement(new
                {
                    protocolVersion = 1,
                    requestId = "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                    ops = new[]
                    {
                        new
                        {
                            id = operationId,
                            op = operationName,
                            args = new { },
                        },
                    },
                }))
            {
                PlanToken = planToken,
            };
        }

        private static NormalizedExecuteRequest CreateNormalizedRequest ()
        {
            return new NormalizedExecuteRequest(
                ProtocolVersion: 1,
                RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                Ops: new[]
                {
                    new NormalizedOperation(
                        Id: "op-1",
                        Op: "ucli.resolve",
                        Args: JsonSerializer.SerializeToElement(new { }),
                        As: null,
                        Expect: null),
                },
                PlanToken: null,
                CanonicalDigestPayloadUtf8: Encoding.UTF8.GetBytes("{}"));
        }

        private sealed class StubExecuteRequestNormalizer : IExecuteRequestNormalizer
        {
            private readonly ExecuteRequestNormalizationResult normalizationResult;

            public StubExecuteRequestNormalizer (ExecuteRequestNormalizationResult normalizationResult)
            {
                this.normalizationResult = normalizationResult;
            }

            public ExecuteRequestNormalizationResult Normalize (
                IpcExecuteRequest request,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return normalizationResult;
            }
        }

        private sealed class SpyExecuteRequestNormalizer : IExecuteRequestNormalizer
        {
            private readonly ExecuteRequestNormalizationResult normalizationResult;

            public SpyExecuteRequestNormalizer (ExecuteRequestNormalizationResult normalizationResult)
            {
                this.normalizationResult = normalizationResult;
            }

            public int CallCount { get; private set; }

            public ExecuteRequestNormalizationResult Normalize (
                IpcExecuteRequest request,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CallCount++;
                return normalizationResult;
            }
        }

        private sealed class SpyOperationPhaseExecutor : IOperationPhaseExecutor
        {
            private readonly PhaseExecutionTrace executionTrace;

            public SpyOperationPhaseExecutor (PhaseExecutionTrace executionTrace)
            {
                this.executionTrace = executionTrace;
            }

            public int CallCount { get; private set; }

            public PhaseExecutionCommand? ReceivedCommand { get; private set; }

            public Task<PhaseExecutionTrace> Execute (
                PhaseExecutionCommand command,
                NormalizedExecuteRequest request,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CallCount++;
                ReceivedCommand = command;
                return Task.FromResult(executionTrace);
            }
        }
    }
}