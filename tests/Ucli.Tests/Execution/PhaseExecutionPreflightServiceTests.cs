using MackySoft.Ucli.Cli.Requests;
using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Context;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Operations;
using MackySoft.Ucli.ReadIndex;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Tests;

public sealed class PhaseExecutionPreflightServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_WhenPreparationAndValidationSucceed_ReturnsPreparedRequest ()
    {
        var preparedRequest = CreatePreparedRequestContext();
        var service = new PhaseExecutionPreflightService(
            new StubRequestPreparationService(RequestPreparationResult.Success(preparedRequest)),
            new StubRequestStaticValidationService(ValidationResult.Success()));

        var result = await service.Prepare(
            requestPath: "request.json",
            projectPath: "/tmp/project",
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.HasValidationErrors);
        Assert.NotNull(result.PreparedRequest);
        Assert.Equal(preparedRequest.RequestJson, result.PreparedRequest!.RequestJson);
        Assert.Equal(preparedRequest.InputSource, result.PreparedRequest.InputSource);
        Assert.Same(preparedRequest.Request, result.PreparedRequest.Request);
        Assert.Same(preparedRequest.ProjectContext.UnityProject, result.PreparedRequest.UnityProject);
        Assert.Same(preparedRequest.ProjectContext.Config, result.PreparedRequest.Config);
        Assert.Equal(preparedRequest.ProjectContext.ConfigSource, result.PreparedRequest.ConfigSource);
        Assert.Null(result.Error);
        Assert.Empty(result.ValidationErrors);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_WhenRequestPreparationFails_ReturnsFailureWithoutValidation ()
    {
        var error = ExecutionError.InvalidArgument("request is invalid.");
        var requestPreparationService = new StubRequestPreparationService(RequestPreparationResult.Failure(error));
        var validationService = new SpyRequestStaticValidationService();
        var service = new PhaseExecutionPreflightService(requestPreparationService, validationService);

        var result = await service.Prepare(
            requestPath: "request.json",
            projectPath: "/tmp/project",
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.HasValidationErrors);
        Assert.Same(error, result.Error);
        Assert.Null(result.PreparedRequest);
        Assert.Equal(0, validationService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_WhenStaticValidationReturnsValidationErrors_ReturnsValidationFailure ()
    {
        var preparedRequest = CreatePreparedRequestContext();
        ValidationError[] validationErrors =
        [
            new ValidationError(
                ValidationErrorCodes.OperationArgsInvalid,
                "Operation args are invalid.",
                "step-1"),
        ];
        var service = new PhaseExecutionPreflightService(
            new StubRequestPreparationService(RequestPreparationResult.Success(preparedRequest)),
            new StubRequestStaticValidationService(new ValidationResult(validationErrors)));

        var result = await service.Prepare(
            requestPath: null,
            projectPath: "/tmp/project",
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.True(result.HasValidationErrors);
        Assert.Null(result.Error);
        Assert.Null(result.PreparedRequest);
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
            new StubRequestPreparationService(RequestPreparationResult.Success(preparedRequest)),
            new StubRequestStaticValidationService(ValidationResult.Failure(error)));

        var result = await service.Prepare(
            requestPath: null,
            projectPath: "/tmp/project",
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.HasValidationErrors);
        Assert.Same(error, result.Error);
        Assert.Null(result.PreparedRequest);
        Assert.Empty(result.ValidationErrors);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_PropagatesCancellationTokenToDependencies ()
    {
        var token = new CancellationTokenSource().Token;
        var preparedRequest = CreatePreparedRequestContext();
        var requestPreparationService = new SpyRequestPreparationService(
            RequestPreparationResult.Success(preparedRequest));
        var validationService = new SpyRequestStaticValidationService(ValidationResult.Success());
        var service = new PhaseExecutionPreflightService(requestPreparationService, validationService);

        var result = await service.Prepare(
            requestPath: "request.json",
            projectPath: "/tmp/project",
            cancellationToken: token);

        Assert.True(result.IsSuccess);
        Assert.Equal(token, requestPreparationService.ReceivedToken);
        Assert.Equal(token, validationService.ReceivedToken);
        Assert.Same(preparedRequest.Request, validationService.ReceivedRequest);
        Assert.Same(preparedRequest.ProjectContext, validationService.ReceivedProjectContext);
    }

    private static PreparedRequestContext CreatePreparedRequestContext ()
    {
        return new PreparedRequestContext(
            RequestJson: """{"protocolVersion":1,"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","steps":[{"kind":"op","id":"step-1","op":"ucli.scene.open","args":{"path":"Assets/Scenes/Main.unity"}}]}""",
            InputSource: RequestInputSource.StandardInput,
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

    private sealed class StubRequestPreparationService : IRequestPreparationService
    {
        private readonly RequestPreparationResult result;

        public StubRequestPreparationService (RequestPreparationResult result)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public ValueTask<ParsedRequestResult> ReadAndParse (
            string? requestPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(CreateParsedRequestResult(result));
        }

        public ValueTask<RequestPreparationResult> Prepare (
            string? requestPath,
            string? projectPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class SpyRequestPreparationService : IRequestPreparationService
    {
        private readonly RequestPreparationResult result;

        public SpyRequestPreparationService (RequestPreparationResult result)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public CancellationToken ReceivedToken { get; private set; }

        public ValueTask<ParsedRequestResult> ReadAndParse (
            string? requestPath,
            CancellationToken cancellationToken = default)
        {
            ReceivedToken = cancellationToken;
            return ValueTask.FromResult(CreateParsedRequestResult(result));
        }

        public ValueTask<RequestPreparationResult> Prepare (
            string? requestPath,
            string? projectPath,
            CancellationToken cancellationToken = default)
        {
            ReceivedToken = cancellationToken;
            return ValueTask.FromResult(result);
        }
    }

    private static ParsedRequestResult CreateParsedRequestResult (RequestPreparationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!result.IsSuccess)
        {
            return ParsedRequestResult.Failure(result.Error!);
        }

        var preparedRequest = result.PreparedRequest!;
        return ParsedRequestResult.Success(new ParsedRequestContext(
            preparedRequest.RequestJson,
            preparedRequest.InputSource,
            preparedRequest.Request));
    }

    private sealed class StubRequestStaticValidationService : IRequestStaticValidationService
    {
        private readonly ValidationResult result;

        public StubRequestStaticValidationService (ValidationResult result)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public ValueTask<ValidationResult> Validate (
            ValidateRequest request,
            ProjectContext projectContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class SpyRequestStaticValidationService : IRequestStaticValidationService
    {
        private readonly ValidationResult result;

        public SpyRequestStaticValidationService ()
            : this(ValidationResult.Success())
        {
        }

        public SpyRequestStaticValidationService (ValidationResult result)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public int CallCount { get; private set; }

        public CancellationToken ReceivedToken { get; private set; }

        public ValidateRequest? ReceivedRequest { get; private set; }

        public ProjectContext? ReceivedProjectContext { get; private set; }

        public ValueTask<ValidationResult> Validate (
            ValidateRequest request,
            ProjectContext projectContext,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            ReceivedToken = cancellationToken;
            ReceivedRequest = request;
            ReceivedProjectContext = projectContext;
            return ValueTask.FromResult(result);
        }
    }
}