using System.Text.Json;
using Json.Schema;
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

    [Fact]
    [Trait("Size", "Medium")]
    public async Task OpsDescribe_WithMissingScriptsDescriptor_ReturnsContractsFromTheSharedContractModel ()
    {
        using var scope = TestDirectories.CreateTempScope("ops-cli-output-contract", "describe-missing-scripts");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var expectedOperation = CreateDescribedEntry(
            UcliPrimitiveOperationNames.ProjectMissingScriptsCheck,
            UcliOperationKind.Query,
            OperationPolicy.Safe,
            CreateMissingScriptsCheckContract(UcliPrimitiveOperationNames.ProjectMissingScriptsCheck));
        ReadIndexCatalogTestSeeder.SeedOpsCatalog(unityProjectPath, [expectedOperation]);

        var result = await RunOpsDescribeCommandAsync(
            UcliPrimitiveOperationNames.ProjectMissingScriptsCheck,
            projectPath: unityProjectPath,
            readIndexMode: UcliContractConstants.Config.ReadIndexModeAllowStale);

        using var outputJson = JsonAssert.ParseMultilineObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasSuccessEnvelope(outputJson.RootElement, UcliCommandNames.OpsDescribe);
        var operation = outputJson.RootElement.GetProperty("payload").GetProperty("operation");
        Assert.Equal(UcliPrimitiveOperationNames.ProjectMissingScriptsCheck, operation.GetProperty("name").GetString());
        Assert.Equal("query", operation.GetProperty("kind").GetString());
        Assert.Equal("safe", operation.GetProperty("policy").GetString());
        Assert.True(operation.TryGetProperty("verdictContract", out var verdictContract));
        Assert.Equal(
            "Returns fail when a missing script slot is confirmed, incomplete when any requested scope or discovered asset is unscanned, and pass otherwise.",
            verdictContract.GetProperty("description").GetString());
        Assert.Equal(expectedOperation.DescriptorDigest!.ToString(), operation.GetProperty("descriptorDigest").GetString());

        var expectedArgsContract = Assert.IsType<UcliOperationJsonContract>(expectedOperation.ArgsContract);
        var expectedResultContract = Assert.IsType<UcliOperationJsonContract>(expectedOperation.ResultContract);
        var argsContract = operation.GetProperty("argsContract");
        var resultContract = operation.GetProperty("resultContract");
        Assert.Equal(expectedArgsContract.ContractDigest.ToString(), argsContract.GetProperty("contractDigest").GetString());
        Assert.Equal(expectedResultContract.ContractDigest.ToString(), resultContract.GetProperty("contractDigest").GetString());
        AssertProjectionDigestsAgree(argsContract);
        AssertProjectionDigestsAgree(resultContract);

        var argsSchema = GetReferencedRootSchema(argsContract.GetProperty("schema"));
        var argsProperties = argsSchema.GetProperty("properties");
        Assert.Equal(
            new[] { "assetKinds", "roots" },
            argsSchema.GetProperty("required").EnumerateArray().Select(static item => item.GetString()).OrderBy(static item => item));
        Assert.Equal(1, argsProperties.GetProperty("roots").GetProperty("minItems").GetInt32());
        var assetKindsSchema = argsProperties.GetProperty("assetKinds");
        Assert.Equal(1, assetKindsSchema.GetProperty("minItems").GetInt32());
        Assert.Equal(
            new[] { "prefab", "scene" },
            assetKindsSchema.GetProperty("items").GetProperty("enum").EnumerateArray().Select(static item => item.GetString()));

        var resultContractSchema = resultContract.GetProperty("schema");
        var resultSchema = GetReferencedRootSchema(resultContractSchema);
        var requestedScopeSchema = ResolveReference(resultSchema.GetProperty("properties").GetProperty("requestedScope"), resultContractSchema);
        var requestedScopeProperties = requestedScopeSchema.GetProperty("properties");
        Assert.Equal(
            new[] { "assetKinds", "roots" },
            requestedScopeSchema.GetProperty("required").EnumerateArray().Select(static item => item.GetString()).OrderBy(static item => item));
        Assert.Equal(1, requestedScopeProperties.GetProperty("roots").GetProperty("minItems").GetInt32());
        var requestedScopeAssetKinds = requestedScopeProperties.GetProperty("assetKinds");
        Assert.Equal(1, requestedScopeAssetKinds.GetProperty("minItems").GetInt32());
        Assert.Equal(
            new[] { "prefab", "scene" },
            requestedScopeAssetKinds.GetProperty("items").GetProperty("enum").EnumerateArray().Select(static item => item.GetString()));
        var missingScriptSlotSchema = ResolveReference(
            resultSchema.GetProperty("properties").GetProperty("missingScriptSlots").GetProperty("items"),
            resultContractSchema);
        Assert.Equal(0, missingScriptSlotSchema.GetProperty("properties").GetProperty("componentIndex").GetProperty("minimum").GetInt32());
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

    private static JsonElement GetReferencedRootSchema (JsonElement schema)
    {
        return ResolveReference(schema, schema);
    }

    private static JsonElement ResolveReference (JsonElement schemaNode, JsonElement schemaDocument)
    {
        var reference = schemaNode.GetProperty("$ref").GetString();
        Assert.StartsWith("#/$defs/", reference, StringComparison.Ordinal);
        return schemaDocument.GetProperty("$defs").GetProperty(reference!["#/$defs/".Length..]);
    }
}
