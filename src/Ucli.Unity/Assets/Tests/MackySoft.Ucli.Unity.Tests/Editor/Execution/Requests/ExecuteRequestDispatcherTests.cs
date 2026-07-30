using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Text.Vocabularies;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Operations;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;
using MackySoft.Ucli.Unity.Execution;
using MackySoft.Ucli.Unity.Execution.Dispatch;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.PlanToken;
using MackySoft.Ucli.Unity.Execution.RequestIdempotency;
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
        private static readonly Sha256Digest OperationDescriptorDigest =
            Sha256Digest.Parse("2b17de93e39b2a735126da7de4cc66e64c24c17baa04d2db1910fcca5345aff1");

        private static readonly IpcProjectIdentity ProjectIdentity = new IpcProjectIdentity(
            ProjectPathTestValues.RepositoryUnityProject,
            ProjectFingerprintTestFactory.Create("execute-request-dispatcher"),
            "6000.1.4f1");

        private static readonly TimeSpan AsyncWaitTimeout = TimeSpan.FromSeconds(5);

        [Test]
        [Category("Size.Small")]
        public void ExecuteDispatchContext_WhenRequestIdIsEmpty_ThrowsArgumentException ()
        {
            var exception = Assert.Throws<ArgumentException>(() =>
                _ = new ExecuteDispatchContext(Guid.Empty, ProjectIdentity));

            Assert.That(exception.ParamName, Is.EqualTo("requestId"));
        }

        [Test]
        [Category("Size.Small")]
        public void ExecuteDispatchContext_WhenProjectIsNull_ThrowsArgumentNullException ()
        {
            var exception = Assert.Throws<ArgumentNullException>(() =>
                _ = new ExecuteDispatchContext(Guid.NewGuid(), null!));

            Assert.That(exception.ParamName, Is.EqualTo("project"));
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenCommandIsPlan_DelegatesToPhaseExecutor () => UniTask.ToCoroutine(async () =>
        {
            await AssertDelegatesToPhaseExecutorAsync(
                UcliCommandIds.Plan.Name,
                PhaseExecutionCommand.Plan,
                MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.Resolve);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenCommandIsCall_DelegatesToPhaseExecutor () => UniTask.ToCoroutine(async () =>
        {
            await AssertDelegatesToPhaseExecutorAsync(
                UcliCommandIds.Call.Name,
                PhaseExecutionCommand.Call,
                MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.Resolve);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenCommandIsResolve_DelegatesToPhaseExecutor () => UniTask.ToCoroutine(async () =>
        {
            await AssertDelegatesToPhaseExecutorAsync(
                UcliCommandIds.Resolve.Name,
                PhaseExecutionCommand.PlanWithoutToken,
                MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.Resolve);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenCommandIsQuery_DelegatesToPhaseExecutor () => UniTask.ToCoroutine(async () =>
        {
            await AssertDelegatesToPhaseExecutorAsync(
                UcliCommandIds.Query.Name,
                PhaseExecutionCommand.PlanWithoutToken,
                MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.Resolve);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenPlanTraceContainsPlanToken_MapsTokenToPayload () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedRequest();
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(CreatePlanSuccessTrace(normalizedRequest, "issued-token"));
            var dispatcher = CreateDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext(Guid.NewGuid(), ProjectIdentity);
            var request = CreateExecuteRequest(UcliCommandIds.Plan.Name);

            var response = await DispatchAsync(dispatcher, request, context, "Plan token payload mapping");

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
            Assert.That(response.Payload.TryGetProperty("planToken", out var planToken), Is.True);
            Assert.That(planToken.GetString(), Is.EqualTo("issued-token"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenOperationTraceContainsResult_MapsResultToPayload () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedRequest(UcliPrimitiveOperationNames.GoDescribe);
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var operationMetadata = new GoDescribeOperation().Metadata;
            var operationResult = new GameObjectDescriptionResult(
                name: "Root",
                globalObjectId: null,
                components: Array.Empty<GameObjectComponentDescriptionResult>(),
                children: Array.Empty<GameObjectDescriptionResult>());
            var operationOutcome = operationMetadata.CompleteCallResult(
                OperationPhaseStepResult.SuccessWithTypedResult(
                    operationResult,
                    applied: false,
                    changed: false,
                    touched: Array.Empty<OperationTouch>()));
            var operationTrace = OperationPhaseTrace.Variants.PlanSuccess(
                opId: new IpcExecuteStepId("op-1"),
                op: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
                outcome: operationOutcome,
                contracts: OperationContractFacts.FromMetadata(operationMetadata));
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(
                normalizedRequest,
                new[]
                {
                    operationTrace,
                }));
            var dispatcher = CreateDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext(Guid.NewGuid(), ProjectIdentity);
            var request = CreateExecuteRequest(UcliCommandIds.Plan.Name, operationName: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe);

            var response = await DispatchAsync(dispatcher, request, context, "Operation result payload mapping");

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
            var opResult = GetSingleArrayElement(response.Payload.GetProperty("opResults"));
            Assert.That(opResult.TryGetProperty("result", out var result), Is.True);
            Assert.That(result.GetProperty("name").GetString(), Is.EqualTo("Root"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenOperationTraceContainsDiagnostics_MapsDiagnosticsToPayload () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedRequest(UcliPrimitiveOperationNames.SceneQuery);
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var operationTrace = OperationPhaseTrace.Variants.PlanSuccess(
                opId: new IpcExecuteStepId("op-1"),
                op: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneQuery,
                outcome: CreateNonJudgingSuccessOutcome(
                    applied: false,
                    changed: false,
                    touched: System.Array.Empty<OperationTouch>(),
                    readInvalidations: System.Array.Empty<OperationReadInvalidation>(),
                    diagnostics: new[]
                    {
                        new OperationDiagnostic(
                            Code: ExecuteRequestErrorCodes.HierarchyPathUnrepresentableObjects,
                            Severity: UcliDiagnosticSeverity.Warning,
                            CoverageImpact: IpcExecuteDiagnosticCoverageImpact.Partial,
                            Message: "Scene query skipped GameObjects whose names contain '/'."),
                    },
                    persisted: false),
                contracts: CreateContractFacts(
                    UcliPrimitiveOperationNames.SceneQuery,
                    UcliOperationKind.Query,
                    mayDirty: false));
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(
                normalizedRequest,
                new[]
                {
                    operationTrace,
                }));
            var dispatcher = CreateDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext(Guid.NewGuid(), ProjectIdentity);
            var request = CreateExecuteRequest(UcliCommandIds.Plan.Name, operationName: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneQuery);

            var response = await DispatchAsync(dispatcher, request, context, "Operation diagnostics payload mapping");

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
            var opResult = GetSingleArrayElement(response.Payload.GetProperty("opResults"));
            var diagnostic = GetSingleArrayElement(opResult.GetProperty("diagnostics"));
            Assert.That(diagnostic.GetProperty("code").GetString(), Is.EqualTo(ExecuteRequestErrorCodes.HierarchyPathUnrepresentableObjects.Value));
            Assert.That(diagnostic.GetProperty("severity").GetString(), Is.EqualTo(Vocabulary.GetText(UcliDiagnosticSeverity.Warning)));
            Assert.That(diagnostic.GetProperty("coverageImpact").GetString(), Is.EqualTo(Vocabulary.GetText(IpcExecuteDiagnosticCoverageImpact.Partial)));
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
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(
                normalizedRequest,
                new[]
                {
                    OperationPhaseTrace.Variants.CallSuccessWithoutVerdict(
                        new IpcExecuteStepId("raw-1"),
                        MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneOpen,
                        outcome: CreateNonJudgingSuccessOutcome(
                            applied: true,
                            changed: true,
                            touched: Array.Empty<OperationTouch>(),
                            readInvalidations: Array.Empty<OperationReadInvalidation>(),
                            diagnostics: Array.Empty<OperationDiagnostic>(),
                            persisted: false),
                        contracts: CreateContractFacts(
                            UcliPrimitiveOperationNames.SceneOpen,
                            UcliOperationKind.Command,
                            mayDirty: true)),
                    OperationPhaseTrace.Variants.CallSuccessWithoutVerdict(
                        new IpcExecuteStepId("refresh"),
                        MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh,
                        outcome: CreateNonJudgingSuccessOutcome(
                            applied: true,
                            changed: true,
                            touched: Array.Empty<OperationTouch>(),
                            readInvalidations: Array.Empty<OperationReadInvalidation>(),
                            diagnostics: Array.Empty<OperationDiagnostic>(),
                            persisted: false),
                        contracts: CreateContractFacts(
                            UcliPrimitiveOperationNames.ProjectRefresh,
                            UcliOperationKind.Command,
                            mayDirty: true)),
                }));
            var dispatcher = CreateDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext(Guid.NewGuid(), ProjectIdentity);
            var request = CreateExecuteRequest(
                UcliCommandIds.Call.Name,
                failFast: false,
                planToken: null,
                ("raw-1", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneOpen),
                ("refresh", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh));

            var response = await DispatchAsync(dispatcher, request, context, "Post-read source payload mapping");

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
            var steps = response.Payload.GetProperty("postReadSource").GetProperty("steps");
            Assert.That(steps.GetArrayLength(), Is.EqualTo(2));
            var rawSource = GetArrayElement(steps, 0);
            Assert.That(rawSource.GetProperty("sourceKind").GetString(), Is.EqualTo(Vocabulary.GetText(IpcExecutePostReadSourceKind.Operation)));
            Assert.That(rawSource.GetProperty("commit").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(rawSource.GetProperty("persistenceExpected").GetBoolean(), Is.False);
            Assert.That(rawSource.GetProperty("expectedPostState").GetString(), Is.EqualTo(Vocabulary.GetText(IpcExecuteExpectedPostState.Unavailable)));
            var refreshSource = GetArrayElement(steps, 1);
            Assert.That(refreshSource.GetProperty("sourceKind").GetString(), Is.EqualTo(Vocabulary.GetText(IpcExecutePostReadSourceKind.Refresh)));
            Assert.That(refreshSource.GetProperty("commit").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(refreshSource.GetProperty("persistenceExpected").GetBoolean(), Is.True);
            Assert.That(refreshSource.GetProperty("expectedPostState").GetString(), Is.EqualTo(Vocabulary.GetText(IpcExecuteExpectedPostState.Unavailable)));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenMayDirtyFalseButChangedTrue_ReportsContractViolation () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedRequest(
                ("step-1", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh));
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(
                normalizedRequest,
                new[]
                {
                    OperationPhaseTrace.Variants.CallSuccessWithoutVerdict(
                        new IpcExecuteStepId("step-1"),
                        MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh,
                        outcome: CreateNonJudgingSuccessOutcome(
                            applied: true,
                            changed: true,
                            touched: System.Array.Empty<OperationTouch>(),
                            readInvalidations: System.Array.Empty<OperationReadInvalidation>(),
                            diagnostics: System.Array.Empty<OperationDiagnostic>(),
                            persisted: false),
                        contracts: CreateContractFacts(
                            UcliPrimitiveOperationNames.ProjectRefresh,
                            UcliOperationKind.Mutation,
                            mayDirty: false)),
                }));
            var dispatcher = CreateDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext(Guid.NewGuid(), ProjectIdentity);
            var request = CreateExecuteRequest(
                UcliCommandIds.Call.Name,
                operationId: "step-1",
                operationName: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh);

            var response = await DispatchAsync(dispatcher, request, context, "mayDirty contract violation response");

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(ExecuteRequestErrorCodes.OperationContractViolation));
            var opResult = GetSingleArrayElement(response.Payload.GetProperty("opResults"));
            Assert.That(opResult.GetProperty("changed").GetBoolean(), Is.True);
            var violation = GetSingleArrayElement(response.Payload.GetProperty("contractViolations"));
            Assert.That(violation.GetProperty("instancePath").GetString(), Is.EqualTo("/opResults/0"));
            Assert.That(violation.GetProperty("operation").GetString(), Is.EqualTo(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh));
            Assert.That(violation.GetProperty("expectedFact").GetString(), Is.EqualTo("assurance.mayDirty=false"));
            Assert.That(violation.GetProperty("observedResult").GetString(), Is.EqualTo("opResults[].changed=true"));
            Assert.That(violation.GetProperty("applicationState").GetString(), Is.EqualTo(Vocabulary.GetText(IpcApplicationState.Applied)));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenMayPersistFalseButPersistedTrue_ReportsContractViolation () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedRequest(
                ("step-1", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh));
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(
                normalizedRequest,
                new[]
                {
                    OperationPhaseTrace.Variants.CallSuccessWithoutVerdict(
                        new IpcExecuteStepId("step-1"),
                        MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh,
                        outcome: CreateNonJudgingSuccessOutcome(
                            applied: false,
                            changed: false,
                            touched: System.Array.Empty<OperationTouch>(),
                            readInvalidations: System.Array.Empty<OperationReadInvalidation>(),
                            diagnostics: System.Array.Empty<OperationDiagnostic>(),
                            persisted: true),
                        contracts: CreateContractFacts(
                            UcliPrimitiveOperationNames.ProjectRefresh,
                            UcliOperationKind.Mutation,
                            mayDirty: true,
                            mayPersist: false)),
                }));
            var dispatcher = CreateDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext(Guid.NewGuid(), ProjectIdentity);
            var request = CreateExecuteRequest(
                UcliCommandIds.Call.Name,
                operationId: "step-1",
                operationName: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh);

            var response = await DispatchAsync(dispatcher, request, context, "mayPersist contract violation response");

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(ExecuteRequestErrorCodes.OperationContractViolation));
            var violation = GetSingleArrayElement(response.Payload.GetProperty("contractViolations"));
            Assert.That(violation.GetProperty("expectedFact").GetString(), Is.EqualTo("assurance.mayPersist=false"));
            Assert.That(violation.GetProperty("observedResult").GetString(), Is.EqualTo("executionTrace.persisted=true"));
            Assert.That(violation.GetProperty("applicationState").GetString(), Is.EqualTo(Vocabulary.GetText(IpcApplicationState.Applied)));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenChangedViolationHasNoApply_ReportsIndeterminateApplicationState () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedRequest(
                ("step-1", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh));
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(
                normalizedRequest,
                new[]
                {
                    OperationPhaseTrace.Variants.PlanSuccess(
                        new IpcExecuteStepId("step-1"),
                        MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh,
                        outcome: CreateNonJudgingSuccessOutcome(
                            applied: false,
                            changed: true,
                            touched: System.Array.Empty<OperationTouch>(),
                            readInvalidations: System.Array.Empty<OperationReadInvalidation>(),
                            diagnostics: System.Array.Empty<OperationDiagnostic>(),
                            persisted: false),
                        contracts: CreateContractFacts(
                            UcliPrimitiveOperationNames.ProjectRefresh,
                            UcliOperationKind.Mutation,
                            mayDirty: false)),
                }));
            var dispatcher = CreateDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext(Guid.NewGuid(), ProjectIdentity);
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan.Name,
                operationId: "step-1",
                operationName: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh);

            var response = await DispatchAsync(dispatcher, request, context, "indeterminate contract violation response");

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            var violation = GetSingleArrayElement(response.Payload.GetProperty("contractViolations"));
            Assert.That(violation.GetProperty("applicationState").GetString(), Is.EqualTo(Vocabulary.GetText(IpcApplicationState.Indeterminate)));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenQueryTraceReportsApplicationEffects_ReportsContractViolations () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedRequest(
                ("step-1", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneQuery));
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(
                normalizedRequest,
                new[]
                {
                    OperationPhaseTrace.Variants.CallSuccessWithoutVerdict(
                        new IpcExecuteStepId("step-1"),
                        MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneQuery,
                        outcome: CreateNonJudgingSuccessOutcome(
                            applied: true,
                            changed: true,
                            touched: new[]
                            {
                                new OperationTouch(UcliTouchedResourceKind.Asset, "Assets/Example.txt", null),
                            },
                            readInvalidations: System.Array.Empty<OperationReadInvalidation>(),
                            diagnostics: System.Array.Empty<OperationDiagnostic>(),
                            persisted: false),
                        contracts: CreateContractFacts(
                            UcliPrimitiveOperationNames.SceneQuery,
                            UcliOperationKind.Query,
                            mayDirty: false)),
                }));
            var dispatcher = CreateDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext(Guid.NewGuid(), ProjectIdentity);
            var request = CreateExecuteRequest(
                UcliCommandIds.Query.Name,
                operationId: "step-1",
                operationName: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneQuery);

            var response = await DispatchAsync(dispatcher, request, context, "query contract violation response");

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(ExecuteRequestErrorCodes.OperationContractViolation));
            var violations = response.Payload.GetProperty("contractViolations");
            Assert.That(violations.GetArrayLength(), Is.EqualTo(3));
            var observedResults = new List<string>();
            foreach (var violation in violations.EnumerateArray())
            {
                Assert.That(violation.GetProperty("expectedFact").GetString(), Is.EqualTo("operation.kind=query"));
                Assert.That(violation.GetProperty("applicationState").GetString(), Is.EqualTo(Vocabulary.GetText(IpcApplicationState.Applied)));
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
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(
                normalizedRequest,
                new[]
                {
                    OperationPhaseTrace.Variants.CallSuccessWithoutVerdict(
                        new IpcExecuteStepId("step-1"),
                        MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh,
                        outcome: CreateNonJudgingSuccessOutcome(
                            applied: true,
                            changed: false,
                            touched: new[]
                            {
                                new OperationTouch(UcliTouchedResourceKind.Asset, "Assets/Example.txt", null),
                            },
                            readInvalidations: System.Array.Empty<OperationReadInvalidation>(),
                            diagnostics: System.Array.Empty<OperationDiagnostic>(),
                            persisted: false),
                        contracts: CreateContractFacts(
                            UcliPrimitiveOperationNames.ProjectRefresh,
                            UcliOperationKind.Mutation,
                            mayDirty: true,
                            UcliTouchedResourceKind.Scene)),
                }));
            var dispatcher = CreateDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext(Guid.NewGuid(), ProjectIdentity);
            var request = CreateExecuteRequest(
                UcliCommandIds.Call.Name,
                operationId: "step-1",
                operationName: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh);

            var response = await DispatchAsync(dispatcher, request, context, "touched kind contract violation response");

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            var violation = GetSingleArrayElement(response.Payload.GetProperty("contractViolations"));
            Assert.That(violation.GetProperty("expectedFact").GetString(), Is.EqualTo("assurance.touchedKinds=[scene]"));
            Assert.That(violation.GetProperty("observedResult").GetString(), Is.EqualTo("opResults[].touched[].kind=asset"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenTouchedKindViolationHasNoApplyOrChange_ReportsNotAppliedApplicationState () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedRequest(
                ("step-1", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh));
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(
                normalizedRequest,
                new[]
                {
                    OperationPhaseTrace.Variants.PlanSuccess(
                        new IpcExecuteStepId("step-1"),
                        MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh,
                        outcome: CreateNonJudgingSuccessOutcome(
                            applied: false,
                            changed: false,
                            touched: new[]
                            {
                                new OperationTouch(UcliTouchedResourceKind.Asset, "Assets/Example.txt", null),
                            },
                            readInvalidations: System.Array.Empty<OperationReadInvalidation>(),
                            diagnostics: System.Array.Empty<OperationDiagnostic>(),
                            persisted: false),
                        contracts: CreateContractFacts(
                            UcliPrimitiveOperationNames.ProjectRefresh,
                            UcliOperationKind.Mutation,
                            mayDirty: true,
                            UcliTouchedResourceKind.Scene)),
                }));
            var dispatcher = CreateDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext(Guid.NewGuid(), ProjectIdentity);
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan.Name,
                operationId: "step-1",
                operationName: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh);

            var response = await DispatchAsync(dispatcher, request, context, "not-applied contract violation response");

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            var violation = GetSingleArrayElement(response.Payload.GetProperty("contractViolations"));
            Assert.That(violation.GetProperty("applicationState").GetString(), Is.EqualTo(Vocabulary.GetText(IpcApplicationState.NotApplied)));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenAllowedMutationReportsTouchedKind_DoesNotReportContractViolation () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedRequest(
                ("step-1", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh));
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(
                normalizedRequest,
                new[]
                {
                    OperationPhaseTrace.Variants.CallSuccessWithoutVerdict(
                        new IpcExecuteStepId("step-1"),
                        MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh,
                        outcome: CreateNonJudgingSuccessOutcome(
                            applied: true,
                            changed: true,
                            touched: new[]
                            {
                                new OperationTouch(UcliTouchedResourceKind.Asset, "Assets/Example.txt", null),
                            },
                            readInvalidations: System.Array.Empty<OperationReadInvalidation>(),
                            diagnostics: System.Array.Empty<OperationDiagnostic>(),
                            persisted: false),
                        contracts: CreateContractFacts(
                            UcliPrimitiveOperationNames.ProjectRefresh,
                            UcliOperationKind.Mutation,
                            mayDirty: true,
                            UcliTouchedResourceKind.Asset)),
                }));
            var dispatcher = CreateDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext(Guid.NewGuid(), ProjectIdentity);
            var request = CreateExecuteRequest(
                UcliCommandIds.Call.Name,
                operationId: "step-1",
                operationName: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh);

            var response = await DispatchAsync(dispatcher, request, context, "allowed mutation response");

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
            Assert.That(response.Payload.TryGetProperty("contractViolations", out _), Is.False);
            Assert.That(response.Errors.Count, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenRuntimeStateMutationReportsChangedWithoutTouchedResources_DoesNotReportContractViolation () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedRequest(
                ("step-1", "game.cheat.runtime-state"));
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(
                normalizedRequest,
                new[]
                {
                    OperationPhaseTrace.Variants.CallSuccessWithoutVerdict(
                        new IpcExecuteStepId("step-1"),
                        "game.cheat.runtime-state",
                        outcome: CreateNonJudgingSuccessOutcome(
                            applied: true,
                            changed: true,
                            touched: System.Array.Empty<OperationTouch>(),
                            readInvalidations: System.Array.Empty<OperationReadInvalidation>(),
                            diagnostics: System.Array.Empty<OperationDiagnostic>(),
                            persisted: false),
                        contracts: CreateContractFacts(
                            UcliPrimitiveOperationNames.ProjectRefresh,
                            UcliOperationKind.Mutation,
                            mayDirty: true)),
                }));
            var dispatcher = CreateDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext(Guid.NewGuid(), ProjectIdentity);
            var request = CreateExecuteRequest(
                UcliCommandIds.Call.Name,
                operationId: "step-1",
                operationName: "game.cheat.runtime-state");

            var response = await DispatchAsync(dispatcher, request, context, "runtime-state mutation response");

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
            Assert.That(response.Payload.TryGetProperty("contractViolations", out _), Is.False);
            Assert.That(response.Errors.Count, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenChangedTrueAndMayPersistTrueWithMayDirtyFalse_DoesNotReportContractViolation () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedRequest(
                ("step-1", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneSave));
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(
                normalizedRequest,
                new[]
                {
                    OperationPhaseTrace.Variants.CallSuccessWithoutVerdict(
                        new IpcExecuteStepId("step-1"),
                        MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneSave,
                        outcome: CreateNonJudgingSuccessOutcome(
                            applied: true,
                            changed: true,
                            touched: new[]
                            {
                                new OperationTouch(UcliTouchedResourceKind.Scene, "Assets/Scenes/Main.unity", null),
                            },
                            readInvalidations: System.Array.Empty<OperationReadInvalidation>(),
                            diagnostics: System.Array.Empty<OperationDiagnostic>(),
                            persisted: true),
                        contracts: CreateContractFacts(
                            UcliPrimitiveOperationNames.ProjectRefresh,
                            UcliOperationKind.Mutation,
                            mayDirty: false,
                            mayPersist: true,
                            UcliTouchedResourceKind.Scene)),
                }));
            var dispatcher = CreateDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext(Guid.NewGuid(), ProjectIdentity);
            var request = CreateExecuteRequest(
                UcliCommandIds.Call.Name,
                operationId: "step-1",
                operationName: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneSave);

            var response = await DispatchAsync(dispatcher, request, context, "allowed save persistence response");

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
            Assert.That(response.Payload.TryGetProperty("contractViolations", out _), Is.False);
            Assert.That(response.Errors.Count, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenPersistedTrueAndMayPersistTrue_DoesNotReportContractViolation () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedRequest(
                ("step-1", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh));
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(
                normalizedRequest,
                new[]
                {
                    OperationPhaseTrace.Variants.CallSuccessWithoutVerdict(
                        new IpcExecuteStepId("step-1"),
                        MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh,
                        outcome: CreateNonJudgingSuccessOutcome(
                            applied: true,
                            changed: false,
                            touched: System.Array.Empty<OperationTouch>(),
                            readInvalidations: System.Array.Empty<OperationReadInvalidation>(),
                            diagnostics: System.Array.Empty<OperationDiagnostic>(),
                            persisted: true),
                        contracts: CreateContractFacts(
                            UcliPrimitiveOperationNames.ProjectRefresh,
                            UcliOperationKind.Mutation,
                            mayDirty: true,
                            mayPersist: true)),
                }));
            var dispatcher = CreateDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext(Guid.NewGuid(), ProjectIdentity);
            var request = CreateExecuteRequest(
                UcliCommandIds.Call.Name,
                operationId: "step-1",
                operationName: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh);

            var response = await DispatchAsync(dispatcher, request, context, "allowed persistence response");

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
            Assert.That(response.Payload.TryGetProperty("contractViolations", out _), Is.False);
            Assert.That(response.Errors.Count, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenPublicStepContainsDiagnostics_MapsDiagnosticsToPayload () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedEditRequest(stepId: "op-1");
            var traceSteps = CreateTraceSteps(normalizedRequest, editPrimitiveCount: 1);
            traceSteps[0] = traceSteps[0] with
            {
                Diagnostics = new[]
                {
                    new OperationDiagnostic(
                        Code: ExecuteRequestErrorCodes.HierarchyPathUnrepresentableObjects,
                        Severity: UcliDiagnosticSeverity.Warning,
                        CoverageImpact: IpcExecuteDiagnosticCoverageImpact.Partial,
                        Message: "Scene edit selection skipped GameObjects whose names contain '/'."),
                },
            };
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(PhaseExecutionTrace.SucceededWithoutPlanToken(
                steps: traceSteps,
                operationTraces: new[]
                {
                    OperationPhaseTrace.Variants.CallSuccessWithoutVerdict(
                        new IpcExecuteStepId("op-1#0"),
                        MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDelete,
                        outcome: CreateNonJudgingSuccessOutcome(
                            applied: true,
                            changed: true,
                            touched: System.Array.Empty<OperationTouch>(),
                            readInvalidations: System.Array.Empty<OperationReadInvalidation>(),
                            diagnostics: System.Array.Empty<OperationDiagnostic>(),
                            persisted: false),
                        contracts: CreateContractFacts(
                            UcliPrimitiveOperationNames.GoDelete,
                            UcliOperationKind.Mutation,
                            mayDirty: true)),
                }));
            var dispatcher = CreateDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext(Guid.NewGuid(), ProjectIdentity);
            var request = CreateExecuteRequest(UcliCommandIds.Call.Name, operationName: "edit");

            var response = await DispatchAsync(dispatcher, request, context, "Public step diagnostics payload mapping");

            Assert.That(
                response.Status,
                Is.EqualTo(IpcResponseStatus.Ok),
                response.Errors.Count == 0 ? null : response.Errors[0].Message);
            var opResult = GetSingleArrayElement(response.Payload.GetProperty("opResults"));
            Assert.That(opResult.GetProperty("op").GetString(), Is.EqualTo("edit"));
            var diagnostic = GetSingleArrayElement(opResult.GetProperty("diagnostics"));
            Assert.That(diagnostic.GetProperty("code").GetString(), Is.EqualTo(ExecuteRequestErrorCodes.HierarchyPathUnrepresentableObjects.Value));
            Assert.That(diagnostic.GetProperty("severity").GetString(), Is.EqualTo(Vocabulary.GetText(UcliDiagnosticSeverity.Warning)));
            Assert.That(diagnostic.GetProperty("coverageImpact").GetString(), Is.EqualTo(Vocabulary.GetText(IpcExecuteDiagnosticCoverageImpact.Partial)));
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
            var dispatcher = CreateDispatcher(
                CreateCatalogNormalizer(),
                CreateCatalogPhaseExecutor());
            var context = new ExecuteDispatchContext(Guid.NewGuid(), ProjectIdentity);
            var request = CreateExecuteRequest(
                UcliCommandIds.Plan.Name,
                new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = new object[]
                    {
                        new
                        {
                            kind = "edit",
                            on = new
                            {
                                kind = "scene",
                                path = scenePath,
                            },
                            select = new
                            {
                                kind = "gameObject",
                                path = "GoodRoot",
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
                            on = new
                            {
                                kind = "scene",
                                path = scenePath,
                            },
                            select = new
                            {
                                kind = "from",
                                op = UcliPrimitiveOperationNames.SceneQuery,
                                args = new
                                {
                                    pathPrefix = "Missing",
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

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(response.Errors[0].InstancePath, Is.EqualTo("/steps/1"));
            var opResults = response.Payload.GetProperty("opResults");
            Assert.That(opResults.GetArrayLength(), Is.EqualTo(2));
            var cleanResult = opResults[0];
            Assert.That(cleanResult.GetProperty("diagnostics").GetArrayLength(), Is.EqualTo(0));
            var failedResult = opResults[1];
            Assert.That(failedResult.GetProperty("op").GetString(), Is.EqualTo("edit"));
            var diagnostic = GetSingleArrayElement(failedResult.GetProperty("diagnostics"));
            Assert.That(diagnostic.GetProperty("code").GetString(), Is.EqualTo(ExecuteRequestErrorCodes.HierarchyPathUnrepresentableObjects.Value));
            Assert.That(diagnostic.GetProperty("severity").GetString(), Is.EqualTo(Vocabulary.GetText(UcliDiagnosticSeverity.Warning)));
            Assert.That(diagnostic.GetProperty("coverageImpact").GetString(), Is.EqualTo(Vocabulary.GetText(IpcExecuteDiagnosticCoverageImpact.Partial)));
            Assert.That(diagnostic.GetProperty("message").GetString(), Does.Contain("hierarchyPath cannot represent"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenOperationTraceContainsReadInvalidations_MapsReadPostconditionToPayload () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedRequest(
                ("op-1", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh));
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var operationTrace = OperationPhaseTrace.Variants.CallSuccessWithoutVerdict(
                opId: new IpcExecuteStepId("op-1"),
                op: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh,
                outcome: CreateNonJudgingSuccessOutcome(
                    applied: true,
                    changed: true,
                    touched: System.Array.Empty<OperationTouch>(),
                    readInvalidations: new[]
                    {
                        new OperationReadInvalidation(OperationReadInvalidationSurface.AssetSearch, ScenePath: null),
                        new OperationReadInvalidation(OperationReadInvalidationSurface.SceneTreeLite, @"Assets\Scenes\Main.unity"),
                    },
                    diagnostics: System.Array.Empty<OperationDiagnostic>(),
                    persisted: false),
                contracts: CreateContractFacts(
                    UcliPrimitiveOperationNames.ProjectRefresh,
                    UcliOperationKind.Command,
                    mayDirty: true));
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(
                normalizedRequest,
                new[]
                {
                    operationTrace,
                }));
            var dispatcher = CreateDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext(Guid.NewGuid(), ProjectIdentity);
            var request = CreateExecuteRequest(
                UcliCommandIds.Call.Name,
                operationName: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh);

            var response = await DispatchAsync(dispatcher, request, context, "Read postcondition payload mapping");

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
            Assert.That(response.Payload.TryGetProperty("readPostcondition", out var readPostcondition), Is.True);
            var requirements = readPostcondition.GetProperty("requirements");
            Assert.That(requirements.GetArrayLength(), Is.EqualTo(2));

            var enumerator = requirements.EnumerateArray();
            Assert.That(enumerator.MoveNext(), Is.True);
            var assetSearchRequirement = enumerator.Current;
            Assert.That(assetSearchRequirement.GetProperty("surface").GetString(), Is.EqualTo(Vocabulary.GetText(IpcExecuteReadPostconditionSurface.AssetSearch)));
            Assert.That(assetSearchRequirement.TryGetProperty("scenePath", out _), Is.False);

            Assert.That(enumerator.MoveNext(), Is.True);
            var sceneTreeRequirement = enumerator.Current;
            Assert.That(sceneTreeRequirement.GetProperty("surface").GetString(), Is.EqualTo(Vocabulary.GetText(IpcExecuteReadPostconditionSurface.SceneTreeLite)));
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
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(
                normalizedRequest,
                new[]
                {
                    OperationPhaseTrace.Variants.CallSuccessWithoutVerdict(
                        opId: new IpcExecuteStepId("op-1"),
                        op: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh,
                        outcome: CreateNonJudgingSuccessOutcome(
                            applied: true,
                            changed: true,
                            touched: System.Array.Empty<OperationTouch>(),
                            readInvalidations: new[]
                            {
                                new OperationReadInvalidation(OperationReadInvalidationSurface.AssetSearch, ScenePath: null),
                                new OperationReadInvalidation(OperationReadInvalidationSurface.SceneTreeLite, "Assets/Scenes/Main.unity"),
                            },
                            diagnostics: System.Array.Empty<OperationDiagnostic>(),
                            persisted: false),
                        contracts: CreateContractFacts(
                            UcliPrimitiveOperationNames.ProjectRefresh,
                            UcliOperationKind.Command,
                            mayDirty: true)),
                    OperationPhaseTrace.Variants.CallSuccessWithoutVerdict(
                        opId: new IpcExecuteStepId("op-2"),
                        op: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh,
                        outcome: CreateNonJudgingSuccessOutcome(
                            applied: true,
                            changed: true,
                            touched: System.Array.Empty<OperationTouch>(),
                            readInvalidations: new[]
                            {
                                new OperationReadInvalidation(OperationReadInvalidationSurface.AssetSearch, ScenePath: null),
                                new OperationReadInvalidation(OperationReadInvalidationSurface.SceneTreeLite, @"Assets\Scenes\Main.unity"),
                            },
                            diagnostics: System.Array.Empty<OperationDiagnostic>(),
                            persisted: false),
                        contracts: CreateContractFacts(
                            UcliPrimitiveOperationNames.ProjectRefresh,
                            UcliOperationKind.Command,
                            mayDirty: true)),
                }));
            var dispatcher = CreateDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext(Guid.NewGuid(), ProjectIdentity);
            var request = CreateExecuteRequest(
                UcliCommandIds.Call.Name,
                failFast: false,
                planToken: null,
                ("op-1", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh),
                ("op-2", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh));

            var response = await DispatchAsync(dispatcher, request, context, "Deduplicated read postcondition payload mapping");

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
            var requirements = response.Payload.GetProperty("readPostcondition").GetProperty("requirements");
            Assert.That(requirements.GetArrayLength(), Is.EqualTo(2));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenSameRequestIdAndSamePayload_ReusesCompletedResponse () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedRequest();
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(
                normalizedRequest,
                new[]
                {
                    OperationPhaseTrace.Variants.PlanSuccess(
                        new IpcExecuteStepId("op-1"),
                        MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.Resolve,
                        outcome: CreateNonJudgingSuccessOutcome(
                            applied: false,
                            changed: false,
                            touched: System.Array.Empty<OperationTouch>(),
                            readInvalidations: System.Array.Empty<OperationReadInvalidation>(),
                            diagnostics: System.Array.Empty<OperationDiagnostic>(),
                            persisted: false),
                        contracts: CreateContractFacts(
                            UcliPrimitiveOperationNames.Resolve,
                            UcliOperationKind.Query,
                            mayDirty: false)),
                }));
            var dispatcher = CreateDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext(Guid.NewGuid(), ProjectIdentity);
            var request = CreateExecuteRequest(UcliCommandIds.Plan.Name);

            var firstResponse = await DispatchAsync(dispatcher, request, context, "Idempotent first response");
            var secondResponse = await DispatchAsync(dispatcher, request, context, "Idempotent second response");

            Assert.That(firstResponse.Status, Is.EqualTo(IpcResponseStatus.Ok));
            Assert.That(secondResponse.Status, Is.EqualTo(IpcResponseStatus.Ok));
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
            var dispatcher = CreateDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext(Guid.NewGuid(), ProjectIdentity);
            var firstRequest = CreateExecuteRequest(
                commandName: UcliCommandIds.Plan.Name,
                operationId: "op-1",
                operationName: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.Resolve);
            var secondRequest = CreateExecuteRequest(
                commandName: UcliCommandIds.Plan.Name,
                operationId: "op-2",
                operationName: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneOpen);

            _ = await DispatchAsync(dispatcher, firstRequest, context, "Initial conflicting request completion");
            var secondResponse = await DispatchAsync(dispatcher, secondRequest, context, "Conflicting payload response");

            Assert.That(secondResponse.Status, Is.EqualTo(IpcResponseStatus.Error));
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
            var dispatcher = CreateDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext(Guid.NewGuid(), ProjectIdentity);
            var firstRequest = CreateExecuteRequest(
                commandName: UcliCommandIds.Call.Name,
                operationId: "op-1",
                operationName: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.Resolve,
                planToken: "token-1");
            var secondRequest = CreateExecuteRequest(
                commandName: UcliCommandIds.Call.Name,
                operationId: "op-1",
                operationName: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.Resolve,
                planToken: "token-2");

            _ = await DispatchAsync(dispatcher, firstRequest, context, "Initial plan token request completion");
            var secondResponse = await DispatchAsync(dispatcher, secondRequest, context, "Conflicting plan token response");

            Assert.That(secondResponse.Status, Is.EqualTo(IpcResponseStatus.Error));
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
            var dispatcher = CreateDispatcher(normalizer, phaseExecutor, readinessGate);
            var context = new ExecuteDispatchContext(Guid.NewGuid(), ProjectIdentity);
            var request = CreateExecuteRequest(UcliCommandIds.Plan.Name, failFast: false);

            var responseTask = dispatcher.DispatchAsync(request, context).AsUniTask();
            await TestAwaiter.WaitAsync(readinessGate.WaitObserved, "Execute dispatcher readiness wait", AsyncWaitTimeout);

            Assert.That(readinessGate.CallCount, Is.EqualTo(1));
            Assert.That(readinessGate.LastFailFast, Is.False);
            Assert.That(readinessGate.LastAllowPlayMode, Is.False);
            Assert.That(phaseExecutor.CallCount, Is.EqualTo(0));

            readinessGate.Release();
            var response = await TestAwaiter.WaitAsync(responseTask, "Execute dispatcher readiness-delayed response", AsyncWaitTimeout);

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
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
            var dispatcher = CreateDispatcher(normalizer, phaseExecutor, readinessGate);
            var context = new ExecuteDispatchContext(Guid.NewGuid(), ProjectIdentity);
            var request = CreateExecuteRequest(UcliCommandIds.Call.Name, failFast: true);

            var response = await DispatchAsync(dispatcher, request, context, "Execute dispatcher lifecycle fail-fast response");

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(EditorLifecycleErrorCodes.EditorBusy));
            Assert.That(readinessGate.CallCount, Is.EqualTo(1));
            Assert.That(readinessGate.LastFailFast, Is.True);
            Assert.That(readinessGate.LastAllowPlayMode, Is.False);
            AssertEmptyOpResultsPayload(response.Payload);
            Assert.That(phaseExecutor.CallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenAllowPlayModeIsSetOnPlan_PassesAllowPlayModeToReadinessGate () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedRequest();
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(normalizedRequest));
            var readinessGate = new StubUnityEditorReadinessGate(DaemonEditorMode.Gui);
            var dispatcher = CreateDispatcher(normalizer, phaseExecutor, readinessGate);
            var context = new ExecuteDispatchContext(Guid.NewGuid(), ProjectIdentity);
            var request = CreateExecuteRequest(UcliCommandIds.Plan.Name) with
            {
                AllowPlayMode = true,
            };

            var response = await DispatchAsync(dispatcher, request, context, "allowPlayMode readiness propagation response");

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
            Assert.That(readinessGate.CallCount, Is.EqualTo(1));
            Assert.That(readinessGate.LastAllowPlayMode, Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenAllowPlayModeIsSetOnCall_PassesAllowPlayModeToReadinessGate () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedRequest();
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(normalizedRequest));
            var readinessGate = new StubUnityEditorReadinessGate(DaemonEditorMode.Gui);
            var dispatcher = CreateDispatcher(normalizer, phaseExecutor, readinessGate);
            var context = new ExecuteDispatchContext(Guid.NewGuid(), ProjectIdentity);
            var request = CreateExecuteRequest(UcliCommandIds.Call.Name) with
            {
                AllowPlayMode = true,
            };

            var response = await DispatchAsync(dispatcher, request, context, "allowPlayMode call readiness propagation response");

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
            Assert.That(readinessGate.CallCount, Is.EqualTo(1));
            Assert.That(readinessGate.LastAllowPlayMode, Is.True);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenCommandIsRefresh_ReturnsCommandNotImplementedError () => UniTask.ToCoroutine(async () =>
        {
            await AssertReturnsCommandNotImplementedErrorAsync(UcliCommandIds.Refresh.Name);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenNormalizationFails_ReturnsNormalizationError () => UniTask.ToCoroutine(async () =>
        {
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Failure(
                new ExecuteRequestNormalizationError(UcliCoreErrorCodes.InvalidArgument, "invalid request", "/steps/0")));
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(CreateNormalizedRequest()));
            var dispatcher = CreateDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext(Guid.NewGuid(), ProjectIdentity);
            var request = CreateExecuteRequest(UcliCommandIds.Plan.Name);

            var response = await DispatchAsync(dispatcher, request, context, "Normalization failure response");

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(response.Errors[0].InstancePath, Is.EqualTo("/steps/0"));
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
            var dispatcher = CreateDispatcher(normalizer, phaseExecutor, readinessGate);
            var context = new ExecuteDispatchContext(Guid.NewGuid(), ProjectIdentity);
            var request = CreateExecuteRequest(UcliCommandIds.Refresh.Name);

            var response = await DispatchAsync(dispatcher, request, context, "Command not implemented without readiness wait");

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.CommandNotImplemented));
            Assert.That(readinessGate.CallCount, Is.EqualTo(0));
            Assert.That(normalizer.CallCount, Is.EqualTo(0));
            Assert.That(phaseExecutor.CallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenAllowPlayModeIsSetOnQuery_ReturnsInvalidArgumentWithoutReadiness () => UniTask.ToCoroutine(async () =>
        {
            var normalizer = new SpyExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Failure(
                new ExecuteRequestNormalizationError(UcliCoreErrorCodes.InvalidArgument, "normalizer should not run", null)));
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(CreateNormalizedRequest()));
            var readinessGate = StubUnityEditorReadinessGate.CreatePending();
            var dispatcher = CreateDispatcher(normalizer, phaseExecutor, readinessGate);
            var context = new ExecuteDispatchContext(Guid.NewGuid(), ProjectIdentity);
            var request = CreateExecuteRequest(UcliCommandIds.Query.Name) with
            {
                AllowPlayMode = true,
            };

            var response = await DispatchAsync(dispatcher, request, context, "allowPlayMode query rejection");

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(response.Errors[0].Message, Does.Contain("allowPlayMode"));
            Assert.That(readinessGate.CallCount, Is.EqualTo(0));
            Assert.That(normalizer.CallCount, Is.EqualTo(0));
            Assert.That(phaseExecutor.CallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenNormalizationFails_DoesNotWaitForReadiness () => UniTask.ToCoroutine(async () =>
        {
            var normalizer = new SpyExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Failure(
                new ExecuteRequestNormalizationError(UcliCoreErrorCodes.InvalidArgument, "invalid request", "/steps/0")));
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(CreateNormalizedRequest()));
            var readinessGate = StubUnityEditorReadinessGate.CreatePending();
            var dispatcher = CreateDispatcher(normalizer, phaseExecutor, readinessGate);
            var context = new ExecuteDispatchContext(Guid.NewGuid(), ProjectIdentity);
            var request = CreateExecuteRequest(UcliCommandIds.Plan.Name);

            var response = await DispatchAsync(dispatcher, request, context, "Normalization failure without readiness wait");

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
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
            var dispatcher = CreateDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext(Guid.NewGuid(), ProjectIdentity);
            var request = new IpcExecuteRequest(UcliCommandIds.Plan.Name, default);

            var response = await DispatchAsync(dispatcher, request, context, "Invalid arguments response");

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
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
                ("op-1", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh),
                ("op-2", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneOpen));
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var operationTraces = new[]
            {
                OperationPhaseTrace.Variants.CallFailure(
                    opId: new IpcExecuteStepId("op-1"),
                    op: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh,
                    outcome: CreateFailureOutcome(
                        applied: true,
                        changed: true,
                        touched: new[]
                        {
                            new OperationTouch(
                                UcliTouchedResourceKind.Scene,
                                "Assets/Scenes/Main.unity",
                                Guid.ParseExact("11111111111111111111111111111111", "N")),
                        },
                        failure: new OperationFailure(UcliCoreErrorCodes.InvalidArgument, "call failed", new IpcExecuteStepId("op-1")),
                        result: null,
                        readInvalidations: Array.Empty<OperationReadInvalidation>(),
                        diagnostics: Array.Empty<OperationDiagnostic>(),
                        persisted: false),
                    contracts: CreateContractFacts(
                        UcliPrimitiveOperationNames.ProjectRefresh,
                        UcliOperationKind.Mutation,
                        mayDirty: true,
                        UcliTouchedResourceKind.Scene)),
            };
            var traceSteps = CreateTraceSteps(normalizedRequest, operationTraces, editPrimitiveCount: 0);
            traceSteps[1] = traceSteps[1] with
            {
                PrimitiveCount = 0,
            };
            var phaseExecutor = new SpyOperationPhaseExecutor(PhaseExecutionTrace.Failed(
                steps: traceSteps,
                operationTraces: operationTraces,
                errors: new[]
                {
                    new OperationFailure(UcliCoreErrorCodes.InvalidArgument, "call failed", new IpcExecuteStepId("op-1")),
                }));
            var dispatcher = CreateDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext(Guid.NewGuid(), ProjectIdentity);
            var request = CreateExecuteRequest(
                UcliCommandIds.Call.Name,
                false,
                null,
                ("op-1", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh),
                ("op-2", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneOpen));

            var response = await DispatchAsync(dispatcher, request, context, "Phase execution failure response");

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(response.Errors[0].InstancePath, Is.EqualTo("/steps/0"));
            Assert.That(response.Payload.TryGetProperty("opResults", out var opResults), Is.True);
            Assert.That(opResults.GetArrayLength(), Is.EqualTo(2));
            Assert.That(
                opResults[1].GetProperty("operationDescriptorDigest").GetString(),
                Is.EqualTo(OperationDescriptorDigest.ToString()));
            Assert.That(
                opResults[1].GetProperty("phase").GetString(),
                Is.EqualTo(Vocabulary.GetText(IpcExecuteOperationPhase.Skipped)));
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
                    OperationPhaseTrace.Variants.CallFailure(
                        opId: new IpcExecuteStepId("edit-1#0"),
                        op: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.CompSet,
                        outcome: CreateFailureOutcome(
                            applied: true,
                            changed: true,
                            touched: Array.Empty<OperationTouch>(),
                            failure: new OperationFailure(UcliCoreErrorCodes.InvalidArgument, "edit failed", new IpcExecuteStepId("edit-1")),
                            result: null,
                            readInvalidations: Array.Empty<OperationReadInvalidation>(),
                            diagnostics: Array.Empty<OperationDiagnostic>(),
                            persisted: false),
                        contracts: CreateContractFacts(
                            UcliPrimitiveOperationNames.CompSet,
                            UcliOperationKind.Mutation,
                            mayDirty: true)),
                    OperationPhaseTrace.Variants.SkippedAgainstContract(
                        new IpcExecuteStepId("edit-1#1"),
                        MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneSave,
                        CreateContractFacts(
                            UcliPrimitiveOperationNames.SceneSave,
                            UcliOperationKind.Mutation,
                            mayDirty: true)),
                },
                errors: new[]
                {
                    new OperationFailure(UcliCoreErrorCodes.InvalidArgument, "edit failed", new IpcExecuteStepId("edit-1")),
                }));
            var dispatcher = CreateDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext(Guid.NewGuid(), ProjectIdentity);
            var request = CreateExecuteRequest(UcliCommandIds.Call.Name, operationId: "edit-1", operationName: "edit");

            var response = await DispatchAsync(dispatcher, request, context, "Aggregated edit failure phase response");

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            var opResult = GetSingleArrayElement(response.Payload.GetProperty("opResults"));
            Assert.That(opResult.GetProperty("op").GetString(), Is.EqualTo("edit"));
            Assert.That(opResult.GetProperty("phase").GetString(), Is.EqualTo(Vocabulary.GetText(IpcExecuteOperationPhase.Call)));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenPlanEditStepCompilesToNoPrimitives_ReturnsPlanPhaseForSuccessfulNoOp () => UniTask.ToCoroutine(async () =>
        {
            await AssertNoPrimitiveEditResponsePhaseAsync(
                UcliCommandIds.Plan.Name,
                IpcExecuteOperationPhase.Plan);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenCallEditStepCompilesToNoPrimitives_ReturnsCallPhaseForSuccessfulNoOp () => UniTask.ToCoroutine(async () =>
        {
            await AssertNoPrimitiveEditResponsePhaseAsync(
                UcliCommandIds.Call.Name,
                IpcExecuteOperationPhase.Call);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenCancellationRequested_ThrowsOperationCanceledException () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedRequest();
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(normalizedRequest));
            var dispatcher = CreateDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext(Guid.NewGuid(), ProjectIdentity);
            var request = CreateExecuteRequest(UcliCommandIds.Plan.Name);
            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await dispatcher.DispatchAsync(request, context, cancellationTokenSource.Token).AsUniTask();
            }, "Canceled execute request dispatch", AsyncWaitTimeout);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Dispatch_WhenOwnerExecutionIsCanceled_AllowsSameRequestToExecuteAgain () => UniTask.ToCoroutine(async () =>
        {
            var normalizedRequest = CreateNormalizedRequest();
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new CancellableThenSuccessfulOperationPhaseExecutor(CreateSuccessTrace(normalizedRequest));
            var dispatcher = CreateDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext(Guid.NewGuid(), ProjectIdentity);
            var request = CreateExecuteRequest(UcliCommandIds.Plan.Name);
            using var cancellationTokenSource = new CancellationTokenSource();

            var firstDispatchTask = dispatcher.DispatchAsync(request, context, cancellationTokenSource.Token);
            await TestAwaiter.WaitAsync(
                phaseExecutor.FirstExecutionStarted,
                "first execute dispatch phase execution start",
                AsyncWaitTimeout);

            var waiterDispatchTask = dispatcher.DispatchAsync(request, context, CancellationToken.None);
            Assert.That(waiterDispatchTask.IsCompleted, Is.False);

            cancellationTokenSource.Cancel();
            await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await TestAwaiter.WaitAsync(firstDispatchTask, "first execute dispatch cancellation", AsyncWaitTimeout);
            }, "canceled owner execute dispatch", AsyncWaitTimeout);
            await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await TestAwaiter.WaitAsync(waiterDispatchTask, "waiter execute dispatch cancellation", AsyncWaitTimeout);
            }, "canceled waiter execute dispatch", AsyncWaitTimeout);

            var secondResponse = await DispatchAsync(
                dispatcher,
                request,
                context,
                "same execute request after owner cancellation");

            Assert.That(secondResponse.Status, Is.EqualTo(IpcResponseStatus.Ok));
            Assert.That(phaseExecutor.CallCount, Is.EqualTo(2));
        });

        private static async UniTask AssertNoPrimitiveEditResponsePhaseAsync (
            string commandName,
            IpcExecuteOperationPhase expectedPhase)
        {
            var normalizedRequest = CreateNormalizedEditRequest(stepId: "edit-1");
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(PhaseExecutionTrace.SucceededWithoutPlanToken(
                steps: CreateTraceSteps(normalizedRequest, editPrimitiveCount: 0),
                operationTraces: Array.Empty<OperationPhaseTrace>()));
            var dispatcher = CreateDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext(Guid.NewGuid(), ProjectIdentity);
            var request = CreateExecuteRequest(commandName, operationId: "edit-1", operationName: "edit");

            var response = await DispatchAsync(dispatcher, request, context, "Successful no-op edit response");

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
            var opResult = GetSingleArrayElement(response.Payload.GetProperty("opResults"));
            Assert.That(opResult.GetProperty("op").GetString(), Is.EqualTo("edit"));
            Assert.That(opResult.GetProperty("phase").GetString(), Is.EqualTo(Vocabulary.GetText(expectedPhase)));
            Assert.That(opResult.GetProperty("applied").GetBoolean(), Is.False);
            Assert.That(opResult.GetProperty("changed").GetBoolean(), Is.False);
            Assert.That(opResult.GetProperty("touched").GetArrayLength(), Is.EqualTo(0));
            var sourceStep = GetSingleArrayElement(response.Payload.GetProperty("postReadSource").GetProperty("steps"));
            Assert.That(sourceStep.GetProperty("sourceKind").GetString(), Is.EqualTo(Vocabulary.GetText(IpcExecutePostReadSourceKind.Edit)));
            Assert.That(sourceStep.GetProperty("commit").GetString(), Is.EqualTo(Vocabulary.GetText(IpcExecutePostReadCommit.None)));
            Assert.That(sourceStep.GetProperty("persistenceExpected").GetBoolean(), Is.False);
            Assert.That(sourceStep.GetProperty("expectedPostState").GetString(), Is.EqualTo(Vocabulary.GetText(IpcExecuteExpectedPostState.Deterministic)));
        }

        private static async UniTask AssertDelegatesToPhaseExecutorAsync (
            string commandName,
            PhaseExecutionCommand expectedCommand,
            string operationName)
        {
            var normalizedRequest = CreateNormalizedRequest(operationName);
            var normalizer = new StubExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Success(normalizedRequest));
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(
                normalizedRequest,
                new[]
                {
                    OperationPhaseTrace.Variants.PlanSuccess(
                        opId: new IpcExecuteStepId("op-1"),
                        op: operationName,
                        outcome: CreateNonJudgingSuccessOutcome(
                            applied: false,
                            changed: false,
                            touched: Array.Empty<OperationTouch>(),
                            readInvalidations: Array.Empty<OperationReadInvalidation>(),
                            diagnostics: Array.Empty<OperationDiagnostic>(),
                            persisted: false),
                        contracts: CreateContractFacts(
                            operationName,
                            UcliOperationKind.Query,
                            mayDirty: false)),
                }));
            var dispatcher = CreateDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext(Guid.NewGuid(), ProjectIdentity);
            var request = CreateExecuteRequest(commandName, operationName: operationName);

            var response = await DispatchAsync(dispatcher, request, context, "Phase executor delegation response");

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
            Assert.That(response.Errors.Count, Is.EqualTo(0));
            Assert.That(phaseExecutor.ReceivedCommand, Is.EqualTo(expectedCommand));
            Assert.That(phaseExecutor.CallCount, Is.EqualTo(1));
            Assert.That(response.Payload.TryGetProperty("opResults", out var opResults), Is.True);
            Assert.That(opResults.GetArrayLength(), Is.EqualTo(1));
            Assert.That(response.Payload.TryGetProperty("operationTraces", out _), Is.False);

            var opResult = GetSingleArrayElement(opResults);
            Assert.That(opResult.GetProperty("op").GetString(), Is.EqualTo(operationName));
            Assert.That(opResult.GetProperty("phase").GetString(), Is.EqualTo(Vocabulary.GetText(IpcExecuteOperationPhase.Plan)));
            Assert.That(opResult.GetProperty("applied").GetBoolean(), Is.False);
            Assert.That(opResult.GetProperty("changed").GetBoolean(), Is.False);
            Assert.That(opResult.GetProperty("touched").GetArrayLength(), Is.EqualTo(0));
        }

        private static async UniTask AssertReturnsCommandNotImplementedErrorAsync (string commandName)
        {
            var normalizer = new SpyExecuteRequestNormalizer(ExecuteRequestNormalizationResult.Failure(
                new ExecuteRequestNormalizationError(UcliCoreErrorCodes.InvalidArgument, "normalizer should not run", null)));
            var phaseExecutor = new SpyOperationPhaseExecutor(CreateSuccessTrace(CreateNormalizedRequest()));
            var dispatcher = CreateDispatcher(normalizer, phaseExecutor);
            var context = new ExecuteDispatchContext(Guid.NewGuid(), ProjectIdentity);
            var request = CreateExecuteRequest(commandName);

            var response = await DispatchAsync(dispatcher, request, context, "Command not implemented response");

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
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

        private static ExecuteRequestDispatcher CreateDispatcher (
            IExecuteRequestNormalizer requestNormalizer,
            IOperationPhaseExecutor operationPhaseExecutor,
            IUnityEditorReadinessGate? readinessGate = null)
        {
            return new ExecuteRequestDispatcher(
                requestNormalizer,
                operationPhaseExecutor,
                new ExecuteRequestIdempotencyCoordinator(new InMemoryExecuteRequestIdempotencyStore(
                    ExecuteRequestIdempotencyCoordinator.DefaultCacheTtl,
                    ExecuteRequestIdempotencyCoordinator.DefaultMaxEntries,
                    new ManualMonotonicClock())),
                readinessGate ?? new StubUnityEditorReadinessGate());
        }

        private static OperationPhaseExecutor CreateCatalogPhaseExecutor ()
        {
            var snapshot = CreateOperationCatalogSnapshot();
            return CreatePhaseExecutor(new InMemoryPhaseOperationRegistry(snapshot.Registrations));
        }

        private static ExecuteRequestNormalizer CreateCatalogNormalizer ()
        {
            var snapshot = CreateOperationCatalogSnapshot();
            return new ExecuteRequestNormalizer(new InMemoryPhaseOperationRegistry(snapshot.Registrations));
        }

        private static UcliOperationCatalogSnapshot CreateOperationCatalogSnapshot ()
        {
            using var serviceProvider = UcliOperationDiscovererTests.CreateOperationServiceProvider();
            return UcliOperationCatalogSnapshotBuilder.Build(serviceProvider);
        }

        private static OperationPhaseExecutor CreatePhaseExecutor (IPhaseOperationRegistry operationRegistry)
        {
            var environment = new DefaultPlanTokenEnvironment();
            return new OperationPhaseExecutor(
                new OperationPlanPassExecutor(
                    new OperationPlanStepRunner(operationRegistry),
                    new ExecuteRequestCompiler(operationRegistry)),
                new OperationCallPassExecutor(),
                new PlanTokenCoordinator(environment),
                new DangerousOperationCallAuthorizer(environment),
                new ImmediateUnityMutationLaneControl());
        }

        private static NormalizedExecuteRequest CreateNormalizedRequest (string operationName = MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.Resolve)
        {
            return CreateNormalizedRequest(("op-1", operationName));
        }

        private static NormalizedExecuteRequest CreateNormalizedRequest (
            params (string OperationId, string OperationName)[] operations)
        {
            return new NormalizedExecuteRequest(
                SourceSteps: CreateSourceSteps(operations),
                AllowDangerous: false,
                AllowPlayMode: false,
                PlanToken: null,
                CanonicalDigestPayloadUtf8: Encoding.UTF8.GetBytes("{}"));
        }

        private static NormalizedExecuteRequest CreateNormalizedEditRequest (
            string stepId,
            params (string OperationId, string OperationName)[] operations)
        {
            return new NormalizedExecuteRequest(
                SourceSteps: new[]
                {
                    CreateEditSourceStep(stepId),
                },
                AllowDangerous: false,
                AllowPlayMode: false,
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
                    op = operations[i].OperationName,
                    args = new { },
                };
            }

            return steps;
        }

        private static IpcExecuteStepContract[] CreateSourceSteps (
            IReadOnlyList<(string OperationId, string OperationName)> operations)
        {
            var steps = new IpcExecuteStepContract[operations.Count];
            for (var i = 0; i < operations.Count; i++)
            {
                steps[i] = new IpcExecuteStepContract(
                    Kind: IpcExecuteStepKind.Op,
                    Id: new IpcExecuteStepId(operations[i].OperationId),
                    OperationName: operations[i].OperationName,
                    Element: JsonSerializer.SerializeToElement(new
                    {
                        kind = "op",
                        op = operations[i].OperationName,
                        args = new { },
                    }));
            }

            return steps;
        }

        private static IpcExecuteStepContract CreateEditSourceStep (string stepId)
        {
            return new IpcExecuteStepContract(
                Kind: IpcExecuteStepKind.Edit,
                Id: new IpcExecuteStepId(stepId),
                OperationName: null,
                Element: JsonSerializer.SerializeToElement(new
                {
                    kind = "edit",
                    on = new
                    {
                        kind = "scene",
                        path = "Assets/Scenes/Main.unity",
                    },
                    select = new
                    {
                        kind = "gameObject",
                        path = "Root",
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
                var isEditStep = sourceStep.Kind == IpcExecuteStepKind.Edit;
                steps[i] = new NormalizedRequestStep(
                    Id: sourceStep.Id!,
                    Kind: sourceStep.Kind,
                    OperationName: isEditStep ? "edit" : sourceStep.OperationName!,
                    PrimitiveCount: isEditStep ? editPrimitiveCount : 1,
                    OperationDescriptorDigest: isEditStep ? null : OperationDescriptorDigest)
                {
                    PostReadSourceStep = CreatePostReadSourceStep(sourceStep, isEditStep),
                };
            }

            return steps;
        }

        private static NormalizedRequestStep[] CreateTraceSteps (
            NormalizedExecuteRequest request,
            IReadOnlyList<OperationPhaseTrace> operationTraces,
            int editPrimitiveCount)
        {
            var steps = CreateTraceSteps(request, editPrimitiveCount);
            var operationTraceIndex = 0;
            for (var stepIndex = 0; stepIndex < steps.Length; stepIndex++)
            {
                var step = steps[stepIndex];
                if (step.Kind == IpcExecuteStepKind.Op
                    && operationTraceIndex < operationTraces.Count
                    && operationTraces[operationTraceIndex].Contracts != null)
                {
                    steps[stepIndex] = step with
                    {
                        OperationDescriptorDigest = operationTraces[operationTraceIndex].Contracts!.DescriptorDigest,
                    };
                }

                operationTraceIndex += step.PrimitiveCount;
            }

            return steps;
        }

        private static IpcExecutePostReadSourceStep CreatePostReadSourceStep (
            IpcExecuteStepContract sourceStep,
            bool isEditStep)
        {
            if (isEditStep)
            {
                return new IpcExecutePostReadSourceStep(
                    SourceKind: IpcExecutePostReadSourceKind.Edit,
                    PlayModeMutation: false,
                    Commit: IpcExecutePostReadCommit.None,
                    PersistenceExpected: false,
                    ExpectedPostState: IpcExecuteExpectedPostState.Deterministic);
            }

            var sourceKind = string.Equals(sourceStep.OperationName, UcliPrimitiveOperationNames.ProjectRefresh, StringComparison.Ordinal)
                ? IpcExecutePostReadSourceKind.Refresh
                : IpcExecutePostReadSourceKind.Operation;
            return new IpcExecutePostReadSourceStep(
                SourceKind: sourceKind,
                PlayModeMutation: false,
                Commit: null,
                PersistenceExpected: sourceKind == IpcExecutePostReadSourceKind.Refresh,
                ExpectedPostState: IpcExecuteExpectedPostState.Unavailable);
        }

        private static OperationPhaseTrace[] CreateDefaultOperationTraces (NormalizedExecuteRequest request)
        {
            var traces = new OperationPhaseTrace[request.SourceSteps.Count];
            for (var i = 0; i < request.SourceSteps.Count; i++)
            {
                var sourceStep = request.SourceSteps[i];
                traces[i] = OperationPhaseTrace.Variants.SkippedBeforeContractResolution(
                    sourceStep.Id!,
                    sourceStep.OperationName!);
            }

            return traces;
        }

        private static OperationContractFacts CreateContractFacts (
            string operationName,
            UcliOperationKind operationKind,
            bool mayDirty,
            params UcliTouchedResourceKind[] touchedKinds)
        {
            return CreateContractFacts(
                operationName,
                operationKind,
                mayDirty,
                mayPersist: false,
                touchedKinds);
        }

        private static OperationContractFacts CreateContractFacts (
            string operationName,
            UcliOperationKind operationKind,
            bool mayDirty,
            bool mayPersist,
            params UcliTouchedResourceKind[] touchedKinds)
        {
            if (operationKind == UcliOperationKind.Query && (mayDirty || mayPersist))
            {
                throw new ArgumentException(
                    "A query fixture cannot declare mutation or persistence.",
                    nameof(operationKind));
            }

            var sideEffects = new List<UcliOperationSideEffect>();
            if (operationKind == UcliOperationKind.Query)
            {
                sideEffects.Add(UcliOperationSideEffect.ObservesUnityState);
            }
            else
            {
                if (mayDirty)
                {
                    sideEffects.Add(UcliOperationSideEffect.RuntimeStateMutation);
                }
                if (mayPersist)
                {
                    sideEffects.Add(UcliOperationSideEffect.FilesystemWrite);
                }
                if (sideEffects.Count == 0)
                {
                    sideEffects.Add(UcliOperationSideEffect.EditorStateChange);
                }
            }

            IReadOnlyList<UcliTouchedResourceKind> effectiveTouchedKinds =
                mayPersist && touchedKinds.Length == 0
                    ? new[] { UcliTouchedResourceKind.ProjectSettings }
                    : touchedKinds;
            var metadata = UcliOperationMetadata.CreateWithoutVerdict<UcliEmptyArgs, UcliNoResult>(
                operationName,
                operationKind,
                "Execute Request dispatcher contract fixture.",
                new UcliOperationAssuranceContract(
                    sideEffects,
                    effectiveTouchedKinds,
                    UcliOperationPlanMode.ObservesLiveUnity,
                    "Validate the fixture operation without applying changes.",
                    "Apply the fixture operation.",
                    "Reports only fixture-declared touched resource kinds.",
                    "May stale the fixture-declared read surfaces.",
                    "Failure means the fixture operation did not complete.",
                    operationKind == UcliOperationKind.Query
                        ? Array.Empty<string>()
                        : new[] { "The fixture operation may change editor or project state." }),
                requiresPreCallPlanReplay: false,
                exposure: UcliOperationExposure.Public,
                playModeSupport: UcliOperationPlayModeSupport.Disallowed,
                codeContract: null);
            return OperationContractFacts.FromMetadata(metadata);
        }

        private static OperationPhaseStepResult CreateNonJudgingSuccessOutcome (
            bool applied,
            bool changed,
            IReadOnlyList<OperationTouch> touched,
            IReadOnlyList<OperationReadInvalidation> readInvalidations,
            IReadOnlyList<OperationDiagnostic> diagnostics,
            bool persisted)
        {
            return OperationPhaseStepResult
                .Success(applied, changed, touched)
                .WithReadInvalidations(readInvalidations)
                .WithTraceAggregation(touched, diagnostics, persisted);
        }

        private static OperationPhaseStepResult CreateFailureOutcome (
            bool applied,
            bool changed,
            IReadOnlyList<OperationTouch> touched,
            OperationFailure failure,
            JsonElement? result,
            IReadOnlyList<OperationReadInvalidation> readInvalidations,
            IReadOnlyList<OperationDiagnostic> diagnostics,
            bool persisted)
        {
            return OperationPhaseStepResult
                .Failed(failure, applied, changed, result, touched)
                .WithReadInvalidations(readInvalidations)
                .WithTraceAggregation(touched, diagnostics, persisted);
        }

        private static PhaseExecutionTrace CreateSuccessTrace (NormalizedExecuteRequest request)
        {
            return CreateSuccessTrace(request, CreateDefaultOperationTraces(request));
        }

        private static PhaseExecutionTrace CreateSuccessTrace (
            NormalizedExecuteRequest request,
            IReadOnlyList<OperationPhaseTrace> operationTraces)
        {
            return PhaseExecutionTrace.SucceededWithoutPlanToken(
                steps: CreateTraceSteps(request, operationTraces, editPrimitiveCount: 0),
                operationTraces: operationTraces);
        }

        private static PhaseExecutionTrace CreatePlanSuccessTrace (
            NormalizedExecuteRequest request,
            string planToken)
        {
            var operationTraces = CreateDefaultOperationTraces(request);
            return PhaseExecutionTrace.PlanSucceeded(
                steps: CreateTraceSteps(request, operationTraces, editPrimitiveCount: 0),
                operationTraces: operationTraces,
                planToken: planToken);
        }

        private static PhaseExecutionTrace CreateFailureTrace (
            NormalizedExecuteRequest request,
            IReadOnlyList<OperationPhaseTrace> operationTraces,
            IReadOnlyList<OperationFailure> errors)
        {
            var editPrimitiveCount = request.SourceSteps.Count == 1
                && request.SourceSteps[0].Kind == IpcExecuteStepKind.Edit
                    ? operationTraces.Count
                    : 0;
            return PhaseExecutionTrace.Failed(
                steps: CreateTraceSteps(request, operationTraces, editPrimitiveCount),
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

        private sealed class CancellableThenSuccessfulOperationPhaseExecutor : IOperationPhaseExecutor
        {
            private readonly PhaseExecutionTrace executionTrace;
            private readonly TaskCompletionSource<bool> firstExecutionStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public CancellableThenSuccessfulOperationPhaseExecutor (PhaseExecutionTrace executionTrace)
            {
                this.executionTrace = executionTrace;
            }

            public Task FirstExecutionStarted => firstExecutionStarted.Task;

            public int CallCount { get; private set; }

            public async Task<PhaseExecutionTrace> ExecuteAsync (
                PhaseExecutionCommand command,
                NormalizedExecuteRequest request,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CallCount++;
                if (CallCount == 1)
                {
                    firstExecutionStarted.TrySetResult(true);
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
                return executionTrace;
            }
        }

    }
}
