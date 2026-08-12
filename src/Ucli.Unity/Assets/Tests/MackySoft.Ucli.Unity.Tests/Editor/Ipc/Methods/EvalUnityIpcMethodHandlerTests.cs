using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Projects;
using MackySoft.Ucli.Infrastructure.Project;
using MackySoft.Ucli.Infrastructure.Execution.ReadPostcondition;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Execution.CsEval;
using MackySoft.Ucli.Unity.Execution.PlanToken;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;
using NUnit.Framework;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class EvalUnityIpcMethodHandlerTests
    {
        [Test]
        [Category("Size.Small")]
        public async Task PlanThenCall_ExecutesOnlyDuringCall_AndRejectsReplay ()
        {
            using var scope = new EvalTestScope();
            var environment = scope.CreateEnvironment();
            var planHandler = new EvalPlanUnityIpcMethodHandler(CreateCompilationService(), new StubUnityEditorReadinessGate(), scope.Project, environment);
            var callHandler = new EvalCallUnityIpcMethodHandler(
                CreateCompilationService(),
                new CsEvalEntryPointReflectionResolver(),
                new CsEvalReturnValueSerializer(),
                new StubUnityEditorReadinessGate(),
                scope.Project,
                environment,
                new MutationReadPostconditionJournal(),
                new StubMutationLaneControl());
            const string source = "throw new System.InvalidOperationException(\"call-only sentinel\");";
            var planResponse = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                planHandler,
                CreateRequest(UnityIpcMethod.EvalPlan, new IpcEvalPlanRequest(source, CsEvalSourceKind.Snippet, true, false)));

            Assert.That(planResponse.Status, Is.EqualTo(IpcResponseStatus.Ok));
            Assert.That(IpcPayloadCodec.TryDeserializeStrict(planResponse.Payload, out IpcEvalResponse plan, out var planError), Is.True, planError.Message);
            Assert.That(plan.Phase, Is.EqualTo(CsEvalPhase.Plan));
            Assert.That(plan.PlanToken, Is.Not.Null.And.Not.Empty);

            var callRequest = new IpcEvalCallRequest(source, CsEvalSourceKind.Snippet, true, false, plan.PlanToken!);
            var callResponse = await UnityIpcMethodHandlerTestInvoker.HandleAsync(callHandler, CreateRequest(UnityIpcMethod.EvalCall, callRequest));

            Assert.That(callResponse.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(IpcPayloadCodec.TryDeserializeStrict(callResponse.Payload, out IpcEvalErrorResponse callError, out var decodeError), Is.True, decodeError.Message);
            Assert.That(callError.ApplicationState, Is.EqualTo(ExecutionApplicationState.Indeterminate));
            Assert.That(callError.ReadPostcondition, Is.Not.Null);
            Assert.That(callError.ReadPostcondition!.Requirements, Has.Count.EqualTo(3));

            var replayResponse = await UnityIpcMethodHandlerTestInvoker.HandleAsync(callHandler, CreateRequest(UnityIpcMethod.EvalCall, callRequest));
            Assert.That(replayResponse.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(IpcPayloadCodec.TryDeserializeStrict(replayResponse.Payload, out IpcEvalErrorResponse replay, out var replayError), Is.True, replayError.Message);
            Assert.That(replay.ApplicationState, Is.EqualTo(ExecutionApplicationState.NotApplied));
        }

        [Test]
        [Category("Size.Small")]
        public async Task Call_WhenTypeInitializerThrows_ReturnsIndeterminateWithReadPostcondition ()
        {
            using var scope = new EvalTestScope();
            var environment = scope.CreateEnvironment();
            const string source = @"
public static class TypeInitializerFailureEval
{
    static TypeInitializerFailureEval () =>
        throw new System.InvalidOperationException(""type initializer failure"");

    public static object? Run (MackySoft.Ucli.Unity.Execution.CsEval.UcliCsEvalContext context)
    {
        context.DeclareNoChanges();
        return null;
    }
}";
            var planResponse = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                CreatePlanHandler(scope, environment),
                CreateRequest(UnityIpcMethod.EvalPlan, new IpcEvalPlanRequest(source, CsEvalSourceKind.CompilationUnit, true, false)));
            Assert.That(planResponse.Status, Is.EqualTo(IpcResponseStatus.Ok));
            Assert.That(IpcPayloadCodec.TryDeserializeStrict(planResponse.Payload, out IpcEvalResponse plan, out var planError), Is.True, planError.Message);

            var callResponse = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                CreateCallHandler(scope, environment),
                CreateRequest(UnityIpcMethod.EvalCall, new IpcEvalCallRequest(source, CsEvalSourceKind.CompilationUnit, true, false, plan.PlanToken!)));

            Assert.That(callResponse.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(IpcPayloadCodec.TryDeserializeStrict(callResponse.Payload, out IpcEvalErrorResponse callError, out var decodeError), Is.True, decodeError.Message);
            Assert.That(callError.ApplicationState, Is.EqualTo(ExecutionApplicationState.Indeterminate));
            Assert.That(callError.ReadPostcondition, Is.Not.Null);
            Assert.That(callError.ReadPostcondition!.Requirements, Has.Count.EqualTo(3));
        }

        [Test]
        [Category("Size.Small")]
        public async Task Plan_WhenEvalIsDisabled_ReturnsNotAppliedStructuredError ()
        {
            using var scope = new EvalTestScope(evalEnabled: false);
            var handler = new EvalPlanUnityIpcMethodHandler(CreateCompilationService(), new StubUnityEditorReadinessGate(), scope.Project, scope.CreateEnvironment());

            var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                handler,
                CreateRequest(UnityIpcMethod.EvalPlan, new IpcEvalPlanRequest("return null;", CsEvalSourceKind.Snippet, true, false)));

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(IpcPayloadCodec.TryDeserializeStrict(response.Payload, out IpcEvalErrorResponse error, out var decodeError), Is.True, decodeError.Message);
            Assert.That(error.ApplicationState, Is.EqualTo(ExecutionApplicationState.NotApplied));
        }

        [Test]
        [Category("Size.Small")]
        public async Task Plan_WhenConfigMissesRequiredProperty_FailsClosedBeforeReadiness ()
        {
            using var scope = new EvalTestScope("{\"operationPolicy\":\"safe\",\"planTokenMode\":\"optional\",\"operationAllowlist\":[\"^ucli\\\\.\"],\"evalEnabled\":true}");
            var readinessGate = new StubUnityEditorReadinessGate();

            var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                new EvalPlanUnityIpcMethodHandler(CreateCompilationService(), readinessGate, scope.Project, scope.CreateEnvironment()),
                CreateRequest(UnityIpcMethod.EvalPlan, new IpcEvalPlanRequest("context.DeclareNoChanges();", CsEvalSourceKind.Snippet, true, false)));

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(readinessGate.CallCount, Is.Zero);
        }

        [Test]
        [Category("Size.Small")]
        public async Task Plan_WhenConfigContainsUnknownProperty_FailsClosedBeforeReadiness ()
        {
            using var scope = new EvalTestScope("{\"schemaVersion\":1,\"operationPolicy\":\"safe\",\"planTokenMode\":\"optional\",\"operationAllowlist\":[\"^ucli\\\\.\"],\"evalEnabled\":true,\"unknown\":true}");
            var readinessGate = new StubUnityEditorReadinessGate();

            var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                new EvalPlanUnityIpcMethodHandler(CreateCompilationService(), readinessGate, scope.Project, scope.CreateEnvironment()),
                CreateRequest(UnityIpcMethod.EvalPlan, new IpcEvalPlanRequest("context.DeclareNoChanges();", CsEvalSourceKind.Snippet, true, false)));

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(readinessGate.CallCount, Is.Zero);
        }

        [Test]
        [Category("Size.Small")]
        public async Task Plan_WhenConfigHasEvalEnabledTypeMismatch_FailsClosedBeforeReadiness ()
        {
            using var scope = new EvalTestScope("{\"schemaVersion\":1,\"operationPolicy\":\"safe\",\"planTokenMode\":\"optional\",\"operationAllowlist\":[\"^ucli\\\\.\"],\"evalEnabled\":\"true\"}");
            var readinessGate = new StubUnityEditorReadinessGate();

            var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                new EvalPlanUnityIpcMethodHandler(CreateCompilationService(), readinessGate, scope.Project, scope.CreateEnvironment()),
                CreateRequest(UnityIpcMethod.EvalPlan, new IpcEvalPlanRequest("context.DeclareNoChanges();", CsEvalSourceKind.Snippet, true, false)));

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(readinessGate.CallCount, Is.Zero);
        }

        [Test]
        [Category("Size.Small")]
        public async Task Plan_WhenConfigCannotBeRead_ReturnsInternalErrorBeforeReadiness ()
        {
            using var scope = new EvalTestScope();
            scope.MakeConfigUnavailable();
            var readinessGate = new StubUnityEditorReadinessGate();

            var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                new EvalPlanUnityIpcMethodHandler(CreateCompilationService(), readinessGate, scope.Project, scope.CreateEnvironment()),
                CreateRequest(UnityIpcMethod.EvalPlan, new IpcEvalPlanRequest("context.DeclareNoChanges();", CsEvalSourceKind.Snippet, true, false)));

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InternalError));
            Assert.That(IpcPayloadCodec.TryDeserializeStrict(response.Payload, out IpcEvalErrorResponse error, out var decodeError), Is.True, decodeError.Message);
            Assert.That(error.ApplicationState, Is.EqualTo(ExecutionApplicationState.NotApplied));
            Assert.That(readinessGate.CallCount, Is.Zero);
        }

        [Test]
        [Category("Size.Small")]
        public async Task Call_WhenEntryPointCompletesWithImpactDeclaration_ReturnsAppliedSuccess ()
        {
            using var scope = new EvalTestScope();
            var environment = scope.CreateEnvironment();
            const string source = "context.DeclareNoChanges();";
            var planResponse = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                CreatePlanHandler(scope, environment),
                CreateRequest(UnityIpcMethod.EvalPlan, new IpcEvalPlanRequest(source, CsEvalSourceKind.Snippet, true, false)));
            Assert.That(IpcPayloadCodec.TryDeserializeStrict(planResponse.Payload, out IpcEvalResponse plan, out var planError), Is.True, planError.Message);

            var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                CreateCallHandler(scope, environment),
                CreateRequest(UnityIpcMethod.EvalCall, new IpcEvalCallRequest(source, CsEvalSourceKind.Snippet, true, false, plan.PlanToken!)));

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
            Assert.That(IpcPayloadCodec.TryDeserializeStrict(response.Payload, out IpcEvalResponse call, out var callError), Is.True, callError.Message);
            Assert.That(call.Phase, Is.EqualTo(CsEvalPhase.Call));
            Assert.That(call.ApplicationState, Is.EqualTo(ExecutionApplicationState.Applied));
            Assert.That(call.Eval, Is.TypeOf<CsEvalCallSuccessResult>());
            var result = (CsEvalCallSuccessResult)call.Eval;
            Assert.That(result.DurationMilliseconds, Is.GreaterThanOrEqualTo(0));
            Assert.That(result.TouchedResources.NoChanges, Is.True);
        }

        [Test]
        [Category("Size.Small")]
        public async Task Call_WhenEvalIsDisabledAfterPlan_DoesNotInvokeEntryPoint ()
        {
            using var scope = new EvalTestScope();
            var environment = scope.CreateEnvironment();
            const string source = "throw new System.InvalidOperationException(\"must not execute\");";
            var plan = await CreateSuccessfulPlanAsync(scope, environment, source, allowPlayMode: false);
            scope.SetEvalEnabled(false);

            var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                CreateCallHandler(scope, environment),
                CreateRequest(UnityIpcMethod.EvalCall, new IpcEvalCallRequest(source, CsEvalSourceKind.Snippet, true, false, plan.PlanToken!)));

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(IpcPayloadCodec.TryDeserializeStrict(response.Payload, out IpcEvalErrorResponse error, out var decodeError), Is.True, decodeError.Message);
            Assert.That(error.ApplicationState, Is.EqualTo(ExecutionApplicationState.NotApplied));
        }

        [Test]
        [Category("Size.Small")]
        public async Task Call_WhenConfigBecomesInvalidAfterPlan_DoesNotInvokeEntryPoint ()
        {
            using var scope = new EvalTestScope();
            var environment = scope.CreateEnvironment();
            const string source = "throw new System.InvalidOperationException(\"must not execute\");";
            var plan = await CreateSuccessfulPlanAsync(scope, environment, source, allowPlayMode: false);
            scope.SetConfigJson("{\"schemaVersion\":1,\"operationPolicy\":\"safe\",\"planTokenMode\":\"optional\",\"operationAllowlist\":[\"^ucli\\\\.\"],\"evalEnabled\":true,\"unknown\":true}");

            var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                CreateCallHandler(scope, environment),
                CreateRequest(UnityIpcMethod.EvalCall, new IpcEvalCallRequest(source, CsEvalSourceKind.Snippet, true, false, plan.PlanToken!)));

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors[0].Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(IpcPayloadCodec.TryDeserializeStrict(response.Payload, out IpcEvalErrorResponse error, out var decodeError), Is.True, decodeError.Message);
            Assert.That(error.ApplicationState, Is.EqualTo(ExecutionApplicationState.NotApplied));
        }

        [Test]
        [Category("Size.Small")]
        public async Task Call_WhenEditorGenerationChangedAfterPlan_DoesNotInvokeEntryPoint ()
        {
            using var scope = new EvalTestScope();
            var environment = scope.CreateEnvironment();
            const string source = "throw new System.InvalidOperationException(\"must not execute\");";
            var plan = await CreateSuccessfulPlanAsync(scope, environment, source, allowPlayMode: false);
            environment.AdvanceDomainReloadGeneration();

            var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                CreateCallHandler(scope, environment),
                CreateRequest(UnityIpcMethod.EvalCall, new IpcEvalCallRequest(source, CsEvalSourceKind.Snippet, true, false, plan.PlanToken!)));

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(IpcPayloadCodec.TryDeserializeStrict(response.Payload, out IpcEvalErrorResponse error, out var decodeError), Is.True, decodeError.Message);
            Assert.That(error.ApplicationState, Is.EqualTo(ExecutionApplicationState.NotApplied));
        }

        [Test]
        [Category("Size.Small")]
        public async Task Call_WhenEditorProcessChangedAfterPlan_DoesNotInvokeEntryPoint ()
        {
            using var scope = new EvalTestScope();
            var environment = scope.CreateEnvironment();
            const string source = "throw new System.InvalidOperationException(\"must not execute\");";
            var plan = await CreateSuccessfulPlanAsync(scope, environment, source, allowPlayMode: false);
            environment.AdvanceEditorInstance();

            var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                CreateCallHandler(scope, environment),
                CreateRequest(UnityIpcMethod.EvalCall, new IpcEvalCallRequest(source, CsEvalSourceKind.Snippet, true, false, plan.PlanToken!)));

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(IpcPayloadCodec.TryDeserializeStrict(response.Payload, out IpcEvalErrorResponse error, out var decodeError), Is.True, decodeError.Message);
            Assert.That(error.ApplicationState, Is.EqualTo(ExecutionApplicationState.NotApplied));
        }

        [Test]
        [Category("Size.Small")]
        public async Task Call_WhenIpcSessionChangedAfterPlan_DoesNotInvokeEntryPoint ()
        {
            using var scope = new EvalTestScope();
            var environment = scope.CreateEnvironment();
            const string source = "throw new System.InvalidOperationException(\"must not execute\");";
            const string planSession = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
            const string callSession = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";
            var planResponse = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                CreatePlanHandler(scope, environment),
                CreateRequest(UnityIpcMethod.EvalPlan, new IpcEvalPlanRequest(source, CsEvalSourceKind.Snippet, true, false), planSession));
            Assert.That(IpcPayloadCodec.TryDeserializeStrict(planResponse.Payload, out IpcEvalResponse plan, out var planError), Is.True, planError.Message);

            var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                CreateCallHandler(scope, environment),
                CreateRequest(UnityIpcMethod.EvalCall, new IpcEvalCallRequest(source, CsEvalSourceKind.Snippet, true, false, plan.PlanToken!), callSession));

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(IpcPayloadCodec.TryDeserializeStrict(response.Payload, out IpcEvalErrorResponse error, out var decodeError), Is.True, decodeError.Message);
            Assert.That(error.ApplicationState, Is.EqualTo(ExecutionApplicationState.NotApplied));
        }

        [Test]
        [Category("Size.Small")]
        public async Task Call_WhenTaskWaitIsCanceled_QuarantinesTheMutationLaneAndPropagatesCancellation ()
        {
            using var scope = new EvalTestScope();
            using var cancellation = new CancellationTokenSource();
            var environment = scope.CreateEnvironment();
            var mutationLane = new StubMutationLaneControl();
            const string source = "await System.Threading.Tasks.Task.Delay(System.Threading.Timeout.Infinite);";
            var plan = await CreateSuccessfulPlanAsync(scope, environment, source, allowPlayMode: false);
            var callTask = UnityIpcMethodHandlerTestInvoker.HandleAsync(
                    CreateCallHandler(scope, environment, mutationLane),
                    CreateRequest(UnityIpcMethod.EvalCall, new IpcEvalCallRequest(source, CsEvalSourceKind.Snippet, true, false, plan.PlanToken!)),
                    cancellation.Token)
                .AsTask();
            await mutationLane.MutationStarted;

            cancellation.Cancel();

            Exception? exception = null;
            try
            {
                await callTask;
            }
            catch (Exception caught)
            {
                exception = caught;
            }

            Assert.That(exception, Is.InstanceOf<OperationCanceledException>());
            Assert.That(mutationLane.IsQuarantined, Is.True);
            Assert.That(mutationLane.QuarantinedTask, Is.Not.Null);
        }

        [Test]
        [Category("Size.Small")]
        public async Task Plan_WhenAllowDangerousIsFalse_DoesNotEnterEvaluation ()
        {
            using var scope = new EvalTestScope();
            var readinessGate = new StubUnityEditorReadinessGate();

            var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                new EvalPlanUnityIpcMethodHandler(CreateCompilationService(), readinessGate, scope.Project, scope.CreateEnvironment()),
                CreateRequest(UnityIpcMethod.EvalPlan, new IpcEvalPlanRequest("context.DeclareNoChanges();", CsEvalSourceKind.Snippet, false, false)));

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(IpcPayloadCodec.TryDeserializeStrict(response.Payload, out IpcEvalErrorResponse error, out var decodeError), Is.True, decodeError.Message);
            Assert.That(error.ApplicationState, Is.EqualTo(ExecutionApplicationState.NotApplied));
            Assert.That(readinessGate.CallCount, Is.Zero);
        }

        [Test]
        [Category("Size.Small")]
        public void Plan_WhenRequestIsCanceled_DoesNotEnterReadiness ()
        {
            using var scope = new EvalTestScope();
            using var cancellation = new CancellationTokenSource();
            var readinessGate = new StubUnityEditorReadinessGate();
            cancellation.Cancel();

            var exception = Assert.CatchAsync(async () => await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                new EvalPlanUnityIpcMethodHandler(CreateCompilationService(), readinessGate, scope.Project, scope.CreateEnvironment()),
                CreateRequest(UnityIpcMethod.EvalPlan, new IpcEvalPlanRequest("context.DeclareNoChanges();", CsEvalSourceKind.Snippet, true, false)),
                cancellation.Token));
            Assert.That(exception, Is.InstanceOf<OperationCanceledException>());
            Assert.That(readinessGate.CallCount, Is.Zero);
        }

        [Test]
        [Category("Size.Small")]
        public async Task Plan_WhenPlayModePermissionIsExplicit_PropagatesItToReadinessAdmission ()
        {
            using var scope = new EvalTestScope();
            var readinessGate = new StubUnityEditorReadinessGate();

            var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                new EvalPlanUnityIpcMethodHandler(CreateCompilationService(), readinessGate, scope.Project, scope.CreateEnvironment()),
                CreateRequest(UnityIpcMethod.EvalPlan, new IpcEvalPlanRequest("context.DeclareNoChanges();", CsEvalSourceKind.Snippet, true, true)));

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
            Assert.That(readinessGate.LastAllowPlayMode, Is.True);
        }

        [Test]
        [Category("Size.Small")]
        public async Task Call_WhenAllowPlayModeDiffersFromPlan_DoesNotInvokeEntryPoint ()
        {
            using var scope = new EvalTestScope();
            var environment = scope.CreateEnvironment();
            const string source = "throw new System.InvalidOperationException(\"must not execute\");";
            var plan = await CreateSuccessfulPlanAsync(scope, environment, source, allowPlayMode: false);

            var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                CreateCallHandler(scope, environment),
                CreateRequest(UnityIpcMethod.EvalCall, new IpcEvalCallRequest(source, CsEvalSourceKind.Snippet, true, true, plan.PlanToken!)));

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(IpcPayloadCodec.TryDeserializeStrict(response.Payload, out IpcEvalErrorResponse error, out var decodeError), Is.True, decodeError.Message);
            Assert.That(error.ApplicationState, Is.EqualTo(ExecutionApplicationState.NotApplied));
        }

        private static EvalPlanUnityIpcMethodHandler CreatePlanHandler (EvalTestScope scope, IPlanTokenEnvironment environment) => new(
            CreateCompilationService(),
            new StubUnityEditorReadinessGate(),
            scope.Project,
            environment);

        private static EvalCallUnityIpcMethodHandler CreateCallHandler (EvalTestScope scope, IPlanTokenEnvironment environment, IUnityMutationLaneControl? mutationLane = null) => new(
            CreateCompilationService(),
            new CsEvalEntryPointReflectionResolver(),
            new CsEvalReturnValueSerializer(),
            new StubUnityEditorReadinessGate(),
            scope.Project,
            environment,
            new MutationReadPostconditionJournal(),
            mutationLane ?? new StubMutationLaneControl());

        private static async Task<IpcEvalResponse> CreateSuccessfulPlanAsync (
            EvalTestScope scope,
            IPlanTokenEnvironment environment,
            string source,
            bool allowPlayMode)
        {
            var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                CreatePlanHandler(scope, environment),
                CreateRequest(UnityIpcMethod.EvalPlan, new IpcEvalPlanRequest(source, CsEvalSourceKind.Snippet, true, allowPlayMode)));
            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
            Assert.That(IpcPayloadCodec.TryDeserializeStrict(response.Payload, out IpcEvalResponse plan, out var error), Is.True, error.Message);
            return plan;
        }

        private static CsEvalCompilationService CreateCompilationService () => new(
            new CsEvalReferenceResolver(),
            new CsEvalEntryPointSymbolValidator(),
            new CsEvalSourcePreparer());

        private static IpcRequestEnvelope CreateRequest (UnityIpcMethod method, object payload, string sessionToken = "eval-handler-test-session") => new(
            IpcProtocol.CurrentVersion,
            Guid.NewGuid(),
            sessionToken,
            TextVocabulary.GetText(method),
            IpcPayloadCodec.SerializeToElement(payload),
            "single",
            DateTimeOffset.UtcNow.AddMinutes(1),
            60_000);

        private sealed class EvalTestScope : IDisposable
        {
            public EvalTestScope (bool evalEnabled = true)
                : this(CreateConfigJson(evalEnabled))
            {
            }

            public EvalTestScope (string configJson)
            {
                RepositoryRoot = Path.Combine(Path.GetTempPath(), "ucli-eval-handler-tests-" + Guid.NewGuid().ToString("N"));
                var projectRoot = Path.Combine(RepositoryRoot, "UnityProject");
                Directory.CreateDirectory(Path.Combine(RepositoryRoot, ".git"));
                Directory.CreateDirectory(Path.Combine(projectRoot, "Assets"));
                Directory.CreateDirectory(Path.Combine(projectRoot, "ProjectSettings"));
                var repository = AbsolutePath.Parse(RepositoryRoot);
                var unityProject = AbsolutePath.Parse(projectRoot);
                var fingerprint = UnityProjectFingerprintCalculator.Create(repository, unityProject);
                Project = new UnityProjectIdentity(projectRoot, fingerprint, "2023.2.22f1");
                var configDirectory = UcliStoragePathResolver.ResolveUcliDirectoryPath(repository);
                Directory.CreateDirectory(configDirectory.Value);
                File.WriteAllText(UcliStoragePathResolver.ResolveConfigPath(repository).Value, configJson);
                Snapshot = new PlanTokenEnvironmentSnapshot(unityProject, repository, fingerprint, "2023.2.22f1", UnityEditorCompileState.Ready, 0, Guid.NewGuid());
            }

            public string RepositoryRoot { get; }
            public UnityProjectIdentity Project { get; }
            public PlanTokenEnvironmentSnapshot Snapshot { get; }

            internal FixedPlanTokenEnvironment CreateEnvironment () => new FixedPlanTokenEnvironment(Snapshot);

            public void SetEvalEnabled (bool evalEnabled)
            {
                SetConfigJson(CreateConfigJson(evalEnabled));
            }

            public void SetConfigJson (string configJson) => File.WriteAllText(
                UcliStoragePathResolver.ResolveConfigPath(AbsolutePath.Parse(RepositoryRoot)).Value,
                configJson);

            public void MakeConfigUnavailable ()
            {
                var configPath = UcliStoragePathResolver.ResolveConfigPath(AbsolutePath.Parse(RepositoryRoot)).Value;
                File.Delete(configPath);
                Directory.CreateDirectory(configPath);
            }

            private static string CreateConfigJson (bool evalEnabled) =>
                "{\"schemaVersion\":1,\"operationPolicy\":\"safe\",\"planTokenMode\":\"optional\",\"operationAllowlist\":[\"^ucli\\\\.\"],\"evalEnabled\":"
                + (evalEnabled ? "true" : "false")
                + "}";

            public void Dispose ()
            {
                if (Directory.Exists(RepositoryRoot)) Directory.Delete(RepositoryRoot, recursive: true);
            }
        }

        internal sealed class FixedPlanTokenEnvironment : IPlanTokenEnvironment
        {
            private PlanTokenEnvironmentSnapshot snapshot;

            public FixedPlanTokenEnvironment (PlanTokenEnvironmentSnapshot snapshot) => this.snapshot = snapshot;

            public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

            public PlanTokenEnvironmentSnapshot Capture () => snapshot;

            public void AdvanceDomainReloadGeneration () => snapshot = new PlanTokenEnvironmentSnapshot(
                snapshot.ProjectRoot,
                snapshot.RepositoryRoot,
                snapshot.ProjectFingerprint,
                snapshot.UnityVersion,
                snapshot.CompileState,
                snapshot.DomainReloadGeneration + 1,
                snapshot.EditorInstanceId);

            public void AdvanceEditorInstance () => snapshot = new PlanTokenEnvironmentSnapshot(
                snapshot.ProjectRoot,
                snapshot.RepositoryRoot,
                snapshot.ProjectFingerprint,
                snapshot.UnityVersion,
                snapshot.CompileState,
                snapshot.DomainReloadGeneration,
                Guid.NewGuid());
        }

        private sealed class StubMutationLaneControl : IUnityMutationLaneControl
        {
            private readonly TaskCompletionSource<bool> mutationStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public bool IsBusy => false;
            public bool HasUnfinishedWork => QuarantinedTask is { IsCompleted: false };
            public bool IsQuarantined { get; private set; }
            public Task? QuarantinedTask { get; private set; }
            public Task MutationStarted => mutationStarted.Task;

            public IUnityMutationActivity BeginMutation ()
            {
                mutationStarted.TrySetResult(true);
                return new StubMutationActivity();
            }

            public void Quarantine (string reason, Task mutationCompletion)
            {
                Assert.That(reason, Is.Not.Null.And.Not.Empty);
                IsQuarantined = true;
                QuarantinedTask = mutationCompletion;
            }

            public bool TrySealAdmissionForRetirement (out IDisposable admissionSeal)
            {
                admissionSeal = new StubDisposable();
                return true;
            }

            public Task WaitForRetirementAsync () => QuarantinedTask ?? Task.CompletedTask;

            private sealed class StubMutationActivity : IUnityMutationActivity
            {
                public void Complete () { }
            }

            private sealed class StubDisposable : IDisposable
            {
                public void Dispose () { }
            }
        }
    }
}
