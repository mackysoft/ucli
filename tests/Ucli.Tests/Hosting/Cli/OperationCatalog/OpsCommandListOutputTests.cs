using MackySoft.Ucli.Application.Features.OperationCatalog.Common.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Ops;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.Cli.OpsCommandTestSupport;

namespace MackySoft.Ucli.Tests.Cli;

public sealed class OpsCommandListOutputTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task List_WhenServiceReturnsOperations_MatchesSuccessGolden ()
    {
        var service = CreateService();
        service.ListResult = CreateListSuccess(
            new OpsOperationListItem(
                UcliPrimitiveOperationNames.GoDescribe,
                "query",
                "safe",
                "Returns a GameObject description including components and child hierarchy."),
            new OpsOperationListItem(
                UcliPrimitiveOperationNames.SceneSave,
                "mutation",
                "advanced",
                "Saves a Unity scene asset."));
        var command = new OpsListCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.ListAsync(cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("ops", "list-success.json"),
            result.StdOut,
            CliOutputGoldenFiles.NormalizeGeneratedAtUtc());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task List_WhenServiceReturnsFilteredOperations_MatchesFilteredGolden ()
    {
        var service = CreateService();
        service.ListResult = CreateListSuccess(
            new OpsOperationListItem(
                UcliPrimitiveOperationNames.SceneOpen,
                "command",
                "advanced",
                "Opens a Unity scene asset in the editor."));
        var command = new OpsListCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.ListAsync(cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("ops", "list-filtered-success.json"),
            result.StdOut,
            CliOutputGoldenFiles.NormalizeGeneratedAtUtc());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenServiceReturnsEmptyOperations_WritesEmptyOperationsPayload ()
    {
        var service = CreateService();
        service.ListResult = CreateListSuccess();
        var command = new OpsListCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.ListAsync(cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.OpsList);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasArrayLength("operations", 0)
                .HasProperty("readIndex", readIndex => readIndex
                    .HasString("source", "index")
                    .HasString("freshness", "probable")));
    }
}
