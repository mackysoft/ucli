using MackySoft.Tests;
using MackySoft.Ucli.Hosting.Cli.Common.Catalog;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;

namespace MackySoft.Ucli.Tests.Cli;

public sealed class CliCommandRegistrationContractTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task HelpOutput_ContainsEveryMetadataCommandPath ()
    {
        var result = await CliProcessRunner.RunCommand("--help");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        for (var i = 0; i < UcliCommandMetadataCatalog.CommandPaths.Count; i++)
        {
            Assert.Contains(UcliCommandMetadataCatalog.CommandPaths[i], result.StdOut, StringComparison.Ordinal);
        }
    }
}
