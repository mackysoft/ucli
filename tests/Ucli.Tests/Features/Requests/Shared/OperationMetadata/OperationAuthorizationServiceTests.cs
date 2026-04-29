namespace MackySoft.Ucli.Tests;

using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Shared.Configuration;
using MackySoft.Ucli.UnityIntegration.Indexing.ReadIndex;

public sealed class OperationAuthorizationServiceTests
{
    private const string ArgsSchemaJson = """{"type":"object"}""";

    public static TheoryData<string, string, string, string, string> AllowedAuthorizationCases => new()
    {
        { MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneOpen, "query", "safe", "safe", "^ucli\\." },
        { "ucli.cs.invoke", "mutation", "dangerous", "dangerous", "^ucli\\." },
    };

    public static TheoryData<string, string, string, string, string, string?> DeniedAuthorizationCases => new()
    {
        { MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneSave, "mutation", "advanced", "safe", "^ucli\\.", null },
        { MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneOpen, "query", "safe", "safe", "^myorg\\.", null },
        { MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneOpen, "query", "safe", "safe", "[", "invalid regex" },
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

        var result = await service.Authorize(operation, config, CancellationToken.None);

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

        var result = await service.Authorize(operation, config, CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.Equal(ValidationErrorCodes.OperationNotAllowed, result.ErrorCode);
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
            SchemaVersion: UcliContractConstants.Config.SchemaVersion,
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
