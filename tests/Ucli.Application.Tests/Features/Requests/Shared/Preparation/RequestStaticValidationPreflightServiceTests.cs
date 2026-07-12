using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Tests;

public sealed class RequestStaticValidationPreflightServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_WhenReadIndexModeIsSpecified_UsesExplicitMode ()
    {
        var preparedRequest = CreatePreparedRequestContext();
        var resolver = new RecordingReadIndexValidationCatalogResolver
        {
            Result = CreateCatalogSuccessResult(),
        };
        var service = new RequestStaticValidationPreflightService(
            resolver,
            new RecordingRequestStaticValidator
            {
                Result = ValidationResult.Success(),
            });

        var result = await service.PrepareAsync(
            preparedRequest,
            readIndexMode: ReadIndexMode.RequireFresh,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        RequestStaticValidationInvocationAssert.ReadIndexCatalogResolvedForPreparedProject(
            resolver,
            preparedRequest,
            ReadIndexMode.RequireFresh);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_WhenValidationInfrastructureFails_ReturnsFailureWithReadIndexOutput ()
    {
        var preparedRequest = CreatePreparedRequestContext();
        var readIndex = CreateReadIndexInfo(
            used: true,
            hit: true,
            freshness: IndexFreshness.Probable,
            fallbackReason: null);
        var resolver = new RecordingReadIndexValidationCatalogResolver
        {
            Result = CreateCatalogSuccessResult(),
        };
        var service = new RequestStaticValidationPreflightService(
            resolver,
            new RecordingRequestStaticValidator
            {
                Result = ValidationResult.Failure(ExecutionError.Timeout("Static validation timed out.")),
            });

        var result = await service.PrepareAsync(
            preparedRequest,
            readIndexMode: null,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.ErrorCode);
        Assert.Same(preparedRequest, result.PreparedRequest);
        Assert.NotNull(result.ReadIndex);
        Assert.Equal(readIndex.GeneratedAtUtc, result.ReadIndex!.GeneratedAtUtc);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_WhenMetadataResolutionFails_ReturnsFailureWithReadIndexOutput ()
    {
        var preparedRequest = CreatePreparedRequestContext();
        var readIndex = CreateReadIndexInfo(
            used: false,
            hit: false,
            freshness: IndexFreshness.Probable,
            fallbackReason: "Index contract file 'ops.catalog.json' is malformed.");
        var validator = new RecordingRequestStaticValidator
        {
            Result = ValidationResult.Success(),
        };
        var resolver = new RecordingReadIndexValidationCatalogResolver
        {
            Result = ReadIndexValidationCatalogResolutionResult.Failure(
                readIndex,
                ReadIndexErrorCodes.ReadIndexFormatInvalid,
                "Index contract file 'ops.catalog.json' is malformed."),
        };
        var service = new RequestStaticValidationPreflightService(
            resolver,
            validator);

        var result = await service.PrepareAsync(
            preparedRequest,
            readIndexMode: null,
            cancellationToken: CancellationToken.None);

        RequestStaticValidationInvocationAssert.MetadataResolutionFailureReturnedBeforeStaticValidation(
            result,
            preparedRequest,
            readIndex,
            ReadIndexErrorCodes.ReadIndexFormatInvalid,
            "ops.catalog.json",
            validator);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_WhenStaticValidationFails_ReturnsValidationErrors ()
    {
        var preparedRequest = CreatePreparedRequestContext();
        var readIndex = CreateReadIndexInfo(
            used: true,
            hit: true,
            freshness: IndexFreshness.Probable,
            fallbackReason: null);
        ValidationError[] validationErrors =
        [
            new ValidationError(
                ValidationErrorCodes.OperationArgsInvalid,
                "Operation args are invalid.",
                "step-1"),
        ];
        var validator = new RecordingRequestStaticValidator
        {
            Result = new ValidationResult(validationErrors),
        };
        var resolver = new RecordingReadIndexValidationCatalogResolver
        {
            Result = CreateCatalogSuccessResult(readIndex),
        };
        var service = new RequestStaticValidationPreflightService(
            resolver,
            validator);

        var result = await service.PrepareAsync(
            preparedRequest,
            readIndexMode: null,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.True(result.HasValidationErrors);
        Assert.Same(preparedRequest, result.PreparedRequest);
        Assert.Same(readIndex, result.ReadIndex);
        Assert.Single(result.ValidationErrors);
        Assert.Equal(ValidationErrorCodes.OperationArgsInvalid, result.ValidationErrors[0].Code);
        RequestStaticValidationInvocationAssert.PureStaticValidationReceivedAvailableOperationCatalog(
            validator,
            preparedRequest,
            "ucli.scene.open");
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_WhenDependenciesSucceed_ReturnsPreparedRequestAndReadIndex ()
    {
        var preparedRequest = CreatePreparedRequestContext();
        var readIndex = CreateReadIndexInfo(
            used: true,
            hit: true,
            freshness: IndexFreshness.Probable,
            fallbackReason: null);
        var validator = new RecordingRequestStaticValidator
        {
            Result = ValidationResult.Success(),
        };
        var resolver = new RecordingReadIndexValidationCatalogResolver
        {
            Result = CreateCatalogSuccessResult(readIndex),
        };
        var service = new RequestStaticValidationPreflightService(
            resolver,
            validator);

        var result = await service.PrepareAsync(
            preparedRequest,
            readIndexMode: null,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(preparedRequest, result.PreparedRequest);
        Assert.Same(readIndex, result.ReadIndex);
        Assert.Empty(result.ValidationErrors);
        Assert.Null(result.Error);
        Assert.Null(result.ErrorCode);
        RequestStaticValidationInvocationAssert.PureStaticValidationReceivedAvailableOperationCatalog(
            validator,
            preparedRequest,
            "ucli.scene.open");
        RequestStaticValidationInvocationAssert.ReadIndexCatalogResolvedForPreparedProject(
            resolver,
            preparedRequest,
            preparedRequest.ProjectContext.Config.ReadIndexDefaultMode);
    }

    private static PreparedRequestContext CreatePreparedRequestContext ()
    {
        return new PreparedRequestContext(
            requestJson: """{"protocolVersion":1,"steps":[]}""",
            request: new ValidateRequest(
                ProtocolVersion: 1,
                Steps: Array.Empty<ValidateRequestStep?>()),
            projectContext: ProjectContextTestFactory.CreateTemporaryFixtureProject());
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
                freshness: IndexFreshness.Probable,
                fallbackReason: null));
    }

    private static ReadIndexInfo CreateReadIndexInfo (
        bool used,
        bool hit,
        IndexFreshness freshness,
        string? fallbackReason)
    {
        return new ReadIndexInfo(
            Used: used,
            Hit: hit,
            Source: ReadIndexInfoSource.Index,
            Freshness: freshness,
            GeneratedAtUtc: used
                ? DateTimeOffset.Parse("2026-03-06T00:00:00+00:00")
                : null,
            FallbackReason: fallbackReason);
    }

}
