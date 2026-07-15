using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Phase;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

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
            new RecordingOperationCatalog
            {
                Operations = operations,
            },
            new RecordingRequestStaticValidator
            {
                Result = ValidationResult.Success(),
            });

        var result = await service.PrepareAsync(
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
                new IpcExecuteStepId("step-1")),
        ];
        var service = new PhaseExecutionPreflightService(
            new RecordingOperationCatalog
            {
                Operations = [operation],
            },
            new RecordingRequestStaticValidator
            {
                Result = ValidationResult.Invalid(validationErrors),
            });

        var result = await service.PrepareAsync(
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
            new RecordingOperationCatalog
            {
                Operations =
                [
                    CreateOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe, OperationPolicy.Safe),
                ],
            },
            new RecordingRequestStaticValidator
            {
                Result = ValidationResult.Failure(error),
            });

        var result = await service.PrepareAsync(
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
            new RecordingOperationCatalog
            {
                ProjectGetAllException = new InvalidOperationException("catalog load failed."),
            },
            new RecordingRequestStaticValidator
            {
                Result = ValidationResult.Success(),
            });

        var result = await service.PrepareAsync(
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
            new RecordingOperationCatalog
            {
                ProjectGetAllException = new OperationCatalogLoadException(
                    ExecutionError.InvalidArgument("Mode must be auto, daemon, or oneshot.")),
            },
            new RecordingRequestStaticValidator
            {
                Result = ValidationResult.Success(),
            });

        var result = await service.PrepareAsync(
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
            new RecordingOperationCatalog
            {
                ProjectGetAllException = new OperationCatalogLoadException(
                    ExecutionError.InternalError("Daemon is not running for mode=daemon."),
                    UnityExecutionModeDecisionErrorCodes.DaemonNotRunning),
            },
            new RecordingRequestStaticValidator
            {
                Result = ValidationResult.Success(),
            });

        var result = await service.PrepareAsync(
            preparedRequest,
            mode: UnityExecutionMode.Daemon,
            deadline: ExecutionDeadline.Start(TimeSpan.FromMilliseconds(30000), TimeProvider.System),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, result.Error!.Kind);
        Assert.Equal(UnityExecutionModeDecisionErrorCodes.DaemonNotRunning, result.Error.Code);
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
        var operationCatalog = new RecordingOperationCatalog
        {
            Operations = [],
        };
        var validationService = new RecordingRequestStaticValidator
        {
            Result = ValidationResult.Success(),
        };
        var service = new PhaseExecutionPreflightService(operationCatalog, validationService);

        var result = await service.PrepareAsync(
            preparedRequest,
            mode: UnityExecutionMode.Auto,
            deadline: deadline,
            cancellationToken: CancellationToken.None);

        PhaseExecutionPreflightInvocationAssert.DeadlineExpiredBeforeCatalogLoad(
            result,
            preparedRequest,
            operationCatalog,
            validationService);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_PropagatesCancellationTokenModeAndRemainingTimeoutToDependencies ()
    {
        var token = new CancellationTokenSource().Token;
        var timeProvider = new ManualTimeProvider();
        var deadline = ExecutionDeadline.Start(TimeSpan.FromMilliseconds(30000), timeProvider);
        var preparedRequest = CreatePreparedRequestContext();
        var operationCatalog = new RecordingOperationCatalog
        {
            Operations =
            [
                CreateOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe, OperationPolicy.Safe),
            ],
        };
        var validationService = new RecordingRequestStaticValidator
        {
            Result = ValidationResult.Success(),
        };
        var service = new PhaseExecutionPreflightService(operationCatalog, validationService);

        var result = await service.PrepareAsync(
            preparedRequest,
            mode: UnityExecutionMode.Daemon,
            deadline: deadline,
            failFast: true,
            cancellationToken: token);

        Assert.True(result.IsSuccess);
        OperationCatalogInvocationAssert.ProjectCatalogLoadedOnce(
            operationCatalog,
            preparedRequest.ProjectContext.UnityProject,
            preparedRequest.ProjectContext.Config,
            UnityExecutionMode.Daemon,
            TimeSpan.FromMilliseconds(30000),
            expectedFailFast: true,
            expectedCancellationToken: token);

        var validationInvocation = RequestStaticValidationInvocationAssert.PureStaticValidationRequestedOnce(
            validationService,
            expectedCatalogAvailable: true);
        Assert.Equal(token, validationInvocation.CancellationToken);
        Assert.Same(preparedRequest.Request, validationInvocation.Request);
        Assert.Same(preparedRequest.ProjectContext.Config, validationInvocation.Config);
        Assert.Contains(validationInvocation.Catalog.Operations, operation => operation.Name == MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe);
    }

    private static PreparedRequestContext CreatePreparedRequestContext ()
    {
        return new PreparedRequestContext(
            requestJson: """{"protocolVersion":1,"steps":[{"kind":"op","id":"step-1","op":"ucli.scene.open","args":{"path":"Assets/Scenes/Main.unity"}}]}""",
            request: new ValidateRequest(
                ProtocolVersion: 1,
                Steps:
                [
                    new ValidateRequestStep(
                        Kind: MackySoft.Ucli.Contracts.Ipc.ContractReading.IpcExecuteStepKind.Op,
                        StepId: new IpcExecuteStepId("step-1"),
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
            projectContext: ProjectContextTestFactory.CreateTemporaryFixtureProject());
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

}
