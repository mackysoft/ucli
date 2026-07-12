namespace MackySoft.Ucli.Application.Tests;

using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.RequestStaticValidatorTestSupport;

public sealed class RequestStaticValidatorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_AddsExpectedError_WhenRequestIsInvalid ()
    {
        foreach (var testCase in InvalidRequestCases)
        {
            var validator = CreateValidator();
            var request = CreateInvalidRequest(testCase.Scenario);

            var result = await validator.ValidateAsync(request, ValidationUnityProject, CreateConfig(OperationPolicy.Safe, "^ucli\\."), CancellationToken.None);

            AssertContainsError(result, testCase.ExpectedErrorCode);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_AddsRequiredErrors_WhenStepsContainsNullElement ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                null,
            ]);

        var result = await validator.ValidateAsync(request, ValidationUnityProject, CreateConfig(OperationPolicy.Safe, "^ucli\\."), CancellationToken.None);

        Assert.False(result.IsValid);
        AssertContainsError(result, ValidationErrorCodes.StepIdRequired);
        AssertContainsError(result, ValidationErrorCodes.StepKindRequired);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_AllowsEmptyStepsAsNoOpRequest ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(steps: Array.Empty<ValidateRequestStep?>());

        var result = await validator.ValidateAsync(request, ValidationUnityProject, CreateConfig(OperationPolicy.Safe, "^ucli\\."), CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenOperationsAreNotProvided_SkipsMetadataDependentChecks ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep("step-1", "ucli.unknown", new
                {
                }),
            ]);

        var result = await validator.ValidateAsync(
            request,
            RequestStaticValidationCatalog.Unavailable,
            CreateConfig(OperationPolicy.Safe, "^ucli\\."),
            CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenEmptyStepsAndProtocolVersionIsInvalid_PreservesProtocolError ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            protocolVersion: IpcProtocol.CurrentVersion + 1,
            steps: Array.Empty<ValidateRequestStep?>());

        var result = await validator.ValidateAsync(request, ValidationUnityProject, CreateConfig(OperationPolicy.Safe, "^ucli\\."), CancellationToken.None);

        Assert.False(result.IsValid);
        AssertContainsError(result, IpcProtocolErrorCodes.ProtocolVersionMismatch);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_ReturnsValidResult_WhenRequestSatisfiesAllChecks ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            steps:
            [
                CreateOpStep("step-1", UcliPrimitiveOperationNames.SceneOpen, new
                {
                    path = "Assets/Scenes/Main.unity",
                }),
                CreateOpStep("step-2", UcliPrimitiveOperationNames.SceneTree, new
                {
                    path = "Assets/Scenes/Main.unity",
                }),
            ]);

        var result = await validator.ValidateAsync(request, ValidationUnityProject, CreateConfig(OperationPolicy.Safe, "^ucli\\."), CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Null(result.Error);
    }

}
