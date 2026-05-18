using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;
using MackySoft.Ucli.Unity.Execution;
using MackySoft.Ucli.Unity.Execution.Dispatch;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.Requests;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
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
            await AssertDelegatesToPhaseExecutorAsync(UcliCommandIds.Plan, PhaseExecutionCommand.Plan);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenCommandIsCall_DelegatesToPhaseExecutor () => UniTask.ToCoroutine(async () =>
        {
            await AssertDelegatesToPhaseExecutorAsync(UcliCommandIds.Call, PhaseExecutionCommand.Call);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenCommandIsResolve_DelegatesToPhaseExecutor () => UniTask.ToCoroutine(async () =>
        {
            await AssertDelegatesToPhaseExecutorAsync(UcliCommandIds.Resolve, PhaseExecutionCommand.PlanWithoutToken);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenCommandIsQuery_DelegatesToPhaseExecutor () => UniTask.ToCoroutine(async () =>
        {
            await AssertDelegatesToPhaseExecutorAsync(UcliCommandIds.Query, PhaseExecutionCommand.PlanWithoutToken);
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
        public IEnumerator Dispatch_WhenOperationTraceContainsDiagnostics_MapsDiagnosticsToPayload () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedRequest();
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var operationTrace = new OperationPhaseTrace(
                "op-1",
                MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneQuery,
                OperationPhase.Plan,
                false,
                false,
                System.Array.Empty<OperationTouch>(),
                null)
            {
                Diagnostics = new[]
                {
                    new OperationDiagnostic(
                        Code: ExecuteRequestErrorCodes.HierarchyPathUnrepresentableObjects,
                        Severity: IpcExecuteDiagnosticSeverityNames.Warning,
                        CoverageImpact: IpcExecuteDiagnosticCoverageImpactNames.Partial,
                        Message: "Scene query skipped GameObjects whose names contain '/'."),
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
            var request = CreateExecuteRequest(UcliCommandIds.Plan, operationName: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneQuery);

            var response = await DispatchAsync(dispatcher, request, context, "Operation diagnostics payload mapping");

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            var opResult = GetSingleArrayElement(response.Payload.GetProperty("opResults"));
            var diagnostic = GetSingleArrayElement(opResult.GetProperty("diagnostics"));
            Assert.That(diagnostic.GetProperty("code").GetString(), Is.EqualTo(ExecuteRequestErrorCodes.HierarchyPathUnrepresentableObjects.Value));
            Assert.That(diagnostic.GetProperty("severity").GetString(), Is.EqualTo(IpcExecuteDiagnosticSeverityNames.Warning));
            Assert.That(diagnostic.GetProperty("coverageImpact").GetString(), Is.EqualTo(IpcExecuteDiagnosticCoverageImpactNames.Partial));
            Assert.That(diagnostic.GetProperty("message").GetString(), Does.Contain("Scene query skipped"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenRequestStepsCarryPostReadSource_MapsAlignedSourceFactsToPayload () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedRequest(
                ("raw-1", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneOpen),
                ("refresh", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh));
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(PhaseExecutionTrace.Success(
                protocolVersion: normalizedRequest.ProtocolVersion,
                requestId: normalizedRequest.RequestId,
                steps: CreateTraceSteps(normalizedRequest),
                operationTraces: new[]
                {
                    new OperationPhaseTrace(
                        "raw-1",
                        MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneOpen,
                        OperationPhase.Call,
                        true,
                        true,
                        Array.Empty<OperationTouch>(),
                        null),
                    new OperationPhaseTrace(
                        "refresh",
                        MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh,
                        OperationPhase.Call,
                        true,
                        true,
                        Array.Empty<OperationTouch>(),
                        null),
                }));
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var request = CreateExecuteRequest(
                UcliCommandIds.Call,
                failFast: false,
                planToken: null,
                ("raw-1", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneOpen),
                ("refresh", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh));

            var response = await DispatchAsync(dispatcher, request, context, "Post-read source payload mapping");

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            var steps = response.Payload.GetProperty("postReadSource").GetProperty("steps");
            Assert.That(steps.GetArrayLength(), Is.EqualTo(2));
            var rawSource = GetArrayElement(steps, 0);
            Assert.That(rawSource.GetProperty("opId").GetString(), Is.EqualTo("raw-1"));
            Assert.That(rawSource.GetProperty("sourceKind").GetString(), Is.EqualTo(IpcExecutePostReadSourceKindNames.Operation));
            Assert.That(rawSource.GetProperty("commit").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(rawSource.GetProperty("persistenceExpected").GetBoolean(), Is.False);
            Assert.That(rawSource.GetProperty("expectedPostState").GetString(), Is.EqualTo(IpcExecuteExpectedPostStateNames.Unavailable));
            var refreshSource = GetArrayElement(steps, 1);
            Assert.That(refreshSource.GetProperty("opId").GetString(), Is.EqualTo("refresh"));
            Assert.That(refreshSource.GetProperty("sourceKind").GetString(), Is.EqualTo(IpcExecutePostReadSourceKindNames.Refresh));
            Assert.That(refreshSource.GetProperty("commit").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(refreshSource.GetProperty("persistenceExpected").GetBoolean(), Is.True);
            Assert.That(refreshSource.GetProperty("expectedPostState").GetString(), Is.EqualTo(IpcExecuteExpectedPostStateNames.Unavailable));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenMayDirtyFalseButChangedTrue_ReportsContractViolation () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedRequest(
                ("step-1", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh));
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(PhaseExecutionTrace.Success(
                protocolVersion: normalizedRequest.ProtocolVersion,
                requestId: normalizedRequest.RequestId,
                steps: CreateTraceSteps(normalizedRequest),
                operationTraces: new[]
                {
                    new OperationPhaseTrace(
                        "step-1",
                        MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh,
                        OperationPhase.Call,
                        true,
                        true,
                        System.Array.Empty<OperationTouch>(),
                        null)
                    {
                        Contracts = CreateContractFacts(UcliOperationKind.Mutation, mayDirty: false),
                    },
                }));
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var request = CreateExecuteRequest(
                UcliCommandIds.Call,
                operationId: "step-1",
                operationName: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh);

            var response = await DispatchAsync(dispatcher, request, context, "mayDirty contract violation response");

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(ExecuteRequestErrorCodes.OperationContractViolation));
            var opResult = GetSingleArrayElement(response.Payload.GetProperty("opResults"));
            Assert.That(opResult.GetProperty("changed").GetBoolean(), Is.True);
            var violation = GetSingleArrayElement(response.Payload.GetProperty("contractViolations"));
            Assert.That(violation.GetProperty("opId").GetString(), Is.EqualTo("step-1"));
            Assert.That(violation.GetProperty("operation").GetString(), Is.EqualTo(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh));
            Assert.That(violation.GetProperty("expectedFact").GetString(), Is.EqualTo("assurance.mayDirty=false"));
            Assert.That(violation.GetProperty("observedResult").GetString(), Is.EqualTo("opResults[].changed=true"));
            Assert.That(violation.GetProperty("applicationState").GetString(), Is.EqualTo(IpcExecuteApplicationStateNames.Indeterminate));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenQueryTraceReportsApplicationEffects_ReportsContractViolations () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedRequest(
                ("step-1", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneQuery));
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(PhaseExecutionTrace.Success(
                protocolVersion: normalizedRequest.ProtocolVersion,
                requestId: normalizedRequest.RequestId,
                steps: CreateTraceSteps(normalizedRequest),
                operationTraces: new[]
                {
                    new OperationPhaseTrace(
                        "step-1",
                        MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneQuery,
                        OperationPhase.Call,
                        true,
                        true,
                        new[]
                        {
                            new OperationTouch(OperationTouchKind.Asset, "Assets/Example.txt", null),
                        },
                        null)
                    {
                        Contracts = CreateContractFacts(
                            UcliOperationKind.Query,
                            mayDirty: true,
                            IpcExecuteTouchedResourceKindNames.Asset),
                    },
                }));
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var request = CreateExecuteRequest(
                UcliCommandIds.Query,
                operationId: "step-1",
                operationName: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneQuery);

            var response = await DispatchAsync(dispatcher, request, context, "query contract violation response");

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(ExecuteRequestErrorCodes.OperationContractViolation));
            var violations = response.Payload.GetProperty("contractViolations");
            Assert.That(violations.GetArrayLength(), Is.EqualTo(3));
            var observedResults = new List<string>();
            foreach (var violation in violations.EnumerateArray())
            {
                Assert.That(violation.GetProperty("expectedFact").GetString(), Is.EqualTo("operation.kind=query"));
                Assert.That(violation.GetProperty("applicationState").GetString(), Is.EqualTo(IpcExecuteApplicationStateNames.Indeterminate));
                observedResults.Add(violation.GetProperty("observedResult").GetString()!);
            }

            Assert.That(observedResults, Does.Contain("opResults[].applied=true"));
            Assert.That(observedResults, Does.Contain("opResults[].changed=true"));
            Assert.That(observedResults, Does.Contain("opResults[].touched.length=1"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenTouchedKindIsOutsideAssurance_ReportsContractViolation () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedRequest(
                ("step-1", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh));
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(PhaseExecutionTrace.Success(
                protocolVersion: normalizedRequest.ProtocolVersion,
                requestId: normalizedRequest.RequestId,
                steps: CreateTraceSteps(normalizedRequest),
                operationTraces: new[]
                {
                    new OperationPhaseTrace(
                        "step-1",
                        MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh,
                        OperationPhase.Call,
                        true,
                        false,
                        new[]
                        {
                            new OperationTouch(OperationTouchKind.Asset, "Assets/Example.txt", null),
                        },
                        null)
                    {
                        Contracts = CreateContractFacts(
                            UcliOperationKind.Mutation,
                            mayDirty: true,
                            IpcExecuteTouchedResourceKindNames.Scene),
                    },
                }));
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var request = CreateExecuteRequest(
                UcliCommandIds.Call,
                operationId: "step-1",
                operationName: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh);

            var response = await DispatchAsync(dispatcher, request, context, "touched kind contract violation response");

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            var violation = GetSingleArrayElement(response.Payload.GetProperty("contractViolations"));
            Assert.That(violation.GetProperty("expectedFact").GetString(), Is.EqualTo("assurance.touchedKinds=[scene]"));
            Assert.That(violation.GetProperty("observedResult").GetString(), Is.EqualTo("opResults[].touched[].kind=asset"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenAllowedMutationReportsTouchedKind_DoesNotReportContractViolation () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedRequest(
                ("step-1", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh));
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(PhaseExecutionTrace.Success(
                protocolVersion: normalizedRequest.ProtocolVersion,
                requestId: normalizedRequest.RequestId,
                steps: CreateTraceSteps(normalizedRequest),
                operationTraces: new[]
                {
                    new OperationPhaseTrace(
                        "step-1",
                        MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh,
                        OperationPhase.Call,
                        true,
                        true,
                        new[]
                        {
                            new OperationTouch(OperationTouchKind.Asset, "Assets/Example.txt", null),
                        },
                        null)
                    {
                        Contracts = CreateContractFacts(
                            UcliOperationKind.Mutation,
                            mayDirty: true,
                            IpcExecuteTouchedResourceKindNames.Asset),
                    },
                }));
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var request = CreateExecuteRequest(
                UcliCommandIds.Call,
                operationId: "step-1",
                operationName: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh);

            var response = await DispatchAsync(dispatcher, request, context, "allowed mutation response");

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            Assert.That(response.Payload.TryGetProperty("contractViolations", out _), Is.False);
            Assert.That(response.Errors.Count, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenPublicStepContainsDiagnostics_MapsDiagnosticsToPayload () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedRequest(operationName: "edit");
            var traceSteps = CreateTraceSteps(normalizedRequest);
            traceSteps[0] = traceSteps[0] with
            {
                Diagnostics = new[]
                {
                    new OperationDiagnostic(
                        Code: ExecuteRequestErrorCodes.HierarchyPathUnrepresentableObjects,
                        Severity: IpcExecuteDiagnosticSeverityNames.Warning,
                        CoverageImpact: IpcExecuteDiagnosticCoverageImpactNames.Partial,
                        Message: "Scene edit selection skipped GameObjects whose names contain '/'."),
                },
            };
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(PhaseExecutionTrace.Success(
                protocolVersion: normalizedRequest.ProtocolVersion,
                requestId: normalizedRequest.RequestId,
                steps: traceSteps,
                operationTraces: new[]
                {
                    new OperationPhaseTrace(
                        "op-1",
                        MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDelete,
                        OperationPhase.Call,
                        true,
                        true,
                        System.Array.Empty<OperationTouch>(),
                        null),
                }));
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var request = CreateExecuteRequest(UcliCommandIds.Call, operationName: "edit");

            var response = await DispatchAsync(dispatcher, request, context, "Public step diagnostics payload mapping");

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusOk));
            var opResult = GetSingleArrayElement(response.Payload.GetProperty("opResults"));
            Assert.That(opResult.GetProperty("op").GetString(), Is.EqualTo("edit"));
            var diagnostic = GetSingleArrayElement(opResult.GetProperty("diagnostics"));
            Assert.That(diagnostic.GetProperty("code").GetString(), Is.EqualTo(ExecuteRequestErrorCodes.HierarchyPathUnrepresentableObjects.Value));
            Assert.That(diagnostic.GetProperty("severity").GetString(), Is.EqualTo(IpcExecuteDiagnosticSeverityNames.Warning));
            Assert.That(diagnostic.GetProperty("coverageImpact").GetString(), Is.EqualTo(IpcExecuteDiagnosticCoverageImpactNames.Partial));
            Assert.That(diagnostic.GetProperty("message").GetString(), Does.Contain("Scene edit selection skipped"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenEditSelectFromSkipsSlashNamedGameObject_PreservesRequestDiagnostics () => UniTask.ToCoroutine(async () =>
        {
            using var scope = new EditorTestScope();
            var scenePath = scope.CreateScenePath(nameof(ExecuteRequestDispatcherTests));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _ = new GameObject("GoodRoot");
            _ = new GameObject("Bad/Root");
            EditorSceneManager.SaveScene(scene, scenePath);
            var dispatcher = new ExecuteRequestDispatcher(new ExecuteRequestNormalizer(), CreateCatalogPhaseExecutor());
            var context = new ExecuteDispatchContext("req-291-diagnostics", IpcProtocol.CurrentVersion);
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    requestId = "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            id = "deleteGoodRootDirectly",
                            on = new
                            {
                                scene = scenePath,
                            },
                            select = new
                            {
                                gameObject = "GoodRoot",
                                cardinality = "one",
                            },
                            actions = new object[]
                            {
                                new
                                {
                                    kind = "delete",
                                },
                            },
                            commit = "none",
                        },
                        new
                        {
                            kind = "edit",
                            id = "deleteMissingFirst",
                            on = new
                            {
                                scene = scenePath,
                            },
                            select = new
                            {
                                from = new
                                {
                                    op = UcliPrimitiveOperationNames.SceneQuery,
                                    args = new
                                    {
                                        pathPrefix = "Missing",
                                    },
                                },
                                cardinality = "first",
                            },
                            actions = new object[]
                            {
                                new
                                {
                                    kind = "delete",
                                },
                            },
                            commit = "none",
                        },
                    },
                });

            var response = await DispatchAsync(dispatcher, request, context, "Request diagnostics from edit select.from");

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(response.Errors[0].OpId, Is.EqualTo("deleteMissingFirst"));
            var opResults = response.Payload.GetProperty("opResults");
            Assert.That(opResults.GetArrayLength(), Is.EqualTo(2));
            var cleanResult = opResults[0];
            Assert.That(cleanResult.GetProperty("opId").GetString(), Is.EqualTo("deleteGoodRootDirectly"));
            Assert.That(cleanResult.GetProperty("diagnostics").GetArrayLength(), Is.EqualTo(0));
            var failedResult = opResults[1];
            Assert.That(failedResult.GetProperty("opId").GetString(), Is.EqualTo("deleteMissingFirst"));
            Assert.That(failedResult.GetProperty("op").GetString(), Is.EqualTo("edit"));
            var diagnostic = GetSingleArrayElement(failedResult.GetProperty("diagnostics"));
            Assert.That(diagnostic.GetProperty("code").GetString(), Is.EqualTo(ExecuteRequestErrorCodes.HierarchyPathUnrepresentableObjects.Value));
            Assert.That(diagnostic.GetProperty("severity").GetString(), Is.EqualTo(IpcExecuteDiagnosticSeverityNames.Warning));
            Assert.That(diagnostic.GetProperty("coverageImpact").GetString(), Is.EqualTo(IpcExecuteDiagnosticCoverageImpactNames.Partial));
            Assert.That(diagnostic.GetProperty("message").GetString(), Does.Contain("hierarchyPath cannot represent"));
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
            Assert.That(secondResponse.Errors[0].Code, Is.EqualTo(ExecuteRequestErrorCodes.RequestIdConflict));
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
            Assert.That(secondResponse.Errors[0].Code, Is.EqualTo(ExecuteRequestErrorCodes.RequestIdConflict));
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

            var responseTask = dispatcher.DispatchAsync(request, context).AsUniTask();
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
            Assert.That(response.Errors[0].Code, Is.EqualTo(EditorLifecycleErrorCodes.EditorBusy));
            Assert.That(readinessGate.CallCount, Is.EqualTo(1));
            Assert.That(readinessGate.LastFailFast, Is.True);
            Assert.That(mainThreadRequestExecutor.CallCount, Is.EqualTo(0));
            AssertEmptyOpResultsPayload(response.Payload);
            Assert.That(phaseExecutor.CallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenCommandIsRefresh_ReturnsCommandNotImplementedError () => UniTask.ToCoroutine(async () =>
        {
            await AssertReturnsCommandNotImplementedErrorAsync(UcliCommandIds.Refresh);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenNormalizationFails_ReturnsNormalizationError () => UniTask.ToCoroutine(async () =>
        {
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Failure(
                new ExecuteRequestNormalizationError(UcliCoreErrorCodes.InvalidArgument, "invalid request", "op-1")));
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(CreateNormalizedRequest()));
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var request = CreateExecuteRequest(UcliCommandIds.Plan);

            var response = await DispatchAsync(dispatcher, request, context, "Normalization failure response");

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(response.Errors[0].OpId, Is.EqualTo("op-1"));
            AssertEmptyOpResultsPayload(response.Payload);
            Assert.That(phaseExecutor.CallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenCommandIsNotImplemented_DoesNotWaitForReadiness () => UniTask.ToCoroutine(async () =>
        {
            var normalizer = new SpyExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Failure(
                new ExecuteRequestNormalizationError(UcliCoreErrorCodes.InvalidArgument, "normalizer should not run", null)));
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(CreateNormalizedRequest()));
            var readinessGate = StubUnityEditorReadinessGate.CreatePending();
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor, readinessGate);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var request = CreateExecuteRequest(UcliCommandIds.Refresh);

            var response = await DispatchAsync(dispatcher, request, context, "Command not implemented without readiness wait");

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.CommandNotImplemented));
            Assert.That(readinessGate.CallCount, Is.EqualTo(0));
            Assert.That(normalizer.CallCount, Is.EqualTo(0));
            Assert.That(phaseExecutor.CallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenNormalizationFails_DoesNotWaitForReadiness () => UniTask.ToCoroutine(async () =>
        {
            var normalizer = new SpyExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Failure(
                new ExecuteRequestNormalizationError(UcliCoreErrorCodes.InvalidArgument, "invalid request", "op-1")));
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(CreateNormalizedRequest()));
            var readinessGate = StubUnityEditorReadinessGate.CreatePending();
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor, readinessGate);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var request = CreateExecuteRequest(UcliCommandIds.Plan);

            var response = await DispatchAsync(dispatcher, request, context, "Normalization failure without readiness wait");

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(readinessGate.CallCount, Is.EqualTo(0));
            Assert.That(normalizer.CallCount, Is.EqualTo(1));
            Assert.That(phaseExecutor.CallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenArgumentsIsNotObject_ReturnsInvalidArgumentWithoutExecuting () => UniTask.ToCoroutine(async () =>
        {
            var normalizer = new SpyExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Failure(
                new ExecuteRequestNormalizationError(UcliCoreErrorCodes.InvalidArgument, "normalizer should not run", null)));
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(CreateNormalizedRequest()));
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var request = new IpcExecuteRequest(UcliCommandIds.Plan, default);

            var response = await DispatchAsync(dispatcher, request, context, "Invalid arguments response");

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
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
                        new OperationFailure(UcliCoreErrorCodes.InvalidArgument, "call failed", "op-1")),
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
                    new OperationFailure(UcliCoreErrorCodes.InvalidArgument, "call failed", "op-1"),
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
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
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
                        new OperationFailure(UcliCoreErrorCodes.InvalidArgument, "edit failed", "edit-1")),
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
                    new OperationFailure(UcliCoreErrorCodes.InvalidArgument, "edit failed", "edit-1"),
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
            var sourceStep = GetSingleArrayElement(response.Payload.GetProperty("postReadSource").GetProperty("steps"));
            Assert.That(sourceStep.GetProperty("opId").GetString(), Is.EqualTo("edit-1"));
            Assert.That(sourceStep.GetProperty("sourceKind").GetString(), Is.EqualTo(IpcExecutePostReadSourceKindNames.Edit));
            Assert.That(sourceStep.GetProperty("commit").GetString(), Is.EqualTo(IpcExecutePostReadCommitNames.None));
            Assert.That(sourceStep.GetProperty("persistenceExpected").GetBoolean(), Is.False);
            Assert.That(sourceStep.GetProperty("expectedPostState").GetString(), Is.EqualTo(IpcExecuteExpectedPostStateNames.Deterministic));
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
                await dispatcher.DispatchAsync(request, context, cancellationTokenSource.Token).AsUniTask();
            }, "Canceled execute request dispatch", AsyncWaitTimeout);
        });

        private static async UniTask AssertDelegatesToPhaseExecutorAsync (
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

        private static async UniTask AssertReturnsCommandNotImplementedErrorAsync (string commandName)
        {
            var normalizer = new SpyExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Failure(
                new ExecuteRequestNormalizationError(UcliCoreErrorCodes.InvalidArgument, "normalizer should not run", null)));
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(CreateNormalizedRequest()));
            var dispatcher = new ExecuteRequestDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext("req-1", IpcProtocol.CurrentVersion);
            var request = CreateExecuteRequest(commandName);

            var response = await DispatchAsync(dispatcher, request, context, "Command not implemented response");

            Assert.That(response.Status, Is.EqualTo(IpcProtocol.StatusError));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.CommandNotImplemented));
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

        private static JsonElement GetArrayElement (
            JsonElement arrayElement,
            int index)
        {
            var currentIndex = 0;
            foreach (var element in arrayElement.EnumerateArray())
            {
                if (currentIndex == index)
                {
                    return element;
                }

                currentIndex++;
            }

            Assert.Fail($"Array element at index {index} was not found.");
            return default;
        }

        private static UniTask<IpcResponse> DispatchAsync (
            ExecuteRequestDispatcher dispatcher,
            IpcExecuteRequest request,
            ExecuteDispatchContext context,
            string description,
            CancellationToken cancellationToken = default)
        {
            return TestAwaiter.WaitAsync(
                dispatcher.DispatchAsync(request, context, cancellationToken).AsUniTask(),
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

        private static IpcExecuteRequest CreateExecuteRequest (
            string commandName,
            object arguments)
        {
            return new IpcExecuteRequest(commandName, JsonSerializer.SerializeToElement(arguments));
        }

        private static OperationPhaseExecutor CreateCatalogPhaseExecutor ()
        {
            var snapshot = UcliOperationCatalogSnapshotBuilder.Build();
            return new OperationPhaseExecutor(new InMemoryPhaseOperationRegistry(snapshot.Registrations));
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
                AllowDangerous: false,
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
                AllowDangerous: false,
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
                    PrimitiveCount: isEditStep ? editPrimitiveCount : 1)
                {
                    PostReadSourceStep = CreatePostReadSourceStep(sourceStep, isEditStep),
                };
            }

            return steps;
        }

        private static IpcExecutePostReadSourceStep CreatePostReadSourceStep (
            IpcRequestContractStep sourceStep,
            bool isEditStep)
        {
            if (isEditStep)
            {
                return new IpcExecutePostReadSourceStep(
                    OpId: sourceStep.Id!,
                    SourceKind: IpcExecutePostReadSourceKindNames.Edit,
                    PlayModeMutation: false,
                    Commit: IpcExecutePostReadCommitNames.None,
                    PersistenceExpected: false,
                    ExpectedPostState: IpcExecuteExpectedPostStateNames.Deterministic);
            }

            var sourceKind = string.Equals(sourceStep.OperationName, UcliPrimitiveOperationNames.ProjectRefresh, StringComparison.Ordinal)
                ? IpcExecutePostReadSourceKindNames.Refresh
                : IpcExecutePostReadSourceKindNames.Operation;
            return new IpcExecutePostReadSourceStep(
                OpId: sourceStep.Id!,
                SourceKind: sourceKind,
                PlayModeMutation: false,
                Commit: null,
                PersistenceExpected: string.Equals(sourceKind, IpcExecutePostReadSourceKindNames.Refresh, StringComparison.Ordinal),
                ExpectedPostState: IpcExecuteExpectedPostStateNames.Unavailable);
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

        private static OperationPhaseTrace.ContractFacts CreateContractFacts (
            UcliOperationKind operationKind,
            bool mayDirty,
            params string[] touchedKinds)
        {
            return new OperationPhaseTrace.ContractFacts(
                operationKind,
                MayDirty: mayDirty,
                MayPersist: false,
                TouchedKinds: touchedKinds);
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

            public Task<PhaseExecutionTrace> ExecuteAsync (
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

            public Task<T> ExecuteAsync<T> (
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
