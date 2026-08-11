using System.Text.Json;
using Json.Schema;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Schemas;
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
        var expectedOperation = CreateDescribedEntry(
            name: UcliPrimitiveOperationNames.GoDescribe,
            kind: UcliOperationKind.Query,
            policy: OperationPolicy.Safe);
        ReadIndexCatalogTestSeeder.SeedOpsCatalog(unityProjectPath, [expectedOperation]);

        var result = await RunOpsDescribeCommandAsync(
            UcliPrimitiveOperationNames.GoDescribe,
            projectPath: unityProjectPath,
            readIndexMode: UcliContractConstants.Config.ReadIndexModeAllowStale);

        using var outputJson = JsonAssert.ParseMultilineObject(result.StdOut);
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
                    .HasProperty("argsContract", argsContract => argsContract
                        .HasProperty("typeMetadata")
                        .HasProperty("schema"))
                    .HasProperty("resultContract", resultContract => resultContract
                        .HasProperty("typeMetadata")
                        .HasProperty("schema"))
                    .HasProperty("assurance", assurance => assurance
                        .HasArrayLength("sideEffects", 1)
                        .HasBoolean("mayDirty", false)
                        .HasBoolean("mayPersist", false)
                        .HasString("planMode", "observesLiveUnity")))
                .HasProperty("readIndex", readIndex => readIndex
                    .HasString("source", "index")
                    .HasString("freshness", "probable")));
        var operationElement = outputJson.RootElement
            .GetProperty("payload")
            .GetProperty("operation");
        var argsContract = operationElement.GetProperty("argsContract");
        var expectedArgsContract = Assert.IsType<UcliOperationJsonContract>(expectedOperation.ArgsContract);
        var expectedResultContract = Assert.IsType<UcliOperationJsonContract>(expectedOperation.ResultContract);
        Assert.Equal(
            expectedArgsContract.ContractDigest.ToString(),
            argsContract.GetProperty("contractDigest").GetString());
        var resultContract = operationElement.GetProperty("resultContract");
        Assert.Equal(
            expectedResultContract.ContractDigest.ToString(),
            resultContract.GetProperty("contractDigest").GetString());
        AssertProjectionDigestsAgree(argsContract);
        AssertProjectionDigestsAgree(resultContract);

        var schemaSet = UcliStaticSchemaSetLoader.Load(
            AbsolutePath.Parse(TestRepositoryPaths.GetFullPath("schemas")));
        var artifact = Assert.IsType<UcliStaticSchemaArtifact>(
            schemaSet.Find("cli-output.payload.ops.describe.ok"));
        var payloadSchema = global::Json.Schema.JsonSchema.Build(
            artifact.Document,
            new BuildOptions
            {
                SchemaRegistry = new SchemaRegistry
                {
                    Fetch = null!,
                },
            });
        Assert.True(
            payloadSchema
                .Evaluate(outputJson.RootElement.GetProperty("payload"))
                .IsValid);
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
                    kind: UcliOperationKind.Query,
                    policy: OperationPolicy.Safe),
            ]);

        var result = await RunOpsDescribeCommandAsync(
            "ucli.unknown",
            projectPath: unityProjectPath,
            readIndexMode: UcliContractConstants.Config.ReadIndexModeAllowStale);

        using var outputJson = JsonAssert.ParseMultilineObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.OpsDescribe);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
    }

    private static void AssertProjectionDigestsAgree (JsonElement contract)
    {
        var outerDigest = contract.GetProperty("contractDigest").GetString();
        var schemaDigest = contract
            .GetProperty("schema")
            .GetProperty("x-contract-digest")
            .GetString();
        var typeMetadataDigest = contract
            .GetProperty("typeMetadata")
            .GetProperty("contractDigest")
            .GetString();

        Assert.Equal(outerDigest, schemaDigest);
        Assert.Equal(outerDigest, typeMetadataDigest);
    }
}
