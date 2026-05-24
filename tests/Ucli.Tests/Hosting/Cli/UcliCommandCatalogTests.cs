using MackySoft.Ucli.Hosting.Cli.Common.Startup;

namespace MackySoft.Ucli.Tests.Cli;

public sealed class UcliCommandCatalogTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void FilterableCommandNames_MatchPublicCommandCatalog ()
    {
        var expected = UcliPublicCommandCatalog.KnownCommands
            .Select(static command => command.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var actual = CreateFilterableCommandNamesFromRegisteredPaths()
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(UcliCommandNames.Daemon, UcliCommandNames.StartSubcommand, null, UcliCommandNames.DaemonStart)]
    [InlineData(UcliCommandNames.Daemon, UcliCommandNames.StopSubcommand, null, UcliCommandNames.DaemonStop)]
    [InlineData(UcliCommandNames.Daemon, UcliCommandNames.CleanupSubcommand, null, UcliCommandNames.DaemonCleanup)]
    [InlineData(UcliCommandNames.Daemon, UcliCommandNames.Status, null, UcliCommandNames.DaemonStatus)]
    [InlineData(UcliCommandNames.Daemon, UcliCommandNames.ListSubcommand, null, UcliCommandNames.DaemonList)]
    [InlineData(UcliCommandNames.Logs, UcliCommandNames.Daemon, UcliCommandNames.ReadSubcommand, UcliCommandNames.LogsDaemonRead)]
    [InlineData(UcliCommandNames.Logs, UcliCommandNames.UnitySubcommand, UcliCommandNames.ReadSubcommand, UcliCommandNames.LogsUnityRead)]
    [InlineData(UcliCommandNames.Logs, UcliCommandNames.UnitySubcommand, UcliCommandNames.ClearSubcommand, UcliCommandNames.LogsUnityClear)]
    [InlineData(UcliCommandNames.Ops, UcliCommandNames.ListSubcommand, null, UcliCommandNames.OpsList)]
    [InlineData(UcliCommandNames.Ops, UcliCommandNames.DescribeSubcommand, null, UcliCommandNames.OpsDescribe)]
    [InlineData(UcliCommandNames.Codes, UcliCommandNames.ListSubcommand, null, UcliCommandNames.CodesList)]
    [InlineData(UcliCommandNames.Codes, UcliCommandNames.DescribeSubcommand, null, UcliCommandNames.CodesDescribe)]
    [InlineData(UcliCommandNames.Play, UcliCommandNames.Status, null, UcliCommandNames.PlayStatus)]
    [InlineData(UcliCommandNames.Play, UcliCommandNames.EnterSubcommand, null, UcliCommandNames.PlayEnter)]
    [InlineData(UcliCommandNames.Play, UcliCommandNames.ExitSubcommand, null, UcliCommandNames.PlayExit)]
    [InlineData(UcliCommandNames.Skills, UcliCommandNames.ListSubcommand, null, UcliCommandNames.SkillsList)]
    [InlineData(UcliCommandNames.Skills, UcliCommandNames.ExportSubcommand, null, UcliCommandNames.SkillsExport)]
    [InlineData(UcliCommandNames.Skills, UcliCommandNames.InstallSubcommand, null, UcliCommandNames.SkillsInstall)]
    [InlineData(UcliCommandNames.Skills, UcliCommandNames.UpdateSubcommand, null, UcliCommandNames.SkillsUpdate)]
    [InlineData(UcliCommandNames.Skills, UcliCommandNames.UninstallSubcommand, null, UcliCommandNames.SkillsUninstall)]
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

        var commandName = UcliCommandCatalog.ResolveResultCommandName(args);

        Assert.Equal(expected, commandName);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(UcliCommandNames.Init)]
    [InlineData(UcliCommandNames.Status)]
    [InlineData(UcliCommandNames.Ready)]
    [InlineData(UcliCommandNames.Compile)]
    [InlineData(UcliCommandNames.Refresh)]
    [InlineData(UcliCommandNames.Resolve)]
    [InlineData(UcliCommandNames.Query)]
    [InlineData(UcliCommandNames.Validate)]
    [InlineData(UcliCommandNames.Plan)]
    [InlineData(UcliCommandNames.Call)]
    [InlineData(UcliCommandNames.Eval)]
    [InlineData(UcliCommandNames.Daemon)]
    [InlineData(UcliCommandNames.Logs)]
    [InlineData(UcliCommandNames.Ops)]
    [InlineData(UcliCommandNames.Codes)]
    [InlineData(UcliCommandNames.Play)]
    [InlineData(UcliCommandNames.Skills)]
    [InlineData(UcliCommandNames.Test)]
    public void IsRegisteredRootCommand_WhenKnownCommandSpecified_ReturnsTrue (string commandName)
    {
        var result = UcliCommandCatalog.IsRegisteredRootCommand(commandName);

        Assert.True(result);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("unknown")]
    [InlineData("errors")]
    [InlineData("")]
    [InlineData(" ")]
    public void IsRegisteredRootCommand_WhenUnknownCommandSpecified_ReturnsFalse (string? commandName)
    {
        var result = UcliCommandCatalog.IsRegisteredRootCommand(commandName);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveResultCommandName_WhenUnknownCommandSpecified_ReturnsRoot ()
    {
        var commandName = UcliCommandCatalog.ResolveResultCommandName(["unknown"]);

        Assert.Equal(UcliCommandNames.Root, commandName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryGetPreDispatchSupportedSubcommands_WhenDaemonCommandSpecified_ReturnsDaemonGroups ()
    {
        var found = UcliCommandCatalog.TryGetPreDispatchSupportedSubcommands(
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
    public void TryGetPreDispatchSupportedSubcommands_WhenPlayCommandSpecified_ReturnsPlayCommands ()
    {
        var found = UcliCommandCatalog.TryGetPreDispatchSupportedSubcommands(
            UcliCommandNames.Play,
            out var subcommands);

        Assert.True(found);
        Assert.Equal(
            [
                UcliCommandNames.Status,
                UcliCommandNames.EnterSubcommand,
                UcliCommandNames.ExitSubcommand,
            ],
            subcommands);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryGetPreDispatchSupportedSubcommands_WhenTestCommandSpecified_ReturnsFalse ()
    {
        var found = UcliCommandCatalog.TryGetPreDispatchSupportedSubcommands(
            UcliCommandNames.Test,
            out var subcommands);

        Assert.False(found);
        Assert.Empty(subcommands);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(UcliCommandNames.AssetsSubcommand, UcliCommandNames.FindSubcommand)]
    [InlineData(UcliCommandNames.SceneSubcommand, UcliCommandNames.TreeSubcommand)]
    [InlineData(UcliCommandNames.GoSubcommand, UcliCommandNames.DescribeSubcommand)]
    [InlineData(UcliCommandNames.CompSubcommand, UcliCommandNames.SchemaSubcommand)]
    [InlineData(UcliCommandNames.AssetSubcommand, UcliCommandNames.SchemaSubcommand)]
    public void TryGetSupportedLeafSubcommands_WhenQueryGroupSpecified_ReturnsExpectedLeaf (
        string groupName,
        string expectedLeafName)
    {
        var found = UcliCommandCatalog.TryGetSupportedLeafSubcommands(
            UcliCommandNames.Query,
            groupName,
            out var subcommands);

        Assert.True(found);
        Assert.Equal([expectedLeafName], subcommands);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryGetSupportedLeafSubcommands_WhenLogsUnityGroupSpecified_ReturnsReadAndClear ()
    {
        var found = UcliCommandCatalog.TryGetSupportedLeafSubcommands(
            UcliCommandNames.Logs,
            UcliCommandNames.UnitySubcommand,
            out var subcommands);

        Assert.True(found);
        Assert.Equal([UcliCommandNames.ReadSubcommand, UcliCommandNames.ClearSubcommand], subcommands);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryGetSupportedLeafSubcommands_WhenLogsDaemonGroupSpecified_ReturnsRead ()
    {
        var found = UcliCommandCatalog.TryGetSupportedLeafSubcommands(
            UcliCommandNames.Logs,
            UcliCommandNames.Daemon,
            out var subcommands);

        Assert.True(found);
        Assert.Equal([UcliCommandNames.ReadSubcommand], subcommands);
    }

    private static string[] CreateFilterableCommandNamesFromRegisteredPaths ()
    {
        var commandNames = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < UcliCommandCatalog.CommandPaths.Count; i++)
        {
            var segments = UcliCommandCatalog.CommandPaths[i].Split(' ');
            for (var segmentCount = 1; segmentCount <= segments.Length; segmentCount++)
            {
                commandNames.Add(string.Join('.', segments.AsSpan(0, segmentCount).ToArray()));
            }
        }

        return commandNames.ToArray();
    }
}
