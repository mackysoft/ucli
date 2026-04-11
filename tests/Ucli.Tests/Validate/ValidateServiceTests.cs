using MackySoft.Ucli.Cli.Requests;
using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Context;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Operations;
using MackySoft.Ucli.ReadIndex;
using MackySoft.Ucli.UnityProject;
using MackySoft.Ucli.Validate;

namespace MackySoft.Ucli.Tests;

public sealed class ValidateServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenPreparationFails_ReturnsFailureWithoutOutput ()
    {
        var error = ExecutionError.InvalidArgument("request is invalid.");
        var service = new ValidateService(
            new StubRequestPreparationService(RequestPreparationResult.Failure(error)),
            new StubRequestStaticValidator(ValidationResult.Success()),
            new StubValidateMetadataResolver(CreateMetadataSuccessResult()));

        var result = await service.Execute(new ValidateCommandInput(null, "/tmp/project", null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.HasValidationErrors);
        Assert.Equal(IpcErrorCodes.InvalidArgument, result.ErrorCode);
        Assert.Null(result.Output);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenReadIndexModeIsInvalid_ReturnsFailureWithoutOutput ()
    {
        var service = new ValidateService(
            new StubRequestPreparationService(
                RequestPreparationResult.Success(CreatePreparedRequestContext())),
            new StubRequestStaticValidator(ValidationResult.Success()),
            new StubValidateMetadataResolver(CreateMetadataSuccessResult()));

        var result = await service.Execute(new ValidateCommandInput(null, "/tmp/project", "unsupported"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.HasValidationErrors);
        Assert.Equal(IpcErrorCodes.InvalidArgument, result.ErrorCode);
        Assert.Null(result.Output);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenMetadataResolutionFails_ReturnsFailureWithReadIndexOutput ()
    {
        var service = new ValidateService(
            new StubRequestPreparationService(RequestPreparationResult.Success(CreatePreparedRequestContext())),
            new StubRequestStaticValidator(ValidationResult.Success()),
            new StubValidateMetadataResolver(ValidateMetadataResolutionResult.Failure(
                CreateReadIndexInfo(
                    used: false,
                    hit: false,
                    freshness: ReadIndexInfoTextCodec.FreshnessProbable),
                IpcErrorCodes.ReadIndexFormatInvalid,
                "Index contract file 'ops.catalog.json' is malformed.")));

        var result = await service.Execute(new ValidateCommandInput(null, "/tmp/project", null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.HasValidationErrors);
        Assert.Equal(IpcErrorCodes.ReadIndexFormatInvalid, result.ErrorCode);
        Assert.NotNull(result.Output);
        Assert.False(result.Output!.ReadIndex.Used);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenStaticValidationReturnsErrors_ReturnsValidationFailure ()
    {
        ValidationError[] validationErrors =
        [
            new ValidationError(
                ValidationErrorCodes.OperationArgsInvalid,
                "Operation args are invalid.",
                "step-1"),
        ];
        var service = new ValidateService(
            new StubRequestPreparationService(RequestPreparationResult.Success(CreatePreparedRequestContext())),
            new StubRequestStaticValidator(new ValidationResult(validationErrors)),
            new StubValidateMetadataResolver(CreateMetadataSuccessResult()));

        var result = await service.Execute(new ValidateCommandInput(null, "/tmp/project", null), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.True(result.HasValidationErrors);
        Assert.Null(result.ErrorCode);
        Assert.NotNull(result.Output);
        Assert.Single(result.ValidationErrors);
        Assert.Equal(ValidationErrorCodes.OperationArgsInvalid, result.ValidationErrors[0].Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenValidationSucceeds_ReturnsSuccess ()
    {
        var validator = new SpyRequestStaticValidator(ValidationResult.Success());
        var metadataResolver = new StubValidateMetadataResolver(CreateMetadataSuccessResult());
        var service = new ValidateService(
            new StubRequestPreparationService(RequestPreparationResult.Success(CreatePreparedRequestContext())),
            validator,
            metadataResolver);

        var result = await service.Execute(new ValidateCommandInput(null, "/tmp/project", null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Static validation passed.", result.Message);
        Assert.NotNull(result.Output);
        Assert.True(result.Output!.ReadIndex.Used);
        Assert.True(validator.LastCatalog!.IsAvailable);
        Assert.Single(validator.LastCatalog.Operations);
        Assert.Equal(1, metadataResolver.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenExplicitReadIndexModeIsDisabled_SkipsProjectPreparationAndMetadataResolution ()
    {
        var parsedRequest = CreateParsedRequestContext();
        var requestPreparationService = new StubRequestPreparationService(
            RequestPreparationResult.Failure(ExecutionError.InvalidArgument("project is invalid.")),
            ParsedRequestResult.Success(parsedRequest));
        var validator = new SpyRequestStaticValidator(ValidationResult.Success());
        var metadataResolver = new StubValidateMetadataResolver(CreateMetadataSuccessResult());
        var service = new ValidateService(
            requestPreparationService,
            validator,
            metadataResolver);

        var result = await service.Execute(
            new ValidateCommandInput(null, null, ReadIndexModeValues.Disabled),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.False(result.Output!.ReadIndex.Used);
        Assert.False(result.Output.ReadIndex.Hit);
        Assert.Equal("readIndex disabled by mode.", result.Output.ReadIndex.FallbackReason);
        Assert.False(validator.LastCatalog!.IsAvailable);
        Assert.Equal(0, metadataResolver.CallCount);
        Assert.Equal(1, requestPreparationService.ReadAndParseCallCount);
        Assert.Equal(0, requestPreparationService.PrepareCallCount);
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

    private static ValidateMetadataResolutionResult CreateMetadataSuccessResult ()
    {
        return ValidateMetadataResolutionResult.Success(
            RequestStaticValidationCatalog.Available(
            [
                new UcliOperationDescriptor(
                    Name: "ucli.scene.open",
                    Kind: UcliOperationKind.Query,
                    Policy: OperationPolicy.Safe,
                    ArgsSchemaJson: """{"type":"object"}"""),
            ]),
            CreateReadIndexInfo(
                used: true,
                hit: true,
                freshness: ReadIndexInfoTextCodec.FreshnessProbable));
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
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
            FallbackReason: null);
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

    private sealed class StubValidateMetadataResolver : IValidateMetadataResolver
    {
        private readonly ValidateMetadataResolutionResult result;

        public StubValidateMetadataResolver (ValidateMetadataResolutionResult result)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public int CallCount { get; private set; }

        public ValueTask<ValidateMetadataResolutionResult> Resolve (
            ResolvedUnityProjectContext unityProject,
            ReadIndexMode readIndexMode,
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
            LastCatalog = catalog;
            return ValueTask.FromResult(result);
        }
    }
}