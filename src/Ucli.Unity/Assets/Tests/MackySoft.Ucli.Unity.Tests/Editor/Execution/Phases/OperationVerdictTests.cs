using System;
using System.Collections;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Text.Vocabularies;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;
using MackySoft.Ucli.Contracts.Operations;
using MackySoft.Ucli.Unity.Execution;
using MackySoft.Ucli.Unity.Execution.Dispatch;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.Requests;
using NUnit.Framework;
using UnityEngine.TestTools;

#nullable enable

using MackySoft.Ucli.Contracts.Projects;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class OperationVerdictTests
    {
        private const string OperationName = "ucli.tests.judging-query";

        private static readonly UnityProjectIdentity ProjectIdentity = new UnityProjectIdentity(
            ProjectPathTestValues.RepositoryUnityProject,
            ProjectFingerprintTestFactory.Create("operation-verdict"),
            "6000.1.4f1");

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator JudgingQuery_WhenPublishedAndCalled_BindsConditionDigestVerdictAndEvidence () =>
            UniTask.ToCoroutine(async () =>
        {
            var operation = new JudgingQueryOperation(callFailure: null);
            var snapshot = UcliOperationCatalogSnapshotBuilder.Build(new[]
            {
                new UcliOperationRegistration(operation.Metadata, operation),
            });

            var response = await ExecuteCallAsync(
                operation,
                CreateNormalizedOperation());

            Assert.That(snapshot.Catalog.Operations, Is.Not.Null);
            Assert.That(snapshot.Catalog.Operations!.Count, Is.EqualTo(1));
            var descriptor = snapshot.Catalog.Operations![0];
            Assert.That(
                descriptor.VerdictContract?.Description,
                Is.EqualTo(JudgingQueryOperation.VerdictDescription));
            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
            var operationResult = GetOperationResult(response);
            Assert.That(
                operationResult.GetProperty("phase").GetString(),
                Is.EqualTo(Vocabulary.GetText(IpcExecuteOperationPhase.Call)));
            Assert.That(
                operationResult.GetProperty("operationDescriptorDigest").GetString(),
                Is.EqualTo(descriptor.DescriptorDigest?.ToString()));
            Assert.That(
                operationResult.GetProperty("verdict").GetString(),
                Is.EqualTo(Vocabulary.GetText(Verdict.Incomplete)));
            Assert.That(
                operationResult.GetProperty("result").GetProperty("evidence").GetString(),
                Is.EqualTo(JudgingQueryOperation.ResultEvidence));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ResultfulCall_WhenImplementationOmitsTypedResult_ReturnsInternalError () =>
            UniTask.ToCoroutine(async () =>
        {
            var operation = new NonJudgingQueryOperation(returnResult: false);

            var response = await ExecuteCallAsync(
                operation,
                CreateNormalizedOperation());

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors.Count, Is.EqualTo(1));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InternalError));
            Assert.That(response.Errors[0].Message, Does.Contain("declared TResult"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator VerdictEmission_WhenPhaseOrContractDoesNotQualify_RemainsAbsent () =>
            UniTask.ToCoroutine(async () =>
        {
            var normalizedOperation = CreateNormalizedOperation();
            var judgingOperation = new JudgingQueryOperation(callFailure: null);

            var planResponse = await ExecutePlanAsync(judgingOperation, normalizedOperation);

            Assert.That(planResponse.Status, Is.EqualTo(IpcResponseStatus.Ok));
            AssertVerdictIsNull(planResponse, IpcExecuteOperationPhase.Plan);

            var nonJudgingOperation = new NonJudgingQueryOperation(returnResult: true);
            var nonJudgingCallResponse = await ExecuteCallAsync(
                nonJudgingOperation,
                normalizedOperation);

            Assert.That(nonJudgingCallResponse.Status, Is.EqualTo(IpcResponseStatus.Ok));
            Assert.That(
                GetOperationResult(nonJudgingCallResponse).GetProperty("result").ValueKind,
                Is.EqualTo(JsonValueKind.Object));
            AssertVerdictIsNull(nonJudgingCallResponse, IpcExecuteOperationPhase.Call);

            var failingOperation = new JudgingQueryOperation(
                callFailure: new OperationFailure(
                    UcliCoreErrorCodes.InternalError,
                    "The judging query could not complete.",
                    normalizedOperation.Id));
            var failedCallResponse = await ExecuteCallAsync(
                failingOperation,
                normalizedOperation);

            Assert.That(failedCallResponse.Status, Is.EqualTo(IpcResponseStatus.Error));
            AssertVerdictIsNull(failedCallResponse, IpcExecuteOperationPhase.Call);
        });

        private static async Task<IpcResponse> ExecutePlanAsync (
            IUcliOperation operation,
            NormalizedOperation normalizedOperation)
        {
            var registry = new InMemoryPhaseOperationRegistry(new[]
            {
                new UcliOperationRegistration(operation.Metadata, operation),
            });
            var runner = new OperationPlanStepRunner(registry);
            using var executionContext = new OperationExecutionContext();
            var outcome = await runner.ExecuteAsync(
                normalizedOperation,
                executionContext,
                operationPreflight: null,
                CancellationToken.None);
            var trace = outcome.Error == null
                ? PhaseExecutionTrace.SucceededWithoutPlanToken(
                    steps: CreateTraceSteps(normalizedOperation, operation.Metadata.DescriptorDigest),
                    operationTraces: new[] { outcome.OperationTrace })
                : PhaseExecutionTrace.Failed(
                    steps: CreateTraceSteps(normalizedOperation, operation.Metadata.DescriptorDigest),
                    operationTraces: new[] { outcome.OperationTrace },
                    errors: new[] { outcome.Error });
            return CreateResponse(OperationPhase.Plan, trace);
        }

        private static async Task<IpcResponse> ExecuteCallAsync (
            IUcliOperation operation,
            NormalizedOperation normalizedOperation)
        {
            using var executionContext = new OperationExecutionContext();
            var callPassResult = await new OperationCallPassExecutor().ExecuteAsync(
                new[]
                {
                    new PreparedOperation(
                        Operation: normalizedOperation,
                        PhaseOperation: operation,
                        PlanTouched: Array.Empty<OperationTouch>(),
                        PlanPersisted: false,
                        RequiresPreCallPlanReplay: false),
                },
                executionContext,
                CancellationToken.None);
            var trace = callPassResult.IsSuccess
                ? PhaseExecutionTrace.SucceededWithoutPlanToken(
                    steps: CreateTraceSteps(normalizedOperation, operation.Metadata.DescriptorDigest),
                    operationTraces: callPassResult.OperationTraces)
                : PhaseExecutionTrace.Failed(
                    steps: CreateTraceSteps(normalizedOperation, operation.Metadata.DescriptorDigest),
                    operationTraces: callPassResult.OperationTraces,
                    errors: callPassResult.Errors);
            return CreateResponse(OperationPhase.Call, trace);
        }

        private static NormalizedRequestStep[] CreateTraceSteps (
            NormalizedOperation operation,
            Sha256Digest operationDescriptorDigest)
        {
            return new[]
            {
                new NormalizedRequestStep(
                    Id: operation.Id,
                    Kind: IpcExecuteStepKind.Op,
                    OperationName: operation.Op,
                    PrimitiveCount: 1,
                    OperationDescriptorDigest: operationDescriptorDigest),
            };
        }

        private static IpcResponse CreateResponse (
            OperationPhase executedPass,
            PhaseExecutionTrace trace)
        {
            return ExecuteResponseBuilder.CreateExecutionResponse(
                new ExecuteDispatchContext(Guid.NewGuid(), ProjectIdentity),
                executedPass,
                trace);
        }

        private static JsonElement GetOperationResult (IpcResponse response)
        {
            return response.Payload.GetProperty("opResults")[0];
        }

        private static void AssertVerdictIsNull (
            IpcResponse response,
            IpcExecuteOperationPhase expectedPhase)
        {
            var operationResult = GetOperationResult(response);
            Assert.That(
                operationResult.GetProperty("phase").GetString(),
                Is.EqualTo(Vocabulary.GetText(expectedPhase)));
            Assert.That(
                operationResult.GetProperty("verdict").ValueKind,
                Is.EqualTo(JsonValueKind.Null));
        }

        private static NormalizedOperation CreateNormalizedOperation ()
        {
            return new NormalizedOperation(
                OperationExecutionKey.ForRawStep(new IpcExecuteStepId("judging-step")),
                OperationName,
                JsonSerializer.SerializeToElement(new { }),
                As: null,
                Expect: null,
                AliasReferences: OperationAliasReferenceMap.Empty,
                PersistenceReportingPolicy: OperationPersistenceReportingPolicy.ReportAll,
                AllowExplicitPrefabAssetMutation: false);
        }

        private static UcliOperationAssuranceContract CreateQueryAssurance ()
        {
            return new UcliOperationAssuranceContract(
                sideEffects: Array.Empty<UcliOperationSideEffect>(),
                touchedKinds: Array.Empty<UcliTouchedResourceKind>(),
                planMode: UcliOperationPlanMode.ValidationOnly,
                planSemantics: "Validate the judging query arguments without changing Unity state.",
                callSemantics: "Observe Unity state and produce result evidence without changing it.",
                touchedContract: "The judging query reports no touched resources.",
                readPostconditionContract: "The judging query does not stale a read surface.",
                failureSemantics: "A failure means the condition could not be judged.",
                dangerousNotes: Array.Empty<string>());
        }

        private sealed class JudgingQueryOperation : UcliOperation<UcliEmptyArgs, JudgingQueryResult>
        {
            internal const string VerdictDescription = "The observed value equals the required value.";

            internal const string ResultEvidence = "typed-result-evidence";

            private readonly JudgingQueryResult result = new JudgingQueryResult
            {
                Evidence = ResultEvidence,
                Complete = false,
            };

            private readonly OperationFailure? callFailure;

            public JudgingQueryOperation (OperationFailure? callFailure)
            {
                this.callFailure = callFailure;
                Metadata = UcliOperationMetadata.CreateJudgingQuery<UcliEmptyArgs, JudgingQueryResult>(
                    operationName: OperationName,
                    description: "Observe one value and judge whether it meets the declared condition.",
                    assurance: CreateQueryAssurance(),
                    verdict: new UcliOperationVerdictDefinition<JudgingQueryResult>(
                        VerdictDescription,
                        EvaluateVerdict),
                    requiresPreCallPlanReplay: false,
                    exposure: UcliOperationExposure.Public,
                    playModeSupport: UcliOperationPlayModeSupport.Disallowed,
                    codeContract: null);
            }

            public override UcliOperationMetadata Metadata { get; }

            protected override Task<OperationPhaseStepResult> ValidateAsync (
                NormalizedOperation operation,
                UcliEmptyArgs args,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(SuccessWithResult(
                    result,
                    applied: false,
                    changed: false,
                    touched: Array.Empty<OperationTouch>()));
            }

            protected override Task<OperationPhaseStepResult> PlanAsync (
                NormalizedOperation operation,
                UcliEmptyArgs args,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(SuccessWithResult(
                    result,
                    applied: false,
                    changed: false,
                    touched: Array.Empty<OperationTouch>()));
            }

            protected override Task<OperationPhaseStepResult> CallAsync (
                NormalizedOperation operation,
                UcliEmptyArgs args,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (callFailure != null)
                {
                    return Task.FromResult(OperationPhaseStepResult.Failed(
                        callFailure,
                        applied: false,
                        changed: false,
                        result: null,
                        touched: Array.Empty<OperationTouch>()));
                }

                return Task.FromResult(SuccessWithResult(
                    result,
                    applied: false,
                    changed: false,
                    touched: Array.Empty<OperationTouch>()));
            }

            private static Verdict EvaluateVerdict (JudgingQueryResult result)
            {
                return result.Complete
                    ? Verdict.Pass
                    : Verdict.Incomplete;
            }
        }

        private sealed class NonJudgingQueryOperation : UcliOperation<UcliEmptyArgs, JudgingQueryResult>
        {
            private readonly JudgingQueryResult result = new JudgingQueryResult
            {
                Evidence = "non-judging-evidence",
                Complete = true,
            };

            private readonly bool returnResult;

            public NonJudgingQueryOperation (bool returnResult)
            {
                this.returnResult = returnResult;
            }

            public override UcliOperationMetadata Metadata { get; } =
                UcliOperationMetadata.CreateWithoutVerdict<UcliEmptyArgs, JudgingQueryResult>(
                    operationName: "ucli.tests.non-judging-query",
                    kind: UcliOperationKind.Query,
                    description: "Observe one value without judging a declared condition.",
                    assurance: CreateQueryAssurance(),
                    requiresPreCallPlanReplay: false,
                    exposure: UcliOperationExposure.Public,
                    playModeSupport: UcliOperationPlayModeSupport.Disallowed,
                    codeContract: null);

            protected override Task<OperationPhaseStepResult> ValidateAsync (
                NormalizedOperation operation,
                UcliEmptyArgs args,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(OperationPhaseStepResult.Success(
                    applied: false,
                    changed: false,
                    touched: Array.Empty<OperationTouch>()));
            }

            protected override Task<OperationPhaseStepResult> PlanAsync (
                NormalizedOperation operation,
                UcliEmptyArgs args,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(OperationPhaseStepResult.Success(
                    applied: false,
                    changed: false,
                    touched: Array.Empty<OperationTouch>()));
            }

            protected override Task<OperationPhaseStepResult> CallAsync (
                NormalizedOperation operation,
                UcliEmptyArgs args,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(returnResult
                    ? SuccessWithResult(
                        result,
                        applied: false,
                        changed: false,
                        touched: Array.Empty<OperationTouch>())
                    : OperationPhaseStepResult.Success(
                        applied: false,
                        changed: false,
                        touched: Array.Empty<OperationTouch>()));
            }
        }

        private sealed class JudgingQueryResult
        {
            public string Evidence { get; set; } = string.Empty;

            public bool Complete { get; set; }
        }
    }
}
