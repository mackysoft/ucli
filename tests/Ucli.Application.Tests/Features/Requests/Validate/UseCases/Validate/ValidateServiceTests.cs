using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Features.Requests.Validate.UseCases.Validate;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Tests;

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
                freshness: IndexFreshness.Probable)));
        var service = new ValidateService(
            new StubRequestPreparationService(RequestPreparationResult.Failure(ExecutionError.InvalidArgument("project path is invalid."))),
            new StubRequestStaticValidator(ValidationResult.Success()),
            preflightService);

        var result = await service.ExecuteAsync(
            new ValidateCommandInput("/tmp/project", null, """{"steps":[]}"""),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, error.Code);
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
                freshness: IndexFreshness.Probable),
            ReadIndexErrorCodes.ReadIndexFormatInvalid));
        var service = new ValidateService(
            new StubRequestPreparationService(RequestPreparationResult.Success(CreatePreparedRequestContext())),
            new StubRequestStaticValidator(ValidationResult.Success()),
            preflightService);

        var result = await service.ExecuteAsync(
            new ValidateCommandInput("/tmp/project", null, """{"steps":[]}"""),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ReadIndexErrorCodes.ReadIndexFormatInvalid, error.Code);
        Assert.NotNull(result.Output);
        Assert.False(result.Output!.ReadIndex.Used);
        Assert.Equal(1, preflightService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenValidateTimeoutConfigIsInvalid_ReturnsFailureWithoutSharedPreflight ()
    {
        var timeoutOverrides = new Dictionary<string, int?>(UcliConfig.CreateDefault().IpcTimeoutMillisecondsByCommand, StringComparer.Ordinal)
        {
            [UcliCommandIds.Validate.Name] = 0,
        };
        var config = UcliConfig.CreateDefault() with
        {
            IpcTimeoutMillisecondsByCommand = timeoutOverrides,
        };
        var preflightService = new StubRequestStaticValidationPreflightService(RequestStaticValidationPreflightResult.Success(
            CreatePreparedRequestContext(),
            CreateReadIndexInfo(
                used: true,
                hit: true,
                freshness: IndexFreshness.Probable)));
        var service = new ValidateService(
            new StubRequestPreparationService(RequestPreparationResult.Success(CreatePreparedRequestContext(config))),
            new StubRequestStaticValidator(ValidationResult.Success()),
            preflightService);

        var result = await service.ExecuteAsync(
            new ValidateCommandInput("/tmp/project", null, """{"steps":[]}"""),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, error.Code);
        Assert.Contains("ipcTimeoutMillisecondsByCommand[validate]", error.Message, StringComparison.Ordinal);
        Assert.Equal(0, preflightService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenExplicitTimeoutElapsesDuringSharedPreflight_ReturnsTimeoutFailure ()
    {
        var timeProvider = new ManualTimeProvider();
        var preflightService = new StubRequestStaticValidationPreflightService(
            RequestStaticValidationPreflightResult.Success(
                CreatePreparedRequestContext(),
                CreateReadIndexInfo(
                    used: true,
                    hit: true,
                    freshness: IndexFreshness.Probable)),
            cancellationToken =>
            {
                timeProvider.Advance(TimeSpan.FromMilliseconds(101));
                cancellationToken.ThrowIfCancellationRequested();
            });
        var service = new ValidateService(
            new StubRequestPreparationService(RequestPreparationResult.Success(CreatePreparedRequestContext())),
            new StubRequestStaticValidator(ValidationResult.Success()),
            preflightService,
            timeProvider);

        var result = await service.ExecuteAsync(
            new ValidateCommandInput("/tmp/project", null, """{"steps":[]}""")
            {
                TimeoutMilliseconds = 100,
            },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ApplicationFailureKind.Timeout, error.Kind);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, error.Code);
        Assert.Equal(1, preflightService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenConfigTimeoutElapsesDuringSharedPreflight_ReturnsTimeoutFailure ()
    {
        var timeProvider = new ManualTimeProvider();
        var config = CreateConfigWithValidateTimeout(timeoutMilliseconds: 100);
        var preparedRequest = CreatePreparedRequestContext(config);
        var preflightService = new StubRequestStaticValidationPreflightService(
            RequestStaticValidationPreflightResult.Success(
                preparedRequest,
                CreateReadIndexInfo(
                    used: true,
                    hit: true,
                    freshness: IndexFreshness.Probable)),
            cancellationToken =>
            {
                timeProvider.Advance(TimeSpan.FromMilliseconds(101));
                cancellationToken.ThrowIfCancellationRequested();
            });
        var service = new ValidateService(
            new StubRequestPreparationService(RequestPreparationResult.Success(preparedRequest)),
            new StubRequestStaticValidator(ValidationResult.Success()),
            preflightService,
            timeProvider);

        var result = await service.ExecuteAsync(
            new ValidateCommandInput("/tmp/project", null, """{"steps":[]}"""),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ApplicationFailureKind.Timeout, error.Kind);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, error.Code);
        Assert.Equal(1, preflightService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenReadIndexDisabledValidationTimesOut_ReturnsTimeoutFailureWithDisabledOutput ()
    {
        var timeProvider = new ManualTimeProvider();
        var validator = new StubRequestStaticValidator(
            ValidationResult.Success(),
            cancellationToken =>
            {
                timeProvider.Advance(TimeSpan.FromMilliseconds(101));
                cancellationToken.ThrowIfCancellationRequested();
            });
        var preflightService = new StubRequestStaticValidationPreflightService(RequestStaticValidationPreflightResult.Success(
            CreatePreparedRequestContext(),
            CreateReadIndexInfo(
                used: true,
                hit: true,
                freshness: IndexFreshness.Probable)));
        var service = new ValidateService(
            new StubRequestPreparationService(RequestPreparationResult.Success(CreatePreparedRequestContext())),
            validator,
            preflightService,
            timeProvider);

        var result = await service.ExecuteAsync(
            new ValidateCommandInput("/tmp/project", ReadIndexMode.Disabled, """{"steps":[]}""")
            {
                TimeoutMilliseconds = 100,
            },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.False(result.Output!.ReadIndex.Used);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ApplicationFailureKind.Timeout, error.Kind);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, error.Code);
        Assert.Equal(0, preflightService.CallCount);
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
                freshness: IndexFreshness.Probable),
            validationErrors));
        var service = new ValidateService(
            new StubRequestPreparationService(RequestPreparationResult.Success(CreatePreparedRequestContext())),
            new StubRequestStaticValidator(ValidationResult.Success()),
            preflightService);

        var result = await service.ExecuteAsync(
            new ValidateCommandInput("/tmp/project", null, """{"steps":[]}"""),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Output);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ValidationErrorCodes.OperationArgsInvalid, error.Code);
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
                freshness: IndexFreshness.Probable)));
        var service = new ValidateService(
            new StubRequestPreparationService(RequestPreparationResult.Success(CreatePreparedRequestContext())),
            new StubRequestStaticValidator(ValidationResult.Success()),
            preflightService);

        var result = await service.ExecuteAsync(
            new ValidateCommandInput("/tmp/project", null, """{"steps":[]}"""),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Static validation passed.", result.Message);
        Assert.NotNull(result.Output);
        Assert.True(result.Output!.ReadIndex.Used);
        Assert.Equal(1, preflightService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenExplicitReadIndexModeIsDisabled_RequiresProjectPreparationAndSkipsSharedPreflight ()
    {
        var requestPreparationService = new StubRequestPreparationService(
            RequestPreparationResult.Success(CreatePreparedRequestContext()));
        var validator = new SpyRequestStaticValidator(ValidationResult.Success());
        var preflightService = new StubRequestStaticValidationPreflightService(RequestStaticValidationPreflightResult.Success(
            CreatePreparedRequestContext(),
            CreateReadIndexInfo(
                used: true,
                hit: true,
                freshness: IndexFreshness.Probable)));
        var service = new ValidateService(
            requestPreparationService,
            validator,
            preflightService);

        var result = await service.ExecuteAsync(
            new ValidateCommandInput(null, ReadIndexMode.Disabled, """{"steps":[]}"""),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Output);
        Assert.False(result.Output!.ReadIndex.Used);
        Assert.False(result.Output.ReadIndex.Hit);
        Assert.Equal("readIndex disabled by mode.", result.Output.ReadIndex.FallbackReason);
        Assert.False(validator.LastCatalog!.IsAvailable);
        Assert.Equal("/tmp/project", result.Output.Project.ProjectPath);
        Assert.Equal(0, requestPreparationService.ParseCallCount);
        Assert.Equal(1, requestPreparationService.PrepareCallCount);
        Assert.Equal(0, preflightService.CallCount);
    }

    private static ParsedRequestContext CreateParsedRequestContext ()
    {
        return new ParsedRequestContext(
            RequestJson: """{"protocolVersion":1,"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","steps":[]}""",
            Request: new ValidateRequest(
                ProtocolVersion: 1,
                RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
                Steps: Array.Empty<ValidateRequestStep?>()));
    }

    private static PreparedRequestContext CreatePreparedRequestContext (UcliConfig? config = null)
    {
        return new PreparedRequestContext(
            RequestJson: """{"protocolVersion":1,"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","steps":[]}""",
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
                config ?? UcliConfig.CreateDefault(),
                ConfigSource.Default));
    }

    private static UcliConfig CreateConfigWithValidateTimeout (int? timeoutMilliseconds)
    {
        var timeoutOverrides = new Dictionary<string, int?>(UcliConfig.CreateDefault().IpcTimeoutMillisecondsByCommand, StringComparer.Ordinal)
        {
            [UcliCommandIds.Validate.Name] = timeoutMilliseconds,
        };

        return UcliConfig.CreateDefault() with
        {
            IpcTimeoutMillisecondsByCommand = timeoutOverrides,
        };
    }

    private static ReadIndexInfo CreateReadIndexInfo (
        bool used,
        bool hit,
        IndexFreshness freshness)
    {
        return new ReadIndexInfo(
            Used: used,
            Hit: hit,
            Source: ReadIndexInfoSource.Index,
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

        public int ParseCallCount { get; private set; }

        public int PrepareCallCount { get; private set; }

        public ParsedRequestResult Parse (string requestJson)
        {
            ParseCallCount++;
            return readAndParseResult;
        }

        public ValueTask<RequestPreparationResult> PrepareAsync (
            string? projectPath,
            string requestJson,
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
                preparedRequest.Request));
        }
    }

    private sealed class StubRequestStaticValidationPreflightService : IRequestStaticValidationPreflightService
    {
        private readonly RequestStaticValidationPreflightResult result;

        private readonly Action<CancellationToken>? onPrepare;

        public StubRequestStaticValidationPreflightService (
            RequestStaticValidationPreflightResult result,
            Action<CancellationToken>? onPrepare = null)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
            this.onPrepare = onPrepare;
        }

        public int CallCount { get; private set; }

        public ValueTask<RequestStaticValidationPreflightResult> PrepareAsync (
            PreparedRequestContext preparedRequest,
            ReadIndexMode? readIndexMode,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            onPrepare?.Invoke(cancellationToken);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class StubRequestStaticValidator : IRequestStaticValidator
    {
        private readonly ValidationResult result;

        private readonly Action<CancellationToken>? onValidate;

        public StubRequestStaticValidator (
            ValidationResult result,
            Action<CancellationToken>? onValidate = null)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
            this.onValidate = onValidate;
        }

        public ValueTask<ValidationResult> ValidateAsync (
            ValidateRequest request,
            RequestStaticValidationCatalog catalog,
            UcliConfig config,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            onValidate?.Invoke(cancellationToken);
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

        public ValueTask<ValidationResult> ValidateAsync (
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
