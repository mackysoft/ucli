using MackySoft.Tests;
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
        ReadIndexCatalogTestSeeder.SeedOpsCatalog(
            unityProjectPath,
            [
                CreateDescribedEntry(
                    name: UcliPrimitiveOperationNames.GoDescribe,
                    kind: "query",
                    policy: "safe",
                    argsSchemaJson: """{"type":"object","properties":{"path":{"type":"string"}}}""",
                    resultSchemaJson: """{"type":"object"}"""),
                CreateDescribedEntry(
                    name: UcliPrimitiveOperationNames.SceneSave,
                    kind: "mutation",
                    policy: "advanced",
                    argsSchemaJson: """{"type":"object","properties":{"path":{"type":"string"}}}""",
                    resultSchemaJson: null,
                    describe: UcliOperationDescribeContractBuilder.Create<ScenePathArgs, UcliNoResult>(
                        "Saves a Unity scene asset.",
                        CreateAssurance("mutation", "advanced"))),
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

        var operations = outputJson.RootElement.GetProperty("payload").GetProperty("operations").EnumerateArray().ToArray();
        Assert.Equal(
            [UcliPrimitiveOperationNames.GoDescribe, UcliPrimitiveOperationNames.SceneSave],
            operations.Select(static operation => operation.GetProperty("name").GetString()));
        foreach (var operation in operations)
        {
            AssertNoFreezeInternalOperationTopLevelFields(operation);
        }
    }
}
