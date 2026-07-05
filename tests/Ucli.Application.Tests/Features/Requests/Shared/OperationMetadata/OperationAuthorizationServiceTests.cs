namespace MackySoft.Ucli.Application.Tests;

using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

public sealed class OperationAuthorizationServiceTests
{
    private const string ArgsSchemaJson = """{"type":"object"}""";

    private static readonly AuthorizationCase[] AllowedAuthorizationCases =
    [
        new(UcliPrimitiveOperationNames.SceneOpen, UcliOperationKind.Command, OperationPolicy.Safe, OperationPolicy.Safe, "^ucli\\."),
        new(UcliPrimitiveOperationNames.CsEval, UcliOperationKind.Mutation, OperationPolicy.Dangerous, OperationPolicy.Dangerous, "^ucli\\."),
    ];

    private static readonly DeniedAuthorizationCase[] DeniedAuthorizationCases =
    [
        new(UcliPrimitiveOperationNames.SceneSave, UcliOperationKind.Mutation, OperationPolicy.Advanced, OperationPolicy.Safe, "^ucli\\.", ExpectedMessageContains: null),
        new(UcliPrimitiveOperationNames.SceneOpen, UcliOperationKind.Command, OperationPolicy.Safe, OperationPolicy.Safe, "^myorg\\.", ExpectedMessageContains: null),
        new(UcliPrimitiveOperationNames.SceneOpen, UcliOperationKind.Command, OperationPolicy.Safe, OperationPolicy.Safe, "[", ExpectedMessageContains: "invalid regex"),
    ];

    [Fact]
    [Trait("Size", "Small")]
    public async Task Authorize_ReturnsAllowed_WhenPolicyAndAllowlistPermitOperation ()
    {
        foreach (AuthorizationCase testCase in AllowedAuthorizationCases)
        {
            var service = new OperationAuthorizationService();
            var operation = CreateOperation(
                testCase.OperationName,
                testCase.OperationKind,
                testCase.RequiredPolicy);
            var config = CreateConfig(testCase.ConfiguredPolicy, testCase.AllowlistPattern);

            var result = await service.AuthorizeAsync(operation, config, CancellationToken.None);

            Assert.True(result.IsAllowed);
            Assert.Null(result.ErrorCode);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Authorize_ReturnsDenied_WhenOperationIsDisallowed ()
    {
        foreach (DeniedAuthorizationCase testCase in DeniedAuthorizationCases)
        {
            var service = new OperationAuthorizationService();
            var operation = CreateOperation(
                testCase.OperationName,
                testCase.OperationKind,
                testCase.RequiredPolicy);
            var config = CreateConfig(testCase.ConfiguredPolicy, testCase.AllowlistPattern);

            var result = await service.AuthorizeAsync(operation, config, CancellationToken.None);

            Assert.False(result.IsAllowed);
            Assert.Equal(OperationAuthorizationErrorCodes.OperationNotAllowed, result.ErrorCode);
            if (!string.IsNullOrWhiteSpace(testCase.ExpectedMessageContains))
            {
                Assert.Contains(testCase.ExpectedMessageContains, result.Message, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Authorize_WhenPolicyBlocksOperation_ReturnsGenericPolicyGuidance ()
    {
        var service = new OperationAuthorizationService();
        var operation = CreateOperation(
            UcliPrimitiveOperationNames.SceneSave,
            UcliOperationKind.Mutation,
            OperationPolicy.Advanced);
        var config = CreateConfig(OperationPolicy.Safe, "^ucli\\.");

        var result = await service.AuthorizeAsync(operation, config, CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.Equal(OperationAuthorizationErrorCodes.OperationNotAllowed, result.ErrorCode);
        Assert.Contains(UcliPrimitiveOperationNames.SceneSave, result.Message, StringComparison.Ordinal);
        Assert.Contains("requires operationPolicy='advanced'", result.Message, StringComparison.Ordinal);
        Assert.Contains("current operationPolicy='safe'", result.Message, StringComparison.Ordinal);
        Assert.Contains(".ucli/config.json", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("AssetDatabase", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ucli refresh", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ucli status", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ucli query", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static UcliOperationDescriptor CreateOperation (
        string name,
        UcliOperationKind kind,
        OperationPolicy policy)
    {
        return new UcliOperationDescriptor(name, kind, policy, ArgsSchemaJson);
    }

    private static UcliConfig CreateConfig (
        OperationPolicy operationPolicy,
        params string[] allowlistPatterns)
    {
        return new UcliConfig(
            SchemaVersion: 1,
            OperationPolicy: operationPolicy,
            PlanTokenMode: PlanTokenMode.Optional,
            ReadIndexDefaultMode: ReadIndexMode.RequireFresh,
            OperationAllowlist: allowlistPatterns);
    }

    private sealed record AuthorizationCase (
        string OperationName,
        UcliOperationKind OperationKind,
        OperationPolicy RequiredPolicy,
        OperationPolicy ConfiguredPolicy,
        string AllowlistPattern);

    private sealed record DeniedAuthorizationCase (
        string OperationName,
        UcliOperationKind OperationKind,
        OperationPolicy RequiredPolicy,
        OperationPolicy ConfiguredPolicy,
        string AllowlistPattern,
        string? ExpectedMessageContains);
}
