using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Validation;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.Requests;
using MackySoft.Ucli.Unity.Execution.Dispatch;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;
using UnityEngine.TestTools;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class ExecuteRequestDispatcherTests
    {
        private static readonly TimeSpan AsyncWaitTimeout = TimeSpan.FromSeconds(5);

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
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(normalizedRequest, planToken: "issued-token"));
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var request = CreateExecuteRequest(UcliCommandIds.Plan);

            var response = await DispatchAsync(dispatcher, request, context, "Plan token payload mapping");

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
                MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
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
                protocolVersion: normalizedRequest.ProtocolVersion,
                requestId: normalizedRequest.RequestId,
                steps: CreateTraceSteps(normalizedRequest),
                operationTraces: new[]
                {
                    operationTrace,
                }));
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var request = CreateExecuteRequest(UcliCommandIds.Plan, operationName: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe);

            var response = await DispatchAsync(dispatcher, request, context, "Operation result payload mapping");

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            var opResult = GetSingleArrayElement(response.Payload.GetProperty("opResults"));
            Assert.That(opResult.TryGetProperty("result", out var result), Is.True);
            Assert.That(result.GetProperty("name").GetString(), Is.EqualTo("Root"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenOperationTraceContainsReadInvalidations_MapsReadPostconditionToPayload () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedRequest(
                ("op-1", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh));
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var operationTrace = new OperationPhaseTrace(
                "op-1",
                MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh,
                OperationPhase.Call,
                true,
                true,
                System.Array.Empty<OperationTouch>(),
                null)
            {
                ReadInvalidations = new[]
                {
                    new OperationReadInvalidation(OperationReadInvalidationSurface.AssetSearch, ScenePath: null),
                    new OperationReadInvalidation(OperationReadInvalidationSurface.SceneTreeLite, @"Assets\Scenes\Main.unity"),
                },
            };
            var phaseExecutor = new SpyOperationPhaseExecutor(PhaseExecutionTrace.Success(
                protocolVersion: normalizedRequest.ProtocolVersion,
                requestId: normalizedRequest.RequestId,
                steps: CreateTraceSteps(normalizedRequest),
                operationTraces: new[]
                {
                    operationTrace,
                }));
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var request = CreateExecuteRequest(
                UcliCommandIds.Call,
                operationName: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh);

            var response = await DispatchAsync(dispatcher, request, context, "Read postcondition payload mapping");

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.Payload.TryGetProperty("readPostcondition", out var readPostcondition), Is.True);
            var requirements = readPostcondition.GetProperty("requirements");
            Assert.That(requirements.GetArrayLength(), Is.EqualTo(2));

            var enumerator = requirements.EnumerateArray();
            Assert.That(enumerator.MoveNext(), Is.True);
            var assetSearchRequirement = enumerator.Current;
            Assert.That(assetSearchRequirement.GetProperty("surface").GetString(), Is.EqualTo(IpcExecuteReadPostconditionSurfaceNames.AssetSearch));
            Assert.That(assetSearchRequirement.TryGetProperty("scenePath", out _), Is.False);

            Assert.That(enumerator.MoveNext(), Is.True);
            var sceneTreeRequirement = enumerator.Current;
            Assert.That(sceneTreeRequirement.GetProperty("surface").GetString(), Is.EqualTo(IpcExecuteReadPostconditionSurfaceNames.SceneTreeLite));
            Assert.That(sceneTreeRequirement.GetProperty("scenePath").GetString(), Is.EqualTo("Assets/Scenes/Main.unity"));
            Assert.That(sceneTreeRequirement.GetProperty("minSafeGeneratedAtUtc").GetString(), Is.EqualTo(assetSearchRequirement.GetProperty("minSafeGeneratedAtUtc").GetString()));
            Assert.That(enumerator.MoveNext(), Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenReadInvalidationsContainDuplicates_DeduplicatesReadPostconditionRequirements () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedRequest(
                ("op-1", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh),
                ("op-2", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh));
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(PhaseExecutionTrace.Success(
                protocolVersion: normalizedRequest.ProtocolVersion,
                requestId: normalizedRequest.RequestId,
                steps: CreateTraceSteps(normalizedRequest),
                operationTraces: new[]
                {
                    new OperationPhaseTrace(
                        "op-1",
                        MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh,
                        OperationPhase.Call,
                        true,
                        true,
                        System.Array.Empty<OperationTouch>(),
                        null)
                    {
                        ReadInvalidations = new[]
                        {
                            new OperationReadInvalidation(OperationReadInvalidationSurface.AssetSearch, ScenePath: null),
                            new OperationReadInvalidation(OperationReadInvalidationSurface.SceneTreeLite, "Assets/Scenes/Main.unity"),
                        },
                    },
                    new OperationPhaseTrace(
                        "op-2",
                        MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh,
                        OperationPhase.Call,
                        true,
                        true,
                        System.Array.Empty<OperationTouch>(),
                        null)
                    {
                        ReadInvalidations = new[]
                        {
                            new OperationReadInvalidation(OperationReadInvalidationSurface.AssetSearch, ScenePath: null),
                            new OperationReadInvalidation(OperationReadInvalidationSurface.SceneTreeLite, @"Assets\Scenes\Main.unity"),
                        },
                    },
                }));
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var request = CreateExecuteRequest(
                UcliCommandIds.Call,
                failFast: false,
                planToken: null,
                ("op-1", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh),
                ("op-2", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh));

            var response = await DispatchAsync(dispatcher, request, context, "Deduplicated read postcondition payload mapping");

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            var requirements = response.Payload.GetProperty("readPostcondition").GetProperty("requirements");
            Assert.That(requirements.GetArrayLength(), Is.EqualTo(2));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenSameRequestIdAndSamePayload_ReusesCompletedResponse () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedRequest();
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(PhaseExecutionTrace.Success(
                protocolVersion: normalizedRequest.ProtocolVersion,
                requestId: normalizedRequest.RequestId,
                steps: CreateTraceSteps(normalizedRequest),
                operationTraces: new[]
                {
                    new OperationPhaseTrace(
                        "op-1",
                        MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.Resolve,
                        OperationPhase.Plan,
                        false,
                        false,
                        System.Array.Empty<OperationTouch>(),
                        null),
                }));
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var request = CreateExecuteRequest(UcliCommandIds.Plan);

            var firstResponse = await DispatchAsync(dispatcher, request, context, "Idempotent first response");
            var secondResponse = await DispatchAsync(dispatcher, request, context, "Idempotent second response");

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
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(normalizedRequest));
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var firstRequest = CreateExecuteRequest(
                commandName: UcliCommandIds.Plan,
                operationId: "op-1",
                operationName: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.Resolve);
            var secondRequest = CreateExecuteRequest(
                commandName: UcliCommandIds.Plan,
                operationId: "op-2",
                operationName: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneOpen);

            _ = await DispatchAsync(dispatcher, firstRequest, context, "Initial conflicting request completion");
            var secondResponse = await DispatchAsync(dispatcher, secondRequest, context, "Conflicting payload response");

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
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(normalizedRequest));
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var firstRequest = CreateExecuteRequest(
                commandName: UcliCommandIds.Call,
                operationId: "op-1",
                operationName: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.Resolve,
                planToken: "token-1");
            var secondRequest = CreateExecuteRequest(
                commandName: UcliCommandIds.Call,
                operationId: "op-1",
                operationName: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.Resolve,
                planToken: "token-2");

            _ = await DispatchAsync(dispatcher, firstRequest, context, "Initial plan token request completion");
            var secondResponse = await DispatchAsync(dispatcher, secondRequest, context, "Conflicting plan token response");

            Assert.That(secondResponse.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(secondResponse.Errors.Count, Is.EqualTo(1));
            Assert.That(secondResponse.Errors[0].Code, Is.EqualTo(IpcErrorCodes.RequestIdConflict));
            Assert.That(phaseExecutor.CallCount, Is.EqualTo(1));
            AssertEmptyOpResultsPayload(secondResponse.Payload);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenFailFastIsDisabled_DelaysPhaseExecutionUntilReady () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedRequest();
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(normalizedRequest));
            var readinessGate = StubUnityEditorReadinessGate.CreatePending();
            var mainThreadRequestExecutor = new SpyUnityMainThreadRequestExecutor();
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor, readinessGate, mainThreadRequestExecutor);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var request = CreateExecuteRequest(UcliCommandIds.Plan, failFast: false);

            var responseTask = dispatcher.Dispatch(request, context).AsUniTask();
            await TestAwaiter.WaitAsync(readinessGate.WaitObserved, "Execute dispatcher readiness wait", AsyncWaitTimeout);

            Assert.That(readinessGate.CallCount, Is.EqualTo(1));
            Assert.That(readinessGate.LastFailFast, Is.False);
            Assert.That(phaseExecutor.CallCount, Is.EqualTo(0));

            readinessGate.Release();
            var response = await TestAwaiter.WaitAsync(responseTask, "Execute dispatcher readiness-delayed response", AsyncWaitTimeout);

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(mainThreadRequestExecutor.CallCount, Is.EqualTo(1));
            Assert.That(phaseExecutor.CallCount, Is.EqualTo(1));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenFailFastIsEnabled_ReturnsLifecycleErrorWithoutExecuting () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedRequest();
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(normalizedRequest));
            var readinessGate = StubUnityEditorReadinessGate.CreatePending();
            var mainThreadRequestExecutor = new SpyUnityMainThreadRequestExecutor();
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor, readinessGate, mainThreadRequestExecutor);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var request = CreateExecuteRequest(UcliCommandIds.Call, failFast: true);

            var response = await DispatchAsync(dispatcher, request, context, "Execute dispatcher lifecycle fail-fast response");

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcErrorCodes.EditorBusy));
            Assert.That(readinessGate.CallCount, Is.EqualTo(1));
            Assert.That(readinessGate.LastFailFast, Is.True);
            Assert.That(mainThreadRequestExecutor.CallCount, Is.EqualTo(0));
            AssertEmptyOpResultsPayload(response.Payload);
            Assert.That(phaseExecutor.CallCount, Is.EqualTo(0));
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
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(CreateNormalizedRequest()));
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var request = CreateExecuteRequest(UcliCommandIds.Plan);

            var response = await DispatchAsync(dispatcher, request, context, "Normalization failure response");

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcErrorCodes.InvalidArgument));
            Assert.That(response.Errors[0].OpId, Is.EqualTo("op-1"));
            AssertEmptyOpResultsPayload(response.Payload);
            Assert.That(phaseExecutor.CallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenCommandIsNotImplemented_DoesNotWaitForReadiness () => UniTask.ToCoroutine(async () =>
        {
            var normalizer = new SpyExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Failure(
                new ExecuteRequestNormalizationError(IpcErrorCodes.InvalidArgument, "normalizer should not run", null)));
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(CreateNormalizedRequest()));
            var readinessGate = StubUnityEditorReadinessGate.CreatePending();
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor, readinessGate);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var request = CreateExecuteRequest(UcliCommandIds.Resolve);

            var response = await DispatchAsync(dispatcher, request, context, "Command not implemented without readiness wait");

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcErrorCodes.CommandNotImplemented));
            Assert.That(readinessGate.CallCount, Is.EqualTo(0));
            Assert.That(normalizer.CallCount, Is.EqualTo(0));
            Assert.That(phaseExecutor.CallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenNormalizationFails_DoesNotWaitForReadiness () => UniTask.ToCoroutine(async () =>
        {
            var normalizer = new SpyExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Failure(
                new ExecuteRequestNormalizationError(IpcErrorCodes.InvalidArgument, "invalid request", "op-1")));
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(CreateNormalizedRequest()));
            var readinessGate = StubUnityEditorReadinessGate.CreatePending();
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor, readinessGate);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var request = CreateExecuteRequest(UcliCommandIds.Plan);

            var response = await DispatchAsync(dispatcher, request, context, "Normalization failure without readiness wait");

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(IpcErrorCodes.InvalidArgument));
            Assert.That(readinessGate.CallCount, Is.EqualTo(0));
            Assert.That(normalizer.CallCount, Is.EqualTo(1));
            Assert.That(phaseExecutor.CallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenArgumentsIsNotObject_ReturnsInvalidArgumentWithoutExecuting () => UniTask.ToCoroutine(async () =>
        {
            var normalizer = new SpyExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Failure(
                new ExecuteRequestNormalizationError(IpcErrorCodes.InvalidArgument, "normalizer should not run", null)));
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(CreateNormalizedRequest()));
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var request = new IpcExecuteRequest(UcliCommandIds.Plan, default);

            var response = await DispatchAsync(dispatcher, request, context, "Invalid arguments response");

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
            var normalizedRequest = CreateNormalizedRequest(
                ("op-1", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.Resolve),
                ("op-2", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneOpen));
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateFailureTrace(
                normalizedRequest,
                new[]
                {
                    new OperationPhaseTrace(
                        "op-1",
                        MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.Resolve,
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
                        MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneOpen,
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
            var request = CreateExecuteRequest(
                UcliCommandIds.Call,
                false,
                null,
                ("op-1", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.Resolve),
                ("op-2", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneOpen));

            var response = await DispatchAsync(dispatcher, request, context, "Phase execution failure response");

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
        public IEnumerator Dispatch_WhenEditStepFailsAndTrailingPrimitiveIsSkipped_PreservesFailurePhase () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedEditRequest(
                stepId: "edit-1",
                ("edit-1#0", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.CompSet),
                ("edit-1#1", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneSave));
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateFailureTrace(
                normalizedRequest,
                new[]
                {
                    new OperationPhaseTrace(
                        "edit-1#0",
                        MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.CompSet,
                        OperationPhase.Call,
                        true,
                        true,
                        Array.Empty<OperationTouch>(),
                        new OperationFailure(IpcErrorCodes.InvalidArgument, "edit failed", "edit-1")),
                    new OperationPhaseTrace(
                        "edit-1#1",
                        MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneSave,
                        OperationPhase.Skipped,
                        false,
                        false,
                        Array.Empty<OperationTouch>(),
                        null),
                },
                errors: new[]
                {
                    new OperationFailure(IpcErrorCodes.InvalidArgument, "edit failed", "edit-1"),
                }));
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var request = CreateExecuteRequest(UcliCommandIds.Call, operationId: "edit-1", operationName: "edit");

            var response = await DispatchAsync(dispatcher, request, context, "Aggregated edit failure phase response");

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            var opResult = GetSingleArrayElement(response.Payload.GetProperty("opResults"));
            Assert.That(opResult.GetProperty("opId").GetString(), Is.EqualTo("edit-1"));
            Assert.That(opResult.GetProperty("op").GetString(), Is.EqualTo("edit"));
            Assert.That(opResult.GetProperty("phase").GetString(), Is.EqualTo(IpcExecuteOperationPhaseNames.Call));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenEditStepCompilesToNoPrimitives_ReturnsPlanPhaseForSuccessfulNoOp () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedEditRequest(stepId: "edit-1");
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(PhaseExecutionTrace.Success(
                protocolVersion: normalizedRequest.ProtocolVersion,
                requestId: normalizedRequest.RequestId,
                steps: CreateTraceSteps(normalizedRequest, editPrimitiveCount: 0),
                operationTraces: Array.Empty<OperationPhaseTrace>()));
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var request = CreateExecuteRequest(UcliCommandIds.Call, operationId: "edit-1", operationName: "edit");

            var response = await DispatchAsync(dispatcher, request, context, "Successful no-op edit response");

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            var opResult = GetSingleArrayElement(response.Payload.GetProperty("opResults"));
            Assert.That(opResult.GetProperty("opId").GetString(), Is.EqualTo("edit-1"));
            Assert.That(opResult.GetProperty("op").GetString(), Is.EqualTo("edit"));
            Assert.That(opResult.GetProperty("phase").GetString(), Is.EqualTo(IpcExecuteOperationPhaseNames.Plan));
            Assert.That(opResult.GetProperty("applied").GetBoolean(), Is.False);
            Assert.That(opResult.GetProperty("changed").GetBoolean(), Is.False);
            Assert.That(opResult.GetProperty("touched").GetArrayLength(), Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenCancellationRequested_ThrowsOperationCanceledException () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedRequest();
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(normalizedRequest));
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var request = CreateExecuteRequest(UcliCommandIds.Plan);
            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await dispatcher.Dispatch(request, context, cancellationTokenSource.Token).AsUniTask();
            }, "Canceled execute request dispatch", AsyncWaitTimeout);
        });

        private static async UniTask AssertDelegatesToPhaseExecutor (
            string commandName,
            PhaseExecutionCommand expectedCommand,
            string operationName = MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.Resolve)
        {
            var normalizedRequest = CreateNormalizedRequest(operationName);
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(PhaseExecutionTrace.Success(
                protocolVersion: normalizedRequest.ProtocolVersion,
                requestId: normalizedRequest.RequestId,
                steps: CreateTraceSteps(normalizedRequest),
                operationTraces: new[]
                {
                    new OperationPhaseTrace(
                        "op-1",
                        operationName,
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
            var request = CreateExecuteRequest(commandName, operationName: operationName);

            var response = await DispatchAsync(dispatcher, request, context, "Phase executor delegation response");

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.Errors.Count, Is.EqualTo(0));
            Assert.That(phaseExecutor.ReceivedCommand, Is.EqualTo(expectedCommand));
            Assert.That(phaseExecutor.CallCount, Is.EqualTo(1));
            Assert.That(response.Payload.TryGetProperty("opResults", out var opResults), Is.True);
            Assert.That(opResults.GetArrayLength(), Is.EqualTo(1));
            Assert.That(response.Payload.TryGetProperty("operationTraces", out _), Is.False);

            var opResult = GetSingleArrayElement(opResults);
            Assert.That(opResult.GetProperty("opId").GetString(), Is.EqualTo("op-1"));
            Assert.That(opResult.GetProperty("op").GetString(), Is.EqualTo(operationName));
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
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(CreateNormalizedRequest()));
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var request = CreateExecuteRequest(commandName);

            var response = await DispatchAsync(dispatcher, request, context, "Command not implemented response");

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

        private static UniTask<IpcResponse> DispatchAsync (
            ExecuteRequestDispatcher dispatcher,
            IpcExecuteRequest request,
            ExecuteDispatchContext context,
            string description,
            CancellationToken cancellationToken = default)
        {
            return TestAwaiter.WaitAsync(
                dispatcher.Dispatch(request, context, cancellationToken).AsUniTask(),
                description,
                AsyncWaitTimeout);
        }

        private static IpcExecuteRequest CreateExecuteRequest (
            string commandName,
            string operationId = "op-1",
            string operationName = MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.Resolve,
            bool failFast = false,
            string? planToken = null)
        {
            return CreateExecuteRequest(
                commandName,
                failFast,
                planToken,
                (operationId, operationName));
        }

        private static IpcExecuteRequest CreateExecuteRequest (
            string commandName,
            bool failFast,
            string? planToken,
            params (string OperationId, string OperationName)[] operations)
        {
            return new IpcExecuteRequest(
                commandName,
                JsonSerializer.SerializeToElement(new
                {
                    protocolVersion = 1,
                    requestId = "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                    steps = CreateExecuteStepContracts(operations),
                }))
            {
                FailFast = failFast,
                PlanToken = planToken,
            };
        }

        private static NormalizedExecuteRequest CreateNormalizedRequest (string operationName = MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.Resolve)
        {
            return CreateNormalizedRequest(("op-1", operationName));
        }

        private static NormalizedExecuteRequest CreateNormalizedRequest (
            params (string OperationId, string OperationName)[] operations)
        {
            return new NormalizedExecuteRequest(
                ProtocolVersion: 1,
                RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                SourceSteps: CreateSourceSteps(operations),
                PlanToken: null,
                CanonicalDigestPayloadUtf8: Encoding.UTF8.GetBytes("{}"));
        }

        private static NormalizedExecuteRequest CreateNormalizedEditRequest (
            string stepId,
            params (string OperationId, string OperationName)[] operations)
        {
            return new NormalizedExecuteRequest(
                ProtocolVersion: 1,
                RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                SourceSteps: new[]
                {
                    CreateEditSourceStep(stepId),
                },
                PlanToken: null,
                CanonicalDigestPayloadUtf8: Encoding.UTF8.GetBytes("{}"));
        }

        private static object[] CreateExecuteStepContracts (
            IReadOnlyList<(string OperationId, string OperationName)> operations)
        {
            var steps = new object[operations.Count];
            for (var i = 0; i < operations.Count; i++)
            {
                steps[i] = new
                {
                    kind = "op",
                    id = operations[i].OperationId,
                    op = operations[i].OperationName,
                    args = new { },
                };
            }

            return steps;
        }

        private static IpcRequestContractStep[] CreateSourceSteps (
            IReadOnlyList<(string OperationId, string OperationName)> operations)
        {
            var steps = new IpcRequestContractStep[operations.Count];
            for (var i = 0; i < operations.Count; i++)
            {
                steps[i] = new IpcRequestContractStep(
                    Kind: IpcRequestStepKind.Op,
                    Id: operations[i].OperationId,
                    OperationName: operations[i].OperationName,
                    Element: JsonSerializer.SerializeToElement(new
                    {
                        kind = "op",
                        id = operations[i].OperationId,
                        op = operations[i].OperationName,
                        args = new { },
                    }));
            }

            return steps;
        }

        private static IpcRequestContractStep CreateEditSourceStep (string stepId)
        {
            return new IpcRequestContractStep(
                Kind: IpcRequestStepKind.Edit,
                Id: stepId,
                OperationName: null,
                Element: JsonSerializer.SerializeToElement(new
                {
                    kind = "edit",
                    id = stepId,
                    on = new
                    {
                        scene = "Assets/Scenes/Main.unity",
                    },
                    select = new
                    {
                        gameObject = "Root",
                        cardinality = "one",
                    },
                    actions = Array.Empty<object>(),
                    commit = "none",
                }));
        }

        private static NormalizedRequestStep[] CreateTraceSteps (
            NormalizedExecuteRequest request,
            int editPrimitiveCount = 0)
        {
            var steps = new NormalizedRequestStep[request.SourceSteps.Count];
            for (var i = 0; i < request.SourceSteps.Count; i++)
            {
                var sourceStep = request.SourceSteps[i];
                var isEditStep = sourceStep.Kind == IpcRequestStepKind.Edit;
                steps[i] = new NormalizedRequestStep(
                    Id: sourceStep.Id!,
                    Kind: sourceStep.Kind ?? IpcRequestStepKind.Op,
                    OperationName: isEditStep ? "edit" : sourceStep.OperationName!,
                    PrimitiveCount: isEditStep ? editPrimitiveCount : 1);
            }

            return steps;
        }

        private static OperationPhaseTrace[] CreateDefaultOperationTraces (NormalizedExecuteRequest request)
        {
            var traces = new OperationPhaseTrace[request.SourceSteps.Count];
            for (var i = 0; i < request.SourceSteps.Count; i++)
            {
                var sourceStep = request.SourceSteps[i];
                traces[i] = new OperationPhaseTrace(
                    sourceStep.Id!,
                    sourceStep.OperationName!,
                    OperationPhase.Skipped,
                    false,
                    false,
                    System.Array.Empty<OperationTouch>(),
                    null);
            }

            return traces;
        }

        private static PhaseExecutionTrace CreateSuccessTrace (
            NormalizedExecuteRequest request,
            IReadOnlyList<OperationPhaseTrace>? operationTraces = null,
            string? planToken = null)
        {
            var traces = operationTraces ?? CreateDefaultOperationTraces(request);
            return PhaseExecutionTrace.Success(
                protocolVersion: request.ProtocolVersion,
                requestId: request.RequestId,
                steps: CreateTraceSteps(request),
                operationTraces: traces,
                planToken: planToken);
        }

        private static PhaseExecutionTrace CreateFailureTrace (
            NormalizedExecuteRequest request,
            IReadOnlyList<OperationPhaseTrace> operationTraces,
            IReadOnlyList<OperationFailure> errors)
        {
            return PhaseExecutionTrace.Failure(
                protocolVersion: request.ProtocolVersion,
                requestId: request.RequestId,
                steps: CreateTraceSteps(
                    request,
                    request.SourceSteps.Count == 1 && request.SourceSteps[0].Kind == IpcRequestStepKind.Edit
                        ? operationTraces.Count
                        : 0),
                operationTraces: operationTraces,
                errors: errors);
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

        private sealed class SpyUnityMainThreadRequestExecutor : IUnityMainThreadRequestExecutor
        {
            public int CallCount { get; private set; }

            public Task<T> Execute<T> (
                Func<Task<T>> workItem,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CallCount++;
                return workItem();
            }
        }

    }
}
