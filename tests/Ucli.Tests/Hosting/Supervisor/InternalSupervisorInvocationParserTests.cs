using MackySoft.Ucli.Hosting.Supervisor;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class InternalSupervisorInvocationParserTests
{
    private static readonly string[][] InvalidInternalInvocationCases =
    [
        [SupervisorConstants.InternalServeFlag],
        [SupervisorConstants.InternalServeFlag, "--unknown", "/repo"],
        [SupervisorConstants.InternalServeFlag, SupervisorConstants.RepositoryRootOption],
        [SupervisorConstants.InternalServeFlag, SupervisorConstants.RepositoryRootOption, " "],
        [SupervisorConstants.InternalServeFlag, SupervisorConstants.RepositoryRootOption, "/repo", "extra"],
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WhenInternalFlagIsMissing_ReturnsNotMatched ()
    {
        var invocation = InternalSupervisorInvocationParser.Parse([UcliCommandNames.Status]);

        Assert.False(invocation.IsMatched);
        Assert.False(invocation.IsValid);
        Assert.Equal(string.Empty, invocation.RepositoryRoot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WhenInternalInvocationIsValid_ReturnsRepositoryRoot ()
    {
        const string repositoryRoot = "/repo";

        var invocation = InternalSupervisorInvocationParser.Parse(
            [SupervisorConstants.InternalServeFlag, SupervisorConstants.RepositoryRootOption, repositoryRoot]);

        Assert.True(invocation.IsMatched);
        Assert.True(invocation.IsValid);
        Assert.Equal(repositoryRoot, invocation.RepositoryRoot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WhenInternalInvocationIsInvalid_ReturnsMatchedWithEmptyRepositoryRoot ()
    {
        foreach (var args in InvalidInternalInvocationCases)
        {
            var invocation = InternalSupervisorInvocationParser.Parse(args);

            Assert.True(invocation.IsMatched);
            Assert.False(invocation.IsValid);
            Assert.Equal(string.Empty, invocation.RepositoryRoot);
        }
    }
}
