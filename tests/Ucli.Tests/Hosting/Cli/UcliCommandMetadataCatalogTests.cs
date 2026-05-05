using MackySoft.Ucli.Hosting.Cli.Common.Catalog;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;

namespace MackySoft.Ucli.Tests.Cli;

public sealed class UcliCommandMetadataCatalogTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData(UcliCommandNames.Daemon, UcliCommandNames.StartSubcommand, null, UcliCommandNames.DaemonStart)]
    [InlineData(UcliCommandNames.Daemon, UcliCommandNames.StopSubcommand, null, UcliCommandNames.DaemonStop)]
    [InlineData(UcliCommandNames.Daemon, UcliCommandNames.CleanupSubcommand, null, UcliCommandNames.DaemonCleanup)]
    [InlineData(UcliCommandNames.Daemon, UcliCommandNames.Status, null, UcliCommandNames.DaemonStatus)]
    [InlineData(UcliCommandNames.Daemon, UcliCommandNames.ListSubcommand, null, UcliCommandNames.DaemonList)]
    [InlineData(UcliCommandNames.Logs, UcliCommandNames.Daemon, null, UcliCommandNames.LogsDaemon)]
    [InlineData(UcliCommandNames.Logs, UcliCommandNames.UnitySubcommand, null, UcliCommandNames.LogsUnity)]
    [InlineData(UcliCommandNames.Ops, UcliCommandNames.ListSubcommand, null, UcliCommandNames.OpsList)]
    [InlineData(UcliCommandNames.Ops, UcliCommandNames.DescribeSubcommand, null, UcliCommandNames.OpsDescribe)]
    [InlineData(UcliCommandNames.Skills, UcliCommandNames.ListSubcommand, null, UcliCommandNames.SkillsList)]
    [InlineData(UcliCommandNames.Skills, UcliCommandNames.ExportSubcommand, null, UcliCommandNames.SkillsExport)]
    [InlineData(UcliCommandNames.Skills, UcliCommandNames.InstallSubcommand, null, UcliCommandNames.SkillsInstall)]
    [InlineData(UcliCommandNames.Skills, UcliCommandNames.DoctorSubcommand, null, UcliCommandNames.SkillsDoctor)]
    [InlineData(UcliCommandNames.Query, UcliCommandNames.AssetsSubcommand, UcliCommandNames.FindSubcommand, UcliCommandNames.QueryAssetsFind)]
    [InlineData(UcliCommandNames.Query, UcliCommandNames.SceneSubcommand, UcliCommandNames.TreeSubcommand, UcliCommandNames.QuerySceneTree)]
    [InlineData(UcliCommandNames.Query, UcliCommandNames.GoSubcommand, UcliCommandNames.DescribeSubcommand, UcliCommandNames.QueryGoDescribe)]
    [InlineData(UcliCommandNames.Query, UcliCommandNames.CompSubcommand, UcliCommandNames.SchemaSubcommand, UcliCommandNames.QueryCompSchema)]
    [InlineData(UcliCommandNames.Query, UcliCommandNames.AssetSubcommand, UcliCommandNames.SchemaSubcommand, UcliCommandNames.QueryAssetSchema)]
    [InlineData(UcliCommandNames.Test, UcliCommandNames.RunSubcommand, null, UcliCommandNames.TestRun)]
    [InlineData(UcliCommandNames.Test, UcliCommandNames.Profile, UcliCommandNames.InitSubcommand, UcliCommandNames.TestProfileInit)]
    public void ResolveResultCommandName_WhenKnownCommandPathSpecified_ReturnsExpectedCommandName (
        string command,
        string subcommand,
        string? leafSubcommand,
        string expected)
    {
        var args = leafSubcommand is null
            ? new[] { command, subcommand }
            : new[] { command, subcommand, leafSubcommand };

        var commandName = UcliCommandMetadataCatalog.ResolveResultCommandName(args);

        Assert.Equal(expected, commandName);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(UcliCommandNames.Init)]
    [InlineData(UcliCommandNames.Status)]
    [InlineData(UcliCommandNames.Refresh)]
    [InlineData(UcliCommandNames.Resolve)]
    [InlineData(UcliCommandNames.Query)]
    [InlineData(UcliCommandNames.Validate)]
    [InlineData(UcliCommandNames.Plan)]
    [InlineData(UcliCommandNames.Call)]
    [InlineData(UcliCommandNames.Daemon)]
    [InlineData(UcliCommandNames.Logs)]
    [InlineData(UcliCommandNames.Ops)]
    [InlineData(UcliCommandNames.Skills)]
    [InlineData(UcliCommandNames.Test)]
    public void IsRegisteredRootCommand_WhenKnownCommandSpecified_ReturnsTrue (string commandName)
    {
        var result = UcliCommandMetadataCatalog.IsRegisteredRootCommand(commandName);

        Assert.True(result);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("unknown")]
    [InlineData("")]
    [InlineData(" ")]
    public void IsRegisteredRootCommand_WhenUnknownCommandSpecified_ReturnsFalse (string? commandName)
    {
        var result = UcliCommandMetadataCatalog.IsRegisteredRootCommand(commandName);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveResultCommandName_WhenUnknownCommandSpecified_ReturnsRoot ()
    {
        var commandName = UcliCommandMetadataCatalog.ResolveResultCommandName(["unknown"]);

        Assert.Equal(UcliCommandNames.Root, commandName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryGetSupportedSubcommands_WhenQueryCommandSpecified_ReturnsQueryGroups ()
    {
        var found = UcliCommandMetadataCatalog.TryGetSupportedSubcommands(
            UcliCommandNames.Query,
            out var subcommands);

        Assert.True(found);
        Assert.Equal(
            [
                UcliCommandNames.AssetsSubcommand,
                UcliCommandNames.SceneSubcommand,
                UcliCommandNames.GoSubcommand,
                UcliCommandNames.CompSubcommand,
                UcliCommandNames.AssetSubcommand,
            ],
            subcommands);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryGetSupportedSubcommands_WhenTestCommandSpecified_ReturnsTestGroups ()
    {
        var found = UcliCommandMetadataCatalog.TryGetSupportedSubcommands(
            UcliCommandNames.Test,
            out var subcommands);

        Assert.True(found);
        Assert.Equal(
            [
                UcliCommandNames.RunSubcommand,
                UcliCommandNames.Profile,
            ],
            subcommands);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryGetPreDispatchSupportedSubcommands_WhenDaemonCommandSpecified_ReturnsDaemonGroups ()
    {
        var found = UcliCommandMetadataCatalog.TryGetPreDispatchSupportedSubcommands(
            UcliCommandNames.Daemon,
            out var subcommands);

        Assert.True(found);
        Assert.Equal(
            [
                UcliCommandNames.StartSubcommand,
                UcliCommandNames.StopSubcommand,
                UcliCommandNames.CleanupSubcommand,
                UcliCommandNames.Status,
                UcliCommandNames.ListSubcommand,
            ],
            subcommands);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryGetPreDispatchSupportedSubcommands_WhenTestCommandSpecified_ReturnsFalse ()
    {
        var found = UcliCommandMetadataCatalog.TryGetPreDispatchSupportedSubcommands(
            UcliCommandNames.Test,
            out var subcommands);

        Assert.False(found);
        Assert.Empty(subcommands);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryGetSupportedLeafSubcommands_WhenQueryAssetsSpecified_ReturnsFind ()
    {
        var found = UcliCommandMetadataCatalog.TryGetSupportedLeafSubcommands(
            UcliCommandNames.Query,
            UcliCommandNames.AssetsSubcommand,
            out var subcommands);

        Assert.True(found);
        Assert.Equal([UcliCommandNames.FindSubcommand], subcommands);
    }
}
