using MackySoft.Ucli.Contracts.Assurance;

namespace MackySoft.Ucli.Contracts.Tests.Assurance.Build;

public sealed class BuildRunProgressEventNamesTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Constants_ExposePublicBuildRunEventNames ()
    {
        Assert.Equal("build.run.started", BuildRunProgressEventNames.Started);
        Assert.Equal("build.run.completed", BuildRunProgressEventNames.Completed);
    }
}
