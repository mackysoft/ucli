using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Phase;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Tests;

public sealed class PhaseExecutionPreflightServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_WhenValidationSucceeds_ReturnsPreparedRequest ()
    {
        var preparedRequest = CreatePreparedRequestContext();
        UcliOperationDescriptor[] operations =
        [
            CreateOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe, OperationPolicy.Safe),
        ];
        var service = new PhaseExecutionPreflightService(
            new StubOperationCatalog(operations),
            new StubRequestStaticValidator(ValidationResult.Success()));

        var result = await service.Prepare(
            preparedRequest,
            mode: UnityExecutionMode.Auto,
            deadline: ExecutionDeadline.Start(TimeSpan.FromMilliseconds(30000), TimeProvider.System),
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.HasValidationErrors);
        Assert.NotNull(result.PreparedRequest);
        Assert.Equal(preparedRequest.RequestJson, result.PreparedRequest!.RequestJson);
        Assert.Same(preparedRequest.Request, result.PreparedRequest.Request);
        Assert.Same(preparedRequest.ProjectContext.UnityProject, result.PreparedRequest.UnityProject);
        Assert.Same(preparedRequest.ProjectContext.Config, result.PreparedRequest.Config);
        Assert.Equal(preparedRequest.ProjectContext.ConfigSource, result.PreparedRequest.ConfigSource);
        Assert.True(result.PreparedRequest.OperationsByName.ContainsKey(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe));
        Assert.Null(result.Error);
        Assert.Empty(result.ValidationErrors);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_WhenStaticValidationReturnsValidationErrors_RetainsPreparedRequest ()
    {
        var preparedRequest = CreatePreparedRequestContext();
        var operation = CreateOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe, OperationPolicy.Safe);
        ValidationError[] validationErrors =
        [
            new ValidationError(
                ValidationErrorCodes.OperationArgsInvalid,
                "Operation args are invalid.",
                "step-1"),
        ];
        var service = new PhaseExecutionPreflightService(
            new StubOperationCatalog([operation]),
            new StubRequestStaticValidator(new ValidationResult(validationErrors)));

        var result = await service.Prepare(
            preparedRequest,
            mode: UnityExecutionMode.Auto,
            deadline: ExecutionDeadline.Start(TimeSpan.FromMilliseconds(30000), TimeProvider.System),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.True(result.HasValidationErrors);
        Assert.Null(result.Error);
        Assert.NotNull(result.PreparedRequest);
        Assert.True(result.PreparedRequest!.OperationsByName.ContainsKey(operation.Name));
        Assert.Single(result.ValidationErrors);
        Assert.Equal(ValidationErrorCodes.OperationArgsInvalid, result.ValidationErrors[0].Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_WhenStaticValidationFailsWithExecutionError_ReturnsFailure ()
    {
        var preparedRequest = CreatePreparedRequestContext();
        var error = ExecutionError.InternalError("operation metadata could not be loaded.");
        var service = new PhaseExecutionPreflightService(
            new StubOperationCatalog([CreateOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe, OperationPolicy.Safe)]),
            new StubRequestStaticValidator(ValidationResult.Failure(error)));

        var result = await service.Prepare(
            preparedRequest,
            mode: UnityExecutionMode.Auto,
            deadline: ExecutionDeadline.Start(TimeSpan.FromMilliseconds(30000), TimeProvider.System),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.HasValidationErrors);
        Assert.Same(error, result.Error);
        Assert.NotNull(result.PreparedRequest);
        Assert.True(result.PreparedRequest!.OperationsByName.ContainsKey(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe));
        Assert.Empty(result.ValidationErrors);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_WhenOperationCatalogFails_ReturnsFailure ()
    {
        var preparedRequest = CreatePreparedRequestContext();
        var service = new PhaseExecutionPreflightService(
            new ThrowingOperationCatalog(),
            new SpyRequestStaticValidator());

        var result = await service.Prepare(
            preparedRequest,
            mode: UnityExecutionMode.Auto,
            deadline: ExecutionDeadline.Start(TimeSpan.FromMilliseconds(30000), TimeProvider.System),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.HasValidationErrors);
        Assert.NotNull(result.Error);
        Assert.NotNull(result.PreparedRequest);
        Assert.Empty(result.PreparedRequest!.OperationsByName);
        Assert.Contains("Static validation could not load operation metadata.", result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_WhenOperationCatalogLoadFailureHasInvalidArgument_PreservesErrorKind ()
    {
        var preparedRequest = CreatePreparedRequestContext();
        var service = new PhaseExecutionPreflightService(
            new TypedFailingOperationCatalog(new OperationCatalogLoadException(
                ExecutionError.InvalidArgument("Mode must be auto, daemon, or oneshot."))),
            new SpyRequestStaticValidator());

        var result = await service.Prepare(
            preparedRequest,
            mode: (UnityExecutionMode)999,
            deadline: ExecutionDeadline.Start(TimeSpan.FromMilliseconds(30000), TimeProvider.System),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Contains("Static validation could not load operation metadata.", result.Error.Message, StringComparison.Ordinal);
        Assert.NotNull(result.PreparedRequest);
        Assert.Empty(result.PreparedRequest!.OperationsByName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_WhenOperationCatalogLoadFailureHasCustomErrorCode_PreservesOriginalErrorCode ()
    {
        var preparedRequest = CreatePreparedRequestContext();
        var service = new PhaseExecutionPreflightService(
            new TypedFailingOperationCatalog(new OperationCatalogLoadException(
                ExecutionError.InternalError("Daemon is not running for mode=daemon."),
                UnityExecutionModeDecisionErrorCodes.DaemonNotRunning)),
            new SpyRequestStaticValidator());

        var result = await service.Prepare(
            preparedRequest,
            mode: UnityExecutionMode.Daemon,
            deadline: ExecutionDeadline.Start(TimeSpan.FromMilliseconds(30000), TimeProvider.System),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, result.Error!.Kind);
        Assert.Equal(UnityExecutionModeDecisionErrorCodes.DaemonNotRunning, result.ErrorCode);
        Assert.Contains("Static validation could not load operation metadata.", result.Error.Message, StringComparison.Ordinal);
        Assert.NotNull(result.PreparedRequest);
        Assert.Empty(result.PreparedRequest!.OperationsByName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_WhenDeadlineIsAlreadyExpired_ReturnsTimeoutWithoutLoadingCatalog ()
    {
        var timeProvider = new ManualTimeProvider();
        var deadline = ExecutionDeadline.Start(TimeSpan.FromMilliseconds(1), timeProvider);
        timeProvider.Advance(TimeSpan.FromMilliseconds(1));

        var preparedRequest = CreatePreparedRequestContext();
        var operationCatalog = new SpyOperationCatalog();
        var validationService = new SpyRequestStaticValidator();
        var service = new PhaseExecutionPreflightService(operationCatalog, validationService);

        var result = await service.Prepare(
            preparedRequest,
            mode: UnityExecutionMode.Auto,
            deadline: deadline,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, result.Error!.Kind);
        Assert.NotNull(result.PreparedRequest);
        Assert.Empty(result.PreparedRequest!.OperationsByName);
        Assert.Equal(0, operationCatalog.CallCount);
        Assert.Equal(0, validationService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_PropagatesCancellationTokenModeAndRemainingTimeoutToDependencies ()
    {
        var token = new CancellationTokenSource().Token;
        var timeProvider = new ManualTimeProvider();
        var deadline = ExecutionDeadline.Start(TimeSpan.FromMilliseconds(30000), timeProvider);
        var preparedRequest = CreatePreparedRequestContext();
        var operationCatalog = new SpyOperationCatalog(
            [CreateOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe, OperationPolicy.Safe)]);
        var validationService = new SpyRequestStaticValidator(ValidationResult.Success());
        var service = new PhaseExecutionPreflightService(operationCatalog, validationService);

        var result = await service.Prepare(
            preparedRequest,
            mode: UnityExecutionMode.Daemon,
            deadline: deadline,
            failFast: true,
            cancellationToken: token);

        Assert.True(result.IsSuccess);
        Assert.Equal(token, operationCatalog.ReceivedToken);
        Assert.Equal(UnityExecutionMode.Daemon, operationCatalog.ReceivedMode);
        Assert.Equal(TimeSpan.FromMilliseconds(30000), operationCatalog.ReceivedTimeout);
        Assert.True(operationCatalog.ReceivedFailFast);
        Assert.Equal(token, validationService.ReceivedToken);
        Assert.Same(preparedRequest.Request, validationService.ReceivedRequest);
        Assert.True(validationService.ReceivedCatalog!.IsAvailable);
        Assert.Single(validationService.ReceivedCatalog.Operations);
    }

    private static PreparedRequestContext CreatePreparedRequestContext ()
    {
        return new PreparedRequestContext(
            RequestJson: """{"protocolVersion":1,"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","steps":[{"kind":"op","id":"step-1","op":"ucli.scene.open","args":{"path":"Assets/Scenes/Main.unity"}}]}""",
            Request: new ValidateRequest(
                ProtocolVersion: 1,
                RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                Steps:
                [
                    new ValidateRequestStep(
                        Kind: MackySoft.Ucli.Contracts.Ipc.Validation.IpcRequestStepKind.Op,
                        StepId: "step-1",
                        Op: "ucli.scene.open",
                        Element: System.Text.Json.JsonSerializer.SerializeToElement(new
                        {
                            kind = "op",
                            id = "step-1",
                            op = "ucli.scene.open",
                            args = new
                            {
                                path = "Assets/Scenes/Main.unity",
                            },
                        })),
                ]),
            ProjectContext: new ProjectContext(
                new ResolvedUnityProjectContext(
                    UnityProjectRoot: "/tmp/project",
                    RepositoryRoot: "/tmp/repository",
                    ProjectFingerprint: "project-fingerprint",
                    PathSource: UnityProjectPathSource.CommandOption),
                UcliConfig.CreateDefault(),
                ConfigSource.Default));
    }

    private static UcliOperationDescriptor CreateOperationDescriptor (
        string name,
        OperationPolicy policy)
    {
        return new UcliOperationDescriptor(
            name,
            UcliOperationKind.Query,
            policy,
            """{"type":"object","additionalProperties":false}""");
    }

    private sealed class StubOperationCatalog : IOperationCatalog
    {
        private readonly IReadOnlyList<UcliOperationDescriptor> operations;

        public StubOperationCatalog (IReadOnlyList<UcliOperationDescriptor> operations)
        {
            this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
        }

        public ValueTask<UcliOperationDescriptor?> Get (string name, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(operations.FirstOrDefault(operation => string.Equals(operation.Name, name, StringComparison.Ordinal)));
        }

        public ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetAll (CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(operations);
        }

        public ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetAll (
            ResolvedUnityProjectContext unityProject,
            UcliConfig config,
            UnityExecutionMode mode = UnityExecutionMode.Auto,
            TimeSpan? timeout = null,
            bool failFast = false,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(operations);
        }
    }

    private sealed class SpyOperationCatalog : IOperationCatalog
    {
        private readonly IReadOnlyList<UcliOperationDescriptor> operations;

        public SpyOperationCatalog ()
            : this([])
        {
        }

        public SpyOperationCatalog (IReadOnlyList<UcliOperationDescriptor> operations)
        {
            this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
        }

        public int CallCount { get; private set; }

        public CancellationToken ReceivedToken { get; private set; }

        public UnityExecutionMode ReceivedMode { get; private set; }

        public TimeSpan? ReceivedTimeout { get; private set; }

        public bool ReceivedFailFast { get; private set; }

        public ValueTask<UcliOperationDescriptor?> Get (string name, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(operations.FirstOrDefault(operation => string.Equals(operation.Name, name, StringComparison.Ordinal)));
        }

        public ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetAll (CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(operations);
        }

        public ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetAll (
            ResolvedUnityProjectContext unityProject,
            UcliConfig config,
            UnityExecutionMode mode = UnityExecutionMode.Auto,
            TimeSpan? timeout = null,
            bool failFast = false,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            ReceivedMode = mode;
            ReceivedTimeout = timeout;
            ReceivedFailFast = failFast;
            ReceivedToken = cancellationToken;
            return ValueTask.FromResult(operations);
        }
    }

    private sealed class ThrowingOperationCatalog : IOperationCatalog
    {
        public ValueTask<UcliOperationDescriptor?> Get (string name, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("catalog load failed.");
        }

        public ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetAll (CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("catalog load failed.");
        }

        public ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetAll (
            ResolvedUnityProjectContext unityProject,
            UcliConfig config,
            UnityExecutionMode mode = UnityExecutionMode.Auto,
            TimeSpan? timeout = null,
            bool failFast = false,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("catalog load failed.");
        }
    }

    private sealed class TypedFailingOperationCatalog : IOperationCatalog
    {
        private readonly Exception exception;

        public TypedFailingOperationCatalog (Exception exception)
        {
            this.exception = exception ?? throw new ArgumentNullException(nameof(exception));
        }

        public ValueTask<UcliOperationDescriptor?> Get (string name, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw exception;
        }

        public ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetAll (CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw exception;
        }

        public ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetAll (
            ResolvedUnityProjectContext unityProject,
            UcliConfig config,
            UnityExecutionMode mode = UnityExecutionMode.Auto,
            TimeSpan? timeout = null,
            bool failFast = false,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw exception;
        }
    }

    private sealed class StubRequestStaticValidator : IRequestStaticValidator
    {
        private readonly ValidationResult result;

        public StubRequestStaticValidator (ValidationResult result)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public ValueTask<ValidationResult> Validate (
            ValidateRequest request,
            RequestStaticValidationCatalog catalog,
            UcliConfig config,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class SpyRequestStaticValidator : IRequestStaticValidator
    {
        private readonly ValidationResult result;

        public SpyRequestStaticValidator ()
            : this(ValidationResult.Success())
        {
        }

        public SpyRequestStaticValidator (ValidationResult result)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public int CallCount { get; private set; }

        public CancellationToken ReceivedToken { get; private set; }

        public ValidateRequest? ReceivedRequest { get; private set; }

        public RequestStaticValidationCatalog? ReceivedCatalog { get; private set; }

        public ValueTask<ValidationResult> Validate (
            ValidateRequest request,
            RequestStaticValidationCatalog catalog,
            UcliConfig config,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            ReceivedToken = cancellationToken;
            ReceivedRequest = request;
            ReceivedCatalog = catalog;
            return ValueTask.FromResult(result);
        }
    }
}
