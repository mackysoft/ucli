using MackySoft.Ucli.Cli;

namespace MackySoft.Ucli.Tests.Cli;

public sealed class UcliCommandNamesTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData(UcliCommandNames.StartSubcommand, UcliCommandNames.DaemonStart)]
    [InlineData(UcliCommandNames.StopSubcommand, UcliCommandNames.DaemonStop)]
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
}