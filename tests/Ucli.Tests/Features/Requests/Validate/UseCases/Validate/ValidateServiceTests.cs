using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Validate.Common.Contracts;
using MackySoft.Ucli.Features.Requests.Validate.UseCases.Validate;
using MackySoft.Ucli.Hosting.Cli.Requests.Input;
using MackySoft.Ucli.Shared.Configuration;
using MackySoft.Ucli.Shared.Context;
using MackySoft.Ucli.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Indexing.ReadIndex;
using MackySoft.Ucli.UnityIntegration.Project;

namespace MackySoft.Ucli.Tests;

public sealed class ValidateServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenRequestPreparationFails_ReturnsFailureWithoutOutput ()
    {
        var preflightService = new StubRequestStaticValidationPreflightService(RequestStaticValidationPreflightResult.Success(
            CreatePreparedRequestContext(),
            CreateReadIndexInfo(
                used: true,
                hit: true,
                freshness: ReadIndexInfoTextCodec.FreshnessProbable)));
        var service = new ValidateService(
            new StubRequestPreparationService(RequestPreparationResult.Failure(ExecutionError.InvalidArgument("project path is invalid."))),
            new StubRequestStaticValidator(ValidationResult.Success()),
            preflightService);

        var result = await service.Execute(
            new ValidateCommandInput(null, "/tmp/project", null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.HasValidationErrors);
        Assert.Equal(IpcErrorCodes.InvalidArgument, result.ErrorCode);
        Assert.Null(result.Output);
        Assert.Equal(0, preflightService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenSharedPreflightFails_ReturnsFailureWithReadIndexOutput ()
    {
        var preflightService = new StubRequestStaticValidationPreflightService(RequestStaticValidationPreflightResult.Failure(
            ExecutionError.InternalError("Index contract file 'ops.catalog.json' is malformed."),
            CreatePreparedRequestContext(),
            CreateReadIndexInfo(
                used: false,
                hit: false,
                freshness: ReadIndexInfoTextCodec.FreshnessProbable),
            IpcErrorCodes.ReadIndexFormatInvalid));
        var service = new ValidateService(
            new StubRequestPreparationService(RequestPreparationResult.Success(CreatePreparedRequestContext())),
            new StubRequestStaticValidator(ValidationResult.Success()),
            preflightService);

        var result = await service.Execute(
            new ValidateCommandInput(null, "/tmp/project", null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.HasValidationErrors);
        Assert.Equal(IpcErrorCodes.ReadIndexFormatInvalid, result.ErrorCode);
        Assert.NotNull(result.Output);
        Assert.False(result.Output!.ReadIndex.Used);
        Assert.Equal(1, preflightService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenSharedPreflightHasValidationErrors_ReturnsValidationFailure ()
    {
        ValidationError[] validationErrors =
        [
            new ValidationError(
                ValidationErrorCodes.OperationArgsInvalid,
                "Operation args are invalid.",
                "step-1"),
        ];
        var preflightService = new StubRequestStaticValidationPreflightService(RequestStaticValidationPreflightResult.ValidationFailure(
            CreatePreparedRequestContext(),
            CreateReadIndexInfo(
                used: true,
                hit: true,
                freshness: ReadIndexInfoTextCodec.FreshnessProbable),
            validationErrors));
        var service = new ValidateService(
            new StubRequestPreparationService(RequestPreparationResult.Success(CreatePreparedRequestContext())),
            new StubRequestStaticValidator(ValidationResult.Success()),
            preflightService);

        var result = await service.Execute(
            new ValidateCommandInput(null, "/tmp/project", null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.True(result.HasValidationErrors);
        Assert.Null(result.ErrorCode);
        Assert.NotNull(result.Output);
        Assert.Single(result.ValidationErrors);
        Assert.Equal(ValidationErrorCodes.OperationArgsInvalid, result.ValidationErrors[0].Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenSharedPreflightSucceeds_ReturnsSuccess ()
    {
        var preflightService = new StubRequestStaticValidationPreflightService(RequestStaticValidationPreflightResult.Success(
            CreatePreparedRequestContext(),
            CreateReadIndexInfo(
                used: true,
                hit: true,
                freshness: ReadIndexInfoTextCodec.FreshnessProbable)));
        var service = new ValidateService(
            new StubRequestPreparationService(RequestPreparationResult.Success(CreatePreparedRequestContext())),
            new StubRequestStaticValidator(ValidationResult.Success()),
            preflightService);

        var result = await service.Execute(
            new ValidateCommandInput(null, "/tmp/project", null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Static validation passed.", result.Message);
        Assert.NotNull(result.Output);
        Assert.True(result.Output!.ReadIndex.Used);
        Assert.Equal(1, preflightService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenExplicitReadIndexModeIsDisabled_SkipsProjectPreparationAndSharedPreflight ()
    {
        var parsedRequest = CreateParsedRequestContext();
        var requestPreparationService = new StubRequestPreparationService(
            RequestPreparationResult.Failure(ExecutionError.InvalidArgument("project is invalid.")),
            ParsedRequestResult.Success(parsedRequest));
        var validator = new SpyRequestStaticValidator(ValidationResult.Success());
        var preflightService = new StubRequestStaticValidationPreflightService(RequestStaticValidationPreflightResult.Success(
            CreatePreparedRequestContext(),
            CreateReadIndexInfo(
                used: true,
                hit: true,
                freshness: ReadIndexInfoTextCodec.FreshnessProbable)));
        var service = new ValidateService(
            requestPreparationService,
            validator,
            preflightService);

        var result = await service.Execute(
            new ValidateCommandInput(null, null, ReadIndexMode.Disabled),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.False(result.Output!.ReadIndex.Used);
        Assert.False(result.Output.ReadIndex.Hit);
        Assert.Equal("readIndex disabled by mode.", result.Output.ReadIndex.FallbackReason);
        Assert.False(validator.LastCatalog!.IsAvailable);
        Assert.Equal(1, requestPreparationService.ReadAndParseCallCount);
        Assert.Equal(0, requestPreparationService.PrepareCallCount);
        Assert.Equal(0, preflightService.CallCount);
    }

    private static ParsedRequestContext CreateParsedRequestContext ()
    {
        return new ParsedRequestContext(
            RequestJson: """{"protocolVersion":1,"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","steps":[]}""",
            InputSource: RequestInputSource.StandardInput,
            Request: new ValidateRequest(
                ProtocolVersion: 1,
                RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                Steps: Array.Empty<ValidateRequestStep?>()));
    }

    private static PreparedRequestContext CreatePreparedRequestContext ()
    {
        return new PreparedRequestContext(
            RequestJson: """{"protocolVersion":1,"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","steps":[]}""",
            InputSource: RequestInputSource.StandardInput,
            Request: new ValidateRequest(
                ProtocolVersion: 1,
                RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                Steps: Array.Empty<ValidateRequestStep?>()),
            ProjectContext: new ProjectContext(
                new ResolvedUnityProjectContext(
                    UnityProjectRoot: "/tmp/project",
                    RepositoryRoot: "/tmp/repository",
                    ProjectFingerprint: "project-fingerprint",
                    PathSource: UnityProjectPathSource.CommandOption),
                UcliConfig.CreateDefault(),
                ConfigSource.Default));
    }

    private static ReadIndexInfo CreateReadIndexInfo (
        bool used,
        bool hit,
        string freshness)
    {
        return new ReadIndexInfo(
            Used: used,
            Hit: hit,
            Source: ReadIndexInfoTextCodec.SourceIndex,
            Freshness: freshness,
            GeneratedAtUtc: used
                ? DateTimeOffset.Parse("2026-03-06T00:00:00+00:00")
                : null,
            FallbackReason: used
                ? null
                : "readIndex disabled by mode.");
    }

    private sealed class StubRequestPreparationService : IRequestPreparationService
    {
        private readonly RequestPreparationResult prepareResult;

        private readonly ParsedRequestResult readAndParseResult;

        public StubRequestPreparationService (RequestPreparationResult prepareResult)
            : this(prepareResult, CreateParsedRequestResult(prepareResult))
        {
        }

        public StubRequestPreparationService (
            RequestPreparationResult prepareResult,
            ParsedRequestResult readAndParseResult)
        {
            this.prepareResult = prepareResult ?? throw new ArgumentNullException(nameof(prepareResult));
            this.readAndParseResult = readAndParseResult ?? throw new ArgumentNullException(nameof(readAndParseResult));
        }

        public int ReadAndParseCallCount { get; private set; }

        public int PrepareCallCount { get; private set; }

        public ValueTask<ParsedRequestResult> ReadAndParse (
            string? requestPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadAndParseCallCount++;
            return ValueTask.FromResult(readAndParseResult);
        }

        public ValueTask<RequestPreparationResult> Prepare (
            string? requestPath,
            string? projectPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PrepareCallCount++;
            return ValueTask.FromResult(prepareResult);
        }

        private static ParsedRequestResult CreateParsedRequestResult (RequestPreparationResult prepareResult)
        {
            ArgumentNullException.ThrowIfNull(prepareResult);

            if (!prepareResult.IsSuccess)
            {
                return ParsedRequestResult.Failure(prepareResult.Error!);
            }

            var preparedRequest = prepareResult.PreparedRequest!;
            return ParsedRequestResult.Success(new ParsedRequestContext(
                preparedRequest.RequestJson,
                preparedRequest.InputSource,
                preparedRequest.Request));
        }
    }

    private sealed class StubRequestStaticValidationPreflightService : IRequestStaticValidationPreflightService
    {
        private readonly RequestStaticValidationPreflightResult result;

        public StubRequestStaticValidationPreflightService (RequestStaticValidationPreflightResult result)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public int CallCount { get; private set; }

        public ValueTask<RequestStaticValidationPreflightResult> Prepare (
            PreparedRequestContext preparedRequest,
            ReadIndexMode? readIndexMode,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return ValueTask.FromResult(result);
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

        public SpyRequestStaticValidator (ValidationResult result)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public RequestStaticValidationCatalog? LastCatalog { get; private set; }

        public ValueTask<ValidationResult> Validate (
            ValidateRequest request,
            RequestStaticValidationCatalog catalog,
            UcliConfig config,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastCatalog = catalog;
            return ValueTask.FromResult(result);
        }
    }
}