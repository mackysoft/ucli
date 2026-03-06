using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Project;
using MackySoft.Ucli.Contracts.Storage;
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
                    ("ucli.resolve", first),
                    ("ucli.resolve", second));
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

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenCommandIsPlan_ExecutesValidateAndPlanOnly () => UniTask.ToCoroutine(async () =>
        {
            var operation = new RecordingPhaseOperation(
                validateResult: OperationPhaseStepResult.Success(),
                planResult: OperationPhaseStepResult.Success(applied: false, changed: true),
                callResult: OperationPhaseStepResult.Success(applied: true, changed: true));
            var executor = CreateExecutor(operation);
            var request = CreateRequest("op-1", "ucli.resolve");

            var trace = await executor.Execute(PhaseExecutionCommand.Plan, request).AsUniTask();

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
            var request = CreateRequest("op-1", "ucli.resolve");

            var trace = await executor.Execute(PhaseExecutionCommand.Call, request).AsUniTask();

            CollectionAssert.AreEqual(new[] { OperationPhase.Validate, OperationPhase.Plan, OperationPhase.Call }, operation.CalledPhases);
            Assert.That(trace.IsSuccess, Is.True);
            Assert.That(trace.OperationTraces[0].Phase, Is.EqualTo(OperationPhase.Call));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenCallContainsDuplicateOperationNames_ReplaysPlanBeforeEachCall () => UniTask.ToCoroutine(async () =>
        {
            var operation = new StatefulPhaseOperation();
            var executor = CreateExecutor(operation);
            var request = CreateRequest(
                ("op-1", "ucli.resolve"),
                ("op-2", "ucli.resolve"));

            var trace = await executor.Execute(PhaseExecutionCommand.Call, request).AsUniTask();

            Assert.That(trace.IsSuccess, Is.True);
            Assert.That(trace.OperationTraces.Count, Is.EqualTo(2));
            Assert.That(trace.OperationTraces[0].Phase, Is.EqualTo(OperationPhase.Call));
            Assert.That(trace.OperationTraces[1].Phase, Is.EqualTo(OperationPhase.Call));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenCommandIsCall_UsesSharedExecutionContextAcrossAllPhases () => UniTask.ToCoroutine(async () =>
        {
            var operation = new ContextCapturingPhaseOperation();
            var executor = CreateExecutor(operation);
            var request = CreateRequest("op-1", "ucli.resolve");

            var trace = await executor.Execute(PhaseExecutionCommand.Call, request).AsUniTask();

            Assert.That(trace.IsSuccess, Is.True);
            Assert.That(operation.ValidateContext, Is.Not.Null);
            Assert.That(operation.ValidateContext, Is.SameAs(operation.PlanContext));
            Assert.That(operation.PlanContext, Is.SameAs(operation.CallContext));
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
                validationResultFactory: _ => PlanTokenValidationResult.Success());
            var executor = new OperationPhaseExecutor(
                CreateRegistry(("ucli.resolve", operation)),
                coordinator);
            var request = CreateRequest("op-1", "ucli.resolve");

            var trace = await executor.Execute(PhaseExecutionCommand.Plan, request).AsUniTask();

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
                validationResultFactory: _ => PlanTokenValidationResult.Failed(new OperationFailure(
                    Code: IpcErrorCodes.PlanTokenInvalid,
                    Message: "invalid token",
                    OpId: null)));
            var executor = new OperationPhaseExecutor(
                CreateRegistry(
                    ("ucli.resolve", firstOperation),
                    ("ucli.scene.open", secondOperation)),
                coordinator);
            var request = CreateRequest(
                ("op-1", "ucli.resolve"),
                ("op-2", "ucli.scene.open"));

            var trace = await executor.Execute(PhaseExecutionCommand.Call, request).AsUniTask();

            Assert.That(trace.IsSuccess, Is.False);
            Assert.That(trace.Errors.Count, Is.EqualTo(1));
            Assert.That(trace.Errors[0].Code, Is.EqualTo(IpcErrorCodes.PlanTokenInvalid));
            Assert.That(trace.OperationTraces[0].Phase, Is.EqualTo(OperationPhase.Plan));
            Assert.That(trace.OperationTraces[1].Phase, Is.EqualTo(OperationPhase.Plan));
            CollectionAssert.AreEqual(new[] { OperationPhase.Validate, OperationPhase.Plan }, firstOperation.CalledPhases);
            CollectionAssert.AreEqual(new[] { OperationPhase.Validate, OperationPhase.Plan }, secondOperation.CalledPhases);
            Assert.That(coordinator.ValidateCallCount, Is.EqualTo(1));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenValidateFails_MarksRemainingOperationsAsSkipped () => UniTask.ToCoroutine(async () =>
        {
            var failingOperation = new RecordingPhaseOperation(
                validateResult: OperationPhaseStepResult.Failed(new OperationFailure(IpcErrorCodes.InvalidArgument, "invalid", "op-1")),
                planResult: OperationPhaseStepResult.Success(),
                callResult: OperationPhaseStepResult.Success());
            var skippedOperation = new RecordingPhaseOperation(
                validateResult: OperationPhaseStepResult.Success(),
                planResult: OperationPhaseStepResult.Success(),
                callResult: OperationPhaseStepResult.Success());
            var executor = new OperationPhaseExecutor(CreateRegistry(
                ("ucli.resolve", failingOperation),
                ("ucli.scene.open", skippedOperation)));
            var request = CreateRequest(
                ("op-1", "ucli.resolve"),
                ("op-2", "ucli.scene.open"));

            var trace = await executor.Execute(PhaseExecutionCommand.Call, request).AsUniTask();

            Assert.That(trace.IsSuccess, Is.False);
            Assert.That(trace.OperationTraces[0].Phase, Is.EqualTo(OperationPhase.Validate));
            Assert.That(trace.OperationTraces[0].Failure, Is.Not.Null);
            Assert.That(trace.OperationTraces[1].Phase, Is.EqualTo(OperationPhase.Skipped));
            Assert.That(trace.OperationTraces[1].Applied, Is.False);
            Assert.That(trace.OperationTraces[1].Changed, Is.False);
            Assert.That(skippedOperation.CalledPhases.Count, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenPlanFails_MarksRemainingOperationsAsSkipped () => UniTask.ToCoroutine(async () =>
        {
            var failingOperation = new RecordingPhaseOperation(
                validateResult: OperationPhaseStepResult.Success(),
                planResult: OperationPhaseStepResult.Failed(new OperationFailure(IpcErrorCodes.InvalidArgument, "plan failed", "op-1")),
                callResult: OperationPhaseStepResult.Success());
            var skippedOperation = new RecordingPhaseOperation(
                validateResult: OperationPhaseStepResult.Success(),
                planResult: OperationPhaseStepResult.Success(),
                callResult: OperationPhaseStepResult.Success());
            var executor = new OperationPhaseExecutor(CreateRegistry(
                ("ucli.resolve", failingOperation),
                ("ucli.scene.open", skippedOperation)));
            var request = CreateRequest(
                ("op-1", "ucli.resolve"),
                ("op-2", "ucli.scene.open"));

            var trace = await executor.Execute(PhaseExecutionCommand.Call, request).AsUniTask();

            Assert.That(trace.IsSuccess, Is.False);
            Assert.That(trace.OperationTraces[0].Phase, Is.EqualTo(OperationPhase.Plan));
            Assert.That(trace.OperationTraces[1].Phase, Is.EqualTo(OperationPhase.Skipped));
            Assert.That(skippedOperation.CalledPhases.Count, Is.EqualTo(0));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Execute_WhenCallFails_MarksRemainingOperationsAsSkipped () => UniTask.ToCoroutine(async () =>
        {
            var failingOperation = new RecordingPhaseOperation(
                validateResult: OperationPhaseStepResult.Success(),
                planResult: OperationPhaseStepResult.Success(),
                callResult: OperationPhaseStepResult.Failed(new OperationFailure(IpcErrorCodes.InvalidArgument, "call failed", "op-1")));
            var skippedOperation = new RecordingPhaseOperation(
                validateResult: OperationPhaseStepResult.Success(),
                planResult: OperationPhaseStepResult.Success(),
                callResult: OperationPhaseStepResult.Success());
            var executor = new OperationPhaseExecutor(CreateRegistry(
                ("ucli.resolve", failingOperation),
                ("ucli.scene.open", skippedOperation)));
            var request = CreateRequest(
                ("op-1", "ucli.resolve"),
                ("op-2", "ucli.scene.open"));

            var trace = await executor.Execute(PhaseExecutionCommand.Call, request).AsUniTask();

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
                operations: new[] { ("op-1", "ucli.resolve") },
                planToken: null,
                canonicalPayloadJson: "{\"ops\":[],\"protocolVersion\":1}");
            var traces = CreatePlanTraceWithTouched(scope.ProjectRoot, "Assets/Scenes/Main.unity");

            var result = coordinator.ValidateCall(request, traces);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Failure, Is.Not.Null);
            Assert.That(result.Failure!.Code, Is.EqualTo(IpcErrorCodes.PlanTokenRequired));
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
                operations: new[] { ("op-1", "ucli.resolve") },
                planToken: null,
                canonicalPayloadJson: "{\"ops\":[],\"protocolVersion\":1}");
            var traces = CreatePlanTraceWithTouched(scope.ProjectRoot, "Assets/Scenes/Main.unity");

            var issueResult = coordinator.Issue(request, traces);

            Assert.That(issueResult.IsSuccess, Is.True);
            Assert.That(issueResult.PlanToken, Is.Not.Null.And.Not.Empty);
            Assert.That(File.Exists(scope.PlanTokenKeyPath), Is.True);
        }

        [Test]
        [Category("Size.Small")]
        public void ValidateCall_WhenTokenExpired_ReturnsPlanTokenExpired ()
        {
            using var scope = new PlanTokenTestScope();
            var environment = scope.CreateEnvironment();
            var coordinator = new PlanTokenCoordinator(environment);
            var request = CreateRequest(
                operations: new[] { ("op-1", "ucli.resolve") },
                planToken: null,
                canonicalPayloadJson: "{\"ops\":[],\"protocolVersion\":1}");
            var traces = CreatePlanTraceWithTouched(scope.ProjectRoot, "Assets/Scenes/Main.unity");

            var issueResult = coordinator.Issue(request, traces);
            Assert.That(issueResult.IsSuccess, Is.True);

            environment.UtcNow = environment.UtcNow.AddMinutes(16).AddSeconds(31);
            var validationRequest = request with
            {
                PlanToken = issueResult.PlanToken,
            };

            var validationResult = coordinator.ValidateCall(validationRequest, traces);

            Assert.That(validationResult.IsSuccess, Is.False);
            Assert.That(validationResult.Failure!.Code, Is.EqualTo(IpcErrorCodes.PlanTokenExpired));
        }

        [Test]
        [Category("Size.Small")]
        public void ValidateCall_WhenRequestDigestDiffers_ReturnsPlanTokenRequestMismatch ()
        {
            using var scope = new PlanTokenTestScope();
            var environment = scope.CreateEnvironment();
            var coordinator = new PlanTokenCoordinator(environment);
            var originalRequest = CreateRequest(
                operations: new[] { ("op-1", "ucli.resolve") },
                planToken: null,
                canonicalPayloadJson: "{\"ops\":[{\"id\":\"op-1\"}],\"protocolVersion\":1}");
            var traces = CreatePlanTraceWithTouched(scope.ProjectRoot, "Assets/Scenes/Main.unity");

            var issueResult = coordinator.Issue(originalRequest, traces);
            Assert.That(issueResult.IsSuccess, Is.True);

            var modifiedRequest = CreateRequest(
                operations: new[] { ("op-1", "ucli.resolve") },
                planToken: issueResult.PlanToken,
                canonicalPayloadJson: "{\"ops\":[{\"id\":\"op-2\"}],\"protocolVersion\":1}");
            var validationResult = coordinator.ValidateCall(modifiedRequest, traces);

            Assert.That(validationResult.IsSuccess, Is.False);
            Assert.That(validationResult.Failure!.Code, Is.EqualTo(IpcErrorCodes.PlanTokenRequestMismatch));
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
                operations: new[] { ("op-1", "ucli.resolve") },
                planToken: null,
                canonicalPayloadJson: "{\"ops\":[],\"protocolVersion\":1}");
            var traces = CreatePlanTraceWithTouched(scope.ProjectRoot, touchedPath);

            var issueResult = coordinator.Issue(request, traces);
            Assert.That(issueResult.IsSuccess, Is.True);

            File.WriteAllText(touchedAbsolutePath, "after");
            File.SetLastWriteTimeUtc(touchedAbsolutePath, DateTime.UtcNow.AddMinutes(2));

            var validationRequest = request with
            {
                PlanToken = issueResult.PlanToken,
            };
            var validationResult = coordinator.ValidateCall(validationRequest, traces);

            Assert.That(validationResult.IsSuccess, Is.False);
            Assert.That(validationResult.Failure!.Code, Is.EqualTo(IpcErrorCodes.StateChangedSincePlan));
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
                operations: new[] { ("op-1", "ucli.resolve") },
                planToken: null,
                canonicalPayloadJson: "{\"ops\":[],\"protocolVersion\":1}");
            var traces = CreatePlanTraceWithTouched(scope.ProjectRoot, touchedPath);

            var issueResult = coordinator.Issue(request, traces);
            Assert.That(issueResult.IsSuccess, Is.True);

            File.WriteAllText(siblingAbsolutePath, "sibling-after");
            File.SetLastWriteTimeUtc(siblingAbsolutePath, DateTime.UtcNow.AddMinutes(2));

            var validationRequest = request with
            {
                PlanToken = issueResult.PlanToken,
            };
            var validationResult = coordinator.ValidateCall(validationRequest, traces);

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
                operations: new[] { ("op-1", "ucli.resolve") },
                planToken: null,
                canonicalPayloadJson: "{\"ops\":[],\"protocolVersion\":1}");
            var traces = CreatePlanTraceWithTouched(scope.ProjectRoot, "Assets/Scenes/Main.unity");

            var issueResult = coordinator.Issue(request, traces);
            Assert.That(issueResult.IsSuccess, Is.True);

            File.WriteAllText(scope.PlanTokenKeyPath, "broken-key");

            var validationRequest = request with
            {
                PlanToken = issueResult.PlanToken,
            };
            var validationResult = coordinator.ValidateCall(validationRequest, traces);

            Assert.That(validationResult.IsSuccess, Is.False);
            Assert.That(validationResult.Failure!.Code, Is.EqualTo(IpcErrorCodes.PlanTokenInvalid));

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
            var request = CreateRequest("op-1", "ucli.resolve");

            await AsyncExceptionCapture.CaptureAsync<OperationCanceledException>(async () =>
            {
                await executor.Execute(PhaseExecutionCommand.Call, request, cancellationTokenSource.Token).AsUniTask();
            });
        });

        private static OperationPhaseExecutor CreateExecutor (IUcliOperation operation)
        {
            return new OperationPhaseExecutor(CreateRegistry(("ucli.resolve", operation)));
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
                        kind: UcliOperationKind.Query,
                        policy: OperationPolicy.Safe),
                    operations[i].Operation);
            }

            return new InMemoryPhaseOperationRegistry(registrations);
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
            string canonicalPayloadJson)
        {
            var normalizedOperations = new List<NormalizedOperation>(operations.Length);
            for (var i = 0; i < operations.Length; i++)
            {
                var operation = operations[i];
                normalizedOperations.Add(new NormalizedOperation(
                    Id: operation.OpId,
                    Op: operation.Op,
                    Args: JsonSerializer.SerializeToElement(new { }),
                    As: null,
                    Expect: null));
            }

            return new NormalizedExecuteRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                Ops: normalizedOperations,
                PlanToken: planToken,
                CanonicalDigestPayloadUtf8: Encoding.UTF8.GetBytes(canonicalPayloadJson));
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
                    Op: "ucli.resolve",
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

        private sealed class StubPlanTokenCoordinator : IPlanTokenCoordinator
        {
            private readonly Func<NormalizedExecuteRequest, PlanTokenIssueResult> issueResultFactory;

            private readonly Func<NormalizedExecuteRequest, PlanTokenValidationResult> validationResultFactory;

            public StubPlanTokenCoordinator (
                Func<NormalizedExecuteRequest, PlanTokenIssueResult> issueResultFactory,
                Func<NormalizedExecuteRequest, PlanTokenValidationResult> validationResultFactory)
            {
                this.issueResultFactory = issueResultFactory ?? throw new ArgumentNullException(nameof(issueResultFactory));
                this.validationResultFactory = validationResultFactory ?? throw new ArgumentNullException(nameof(validationResultFactory));
            }

            public int IssueCallCount { get; private set; }

            public int ValidateCallCount { get; private set; }

            public PlanTokenIssueResult Issue (
                NormalizedExecuteRequest request,
                IReadOnlyList<OperationPhaseTrace> operationTraces,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IssueCallCount++;
                return issueResultFactory(request);
            }

            public PlanTokenValidationResult ValidateCall (
                NormalizedExecuteRequest request,
                IReadOnlyList<OperationPhaseTrace> operationTraces,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ValidateCallCount++;
                return validationResultFactory(request);
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

        private sealed class RecordingPhaseOperation : IUcliOperation
        {
            private readonly OperationPhaseStepResult validateResult;
            private readonly OperationPhaseStepResult planResult;
            private readonly OperationPhaseStepResult callResult;

            public RecordingPhaseOperation (
                OperationPhaseStepResult validateResult,
                OperationPhaseStepResult planResult,
                OperationPhaseStepResult callResult)
            {
                this.validateResult = validateResult;
                this.planResult = planResult;
                this.callResult = callResult;
            }

            public UcliOperationMetadata Metadata { get; } = new UcliOperationMetadata(
                operationName: "ucli.tests.recording",
                kind: UcliOperationKind.Query,
                policy: OperationPolicy.Safe);

            public List<OperationPhase> CalledPhases { get; } = new List<OperationPhase>();

            public Task<OperationPhaseStepResult> Validate (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CalledPhases.Add(OperationPhase.Validate);
                return Task.FromResult(validateResult);
            }

            public Task<OperationPhaseStepResult> Plan (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CalledPhases.Add(OperationPhase.Plan);
                return Task.FromResult(planResult);
            }

            public Task<OperationPhaseStepResult> Call (
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
                policy: OperationPolicy.Safe);

            public Task<OperationPhaseStepResult> Validate (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(OperationPhaseStepResult.Success());
            }

            public Task<OperationPhaseStepResult> Plan (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                lastPlannedOperationId = operation.Id;
                return Task.FromResult(OperationPhaseStepResult.Success());
            }

            public Task<OperationPhaseStepResult> Call (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!string.Equals(lastPlannedOperationId, operation.Id, StringComparison.Ordinal))
                {
                    return Task.FromResult(OperationPhaseStepResult.Failed(new OperationFailure(
                        Code: IpcErrorCodes.InternalError,
                        Message: "Call phase was not adjacent to the latest plan of the same operation.",
                        OpId: operation.Id)));
                }

                return Task.FromResult(OperationPhaseStepResult.Success());
            }
        }

        private sealed class ContextCapturingPhaseOperation : IUcliOperation
        {
            public UcliOperationMetadata Metadata { get; } = new UcliOperationMetadata(
                operationName: "ucli.tests.context",
                kind: UcliOperationKind.Query,
                policy: OperationPolicy.Safe);

            public OperationExecutionContext? ValidateContext { get; private set; }

            public OperationExecutionContext? PlanContext { get; private set; }

            public OperationExecutionContext? CallContext { get; private set; }

            public Task<OperationPhaseStepResult> Validate (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ValidateContext = executionContext;
                return Task.FromResult(OperationPhaseStepResult.Success());
            }

            public Task<OperationPhaseStepResult> Plan (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PlanContext = executionContext;
                return Task.FromResult(OperationPhaseStepResult.Success());
            }

            public Task<OperationPhaseStepResult> Call (
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
