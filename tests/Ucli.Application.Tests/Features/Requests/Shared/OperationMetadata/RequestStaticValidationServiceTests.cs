using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Tests;

public sealed class RequestStaticValidationServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenCatalogLoadSucceeds_DelegatesToPureValidator ()
    {
        UcliOperationDescriptor[] operations =
        [
            new UcliOperationDescriptor(
                Name: "ucli.scene.open",
                Kind: UcliOperationKind.Query,
                Policy: OperationPolicy.Safe,
                ArgsSchemaJson: """{"type":"object"}"""),
        ];
        var pureValidator = new RecordingRequestStaticValidator
        {
            Result = ValidationResult.Success(),
        };
        var service = new RequestStaticValidationService(
            new RecordingOperationCatalog
            {
                Operations = operations,
            },
            pureValidator);
        var projectContext = ProjectContextTestFactory.CreateTemporaryFixtureProject();
        var request = CreateRequest();
        var token = new CancellationTokenSource().Token;

        var result = await service.ValidateAsync(request, projectContext, token);

        Assert.True(result.IsValid);
        RequestStaticValidationInvocationAssert.PureStaticValidationReceivedAvailableOperationCatalog(
            pureValidator,
            request,
            projectContext.Config,
            token,
            "ucli.scene.open");
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenCatalogLoadThrows_ReturnsFailureResult ()
    {
        var service = new RequestStaticValidationService(
            new RecordingOperationCatalog
            {
                ProjectGetAllException = new InvalidOperationException("catalog discovery failed"),
            },
            new RecordingRequestStaticValidator
            {
                Result = ValidationResult.Success(),
            });

        var result = await service.ValidateAsync(
            CreateRequest(),
            ProjectContextTestFactory.CreateTemporaryFixtureProject(),
            CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Empty(result.Errors);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("operation metadata", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenCatalogLoadThrowsTypedFailure_PreservesErrorKind ()
    {
        var service = new RequestStaticValidationService(
            new RecordingOperationCatalog
            {
                ProjectGetAllException = new OperationCatalogLoadException(
                    ExecutionError.Timeout("Timed out before operation metadata discovery could begin.")),
            },
            new RecordingRequestStaticValidator
            {
                Result = ValidationResult.Success(),
            });

        var result = await service.ValidateAsync(
            CreateRequest(),
            ProjectContextTestFactory.CreateTemporaryFixtureProject(),
            CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Empty(result.Errors);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, error.Code);
        Assert.Contains("Static validation could not load operation metadata.", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenPureValidatorReturnsError_PropagatesResult ()
    {
        var error = ExecutionError.InternalError("could not validate args.");
        var service = new RequestStaticValidationService(
            new RecordingOperationCatalog
            {
                Operations =
                [
                    new UcliOperationDescriptor(
                        Name: "ucli.scene.open",
                        Kind: UcliOperationKind.Query,
                        Policy: OperationPolicy.Safe,
                        ArgsSchemaJson: "{ invalid-schema"),
                ],
            },
            new RecordingRequestStaticValidator
            {
                Result = ValidationResult.Failure(error),
            });

        var result = await service.ValidateAsync(
            CreateRequest(),
            ProjectContextTestFactory.CreateTemporaryFixtureProject(),
            CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Same(error, result.Error);
    }

    private static ValidateRequest CreateRequest ()
    {
        return new ValidateRequest(
            ProtocolVersion: 1,
            RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
            Steps: Array.Empty<ValidateRequestStep?>());
    }

}
