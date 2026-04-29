using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests.Cli;

public sealed class UcliCommandNamesTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData(UcliCommandNames.StartSubcommand, UcliCommandNames.DaemonStart)]
    [InlineData(UcliCommandNames.StopSubcommand, UcliCommandNames.DaemonStop)]
    [InlineData(UcliCommandNames.CleanupSubcommand, UcliCommandNames.DaemonCleanup)]
    [InlineData(UcliCommandNames.Status, UcliCommandNames.DaemonStatus)]
    [InlineData("foo", UcliCommandNames.Daemon)]
    [InlineData(null, UcliCommandNames.Daemon)]
    public void ResolveResultCommandName_WhenDaemonCommandSpecified_ReturnsExpectedCommandName (
        string? subcommand,
        string expected)
    {
        var args = subcommand is null
            ? [UcliCommandNames.Daemon]
            : new[] { UcliCommandNames.Daemon, subcommand };

        var commandName = UcliCommandNames.ResolveResultCommandName(args);

        Assert.Equal(expected, commandName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsRegistered_WhenDaemonCommandSpecified_ReturnsTrue ()
    {
        var result = UcliCommandNames.IsRegistered(UcliCommandNames.Daemon);

        Assert.True(result);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(UcliCommandNames.Daemon, UcliCommandNames.LogsDaemon)]
    [InlineData(UcliCommandNames.UnitySubcommand, UcliCommandNames.LogsUnity)]
    [InlineData("foo", UcliCommandNames.Logs)]
    [InlineData(null, UcliCommandNames.Logs)]
    public void ResolveResultCommandName_WhenLogsCommandSpecified_ReturnsExpectedCommandName (
        string? subcommand,
        string expected)
    {
        var args = subcommand is null
            ? [UcliCommandNames.Logs]
            : new[] { UcliCommandNames.Logs, subcommand };

        var commandName = UcliCommandNames.ResolveResultCommandName(args);

        Assert.Equal(expected, commandName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsRegistered_WhenLogsCommandSpecified_ReturnsTrue ()
    {
        var result = UcliCommandNames.IsRegistered(UcliCommandNames.Logs);

        Assert.True(result);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(UcliCommandNames.ListSubcommand, UcliCommandNames.OpsList)]
    [InlineData(UcliCommandNames.DescribeSubcommand, UcliCommandNames.OpsDescribe)]
    [InlineData("foo", UcliCommandNames.Ops)]
    [InlineData(null, UcliCommandNames.Ops)]
    public void ResolveResultCommandName_WhenOpsCommandSpecified_ReturnsExpectedCommandName (
        string? subcommand,
        string expected)
    {
        var args = subcommand is null
            ? [UcliCommandNames.Ops]
            : new[] { UcliCommandNames.Ops, subcommand };

        var commandName = UcliCommandNames.ResolveResultCommandName(args);

        Assert.Equal(expected, commandName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsRegistered_WhenOpsCommandSpecified_ReturnsTrue ()
    {
        var result = UcliCommandNames.IsRegistered(UcliCommandNames.Ops);

        Assert.True(result);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(UcliCommandNames.AssetsSubcommand, UcliCommandNames.FindSubcommand, UcliCommandNames.QueryAssetsFind)]
    [InlineData(UcliCommandNames.SceneSubcommand, UcliCommandNames.TreeSubcommand, UcliCommandNames.QuerySceneTree)]
    [InlineData(UcliCommandNames.GoSubcommand, UcliCommandNames.DescribeSubcommand, UcliCommandNames.QueryGoDescribe)]
    [InlineData(UcliCommandNames.CompSubcommand, UcliCommandNames.SchemaSubcommand, UcliCommandNames.QueryCompSchema)]
    [InlineData(UcliCommandNames.AssetSubcommand, UcliCommandNames.SchemaSubcommand, UcliCommandNames.QueryAssetSchema)]
    public void ResolveResultCommandName_WhenQueryCommandSpecified_ReturnsExpectedCommandName (
        string group,
        string subcommand,
        string expected)
    {
        var commandName = UcliCommandNames.ResolveResultCommandName([UcliCommandNames.Query, group, subcommand]);

        Assert.Equal(expected, commandName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsRegistered_WhenQueryCommandSpecified_ReturnsTrue ()
    {
        var result = UcliCommandNames.IsRegistered(UcliCommandNames.Query);

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveResultCommandName_WhenRefreshCommandSpecified_ReturnsRefresh ()
    {
        var commandName = UcliCommandNames.ResolveResultCommandName([UcliCommandNames.Refresh]);

        Assert.Equal(UcliCommandNames.Refresh, commandName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsRegistered_WhenRefreshCommandSpecified_ReturnsTrue ()
    {
        var result = UcliCommandNames.IsRegistered(UcliCommandNames.Refresh);

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveResultCommandName_WhenValidateCommandSpecified_ReturnsValidate ()
    {
        var commandName = UcliCommandNames.ResolveResultCommandName([UcliCommandNames.Validate]);

        Assert.Equal(UcliCommandNames.Validate, commandName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsRegistered_WhenValidateCommandSpecified_ReturnsTrue ()
    {
        var result = UcliCommandNames.IsRegistered(UcliCommandNames.Validate);

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveResultCommandName_WhenPlanCommandSpecified_ReturnsPlan ()
    {
        var commandName = UcliCommandNames.ResolveResultCommandName([UcliCommandNames.Plan]);

        Assert.Equal(UcliCommandNames.Plan, commandName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsRegistered_WhenPlanCommandSpecified_ReturnsTrue ()
    {
        var result = UcliCommandNames.IsRegistered(UcliCommandNames.Plan);

        Assert.True(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveResultCommandName_WhenCallCommandSpecified_ReturnsCall ()
    {
        var commandName = UcliCommandNames.ResolveResultCommandName([UcliCommandNames.Call]);

        Assert.Equal(UcliCommandNames.Call, commandName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsRegistered_WhenCallCommandSpecified_ReturnsTrue ()
    {
        var result = UcliCommandNames.IsRegistered(UcliCommandNames.Call);

        Assert.True(result);
    }
}
