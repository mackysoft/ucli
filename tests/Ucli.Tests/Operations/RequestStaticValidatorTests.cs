namespace MackySoft.Ucli.Tests;

using System.Text.Json;
using MackySoft.Ucli.Cli;
using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Operations;

public sealed class RequestStaticValidatorTests
{
    public static TheoryData<string, string> InvalidRequestCases => new()
    {
        { "protocol-version-mismatch", ValidationErrorCodes.ProtocolVersionMismatch },
        { "request-id-invalid", ValidationErrorCodes.RequestIdInvalid },
        { "request-id-not-canonical-d", ValidationErrorCodes.RequestIdInvalid },
        { "ops-required", ValidationErrorCodes.OpsRequired },
        { "op-id-duplicated", ValidationErrorCodes.OpIdDuplicated },
        { "operation-not-found", ValidationErrorCodes.OperationNotFound },
        { "operation-not-allowed", ValidationErrorCodes.OperationNotAllowed },
    };

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(InvalidRequestCases))]
    public async Task Validate_AddsExpectedError_WhenRequestIsInvalid (
        string scenario,
        string expectedErrorCode)
    {
        var validator = CreateValidator();
        var request = CreateInvalidRequest(scenario);

        var result = await validator.Validate(request, CreateConfig(OperationPolicy.Safe, "^ucli\\."), CancellationToken.None);

        AssertContainsError(result, expectedErrorCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_AddsRequiredErrors_WhenOpsContainsNullElement ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            ops:
            [
                null,
            ]);

        var result = await validator.Validate(request, CreateConfig(OperationPolicy.Safe, "^ucli\\."), CancellationToken.None);

        Assert.False(result.IsValid);
        AssertContainsError(result, ValidationErrorCodes.OpIdRequired);
        AssertContainsError(result, ValidationErrorCodes.OpNameRequired);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_ReturnsValidResult_WhenRequestSatisfiesAllChecks ()
    {
        var validator = CreateValidator();
        var request = CreateRequest(
            ops:
            [
                new ValidateRequestOperation("op-1", "ucli.scene.open", CreateArgs()),
                new ValidateRequestOperation("op-2", "ucli.scene.tree", CreateArgs()),
            ]);

        var result = await validator.Validate(request, CreateConfig(OperationPolicy.Safe, "^ucli\\."), CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    private static IRequestStaticValidator CreateValidator ()
    {
        var catalog = new OperationCatalog(new InMemoryOperationCatalogProvider());
        var authorizationService = new OperationAuthorizationService();
        return new RequestStaticValidator(catalog, authorizationService);
    }

    private static ValidateRequest CreateRequest (
        int protocolVersion = CliProtocol.CurrentVersion,
        string? requestId = null,
        IReadOnlyList<ValidateRequestOperation?>? ops = null)
    {
        return new ValidateRequest(
            ProtocolVersion: protocolVersion,
            RequestId: requestId ?? Guid.NewGuid().ToString(),
            Ops: ops ??
            [
                new ValidateRequestOperation("op-1", "ucli.scene.open", CreateArgs()),
            ]);
    }

    private static ValidateRequest CreateInvalidRequest (string scenario)
    {
        return scenario switch
        {
            "protocol-version-mismatch" => CreateRequest(protocolVersion: CliProtocol.CurrentVersion + 1),
            "request-id-invalid" => CreateRequest(requestId: "invalid-request-id"),
            "request-id-not-canonical-d" => CreateRequest(requestId: Guid.NewGuid().ToString("B")),
            "ops-required" => new ValidateRequest(
                ProtocolVersion: CliProtocol.CurrentVersion,
                RequestId: Guid.NewGuid().ToString(),
                Ops: null),
            "op-id-duplicated" => CreateRequest(
                ops:
                [
                    new ValidateRequestOperation("dup", "ucli.scene.open", CreateArgs()),
                    new ValidateRequestOperation("dup", "ucli.scene.tree", CreateArgs()),
                ]),
            "operation-not-found" => CreateRequest(
                ops:
                [
                    new ValidateRequestOperation("op-1", "ucli.unknown", CreateArgs()),
                ]),
            "operation-not-allowed" => CreateRequest(
                ops:
                [
                    new ValidateRequestOperation("op-1", "ucli.scene.save", CreateArgs()),
                ]),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unsupported invalid request scenario."),
        };
    }

    private static UcliConfig CreateConfig (
        OperationPolicy operationPolicy,
        params string[] allowlistPatterns)
    {
        return new UcliConfig(
            SchemaVersion: UcliContractConstants.Config.SchemaVersion,
            OperationPolicy: operationPolicy,
            PlanTokenMode: PlanTokenMode.Optional,
            OperationAllowlist: allowlistPatterns);
    }

    private static JsonElement CreateArgs ()
    {
        return JsonSerializer.SerializeToElement(new { });
    }

    private static void AssertContainsError (ValidationResult result, string errorCode)
    {
        Assert.Contains(
            result.Errors,
            error => string.Equals(error.Code, errorCode, StringComparison.Ordinal));
    }
}
