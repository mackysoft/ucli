using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Requests.Input;
using MackySoft.Ucli.Shared.Configuration;
using MackySoft.Ucli.Shared.Context;
using MackySoft.Ucli.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Indexing.ReadIndex;
using MackySoft.Ucli.Shared.Context.Project;

namespace MackySoft.Ucli.Tests;

public sealed class RequestStaticValidationPreflightServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_WhenReadIndexModeIsSpecified_UsesExplicitMode ()
    {
        var resolver = new StubReadIndexValidationCatalogResolver(CreateCatalogSuccessResult());
        var service = new RequestStaticValidationPreflightService(
            resolver,
            new StubRequestStaticValidator(ValidationResult.Success()));

        var result = await service.Prepare(
            CreatePreparedRequestContext(),
            readIndexMode: ReadIndexMode.RequireFresh,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, resolver.CallCount);
        Assert.Equal(ReadIndexMode.RequireFresh, resolver.ReceivedMode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_WhenValidationInfrastructureFails_ReturnsFailureWithReadIndexOutput ()
    {
        var preparedRequest = CreatePreparedRequestContext();
        var readIndex = CreateReadIndexInfo(
            used: true,
            hit: true,
            freshness: ReadIndexInfoTextCodec.FreshnessProbable,
            fallbackReason: null);
        var resolver = new StubReadIndexValidationCatalogResolver(CreateCatalogSuccessResult());
        var service = new RequestStaticValidationPreflightService(
            resolver,
            new StubRequestStaticValidator(ValidationResult.Failure(ExecutionError.Timeout("Static validation timed out."))));

        var result = await service.Prepare(
            preparedRequest,
            readIndexMode: null,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
        Assert.Same(preparedRequest, result.PreparedRequest);
        Assert.NotNull(result.ReadIndex);
        Assert.Equal(readIndex.GeneratedAtUtc, result.ReadIndex!.GeneratedAtUtc);
        Assert.Equal(1, resolver.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_WhenMetadataResolutionFails_ReturnsFailureWithReadIndexOutput ()
    {
        var preparedRequest = CreatePreparedRequestContext();
        var readIndex = CreateReadIndexInfo(
            used: false,
            hit: false,
            freshness: ReadIndexInfoTextCodec.FreshnessProbable,
            fallbackReason: "Index contract file 'ops.catalog.json' is malformed.");
        var validator = new SpyRequestStaticValidator(ValidationResult.Success());
        var resolver = new StubReadIndexValidationCatalogResolver(ReadIndexValidationCatalogResolutionResult.Failure(
            readIndex,
            IpcErrorCodes.ReadIndexFormatInvalid,
            "Index contract file 'ops.catalog.json' is malformed."));
        var service = new RequestStaticValidationPreflightService(
            resolver,
            validator);

        var result = await service.Prepare(
            preparedRequest,
            readIndexMode: null,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(IpcErrorCodes.ReadIndexFormatInvalid, result.ErrorCode);
        Assert.Same(preparedRequest, result.PreparedRequest);
        Assert.Same(readIndex, result.ReadIndex);
        Assert.Equal(0, validator.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_WhenStaticValidationFails_ReturnsValidationErrors ()
    {
        var preparedRequest = CreatePreparedRequestContext();
        var readIndex = CreateReadIndexInfo(
            used: true,
            hit: true,
            freshness: ReadIndexInfoTextCodec.FreshnessProbable,
            fallbackReason: null);
        ValidationError[] validationErrors =
        [
            new ValidationError(
                ValidationErrorCodes.OperationArgsInvalid,
                "Operation args are invalid.",
                "step-1"),
        ];
        var validator = new SpyRequestStaticValidator(new ValidationResult(validationErrors));
        var resolver = new StubReadIndexValidationCatalogResolver(CreateCatalogSuccessResult(readIndex));
        var service = new RequestStaticValidationPreflightService(
            resolver,
            validator);

        var result = await service.Prepare(
            preparedRequest,
            readIndexMode: null,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.True(result.HasValidationErrors);
        Assert.Same(preparedRequest, result.PreparedRequest);
        Assert.Same(readIndex, result.ReadIndex);
        Assert.Single(result.ValidationErrors);
        Assert.Equal(ValidationErrorCodes.OperationArgsInvalid, result.ValidationErrors[0].Code);
        Assert.True(validator.LastCatalog!.IsAvailable);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_WhenDependenciesSucceed_ReturnsPreparedRequestAndReadIndex ()
    {
        var preparedRequest = CreatePreparedRequestContext();
        var readIndex = CreateReadIndexInfo(
            used: true,
            hit: true,
            freshness: ReadIndexInfoTextCodec.FreshnessProbable,
            fallbackReason: null);
        var validator = new SpyRequestStaticValidator(ValidationResult.Success());
        var resolver = new StubReadIndexValidationCatalogResolver(CreateCatalogSuccessResult(readIndex));
        var service = new RequestStaticValidationPreflightService(
            resolver,
            validator);

        var result = await service.Prepare(
            preparedRequest,
            readIndexMode: null,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(preparedRequest, result.PreparedRequest);
        Assert.Same(readIndex, result.ReadIndex);
        Assert.Empty(result.ValidationErrors);
        Assert.Null(result.Error);
        Assert.Null(result.ErrorCode);
        Assert.True(validator.LastCatalog!.IsAvailable);
        Assert.Single(validator.LastCatalog.Operations);
        Assert.Equal(preparedRequest.ProjectContext.Config.ReadIndexDefaultMode, resolver.ReceivedMode);
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

    private static ReadIndexValidationCatalogResolutionResult CreateCatalogSuccessResult (ReadIndexInfo? readIndex = null)
    {
        return ReadIndexValidationCatalogResolutionResult.Success(
            RequestStaticValidationCatalog.Available(
            [
                new UcliOperationDescriptor(
                    Name: "ucli.scene.open",
                    Kind: UcliOperationKind.Query,
                    Policy: OperationPolicy.Safe,
                    ArgsSchemaJson: """{"type":"object"}"""),
            ]),
            readIndex ?? CreateReadIndexInfo(
                used: true,
                hit: true,
                freshness: ReadIndexInfoTextCodec.FreshnessProbable,
                fallbackReason: null));
    }

    private static ReadIndexInfo CreateReadIndexInfo (
        bool used,
        bool hit,
        string freshness,
        string? fallbackReason)
    {
        return new ReadIndexInfo(
            Used: used,
            Hit: hit,
            Source: ReadIndexInfoTextCodec.SourceIndex,
            Freshness: freshness,
            GeneratedAtUtc: used
                ? DateTimeOffset.Parse("2026-03-06T00:00:00+00:00")
                : null,
            FallbackReason: fallbackReason);
    }

    private sealed class StubReadIndexValidationCatalogResolver : IReadIndexValidationCatalogResolver
    {
        private readonly ReadIndexValidationCatalogResolutionResult result;

        public StubReadIndexValidationCatalogResolver (ReadIndexValidationCatalogResolutionResult result)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public int CallCount { get; private set; }

        public ValueTask<ReadIndexValidationCatalogResolutionResult> Resolve (
            ResolvedUnityProjectContext unityProject,
            ReadIndexMode readIndexMode,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            ReceivedMode = readIndexMode;
            return ValueTask.FromResult(result);
        }

        public ReadIndexMode ReceivedMode { get; private set; }
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

        public int CallCount { get; private set; }

        public ValueTask<ValidationResult> Validate (
            ValidateRequest request,
            RequestStaticValidationCatalog catalog,
            UcliConfig config,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastCatalog = catalog;
            return ValueTask.FromResult(result);
        }
    }
}