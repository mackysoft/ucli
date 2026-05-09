using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Contracts;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Infrastructure.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;
using MackySoft.Ucli.Infrastructure.Project;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.PlanToken;
using MackySoft.Ucli.Unity.Execution.Requests;
using NUnit.Framework;
using UnityEngine.TestTools;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class OperationPhaseExecutorTests
    {
        private static readonly TimeSpan AsyncWaitTimeout = TimeSpan.FromSeconds(5);

        [Test]
        [Category("Size.Small")]
        public void InMemoryRegistry_WhenOperationNameIsDuplicated_ThrowsArgumentException ()
        {
            var first = new RecordingPhaseOperation(
                validateResult: OperationPhaseStepResult.Success(),
                planResult: OperationPhaseStepResult.Success(),
                callResult: OperationPhaseStepResult.Success());
            var second = new RecordingPhaseOperation(
                validateResult: OperationPhaseStepResult.Success(),
                planResult: OperationPhaseStepResult.Success(),
                callResult: OperationPhaseStepResult.Success());

            Assert.Throws<ArgumentException>(() =>
            {
                _ = CreateRegistry(
                    (UcliPrimitiveOperationNames.Resolve, first),
                    (UcliPrimitiveOperationNames.Resolve, second));
            });
        }

        [Test]
        [Category("Size.Small")]
        public void InMemoryRegistry_WhenOperationNameContainsOuterWhitespace_ThrowsArgumentException ()
        {
            var operation = new RecordingPhaseOperation(
                validateResult: OperationPhaseStepResult.Success(),
                planResult: OperationPhaseStepResult.Success(),
                callResult: OperationPhaseStepResult.Success());

            Assert.Throws<ArgumentException>(() =>
            {
                _ = CreateRegistry(("ucli.resolve ", operation));
            });
        }

        [Test]
        [Category("Size.Small")]
        public async Task TypedOperation_WhenArgsFailContractValidation_ReturnsInvalidArgumentWithoutCallingBody ()
        {
            var operation = new RequiredTypedOperation();
            using var executionContext = new OperationExecutionContext();
            var normalizedOperation = CreateNormalizedOperation(
                "op-typed",
                "ucli.tests.required",
                new
                {
                });

            var result = await operation.ValidateAsync(normalizedOperation, executionContext, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Failure!.Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(operation.ValidateBodyCalled, Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public async Task TypedOperation_WhenArgsFailDeserialize_ReturnsInvalidArgumentWithoutCallingBody ()
        {
            var operation = new RequiredTypedOperation();
            using var executionContext = new OperationExecutionContext();
            var normalizedOperation = CreateNormalizedOperation(
                "op-typed",
                "ucli.tests.required",
                new
                {
                    name = 1,
                });

            var result = await operation.ValidateAsync(normalizedOperation, executionContext, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Failure!.Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(operation.ValidateBodyCalled, Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public async Task TypedOperation_WhenPlanArgsFailContractValidation_ReturnsInvalidArgumentWithoutCallingBody ()
        {
            var operation = new RequiredTypedOperation();
            using var executionContext = new OperationExecutionContext();
            var normalizedOperation = CreateNormalizedOperation(
                "op-typed",
                "ucli.tests.required",
                new
                {
                });

            var result = await operation.PlanAsync(normalizedOperation, executionContext, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Failure!.Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(operation.PlanBodyCalled, Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public async Task TypedOperation_WhenCallArgsFailContractValidation_ReturnsInvalidArgumentWithoutCallingBody ()
        {
            var operation = new RequiredTypedOperation();
            using var executionContext = new OperationExecutionContext();
            var normalizedOperation = CreateNormalizedOperation(
                "op-typed",
                "ucli.tests.required",
                new
                {
                });

            var result = await operation.CallAsync(normalizedOperation, executionContext, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Failure!.Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(operation.CallBodyCalled, Is.False);
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator TypedOperation_WhenPublicOpUsesRequestLocalAlias_ReturnsInvalidArgumentWithoutCallingBody () => UniTask.ToCoroutine(async () =>
        {
            var operation = new AliasReferenceTypedOperation();
            using var executionContext = new OperationExecutionContext();
            var normalizedOperation = new NormalizedOperation(
                Id: "op-alias",
                Op: "ucli.tests.alias-reference",
                Args: JsonSerializer.SerializeToElement(new
                {
                    target = new
                    {
                        @var = "created",
                    },
                }),
                As: null,
                Expect: null,
                AllowRequestLocalAliases: false);

            var result = await operation.ValidateAsync(normalizedOperation, executionContext, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Failure!.Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(
                result.Failure.Message,
                Is.EqualTo("Operation 'args.target.var' cannot use reserved request-local alias property 'var' in public op steps."));
            Assert.That(operation.ValidateBodyCalled, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator TypedOperation_WhenPublicOpIncludesNullRequestLocalAliasProperty_ReturnsInvalidArgumentWithoutCallingBody () => UniTask.ToCoroutine(async () =>
        {
            var operation = new AliasReferenceTypedOperation();
            using var executionContext = new OperationExecutionContext();
            var normalizedOperation = new NormalizedOperation(
                Id: "op-alias",
                Op: "ucli.tests.alias-reference",
                Args: JsonSerializer.SerializeToElement(new
                {
                    target = new
                    {
                        @var = (string?)null,
                        scene = "Assets/Scenes/Main.unity",
                        hierarchyPath = "Root",
                    },
                }),
                As: null,
                Expect: null,
                AllowRequestLocalAliases: false);

            var result = await operation.ValidateAsync(normalizedOperation, executionContext, CancellationToken.None);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Failure!.Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(
                result.Failure.Message,
                Is.EqualTo("Operation 'args.target.var' cannot use reserved request-local alias property 'var' in public op steps."));
            Assert.That(operation.ValidateBodyCalled, Is.False);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenCommandIsPlan_ExecutesValidateAndPlanOnly () => UniTask.ToCoroutine(async () =>
        {
            var operation = new RecordingPhaseOperation(
                validateResult: OperationPhaseStepResult.Success(),
                planResult: OperationPhaseStepResult.Success(applied: false, changed: true),
                callResult: OperationPhaseStepResult.Success(applied: true, changed: true));
            var executor = CreateExecutor(operation);
            var request = CreateRequest("op-1", UcliPrimitiveOperationNames.Resolve);

            var trace = await ExecuteAsync(executor, PhaseExecutionCommand.Plan, request, "Plan phase execution");

            CollectionAssert.AreEqual(new[] { OperationPhase.Validate, OperationPhase.Plan }, operation.CalledPhases);
            Assert.That(trace.IsSuccess, Is.True);
            Assert.That(trace.OperationTraces[0].Phase, Is.EqualTo(OperationPhase.Plan));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenCommandIsCall_ExecutesValidatePlanAndCall () => UniTask.ToCoroutine(async () =>
        {
            var operation = new RecordingPhaseOperation(
                validateResult: OperationPhaseStepResult.Success(),
                planResult: OperationPhaseStepResult.Success(),
                callResult: OperationPhaseStepResult.Success(applied: true, changed: true));
            var executor = CreateExecutor(operation);
            var request = CreateRequest("op-1", UcliPrimitiveOperationNames.Resolve);

            var trace = await ExecuteAsync(executor, PhaseExecutionCommand.Call, request, "Call phase execution");

            CollectionAssert.AreEqual(new[] { OperationPhase.Validate, OperationPhase.Plan, OperationPhase.Call }, operation.CalledPhases);
            Assert.That(trace.IsSuccess, Is.True);
            Assert.That(trace.OperationTraces[0].Phase, Is.EqualTo(OperationPhase.Call));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenCommandIsPlanWithoutToken_ExecutesValidateAndPlanWithoutPlanToken () => UniTask.ToCoroutine(async () =>
        {
            var operation = new RecordingPhaseOperation(
                validateResult: OperationPhaseStepResult.Success(),
                planResult: OperationPhaseStepResult.Success(applied: false, changed: false),
                callResult: OperationPhaseStepResult.Success(applied: true, changed: true));
            var coordinator = new StubPlanTokenCoordinator(
                issueResultFactory: _ => PlanTokenIssueResult.Success("unused"),
                requestValidationResultFactory: _ => PlanTokenValidationResult.Success(),
                validationResultFactory: _ => PlanTokenValidationResult.Success());
            var executor = new OperationPhaseExecutor(
                CreateRegistry((UcliPrimitiveOperationNames.Resolve, operation)),
                coordinator);
            var request = CreateRequest("op-1", UcliPrimitiveOperationNames.Resolve);

            var trace = await ExecuteAsync(executor, PhaseExecutionCommand.PlanWithoutToken, request, "Resolve phase execution");

            CollectionAssert.AreEqual(new[] { OperationPhase.Validate, OperationPhase.Plan }, operation.CalledPhases);
            Assert.That(trace.IsSuccess, Is.True);
            Assert.That(trace.PlanToken, Is.Null);
            Assert.That(trace.OperationTraces[0].Phase, Is.EqualTo(OperationPhase.Plan));
            Assert.That(coordinator.IssueCallCount, Is.EqualTo(0));
            Assert.That(coordinator.ValidateCallRequestCount, Is.EqualTo(0));
            Assert.That(coordinator.ValidateCallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenCallContainsDuplicateOperationNames_ReplaysPlanBeforeEachCall () => UniTask.ToCoroutine(async () =>
        {
            var operation = new StatefulPhaseOperation();
            var executor = CreateExecutor(operation);
            var request = CreateRequest(
                ("op-1", UcliPrimitiveOperationNames.Resolve),
                ("op-2", UcliPrimitiveOperationNames.Resolve));

            var trace = await ExecuteAsync(executor, PhaseExecutionCommand.Call, request, "Duplicate operation call execution");

            Assert.That(trace.IsSuccess, Is.True);
            Assert.That(trace.OperationTraces.Count, Is.EqualTo(2));
            Assert.That(trace.OperationTraces[0].Phase, Is.EqualTo(OperationPhase.Call));
            Assert.That(trace.OperationTraces[1].Phase, Is.EqualTo(OperationPhase.Call));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenCallContainsDuplicateOperationNamesAndReplayIsNotRequired_DoesNotReplayPlanBeforeCall () => UniTask.ToCoroutine(async () =>
        {
            var operation = new RecordingPhaseOperation(
                validateResult: OperationPhaseStepResult.Success(),
                planResult: OperationPhaseStepResult.Success(),
                callResult: OperationPhaseStepResult.Success(applied: true, changed: false));
            var executor = CreateExecutor(operation);
            var request = CreateRequest(
                ("op-1", UcliPrimitiveOperationNames.Resolve),
                ("op-2", UcliPrimitiveOperationNames.Resolve));

            var trace = await ExecuteAsync(executor, PhaseExecutionCommand.Call, request, "Duplicate operation call execution without replay");

            Assert.That(trace.IsSuccess, Is.True);
            CollectionAssert.AreEqual(
                new[]
                {
                    OperationPhase.Validate,
                    OperationPhase.Plan,
                    OperationPhase.Validate,
                    OperationPhase.Plan,
                    OperationPhase.Call,
                    OperationPhase.Call,
                },
                operation.CalledPhases);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenCommandIsCall_UsesSharedExecutionContextAcrossAllPhases () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ContextCapturingPhaseOperation();
            var executor = CreateExecutor(operation);
            var request = CreateRequest("op-1", UcliPrimitiveOperationNames.Resolve);

            var trace = await ExecuteAsync(executor, PhaseExecutionCommand.Call, request, "Shared context call execution");

            Assert.That(trace.IsSuccess, Is.True);
            Assert.That(operation.ValidateContext, Is.Not.Null);
            Assert.That(operation.ValidateContext, Is.SameAs(operation.PlanContext));
            Assert.That(operation.PlanContext, Is.SameAs(operation.CallContext));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenCallContainsDangerousOperationWithoutAllowDangerous_DoesNotExecuteValidatePlanOrCallPhase () => UniTask.ToCoroutine(async () =>
        {
            var operation = new RecordingPhaseOperation(
                validateResult: OperationPhaseStepResult.Success(),
                planResult: OperationPhaseStepResult.Success(),
                callResult: OperationPhaseStepResult.Success(applied: true, changed: true),
                policy: OperationPolicy.Dangerous);
            var executor = new OperationPhaseExecutor(CreateRegistry(("ucli.tests.dangerous", operation)));
            var request = CreateRequest("op-1", "ucli.tests.dangerous");

            var trace = await ExecuteAsync(executor, PhaseExecutionCommand.Call, request, "Dangerous call denied execution");

            Assert.That(trace.IsSuccess, Is.False);
            Assert.That(trace.Errors.Count, Is.EqualTo(1));
            Assert.That(trace.Errors[0].Code.Value, Is.EqualTo("OPERATION_NOT_ALLOWED"));
            Assert.That(trace.Errors[0].OpId, Is.EqualTo("op-1"));
            Assert.That(trace.OperationTraces[0].Phase, Is.EqualTo(OperationPhase.Validate));
            CollectionAssert.IsEmpty(operation.CalledPhases);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenCallContainsDangerousOperationWithAllowDangerous_ExecutesCallPhase () => UniTask.ToCoroutine(async () =>
        {
            using var scope = new PlanTokenTestScope();
            scope.WriteConfigJson("{\"operationPolicy\":\"dangerous\",\"operationAllowlist\":[\"^ucli\\\\.tests\\\\.\"]}");
            var environment = scope.CreateEnvironment();
            var operation = new RecordingPhaseOperation(
                validateResult: OperationPhaseStepResult.Success(),
                planResult: OperationPhaseStepResult.Success(),
                callResult: OperationPhaseStepResult.Success(applied: true, changed: true),
                policy: OperationPolicy.Dangerous);
            var executor = new OperationPhaseExecutor(
                CreateRegistry(("ucli.tests.dangerous", operation)),
                new PlanTokenCoordinator(environment),
                new DangerousOperationCallAuthorizer(environment));
            var request = CreateRequest(
                operations: new[] { ("op-1", "ucli.tests.dangerous") },
                planToken: null,
                canonicalPayloadJson: "{}",
                allowDangerous: true);

            var trace = await ExecuteAsync(executor, PhaseExecutionCommand.Call, request, "Dangerous call allowed execution");

            Assert.That(trace.IsSuccess, Is.True);
            CollectionAssert.AreEqual(
                new[] { OperationPhase.Validate, OperationPhase.Plan, OperationPhase.Call },
                operation.CalledPhases);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenDangerousAllowFlagSetButConfigPolicyBlocks_DoesNotExecuteCallPhase () => UniTask.ToCoroutine(async () =>
        {
            using var scope = new PlanTokenTestScope();
            scope.WriteConfigJson("{\"operationPolicy\":\"safe\",\"operationAllowlist\":[\"^ucli\\\\.tests\\\\.\"]}");
            var environment = scope.CreateEnvironment();
            var operation = new RecordingPhaseOperation(
                validateResult: OperationPhaseStepResult.Success(),
                planResult: OperationPhaseStepResult.Success(),
                callResult: OperationPhaseStepResult.Success(applied: true, changed: true),
                policy: OperationPolicy.Dangerous);
            var executor = new OperationPhaseExecutor(
                CreateRegistry(("ucli.tests.dangerous", operation)),
                new PlanTokenCoordinator(environment),
                new DangerousOperationCallAuthorizer(environment));
            var request = CreateRequest(
                operations: new[] { ("op-1", "ucli.tests.dangerous") },
                planToken: null,
                canonicalPayloadJson: "{}",
                allowDangerous: true);

            var trace = await ExecuteAsync(executor, PhaseExecutionCommand.Call, request, "Dangerous call blocked by Unity-side config");

            Assert.That(trace.IsSuccess, Is.False);
            Assert.That(trace.Errors.Count, Is.EqualTo(1));
            Assert.That(trace.Errors[0].Code.Value, Is.EqualTo("OPERATION_NOT_ALLOWED"));
            Assert.That(trace.Errors[0].Message, Does.Contain("operationPolicy='safe'"));
            CollectionAssert.AreEqual(new[] { OperationPhase.Validate, OperationPhase.Plan }, operation.CalledPhases);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenPlanSucceeds_IssuesPlanToken () => UniTask.ToCoroutine(async () =>
        {
            var operation = new RecordingPhaseOperation(
                validateResult: OperationPhaseStepResult.Success(),
                planResult: OperationPhaseStepResult.Success(),
                callResult: OperationPhaseStepResult.Success());
            var coordinator = new StubPlanTokenCoordinator(
                issueResultFactory: _ => PlanTokenIssueResult.Success("issued-token"),
                requestValidationResultFactory: _ => PlanTokenValidationResult.Success(),
                validationResultFactory: _ => PlanTokenValidationResult.Success());
            var executor = new OperationPhaseExecutor(
                CreateRegistry((UcliPrimitiveOperationNames.Resolve, operation)),
                coordinator);
            var request = CreateRequest("op-1", UcliPrimitiveOperationNames.Resolve);

            var trace = await ExecuteAsync(executor, PhaseExecutionCommand.Plan, request, "Plan token issue execution");

            Assert.That(trace.IsSuccess, Is.True);
            Assert.That(trace.PlanToken, Is.EqualTo("issued-token"));
            Assert.That(coordinator.IssueCallCount, Is.EqualTo(1));
            Assert.That(coordinator.ValidateCallCount, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenCallPlanTokenValidationFails_DoesNotExecuteCallPhase () => UniTask.ToCoroutine(async () =>
        {
            var firstOperation = new RecordingPhaseOperation(
                validateResult: OperationPhaseStepResult.Success(),
                planResult: OperationPhaseStepResult.Success(),
                callResult: OperationPhaseStepResult.Success());
            var secondOperation = new RecordingPhaseOperation(
                validateResult: OperationPhaseStepResult.Success(),
                planResult: OperationPhaseStepResult.Success(),
                callResult: OperationPhaseStepResult.Success());
            var coordinator = new StubPlanTokenCoordinator(
                issueResultFactory: _ => PlanTokenIssueResult.Success("unused"),
                requestValidationResultFactory: _ => PlanTokenValidationResult.Success(),
                validationResultFactory: _ => PlanTokenValidationResult.Failed(new OperationFailure(
                    Code: PlanTokenErrorCodes.PlanTokenInvalid,
                    Message: "invalid token",
                    OpId: null)));
            var executor = new OperationPhaseExecutor(
                CreateRegistry(
                    (UcliPrimitiveOperationNames.Resolve, firstOperation),
                    (UcliPrimitiveOperationNames.SceneOpen, secondOperation)),
                coordinator);
            var request = CreateRequest(
                ("op-1", UcliPrimitiveOperationNames.Resolve),
                ("op-2", UcliPrimitiveOperationNames.SceneOpen));

            var trace = await ExecuteAsync(executor, PhaseExecutionCommand.Call, request, "Plan token validation failure execution");

            Assert.That(trace.IsSuccess, Is.False);
            Assert.That(trace.Errors.Count, Is.EqualTo(1));
            Assert.That(trace.Errors[0].Code, Is.EqualTo(PlanTokenErrorCodes.PlanTokenInvalid));
            Assert.That(trace.OperationTraces[0].Phase, Is.EqualTo(OperationPhase.Plan));
            Assert.That(trace.OperationTraces[1].Phase, Is.EqualTo(OperationPhase.Plan));
            CollectionAssert.AreEqual(new[] { OperationPhase.Validate, OperationPhase.Plan }, firstOperation.CalledPhases);
            CollectionAssert.AreEqual(new[] { OperationPhase.Validate, OperationPhase.Plan }, secondOperation.CalledPhases);
            Assert.That(coordinator.ValidateCallCount, Is.EqualTo(1));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenCallCompileFailsAfterPlanTokenPrevalidation_ReturnsCompiledExecutionChangedSincePlan () => UniTask.ToCoroutine(async () =>
        {
            var request = CreateRequest(
                operations: new[] { ("edit-1", "unused") },
                planToken: "token",
                canonicalPayloadJson: "{}");
            var planPassResult = new PlanPassResult(
                CompiledSteps: new[]
                {
                    new NormalizedRequestStep("edit-1", IpcRequestStepKind.Edit, "edit", 1),
                },
                CompiledDigestPayloadUtf8: CreateCompiledDigestPayloadUtf8(),
                OperationTraces: new[]
                {
                    new OperationPhaseTrace(
                        OpId: "edit-1",
                        Op: "edit",
                        Phase: OperationPhase.Validate,
                        Applied: false,
                        Changed: false,
                        Touched: Array.Empty<OperationTouch>(),
                        Failure: new OperationFailure(UcliCoreErrorCodes.InvalidArgument, "selection no longer resolves.", "edit-1")),
                },
                Errors: new[]
                {
                    new OperationFailure(UcliCoreErrorCodes.InvalidArgument, "selection no longer resolves.", "edit-1"),
                },
                PreparedOperations: Array.Empty<PreparedOperation>());
            var executor = new OperationPhaseExecutor(
                new StubPlanPassExecutor(planPassResult),
                new OperationCallPassExecutor(),
                new StubPlanTokenCoordinator(
                    issueResultFactory: _ => throw new InvalidOperationException("Issue should not be called."),
                    requestValidationResultFactory: _ => PlanTokenValidationResult.Success(),
                    validationResultFactory: _ => PlanTokenValidationResult.Failed(new OperationFailure(
                        PlanTokenErrorCodes.StateChangedSincePlan,
                        "Compiled execution changed since plan token issuance.",
                        null))),
                new DangerousOperationCallAuthorizer());

            var trace = await ExecuteAsync(executor, PhaseExecutionCommand.Call, request, "Call compile failure should map to compiled execution drift");

            Assert.That(trace.IsSuccess, Is.False);
            Assert.That(trace.Errors.Count, Is.EqualTo(1));
            Assert.That(trace.Errors[0].Code, Is.EqualTo(PlanTokenErrorCodes.StateChangedSincePlan));
            Assert.That(trace.Errors[0].Message, Is.EqualTo("Compiled execution changed since plan token issuance."));
            Assert.That(trace.Errors[0].OpId, Is.EqualTo("edit-1"));
            Assert.That(trace.OperationTraces[0].Failure, Is.Not.Null);
            Assert.That(trace.OperationTraces[0].Failure!.Code, Is.EqualTo(PlanTokenErrorCodes.StateChangedSincePlan));
            Assert.That(trace.OperationTraces[0].Failure!.Message, Is.EqualTo("Compiled execution changed since plan token issuance."));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenCallCompileFailsAfterPlanTokenPrevalidationAndTokenStillValid_ReturnsOriginalCompileFailure () => UniTask.ToCoroutine(async () =>
        {
            var request = CreateRequest(
                operations: new[] { ("edit-1", "unused") },
                planToken: "token",
                canonicalPayloadJson: "{}");
            var planPassResult = new PlanPassResult(
                CompiledSteps: new[]
                {
                    new NormalizedRequestStep("edit-1", IpcRequestStepKind.Edit, "edit", 1),
                },
                CompiledDigestPayloadUtf8: CreateCompiledDigestPayloadUtf8(),
                OperationTraces: new[]
                {
                    new OperationPhaseTrace(
                        OpId: "edit-1",
                        Op: "edit",
                        Phase: OperationPhase.Validate,
                        Applied: false,
                        Changed: false,
                        Touched: Array.Empty<OperationTouch>(),
                        Failure: new OperationFailure(UcliCoreErrorCodes.InvalidArgument, "selection no longer resolves.", "edit-1")),
                },
                Errors: new[]
                {
                    new OperationFailure(UcliCoreErrorCodes.InvalidArgument, "selection no longer resolves.", "edit-1"),
                },
                PreparedOperations: Array.Empty<PreparedOperation>());
            var coordinator = new StubPlanTokenCoordinator(
                issueResultFactory: _ => throw new InvalidOperationException("Issue should not be called."),
                requestValidationResultFactory: _ => PlanTokenValidationResult.Success(),
                validationResultFactory: _ => PlanTokenValidationResult.Success());
            var executor = new OperationPhaseExecutor(
                new StubPlanPassExecutor(planPassResult),
                new OperationCallPassExecutor(),
                coordinator,
                new DangerousOperationCallAuthorizer());

            var trace = await ExecuteAsync(executor, PhaseExecutionCommand.Call, request, "Legacy-compatible token should preserve compile failure");

            Assert.That(trace.IsSuccess, Is.False);
            Assert.That(trace.Errors.Count, Is.EqualTo(1));
            Assert.That(trace.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(trace.Errors[0].Message, Is.EqualTo("selection no longer resolves."));
            Assert.That(trace.Errors[0].OpId, Is.EqualTo("edit-1"));
            Assert.That(trace.OperationTraces[0].Failure, Is.Not.Null);
            Assert.That(trace.OperationTraces[0].Failure!.Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(trace.OperationTraces[0].Failure!.Message, Is.EqualTo("selection no longer resolves."));
            Assert.That(coordinator.ValidateCallCount, Is.EqualTo(1));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenValidateFails_MarksRemainingOperationsAsSkipped () => UniTask.ToCoroutine(async () =>
        {
            var failingOperation = new RecordingPhaseOperation(
                validateResult: OperationPhaseStepResult.Failed(new OperationFailure(UcliCoreErrorCodes.InvalidArgument, "invalid", "op-1")),
                planResult: OperationPhaseStepResult.Success(),
                callResult: OperationPhaseStepResult.Success());
            var skippedOperation = new RecordingPhaseOperation(
                validateResult: OperationPhaseStepResult.Success(),
                planResult: OperationPhaseStepResult.Success(),
                callResult: OperationPhaseStepResult.Success());
            var executor = new OperationPhaseExecutor(CreateRegistry(
                (UcliPrimitiveOperationNames.Resolve, failingOperation),
                (UcliPrimitiveOperationNames.SceneOpen, skippedOperation)));
            var request = CreateRequest(
                ("op-1", UcliPrimitiveOperationNames.Resolve),
                ("op-2", UcliPrimitiveOperationNames.SceneOpen));

            var trace = await ExecuteAsync(executor, PhaseExecutionCommand.Call, request, "Validate failure execution");

            Assert.That(trace.IsSuccess, Is.False);
            Assert.That(trace.Steps.Count, Is.EqualTo(2));
            Assert.That(trace.Steps[0].PrimitiveCount, Is.EqualTo(1));
            Assert.That(trace.Steps[1].PrimitiveCount, Is.EqualTo(0));
            Assert.That(trace.OperationTraces.Count, Is.EqualTo(1));
            Assert.That(trace.OperationTraces[0].Phase, Is.EqualTo(OperationPhase.Validate));
            Assert.That(trace.OperationTraces[0].Failure, Is.Not.Null);
            Assert.That(skippedOperation.CalledPhases.Count, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenPlanFails_MarksRemainingOperationsAsSkipped () => UniTask.ToCoroutine(async () =>
        {
            var failingOperation = new RecordingPhaseOperation(
                validateResult: OperationPhaseStepResult.Success(),
                planResult: OperationPhaseStepResult.Failed(new OperationFailure(UcliCoreErrorCodes.InvalidArgument, "plan failed", "op-1")),
                callResult: OperationPhaseStepResult.Success());
            var skippedOperation = new RecordingPhaseOperation(
                validateResult: OperationPhaseStepResult.Success(),
                planResult: OperationPhaseStepResult.Success(),
                callResult: OperationPhaseStepResult.Success());
            var executor = new OperationPhaseExecutor(CreateRegistry(
                (UcliPrimitiveOperationNames.Resolve, failingOperation),
                (UcliPrimitiveOperationNames.SceneOpen, skippedOperation)));
            var request = CreateRequest(
                ("op-1", UcliPrimitiveOperationNames.Resolve),
                ("op-2", UcliPrimitiveOperationNames.SceneOpen));

            var trace = await ExecuteAsync(executor, PhaseExecutionCommand.Call, request, "Plan failure execution");

            Assert.That(trace.IsSuccess, Is.False);
            Assert.That(trace.Steps.Count, Is.EqualTo(2));
            Assert.That(trace.Steps[0].PrimitiveCount, Is.EqualTo(1));
            Assert.That(trace.Steps[1].PrimitiveCount, Is.EqualTo(0));
            Assert.That(trace.OperationTraces.Count, Is.EqualTo(1));
            Assert.That(trace.OperationTraces[0].Phase, Is.EqualTo(OperationPhase.Plan));
            Assert.That(skippedOperation.CalledPhases.Count, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenCallFails_MarksRemainingOperationsAsSkipped () => UniTask.ToCoroutine(async () =>
        {
            var failingOperation = new RecordingPhaseOperation(
                validateResult: OperationPhaseStepResult.Success(),
                planResult: OperationPhaseStepResult.Success(),
                callResult: OperationPhaseStepResult.Failed(new OperationFailure(UcliCoreErrorCodes.InvalidArgument, "call failed", "op-1")));
            var skippedOperation = new RecordingPhaseOperation(
                validateResult: OperationPhaseStepResult.Success(),
                planResult: OperationPhaseStepResult.Success(),
                callResult: OperationPhaseStepResult.Success());
            var executor = new OperationPhaseExecutor(CreateRegistry(
                (UcliPrimitiveOperationNames.Resolve, failingOperation),
                (UcliPrimitiveOperationNames.SceneOpen, skippedOperation)));
            var request = CreateRequest(
                ("op-1", UcliPrimitiveOperationNames.Resolve),
                ("op-2", UcliPrimitiveOperationNames.SceneOpen));

            var trace = await ExecuteAsync(executor, PhaseExecutionCommand.Call, request, "Call failure execution");

            Assert.That(trace.IsSuccess, Is.False);
            Assert.That(trace.OperationTraces[0].Phase, Is.EqualTo(OperationPhase.Call));
            Assert.That(trace.OperationTraces[1].Phase, Is.EqualTo(OperationPhase.Skipped));
            CollectionAssert.AreEqual(new[] { OperationPhase.Validate, OperationPhase.Plan }, skippedOperation.CalledPhases);
        });

        [Test]
        [Category("Size.Small")]
        public void ValidateCall_WhenModeRequiredAndTokenMissing_ReturnsPlanTokenRequired ()
        {
            using var scope = new PlanTokenTestScope();
            scope.WriteConfigJson("{\"planTokenMode\":\"required\"}");

            var environment = scope.CreateEnvironment();
            var coordinator = new PlanTokenCoordinator(environment);
            var request = CreateRequest(
                operations: new[] { ("op-1", UcliPrimitiveOperationNames.Resolve) },
                planToken: null,
                canonicalPayloadJson: "{\"ops\":[],\"protocolVersion\":1}");
            var traces = CreatePlanTraceWithTouched(scope.ProjectRoot, "Assets/Scenes/Main.unity");

            var result = coordinator.ValidateCall(request, traces, CreateCompiledDigestPayloadUtf8());

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Failure, Is.Not.Null);
            Assert.That(result.Failure!.Code, Is.EqualTo(PlanTokenErrorCodes.PlanTokenRequired));
        }

        [Test]
        [Category("Size.Small")]
        public void Issue_WhenKeyFileMissing_CreatesKeyFileAndReturnsToken ()
        {
            using var scope = new PlanTokenTestScope();
            scope.WriteConfigJson("{\"planTokenMode\":\"optional\"}");

            var environment = scope.CreateEnvironment();
            var coordinator = new PlanTokenCoordinator(environment);
            var request = CreateRequest(
                operations: new[] { ("op-1", UcliPrimitiveOperationNames.Resolve) },
                planToken: null,
                canonicalPayloadJson: "{\"ops\":[],\"protocolVersion\":1}");
            var traces = CreatePlanTraceWithTouched(scope.ProjectRoot, "Assets/Scenes/Main.unity");

            var issueResult = coordinator.Issue(request, traces, CreateCompiledDigestPayloadUtf8());

            Assert.That(issueResult.IsSuccess, Is.True);
            Assert.That(issueResult.PlanToken, Is.Not.Null.And.Not.Empty);
            Assert.That(File.Exists(scope.PlanTokenKeyPath), Is.True);
        }

        [Test]
        [Category("Size.Small")]
        public void Issue_WhenTokenIsIssued_UsesCurrentCompactTokenFormatVersion ()
        {
            using var scope = new PlanTokenTestScope();
            var environment = scope.CreateEnvironment();
            var coordinator = new PlanTokenCoordinator(environment);
            var request = CreateRequest(
                operations: new[] { ("op-1", UcliPrimitiveOperationNames.Resolve) },
                planToken: null,
                canonicalPayloadJson: "{\"ops\":[],\"protocolVersion\":1}");
            var traces = CreatePlanTraceWithTouched(scope.ProjectRoot, "Assets/Scenes/Main.unity");

            var issueResult = coordinator.Issue(request, traces, CreateCompiledDigestPayloadUtf8());

            Assert.That(issueResult.IsSuccess, Is.True);
            Assert.That(issueResult.PlanToken, Is.Not.Null.And.Not.Empty);
            Assert.That(PlanTokenCompactCodec.TryDecodeToken(issueResult.PlanToken!, out var decodedToken), Is.True);
            Assert.That(decodedToken, Is.Not.Null);
            Assert.That(decodedToken!.Header.KeyId, Is.EqualTo(PlanTokenCompactCodec.TokenKeyId));
            Assert.That(decodedToken.Payload.KeyId, Is.EqualTo(PlanTokenCompactCodec.TokenKeyId));
            Assert.That(decodedToken.Payload.Version, Is.EqualTo(PlanTokenCompactCodec.TokenVersion));
            Assert.That(PlanTokenCompactCodec.IsSupported(decodedToken), Is.True);
        }

        [Test]
        [Category("Size.Small")]
        public void ValidateCall_WhenTokenExpired_ReturnsPlanTokenExpired ()
        {
            using var scope = new PlanTokenTestScope();
            var environment = scope.CreateEnvironment();
            var coordinator = new PlanTokenCoordinator(environment);
            var request = CreateRequest(
                operations: new[] { ("op-1", UcliPrimitiveOperationNames.Resolve) },
                planToken: null,
                canonicalPayloadJson: "{\"ops\":[],\"protocolVersion\":1}");
            var traces = CreatePlanTraceWithTouched(scope.ProjectRoot, "Assets/Scenes/Main.unity");

            var issueResult = coordinator.Issue(request, traces, CreateCompiledDigestPayloadUtf8());
            Assert.That(issueResult.IsSuccess, Is.True);

            environment.UtcNow = environment.UtcNow.AddMinutes(16).AddSeconds(31);
            var validationRequest = request with
            {
                PlanToken = issueResult.PlanToken,
            };

            var validationResult = coordinator.ValidateCall(validationRequest, traces, CreateCompiledDigestPayloadUtf8());

            Assert.That(validationResult.IsSuccess, Is.False);
            Assert.That(validationResult.Failure!.Code, Is.EqualTo(PlanTokenErrorCodes.PlanTokenExpired));
        }

        [Test]
        [Category("Size.Small")]
        public void ValidateCall_WhenLegacyTokenOmitsCompiledExecutionDigest_Succeeds ()
        {
            using var scope = new PlanTokenTestScope();
            var environment = scope.CreateEnvironment();
            var coordinator = new PlanTokenCoordinator(environment);
            var request = CreateRequest(
                operations: new[] { ("op-1", UcliPrimitiveOperationNames.Resolve) },
                planToken: null,
                canonicalPayloadJson: "{\"ops\":[],\"protocolVersion\":1}");
            var traces = CreatePlanTraceWithTouched(scope.ProjectRoot, "Assets/Scenes/Main.unity");

            var currentIssueResult = coordinator.Issue(request, traces, CreateCompiledDigestPayloadUtf8());
            Assert.That(currentIssueResult.IsSuccess, Is.True);

            var signingKey = ReadSigningKey(scope.PlanTokenKeyPath);
            var legacyToken = CreateLegacyPlanTokenWithoutCompiledExecutionDigest(
                signingKey,
                version: PlanTokenCompactCodec.TokenVersion,
                keyId: PlanTokenCompactCodec.TokenKeyId,
                projectFingerprint: scope.ProjectFingerprint,
                requestDigest: Sha256LowerHex.Compute(request.CanonicalDigestPayloadUtf8.ToArray()),
                stateFingerprint: PlanTokenStateFingerprintCalculator.Compute(environment.Capture(), traces),
                issuedAtUtc: environment.UtcNow,
                expiresAtUtc: environment.UtcNow.AddMinutes(15),
                nonce: "legacy-nonce");
            var validationRequest = request with
            {
                PlanToken = legacyToken,
            };

            var validationResult = coordinator.ValidateCall(validationRequest, traces, CreateCompiledDigestPayloadUtf8());

            Assert.That(validationResult.IsSuccess, Is.True);
            Assert.That(validationResult.Failure, Is.Null);
        }

        [Test]
        [Category("Size.Small")]
        public void ValidateCall_WhenRequestDigestDiffers_ReturnsPlanTokenRequestMismatch ()
        {
            using var scope = new PlanTokenTestScope();
            var environment = scope.CreateEnvironment();
            var coordinator = new PlanTokenCoordinator(environment);
            var originalRequest = CreateRequest(
                operations: new[] { ("op-1", UcliPrimitiveOperationNames.Resolve) },
                planToken: null,
                canonicalPayloadJson: "{\"ops\":[{\"id\":\"op-1\"}],\"protocolVersion\":1}");
            var traces = CreatePlanTraceWithTouched(scope.ProjectRoot, "Assets/Scenes/Main.unity");

            var issueResult = coordinator.Issue(originalRequest, traces, CreateCompiledDigestPayloadUtf8());
            Assert.That(issueResult.IsSuccess, Is.True);

            var modifiedRequest = CreateRequest(
                operations: new[] { ("op-1", UcliPrimitiveOperationNames.Resolve) },
                planToken: issueResult.PlanToken,
                canonicalPayloadJson: "{\"ops\":[{\"id\":\"op-2\"}],\"protocolVersion\":1}");
            var validationResult = coordinator.ValidateCall(modifiedRequest, traces, CreateCompiledDigestPayloadUtf8());

            Assert.That(validationResult.IsSuccess, Is.False);
            Assert.That(validationResult.Failure!.Code, Is.EqualTo(PlanTokenErrorCodes.PlanTokenRequestMismatch));
        }

        [Test]
        [Category("Size.Small")]
        public void ValidateCall_WhenCompiledExecutionDigestDiffers_ReturnsStateChangedSincePlan ()
        {
            using var scope = new PlanTokenTestScope();
            var environment = scope.CreateEnvironment();
            var coordinator = new PlanTokenCoordinator(environment);
            var request = CreateRequest(
                operations: new[] { ("op-1", UcliPrimitiveOperationNames.Resolve) },
                planToken: null,
                canonicalPayloadJson: "{\"ops\":[],\"protocolVersion\":1}");
            var traces = CreatePlanTraceWithTouched(scope.ProjectRoot, "Assets/Scenes/Main.unity");

            var issueResult = coordinator.Issue(request, traces, CreateCompiledDigestPayloadUtf8());
            Assert.That(issueResult.IsSuccess, Is.True);

            var validationRequest = request with
            {
                PlanToken = issueResult.PlanToken,
            };
            var changedDigestPayload = Encoding.UTF8.GetBytes("{\"ops\":[{\"id\":\"different\"}],\"steps\":[]}");
            var validationResult = coordinator.ValidateCall(validationRequest, traces, changedDigestPayload);

            Assert.That(validationResult.IsSuccess, Is.False);
            Assert.That(validationResult.Failure, Is.Not.Null);
            Assert.That(validationResult.Failure!.Code, Is.EqualTo(PlanTokenErrorCodes.StateChangedSincePlan));
        }

        [Test]
        [Category("Size.Small")]
        public void ValidateCall_WhenTouchedStateChanged_ReturnsStateChangedSincePlan ()
        {
            using var scope = new PlanTokenTestScope();
            var touchedPath = "Assets/Scenes/Main.unity";
            var touchedAbsolutePath = Path.Combine(scope.ProjectRoot, touchedPath.Replace('/', Path.DirectorySeparatorChar));
            var touchedDirectoryPath = Path.GetDirectoryName(touchedAbsolutePath);
            if (!string.IsNullOrWhiteSpace(touchedDirectoryPath))
            {
                Directory.CreateDirectory(touchedDirectoryPath);
            }
            File.WriteAllText(touchedAbsolutePath, "before");

            var environment = scope.CreateEnvironment();
            var coordinator = new PlanTokenCoordinator(environment);
            var request = CreateRequest(
                operations: new[] { ("op-1", UcliPrimitiveOperationNames.Resolve) },
                planToken: null,
                canonicalPayloadJson: "{\"ops\":[],\"protocolVersion\":1}");
            var traces = CreatePlanTraceWithTouched(scope.ProjectRoot, touchedPath);

            var issueResult = coordinator.Issue(request, traces, CreateCompiledDigestPayloadUtf8());
            Assert.That(issueResult.IsSuccess, Is.True);

            File.WriteAllText(touchedAbsolutePath, "after");
            File.SetLastWriteTimeUtc(touchedAbsolutePath, DateTime.UtcNow.AddMinutes(2));

            var validationRequest = request with
            {
                PlanToken = issueResult.PlanToken,
            };
            var validationResult = coordinator.ValidateCall(validationRequest, traces, CreateCompiledDigestPayloadUtf8());

            Assert.That(validationResult.IsSuccess, Is.False);
            Assert.That(validationResult.Failure!.Code, Is.EqualTo(PlanTokenErrorCodes.StateChangedSincePlan));
        }

        [Test]
        [Category("Size.Small")]
        public void ValidateCall_WhenTouchedPathHasLeadingWhitespace_DoesNotTrackTrimmedSiblingPath ()
        {
            using var scope = new PlanTokenTestScope();
            var touchedPath = " Assets/Scenes/Main.unity";
            var siblingPath = "Assets/Scenes/Main.unity";
            var touchedAbsolutePath = Path.Combine(scope.ProjectRoot, touchedPath.Replace('/', Path.DirectorySeparatorChar));
            var siblingAbsolutePath = Path.Combine(scope.ProjectRoot, siblingPath.Replace('/', Path.DirectorySeparatorChar));

            var touchedDirectoryPath = Path.GetDirectoryName(touchedAbsolutePath);
            if (!string.IsNullOrWhiteSpace(touchedDirectoryPath))
            {
                Directory.CreateDirectory(touchedDirectoryPath);
            }

            var siblingDirectoryPath = Path.GetDirectoryName(siblingAbsolutePath);
            if (!string.IsNullOrWhiteSpace(siblingDirectoryPath))
            {
                Directory.CreateDirectory(siblingDirectoryPath);
            }

            File.WriteAllText(touchedAbsolutePath, "touched-before");
            File.WriteAllText(siblingAbsolutePath, "sibling-before");

            var environment = scope.CreateEnvironment();
            var coordinator = new PlanTokenCoordinator(environment);
            var request = CreateRequest(
                operations: new[] { ("op-1", UcliPrimitiveOperationNames.Resolve) },
                planToken: null,
                canonicalPayloadJson: "{\"ops\":[],\"protocolVersion\":1}");
            var traces = CreatePlanTraceWithTouched(scope.ProjectRoot, touchedPath);

            var issueResult = coordinator.Issue(request, traces, CreateCompiledDigestPayloadUtf8());
            Assert.That(issueResult.IsSuccess, Is.True);

            File.WriteAllText(siblingAbsolutePath, "sibling-after");
            File.SetLastWriteTimeUtc(siblingAbsolutePath, DateTime.UtcNow.AddMinutes(2));

            var validationRequest = request with
            {
                PlanToken = issueResult.PlanToken,
            };
            var validationResult = coordinator.ValidateCall(validationRequest, traces, CreateCompiledDigestPayloadUtf8());

            Assert.That(validationResult.IsSuccess, Is.True);
            Assert.That(validationResult.Failure, Is.Null);
        }

        [Test]
        [Category("Size.Small")]
        public void ValidateCall_WhenKeyFileIsCorrupted_RegeneratesKeyAndRejectsOldToken ()
        {
            using var scope = new PlanTokenTestScope();
            var environment = scope.CreateEnvironment();
            var coordinator = new PlanTokenCoordinator(environment);
            var request = CreateRequest(
                operations: new[] { ("op-1", UcliPrimitiveOperationNames.Resolve) },
                planToken: null,
                canonicalPayloadJson: "{\"ops\":[],\"protocolVersion\":1}");
            var traces = CreatePlanTraceWithTouched(scope.ProjectRoot, "Assets/Scenes/Main.unity");

            var issueResult = coordinator.Issue(request, traces, CreateCompiledDigestPayloadUtf8());
            Assert.That(issueResult.IsSuccess, Is.True);

            File.WriteAllText(scope.PlanTokenKeyPath, "broken-key");

            var validationRequest = request with
            {
                PlanToken = issueResult.PlanToken,
            };
            var validationResult = coordinator.ValidateCall(validationRequest, traces, CreateCompiledDigestPayloadUtf8());

            Assert.That(validationResult.IsSuccess, Is.False);
            Assert.That(validationResult.Failure!.Code, Is.EqualTo(PlanTokenErrorCodes.PlanTokenInvalid));

            var regeneratedEncodedKey = File.ReadAllText(scope.PlanTokenKeyPath).Trim();
            Assert.DoesNotThrow(() => Convert.FromBase64String(regeneratedEncodedKey));
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenCancellationRequested_ThrowsOperationCanceledException () => UniTask.ToCoroutine(async () =>
        {
            var operation = new RecordingPhaseOperation(
                validateResult: OperationPhaseStepResult.Success(),
                planResult: OperationPhaseStepResult.Success(),
                callResult: OperationPhaseStepResult.Success());
            var executor = CreateExecutor(operation);
            using var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();
            var request = CreateRequest("op-1", UcliPrimitiveOperationNames.Resolve);

            await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await executor.ExecuteAsync(PhaseExecutionCommand.Call, request, cancellationTokenSource.Token).AsUniTask();
            }, "Canceled operation phase execution", AsyncWaitTimeout);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenPreCallPlanReplayFails_ReturnsReplayFailureAndSkipsRemainingOperations () => UniTask.ToCoroutine(async () =>
        {
            var replayOperation = new ReplayFailingPhaseOperation();
            var secondOperation = new RecordingPhaseOperation(
                validateResult: OperationPhaseStepResult.Success(),
                planResult: OperationPhaseStepResult.Success(),
                callResult: OperationPhaseStepResult.Success(applied: true, changed: false));
            var executor = new OperationPhaseExecutor(CreateRegistry(
                ("ucli.tests.replay-failing", replayOperation),
                (UcliPrimitiveOperationNames.Resolve, secondOperation)));
            var request = CreateRequest(
                ("op-1", "ucli.tests.replay-failing"),
                ("op-2", UcliPrimitiveOperationNames.Resolve));

            var trace = await ExecuteAsync(executor, PhaseExecutionCommand.Call, request, "Replay failure before call execution");

            Assert.That(trace.IsSuccess, Is.False);
            Assert.That(trace.Errors.Count, Is.EqualTo(1));
            Assert.That(trace.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(trace.OperationTraces.Count, Is.EqualTo(2));
            Assert.That(trace.OperationTraces[0].Phase, Is.EqualTo(OperationPhase.Plan));
            Assert.That(trace.OperationTraces[0].Failure, Is.Not.Null);
            Assert.That(trace.OperationTraces[0].Failure!.Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(trace.OperationTraces[1].Phase, Is.EqualTo(OperationPhase.Skipped));
            CollectionAssert.AreEqual(
                new[] { OperationPhase.Validate, OperationPhase.Plan, OperationPhase.Plan },
                replayOperation.CalledPhases);
            CollectionAssert.AreEqual(
                new[] { OperationPhase.Validate, OperationPhase.Plan },
                secondOperation.CalledPhases);
        });

        private static OperationPhaseExecutor CreateExecutor (IUcliOperation operation)
        {
            return new OperationPhaseExecutor(CreateRegistry((UcliPrimitiveOperationNames.Resolve, operation)));
        }

        private static UniTask<PhaseExecutionTrace> ExecuteAsync (
            OperationPhaseExecutor executor,
            PhaseExecutionCommand command,
            NormalizedExecuteRequest request,
            string description,
            CancellationToken cancellationToken = default)
        {
            return TestAwaiter.WaitAsync(
                executor.ExecuteAsync(command, request, cancellationToken).AsUniTask(),
                description,
                AsyncWaitTimeout);
        }

        private static InMemoryPhaseOperationRegistry CreateRegistry (
            params (string Name, IUcliOperation Operation)[] operations)
        {
            var registrations = new UcliOperationRegistration[operations.Length];
            for (var i = 0; i < operations.Length; i++)
            {
                registrations[i] = new UcliOperationRegistration(
                    new UcliOperationMetadata(
                        operationName: operations[i].Name,
                        kind: operations[i].Operation.Metadata.Kind,
                        policy: operations[i].Operation.Metadata.Policy,
                        describeContract: operations[i].Operation.Metadata.DescribeContract,
                        argsType: operations[i].Operation.Metadata.ArgsType,
                        resultType: operations[i].Operation.Metadata.ResultType,
                        requiresPreCallPlanReplay: operations[i].Operation.Metadata.RequiresPreCallPlanReplay),
                    operations[i].Operation);
            }

            return new InMemoryPhaseOperationRegistry(registrations);
        }

        private static UcliOperationDescribeContract CreateDescribeContract (string operationName)
        {
            return new UcliOperationDescribeContract(
                $"{operationName} test operation.",
                Array.Empty<UcliOperationInputContract>(),
                UcliOperationResultContract.NoResult("This test operation does not emit operation-specific result data."),
                new UcliOperationAssuranceContract(
                    Array.Empty<UcliOperationSideEffect>(),
                    mayDirty: false,
                    mayPersist: false,
                    Array.Empty<string>(),
                    UcliOperationPlanMode.ValidationOnly));
        }

        private static NormalizedExecuteRequest CreateRequest (
            string opId,
            string operationName)
        {
            return CreateRequest(
                operations: new[] { (opId, operationName) },
                planToken: null,
                canonicalPayloadJson: "{}");
        }

        private static NormalizedExecuteRequest CreateRequest (
            (string OpId, string Op)[] operations,
            string? planToken,
            string canonicalPayloadJson,
            bool allowDangerous = false)
        {
            var sourceSteps = new List<IpcRequestContractStep>(operations.Length);
            for (var i = 0; i < operations.Length; i++)
            {
                var operation = operations[i];
                sourceSteps.Add(new IpcRequestContractStep(
                    Kind: IpcRequestStepKind.Op,
                    Id: operation.OpId,
                    OperationName: operation.Op,
                    Element: JsonSerializer.SerializeToElement(new
                    {
                        kind = "op",
                        id = operation.OpId,
                        op = operation.Op,
                        args = new { },
                    })));
            }

            return new NormalizedExecuteRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                SourceSteps: sourceSteps,
                AllowDangerous: allowDangerous,
                PlanToken: planToken,
                CanonicalDigestPayloadUtf8: Encoding.UTF8.GetBytes(canonicalPayloadJson));
        }

        private static NormalizedOperation CreateNormalizedOperation (
            string operationId,
            string operationName,
            object args)
        {
            return new NormalizedOperation(
                operationId,
                operationName,
                JsonSerializer.SerializeToElement(args),
                As: null,
                Expect: null);
        }

        private static NormalizedExecuteRequest CreateRequest (
            params (string OpId, string Op)[] operations)
        {
            return CreateRequest(
                operations: operations,
                planToken: null,
                canonicalPayloadJson: "{}");
        }

        private static OperationPhaseTrace[] CreatePlanTraceWithTouched (
            string projectRoot,
            string relativePath)
        {
            var relativePathWithDirectorySeparator = relativePath.Replace('/', Path.DirectorySeparatorChar);
            var absolutePath = Path.Combine(projectRoot, relativePathWithDirectorySeparator);
            var parentDirectory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrWhiteSpace(parentDirectory))
            {
                Directory.CreateDirectory(parentDirectory);
            }

            if (!File.Exists(absolutePath))
            {
                File.WriteAllText(absolutePath, "seed");
            }

            return new[]
            {
                new OperationPhaseTrace(
                    OpId: "op-1",
                    Op: UcliPrimitiveOperationNames.Resolve,
                    Phase: OperationPhase.Plan,
                    Applied: false,
                    Changed: false,
                    Touched: new[]
                    {
                        new OperationTouch(OperationTouchKind.Scene, relativePath, "11111111111111111111111111111111"),
                    },
                    Failure: null),
            };
        }

        private static ReadOnlyMemory<byte> CreateCompiledDigestPayloadUtf8 ()
        {
            return Encoding.UTF8.GetBytes("{\"ops\":[],\"steps\":[]}");
        }

        private static byte[] ReadSigningKey (string planTokenKeyPath)
        {
            var encodedKey = File.ReadAllText(planTokenKeyPath).Trim();
            return Convert.FromBase64String(encodedKey);
        }

        private static string CreateLegacyPlanTokenWithoutCompiledExecutionDigest (
            byte[] signingKey,
            int version,
            string keyId,
            string projectFingerprint,
            string requestDigest,
            string stateFingerprint,
            DateTimeOffset issuedAtUtc,
            DateTimeOffset expiresAtUtc,
            string nonce)
        {
            var headerBytes = Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"kid\":\"v1\",\"typ\":\"ucli-plan-token\"}");
            var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(new
            {
                v = version,
                kid = keyId,
                projectFingerprint,
                requestDigest,
                stateFingerprint,
                issuedAtUtc = issuedAtUtc.ToUniversalTime().ToString("O"),
                expiresAtUtc = expiresAtUtc.ToUniversalTime().ToString("O"),
                nonce,
            });
            var headerSegment = Base64UrlCodec.Encode(headerBytes);
            var payloadSegment = Base64UrlCodec.Encode(payloadBytes);
            var signingInput = headerSegment + "." + payloadSegment;
            var signingInputBytes = Encoding.UTF8.GetBytes(signingInput);

            using var hmac = new HMACSHA256(signingKey);
            var signatureBytes = hmac.ComputeHash(signingInputBytes);
            var signatureSegment = Base64UrlCodec.Encode(signatureBytes);
            return signingInput + "." + signatureSegment;
        }

        private sealed class StubPlanTokenCoordinator : IPlanTokenCoordinator
        {
            private readonly Func<NormalizedExecuteRequest, PlanTokenIssueResult> issueResultFactory;

            private readonly Func<NormalizedExecuteRequest, PlanTokenValidationResult> requestValidationResultFactory;

            private readonly Func<NormalizedExecuteRequest, PlanTokenValidationResult> validationResultFactory;

            public StubPlanTokenCoordinator (
                Func<NormalizedExecuteRequest, PlanTokenIssueResult> issueResultFactory,
                Func<NormalizedExecuteRequest, PlanTokenValidationResult> requestValidationResultFactory,
                Func<NormalizedExecuteRequest, PlanTokenValidationResult> validationResultFactory)
            {
                this.issueResultFactory = issueResultFactory ?? throw new ArgumentNullException(nameof(issueResultFactory));
                this.requestValidationResultFactory = requestValidationResultFactory ?? throw new ArgumentNullException(nameof(requestValidationResultFactory));
                this.validationResultFactory = validationResultFactory ?? throw new ArgumentNullException(nameof(validationResultFactory));
            }

            public int IssueCallCount { get; private set; }

            public int ValidateCallRequestCount { get; private set; }

            public int ValidateCallCount { get; private set; }

            public PlanTokenIssueResult Issue (
                NormalizedExecuteRequest request,
                IReadOnlyList<OperationPhaseTrace> operationTraces,
                ReadOnlyMemory<byte> compiledDigestPayloadUtf8,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IssueCallCount++;
                return issueResultFactory(request);
            }

            public PlanTokenValidationResult ValidateCallRequest (
                NormalizedExecuteRequest request,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ValidateCallRequestCount++;
                return requestValidationResultFactory(request);
            }

            public PlanTokenValidationResult ValidateCall (
                NormalizedExecuteRequest request,
                IReadOnlyList<OperationPhaseTrace> operationTraces,
                ReadOnlyMemory<byte> compiledDigestPayloadUtf8,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ValidateCallCount++;
                return validationResultFactory(request);
            }
        }

        private sealed class StubPlanPassExecutor : IOperationPlanPassExecutor
        {
            private readonly PlanPassResult result;

            public StubPlanPassExecutor (PlanPassResult result)
            {
                this.result = result;
            }

            public Task<PlanPassResult> ExecuteAsync (
                NormalizedExecuteRequest request,
                OperationExecutionContext executionContext,
                Func<NormalizedOperation, IUcliOperation, OperationFailure?>? operationPreflight,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(result);
            }
        }

        private sealed class MutablePlanTokenEnvironment : IPlanTokenEnvironment
        {
            public MutablePlanTokenEnvironment (
                PlanTokenEnvironmentSnapshot snapshot,
                DateTimeOffset utcNow)
            {
                Snapshot = snapshot;
                UtcNow = utcNow;
            }

            public PlanTokenEnvironmentSnapshot Snapshot { get; set; }

            public DateTimeOffset UtcNow { get; set; }

            public PlanTokenEnvironmentSnapshot Capture ()
            {
                return Snapshot;
            }
        }

        private sealed class PlanTokenTestScope : IDisposable
        {
            public PlanTokenTestScope ()
            {
                RepositoryRoot = Path.Combine(Path.GetTempPath(), $"ucli-plan-token-tests-{Guid.NewGuid():N}");
                ProjectRoot = Path.Combine(RepositoryRoot, "UnityProject");
                Directory.CreateDirectory(RepositoryRoot);
                Directory.CreateDirectory(ProjectRoot);
                Directory.CreateDirectory(Path.Combine(RepositoryRoot, ".git"));
                Directory.CreateDirectory(Path.Combine(ProjectRoot, "Assets"));
                Directory.CreateDirectory(Path.Combine(ProjectRoot, "ProjectSettings"));
                ProjectFingerprint = UnityProjectFingerprintCalculator.Create(RepositoryRoot, ProjectRoot);
                PlanTokenKeyPath = UcliStoragePathResolver.ResolvePlanTokenKeyPath(RepositoryRoot, ProjectFingerprint);
            }

            public string RepositoryRoot { get; }

            public string ProjectRoot { get; }

            public string ProjectFingerprint { get; }

            public string PlanTokenKeyPath { get; }

            public MutablePlanTokenEnvironment CreateEnvironment ()
            {
                var snapshot = new PlanTokenEnvironmentSnapshot(
                    ProjectRoot: ProjectRoot,
                    RepositoryRoot: RepositoryRoot,
                    ProjectFingerprint: ProjectFingerprint,
                    UnityVersion: "6000.0.0f1",
                    CompileState: IpcCompileStateCodec.Ready,
                    DomainReloadGeneration: "na");
                return new MutablePlanTokenEnvironment(snapshot, DateTimeOffset.UtcNow);
            }

            public void WriteConfigJson (string json)
            {
                var configDirectoryPath = UcliStoragePathResolver.ResolveUcliDirectoryPath(RepositoryRoot);
                Directory.CreateDirectory(configDirectoryPath);
                File.WriteAllText(UcliStoragePathResolver.ResolveConfigPath(RepositoryRoot), json);
            }

            public void Dispose ()
            {
                if (Directory.Exists(RepositoryRoot))
                {
                    Directory.Delete(RepositoryRoot, recursive: true);
                }
            }
        }

        private sealed class RequiredTypedOperation : UcliOperation<RequiredTypedArgs, UcliNoResult>
        {
            public override UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<RequiredTypedArgs, UcliNoResult>(
                operationName: "ucli.tests.required",
                kind: UcliOperationKind.Query,
                policy: OperationPolicy.Safe,
                description: "Test operation requiring one typed arg.",
                assurance: new UcliOperationAssuranceContract(
                    Array.Empty<UcliOperationSideEffect>(),
                    mayDirty: false,
                    mayPersist: false,
                    Array.Empty<string>(),
                    UcliOperationPlanMode.ValidationOnly));

            public bool ValidateBodyCalled { get; private set; }

            public bool PlanBodyCalled { get; private set; }

            public bool CallBodyCalled { get; private set; }

            protected override Task<OperationPhaseStepResult> ValidateAsync (
                NormalizedOperation operation,
                RequiredTypedArgs args,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken)
            {
                ValidateBodyCalled = true;
                return Task.FromResult(OperationPhaseStepResult.Success());
            }

            protected override Task<OperationPhaseStepResult> PlanAsync (
                NormalizedOperation operation,
                RequiredTypedArgs args,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken)
            {
                PlanBodyCalled = true;
                return Task.FromResult(OperationPhaseStepResult.Success());
            }

            protected override Task<OperationPhaseStepResult> CallAsync (
                NormalizedOperation operation,
                RequiredTypedArgs args,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken)
            {
                CallBodyCalled = true;
                return Task.FromResult(OperationPhaseStepResult.Success());
            }
        }

        [UcliDescription("Required typed operation args.")]
        private sealed class RequiredTypedArgs
        {
            [UcliRequired]
            [UcliDescription("Required name.")]
            public string? Name { get; set; }
        }

        private sealed class AliasReferenceTypedOperation : UcliOperation<AliasReferenceTypedArgs, UcliNoResult>
        {
            public override UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<AliasReferenceTypedArgs, UcliNoResult>(
                operationName: "ucli.tests.alias-reference",
                kind: UcliOperationKind.Query,
                policy: OperationPolicy.Safe,
                description: "Test operation accepting one object reference.",
                assurance: new UcliOperationAssuranceContract(
                    Array.Empty<UcliOperationSideEffect>(),
                    mayDirty: false,
                    mayPersist: false,
                    Array.Empty<string>(),
                    UcliOperationPlanMode.ValidationOnly));

            public bool ValidateBodyCalled { get; private set; }

            protected override Task<OperationPhaseStepResult> ValidateAsync (
                NormalizedOperation operation,
                AliasReferenceTypedArgs args,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken)
            {
                ValidateBodyCalled = true;
                return Task.FromResult(OperationPhaseStepResult.Success());
            }

            protected override Task<OperationPhaseStepResult> PlanAsync (
                NormalizedOperation operation,
                AliasReferenceTypedArgs args,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(OperationPhaseStepResult.Success());
            }

            protected override Task<OperationPhaseStepResult> CallAsync (
                NormalizedOperation operation,
                AliasReferenceTypedArgs args,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(OperationPhaseStepResult.Success());
            }
        }

        [UcliDescription("Alias reference operation args.")]
        private sealed class AliasReferenceTypedArgs
        {
            [UcliRequired]
            [UcliDescription("Target GameObject reference.")]
            public GameObjectReferenceArgs? Target { get; set; }
        }

        private sealed class RecordingPhaseOperation : IUcliOperation
        {
            private readonly OperationPhaseStepResult validateResult;
            private readonly OperationPhaseStepResult planResult;
            private readonly OperationPhaseStepResult callResult;

            public RecordingPhaseOperation (
                OperationPhaseStepResult validateResult,
                OperationPhaseStepResult planResult,
                OperationPhaseStepResult callResult,
                OperationPolicy policy = OperationPolicy.Safe)
            {
                this.validateResult = validateResult;
                this.planResult = planResult;
                this.callResult = callResult;
                Metadata = new UcliOperationMetadata(
                    operationName: "ucli.tests.recording",
                    kind: UcliOperationKind.Query,
                    policy: policy,
                    describeContract: CreateDescribeContract("ucli.tests.recording"));
            }

            public UcliOperationMetadata Metadata { get; }

            public List<OperationPhase> CalledPhases { get; } = new List<OperationPhase>();

            public Task<OperationPhaseStepResult> ValidateAsync (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CalledPhases.Add(OperationPhase.Validate);
                return Task.FromResult(validateResult);
            }

            public Task<OperationPhaseStepResult> PlanAsync (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CalledPhases.Add(OperationPhase.Plan);
                return Task.FromResult(planResult);
            }

            public Task<OperationPhaseStepResult> CallAsync (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CalledPhases.Add(OperationPhase.Call);
                return Task.FromResult(callResult);
            }
        }

        private sealed class StatefulPhaseOperation : IUcliOperation
        {
            private string lastPlannedOperationId = string.Empty;

            public UcliOperationMetadata Metadata { get; } = new UcliOperationMetadata(
                operationName: "ucli.tests.stateful",
                kind: UcliOperationKind.Query,
                policy: OperationPolicy.Safe,
                describeContract: CreateDescribeContract("ucli.tests.stateful"),
                argsType: typeof(UcliEmptyArgs),
                resultType: typeof(UcliNoResult),
                requiresPreCallPlanReplay: true);

            public Task<OperationPhaseStepResult> ValidateAsync (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(OperationPhaseStepResult.Success());
            }

            public Task<OperationPhaseStepResult> PlanAsync (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                lastPlannedOperationId = operation.Id;
                return Task.FromResult(OperationPhaseStepResult.Success());
            }

            public Task<OperationPhaseStepResult> CallAsync (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!string.Equals(lastPlannedOperationId, operation.Id, StringComparison.Ordinal))
                {
                    return Task.FromResult(OperationPhaseStepResult.Failed(new OperationFailure(
                        Code: UcliCoreErrorCodes.InternalError,
                        Message: "Call phase was not adjacent to the latest plan of the same operation.",
                        OpId: operation.Id)));
                }

                return Task.FromResult(OperationPhaseStepResult.Success());
            }
        }

        private sealed class ReplayFailingPhaseOperation : IUcliOperation
        {
            private int planCallCount;

            public UcliOperationMetadata Metadata { get; } = new UcliOperationMetadata(
                operationName: "ucli.tests.replay-failing",
                kind: UcliOperationKind.Mutation,
                policy: OperationPolicy.Advanced,
                describeContract: CreateDescribeContract("ucli.tests.replay-failing"),
                argsType: typeof(UcliEmptyArgs),
                resultType: typeof(UcliNoResult),
                requiresPreCallPlanReplay: true);

            public List<OperationPhase> CalledPhases { get; } = new List<OperationPhase>();

            public Task<OperationPhaseStepResult> ValidateAsync (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CalledPhases.Add(OperationPhase.Validate);
                return Task.FromResult(OperationPhaseStepResult.Success());
            }

            public Task<OperationPhaseStepResult> PlanAsync (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CalledPhases.Add(OperationPhase.Plan);
                planCallCount++;
                if (planCallCount >= 2)
                {
                    return Task.FromResult(OperationPhaseStepResult.Failed(new OperationFailure(
                        Code: UcliCoreErrorCodes.InvalidArgument,
                        Message: "Replay plan failed.",
                        OpId: operation.Id)));
                }

                return Task.FromResult(OperationPhaseStepResult.Success(applied: false, changed: true));
            }

            public Task<OperationPhaseStepResult> CallAsync (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CalledPhases.Add(OperationPhase.Call);
                return Task.FromResult(OperationPhaseStepResult.Success(applied: true, changed: true));
            }
        }

        private sealed class ContextCapturingPhaseOperation : IUcliOperation
        {
            public UcliOperationMetadata Metadata { get; } = new UcliOperationMetadata(
                operationName: "ucli.tests.context",
                kind: UcliOperationKind.Query,
                policy: OperationPolicy.Safe,
                describeContract: CreateDescribeContract("ucli.tests.context"));

            public OperationExecutionContext? ValidateContext { get; private set; }

            public OperationExecutionContext? PlanContext { get; private set; }

            public OperationExecutionContext? CallContext { get; private set; }

            public Task<OperationPhaseStepResult> ValidateAsync (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ValidateContext = executionContext;
                return Task.FromResult(OperationPhaseStepResult.Success());
            }

            public Task<OperationPhaseStepResult> PlanAsync (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PlanContext = executionContext;
                return Task.FromResult(OperationPhaseStepResult.Success());
            }

            public Task<OperationPhaseStepResult> CallAsync (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CallContext = executionContext;
                return Task.FromResult(OperationPhaseStepResult.Success(applied: true, changed: false));
            }
        }
    }
}
