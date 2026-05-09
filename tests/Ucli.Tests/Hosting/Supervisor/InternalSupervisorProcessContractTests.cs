using MackySoft.Tests;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class InternalSupervisorProcessContractTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task InvalidInternalSupervisorInvocation_ReturnsExitCodeOneWithoutPublicCliOutput ()
    {
        var result = await CliProcessRunner.RunCommandAsync(SupervisorConstants.InternalServeFlag);

        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.StdOut);
        Assert.Equal(string.Empty, result.StdErr);
    }
}
