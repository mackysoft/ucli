using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Tests.OpsCliOutputContractTestSupport;

namespace MackySoft.Ucli.Tests;

public sealed class OpsCliOutputListContractTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task OpsList_WithPreseededReadIndex_ReturnsJsonEnvelopeSuccess ()
    {
        using var scope = TestDirectories.CreateTempScope("ops-cli-output-contract", "list-success");
        var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
        var sceneSaveGeneration = UcliOperationJsonContractGenerator.Generate(
            UcliPrimitiveOperationNames.SceneSave,
            IpcJsonSerializerOptions.PublicRawOperationContracts.GetTypeInfo(typeof(ScenePathArgs)),
            resultTypeInfo: null);
        var sceneSaveDescribe = UcliOperationDescribeContractBuilder.Create(
            sceneSaveGeneration,
            "Saves a Unity scene asset.",
            CreateAssurance(UcliOperationKind.Mutation, OperationPolicy.Advanced));
        ReadIndexCatalogTestSeeder.SeedOpsCatalog(
            unityProjectPath,
            [
                CreateDescribedEntry(
                    name: UcliPrimitiveOperationNames.GoDescribe,
                    kind: UcliOperationKind.Query,
                    policy: OperationPolicy.Safe),
                CreateDescribedEntry(
                    name: UcliPrimitiveOperationNames.SceneSave,
                    kind: UcliOperationKind.Mutation,
                    policy: OperationPolicy.Advanced,
                    describe: sceneSaveDescribe),
            ]);

        var result = await RunOpsListCommandAsync(
            projectPath: unityProjectPath,
            readIndexMode: UcliContractConstants.Config.ReadIndexModeAllowStale);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.OpsList);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasProperty("readIndex", readIndex => readIndex
                    .HasString("source", "index")
                    .HasString("freshness", "probable")));

        var operations = outputJson.RootElement
            .GetProperty("payload")
            .GetProperty("operations")
            .EnumerateArray()
            .ToArray();
        Assert.Equal(
            [UcliPrimitiveOperationNames.GoDescribe, UcliPrimitiveOperationNames.SceneSave],
            operations.Select(static operation => operation.GetProperty("name").GetString()));
    }
}
