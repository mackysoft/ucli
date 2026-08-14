using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Programs.Parsing;
using MackySoft.Ucli.Application.Features.Programs.Planning;
using MackySoft.Ucli.Application.Features.Programs.Resolution;
using MackySoft.Ucli.Application.Features.Programs.Supervision;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;

namespace MackySoft.Ucli.Application.Tests.Features.Programs.Planning;

public sealed class ProgramPlanPreflightServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ValidateAsync_CurrentCallCatalogFailure_DoesNotValidateOrPlan ()
    {
        var catalog = new RecordingCatalogReader(ProgramFixedHostCatalogReadResult.Failed());
        var validator = new RecordingValidator(ValidationResult.Success());
        var binding = new RecordingBinding(CreateResolvedProject());
        var result = await CreateService(catalog, validator).ValidateAsync(
            CreateDefinition([new InlineCallProgramStep(null, EmptyRequest())]),
            CreateProjectContext(), binding, CreateDeadline(), false);

        Assert.False(result.IsSuccess);
        Assert.Equal("PROGRAM_CALL_CATALOG_UNAVAILABLE", result.Diagnostic!.Code);
        Assert.Single(catalog.Invocations);
        Assert.Empty(validator.Invocations);
        Assert.Empty(binding.Commands);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("inline")]
    [InlineData("requestPath")]
    public async Task ValidateAsync_CurrentCallWithEval_IsRejectedBeforeCatalogDiscovery (string source)
    {
        var catalog = new RecordingCatalogReader(ProgramFixedHostCatalogReadResult.Failed());
        var validator = new RecordingValidator(ValidationResult.Success());
        var binding = new RecordingBinding(CreateResolvedProject());
        var request = CreateOpRequest("ucli.cs.eval");
        var definition = source == "inline"
            ? CreateDefinition([new InlineCallProgramStep(null, request)])
            : CreateDefinition(
                [new ReferencedCallProgramStep(null, RootRelativePath.Parse("request.json"))],
                [new ResolvedProgramSource("/steps/0/requestPath", RootRelativePath.Parse("request.json"), Sha256Digest.Parse(new string('c', 64)), 1, "{}", request)]);
        var result = await CreateService(catalog, validator).ValidateAsync(
            definition,
            CreateProjectContext(), binding, CreateDeadline(), false);

        Assert.False(result.IsSuccess);
        Assert.Equal("PROGRAM_CALL_EVAL_NOT_ALLOWED", result.Diagnostic!.Code);
        Assert.Empty(catalog.Invocations);
        Assert.Empty(validator.Invocations);
        Assert.Empty(binding.Commands);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ValidateAsync_CurrentCallStaticValidationFailure_DoesNotCreateRequestPlan ()
    {
        var catalog = new RecordingCatalogReader(ProgramFixedHostCatalogReadResult.Success([]));
        var validator = new RecordingValidator(ValidationResult.Failure(ExecutionError.InvalidArgument("Denied.")));
        var binding = new RecordingBinding(CreateResolvedProject());
        var result = await CreateService(catalog, validator).ValidateAsync(
            CreateDefinition([new InlineCallProgramStep(null, EmptyRequest())]),
            CreateProjectContext(), binding, CreateDeadline(), false);

        Assert.False(result.IsSuccess);
        Assert.Single(catalog.Invocations);
        Assert.Single(validator.Invocations);
        Assert.Empty(binding.Commands);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ValidateAsync_CurrentCallRequestPlanFailure_DoesNotCreateRunFacts ()
    {
        var catalog = new RecordingCatalogReader(ProgramFixedHostCatalogReadResult.Success([]));
        var validator = new RecordingValidator(ValidationResult.Success());
        var binding = new RecordingBinding(CreateResolvedProject())
        {
            ExecuteResult = UnityRequestExecutionResult.Failure(
                new UnityRequestFailure(UnityRequestFailureKind.General, new UcliCode("PLAN_FAILED"), "Plan failed.")),
        };
        var result = await CreateService(catalog, validator).ValidateAsync(
            CreateDefinition([new InlineCallProgramStep(null, EmptyRequest())]),
            CreateProjectContext(), binding, CreateDeadline(), false);

        Assert.False(result.IsSuccess);
        Assert.Equal("PROGRAM_CALL_PLAN_REJECTED", result.Diagnostic!.Code);
        Assert.Single(catalog.Invocations);
        Assert.Single(validator.Invocations);
        Assert.Equal([UcliCommandIds.Plan], binding.Commands);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ValidateAsync_LaterCallBeyondTheFirstLifecycleBoundary_IsNotReadOrPlanned ()
    {
        var catalog = new RecordingCatalogReader(ProgramFixedHostCatalogReadResult.Failed());
        var validator = new RecordingValidator(ValidationResult.Success());
        var binding = new RecordingBinding(CreateResolvedProject());
        var result = await CreateService(catalog, validator).ValidateAsync(
            CreateDefinition([new RefreshProgramStep(null), new InlineCallProgramStep(null, EmptyRequest())]),
            CreateProjectContext(), binding, CreateDeadline(), false);

        Assert.True(result.IsSuccess);
        Assert.Empty(catalog.Invocations);
        Assert.Empty(validator.Invocations);
        Assert.Empty(binding.Commands);
    }

    private static ProgramPlanPreflightService CreateService (
        IProgramFixedHostCatalogReader catalog,
        IRequestStaticValidator validator) => new(new ThrowingReadyService(), catalog, validator);

    private static ResolvedProgramDefinition CreateDefinition (
        IReadOnlyList<ProgramStep> steps,
        IReadOnlyList<ResolvedProgramSource>? sources = null)
    {
        using var document = JsonDocument.Parse("{\"steps\":[]}");
        var digest = Sha256Digest.Parse(new string('a', 64));
        return new ResolvedProgramDefinition(
            new ProgramDefinition(steps, document.RootElement.Clone()),
            sources ?? [],
            new ProgramSourceManifest(digest, ProgramRootSource.Stdin, null, null, digest, []),
            digest);
    }

    private static ValidateRequest EmptyRequest () => new(IpcProtocol.CurrentVersion, [], false);

    private static ValidateRequest CreateOpRequest (string operation) => new(
        IpcProtocol.CurrentVersion,
        [new ValidateRequestStep(IpcExecuteStepKind.Op, 0, operation, JsonSerializer.SerializeToElement(new { }))],
        false);

    private static ProjectContext CreateProjectContext () => new(CreateResolvedProject(), UcliConfig.CreateDefault(), ConfigSource.Default);

    private static ResolvedUnityProjectContext CreateResolvedProject () => ResolvedUnityProjectContext.Create(
        AbsolutePath.Parse(ProjectPathTestValues.WorkspaceUnityProject),
        AbsolutePath.Parse(ProjectPathTestValues.WorkspaceRoot),
        new ProjectFingerprint(new string('b', 64)),
        UnityProjectPathSource.CurrentDirectory,
        null,
        "6000.1.0f1");

    private static ExecutionDeadline CreateDeadline () => ExecutionDeadline.Start(TimeSpan.FromMinutes(1), TimeProvider.System);

    private sealed class RecordingCatalogReader (ProgramFixedHostCatalogReadResult result) : IProgramFixedHostCatalogReader
    {
        public List<object> Invocations { get; } = [];

        public ValueTask<ProgramFixedHostCatalogReadResult> ReadAsync (
            ProjectContext project,
            IUnityExecutionHostBinding binding,
            ExecutionDeadline deadline,
            CancellationToken cancellationToken = default)
        {
            Invocations.Add(new object());
            return ValueTask.FromResult(result);
        }
    }

    private sealed class RecordingValidator (ValidationResult result) : IRequestStaticValidator
    {
        public List<ValidateRequest> Invocations { get; } = [];

        public ValueTask<ValidationResult> ValidateAsync (
            ValidateRequest request,
            RequestStaticValidationCatalog catalog,
            UcliConfig config,
            CancellationToken cancellationToken = default)
        {
            Invocations.Add(request);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class RecordingBinding (ResolvedUnityProjectContext project) : IUnityExecutionHostBinding
    {
        public ResolvedUnityProjectContext Project { get; } = project;
        public UnityExecutionTarget Target => UnityExecutionTarget.Daemon;
        public List<UcliCommand> Commands { get; } = [];
        public UnityRequestExecutionResult ExecuteResult { get; init; } = UnityRequestExecutionResult.Failure(
            new UnityRequestFailure(UnityRequestFailureKind.General, new UcliCode("UNEXPECTED"), "Unexpected request."));

        public ValueTask<UnityRequestExecutionResult> ExecuteAsync (UcliCommand command, UnityRequestPayload payload, ExecutionDeadline deadline, CancellationToken cancellationToken = default)
        {
            Commands.Add(command);
            return ValueTask.FromResult(ExecuteResult);
        }

        public ValueTask<UnityRequestExecutionResult> StartAsync (UcliCommand command, UnityRequestPayload payload, LifecycleExecutionStartInvocation invocation, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<UnityRequestExecutionResult> ReconnectAsync (UcliCommand command, UnityRequestPayload payload, LifecycleExecutionReconnectInvocation invocation, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask DisposeAsync () => ValueTask.CompletedTask;
    }

    private sealed class ThrowingReadyService : IReadyService
    {
        public ValueTask<ReadyExecutionResult> ExecuteAsync (ReadyCommandInput input, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ProgramReadyObservation> ObserveOnFixedHostAsync (ProjectContext context, IUnityExecutionHostBinding binding, ExecutionDeadline deadline, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
