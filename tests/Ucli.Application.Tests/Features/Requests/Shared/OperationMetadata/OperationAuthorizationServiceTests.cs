namespace MackySoft.Ucli.Application.Tests;

using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

public sealed class OperationAuthorizationServiceTests
{
    private const string ArgsSchemaJson = """{"type":"object"}""";

    public static TheoryData<string, string, string, string, string> AllowedAuthorizationCases => new()
    {
        { UcliPrimitiveOperationNames.SceneOpen, "command", "safe", "safe", "^ucli\\." },
        { UcliPrimitiveOperationNames.CsEval, "mutation", "dangerous", "dangerous", "^ucli\\." },
    };

    public static TheoryData<string, string, string, string, string, string?> DeniedAuthorizationCases => new()
    {
        { UcliPrimitiveOperationNames.SceneSave, "mutation", "advanced", "safe", "^ucli\\.", null },
        { UcliPrimitiveOperationNames.SceneOpen, "command", "safe", "safe", "^myorg\\.", null },
        { UcliPrimitiveOperationNames.SceneOpen, "command", "safe", "safe", "[", "invalid regex" },
    };

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(AllowedAuthorizationCases))]
    public async Task Authorize_ReturnsAllowed_WhenPolicyAndAllowlistPermitOperation (
        string operationName,
        string operationKind,
        string requiredPolicy,
        string configuredPolicy,
        string allowlistPattern)
    {
        var service = new OperationAuthorizationService();
        var operation = CreateOperation(
            operationName,
            ParseOperationKind(operationKind),
            ParseOperationPolicy(requiredPolicy));
        var config = CreateConfig(ParseOperationPolicy(configuredPolicy), allowlistPattern);

        var result = await service.AuthorizeAsync(operation, config, CancellationToken.None);

        Assert.True(result.IsAllowed);
        Assert.Null(result.ErrorCode);
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(DeniedAuthorizationCases))]
    public async Task Authorize_ReturnsDenied_WhenOperationIsDisallowed (
        string operationName,
        string operationKind,
        string requiredPolicy,
        string configuredPolicy,
        string allowlistPattern,
        string? expectedMessageContains)
    {
        var service = new OperationAuthorizationService();
        var operation = CreateOperation(
            operationName,
            ParseOperationKind(operationKind),
            ParseOperationPolicy(requiredPolicy));
        var config = CreateConfig(ParseOperationPolicy(configuredPolicy), allowlistPattern);

        var result = await service.AuthorizeAsync(operation, config, CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.Equal(OperationAuthorizationErrorCodes.OperationNotAllowed, result.ErrorCode);
        if (!string.IsNullOrWhiteSpace(expectedMessageContains))
        {
            Assert.Contains(expectedMessageContains, result.Message, StringComparison.OrdinalIgnoreCase);
        }
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

    private static UcliOperationKind ParseOperationKind (string operationKind)
    {
        return operationKind switch
        {
            "query" => UcliOperationKind.Query,
            "command" => UcliOperationKind.Command,
            "mutation" => UcliOperationKind.Mutation,
            _ => throw new ArgumentOutOfRangeException(nameof(operationKind), operationKind, "Unsupported operation kind case."),
        };
    }

    private static OperationPolicy ParseOperationPolicy (string operationPolicy)
    {
        return operationPolicy switch
        {
            "safe" => OperationPolicy.Safe,
            "advanced" => OperationPolicy.Advanced,
            "dangerous" => OperationPolicy.Dangerous,
            _ => throw new ArgumentOutOfRangeException(nameof(operationPolicy), operationPolicy, "Unsupported operation policy case."),
        };
    }
}
