using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Tests.OpsCliOutputContractTestSupport;

namespace MackySoft.Ucli.Tests;

public sealed class OpsCliOutputDescribeContractTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task OpsDescribe_WithPreseededReadIndex_ReturnsOperationSchema ()
    {
        using var scope = TestDirectories.CreateTempScope("ops-cli-output-contract", "describe-success");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        ReadIndexCatalogTestSeeder.SeedOpsCatalog(
            unityProjectPath,
            [
                CreateDescribedEntry(
                    name: UcliPrimitiveOperationNames.GoDescribe,
                    kind: "query",
                    policy: "safe",
                    argsSchemaJson: """{"type":"object","properties":{"path":{"type":"string"}}}""",
                    resultSchemaJson: """{"type":"object"}"""),
            ]);

        var result = await RunOpsDescribeCommandAsync(
            UcliPrimitiveOperationNames.GoDescribe,
            projectPath: unityProjectPath,
            readIndexMode: UcliContractConstants.Config.ReadIndexModeAllowStale);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.OpsDescribe);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasProperty("operation", operation => operation
                    .HasString("name", UcliPrimitiveOperationNames.GoDescribe)
                    .HasString("kind", "query")
                    .HasString("policy", "safe")
                    .HasString("playModeSupport", "disallowed")
                    .HasString("description", "Returns a GameObject description including components and child hierarchy.")
                    .HasProperty("inputs")
                    .HasProperty("resultContract", resultContract => resultContract
                        .HasBoolean("emitted", true)
                        .HasString("resultType", "GameObjectDescriptionResult"))
                    .HasProperty("assurance", assurance => assurance
                        .HasArrayLength("sideEffects", 1)
                        .HasBoolean("mayDirty", false)
                        .HasBoolean("mayPersist", false)
                        .HasString("planMode", "observesLiveUnity"))
                    .HasProperty("argsSchema", argsSchema => argsSchema
                        .HasString("type", "object")
                        .HasProperty("properties", properties => properties
                            .HasProperty("path", path => path
                                .HasString("type", "string"))))
                    .HasProperty("resultSchema", resultSchema => resultSchema
                        .HasString("type", "object")))
                .HasProperty("readIndex", readIndex => readIndex
                    .HasString("source", "index")
                    .HasString("freshness", "probable")));
        var operationElement = outputJson.RootElement.GetProperty("payload").GetProperty("operation");
        Assert.False(operationElement.TryGetProperty("outputs", out _));
        AssertNoFreezeInternalOperationTopLevelFields(operationElement);
        AssertDescribeVariantFields(operationElement);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task OpsDescribe_WithUnknownOperation_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("ops-cli-output-contract", "describe-unknown-operation");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        ReadIndexCatalogTestSeeder.SeedOpsCatalog(
            unityProjectPath,
            [
                CreateDescribedEntry(
                    name: UcliPrimitiveOperationNames.GoDescribe,
                    kind: "query",
                    policy: "safe",
                    argsSchemaJson: """{"type":"object"}""",
                    resultSchemaJson: """{"type":"object"}"""),
            ]);

        var result = await RunOpsDescribeCommandAsync(
            "ucli.unknown",
            projectPath: unityProjectPath,
            readIndexMode: UcliContractConstants.Config.ReadIndexModeAllowStale);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.OpsDescribe);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
    }
}
