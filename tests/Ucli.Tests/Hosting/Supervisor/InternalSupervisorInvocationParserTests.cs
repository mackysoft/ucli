using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Supervisor;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class InternalSupervisorInvocationParserTests
{
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

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(InvalidInternalInvocationCases))]
    public void Parse_WhenInternalInvocationIsInvalid_ReturnsMatchedWithEmptyRepositoryRoot (string[] args)
    {
        var invocation = InternalSupervisorInvocationParser.Parse(args);

        Assert.True(invocation.IsMatched);
        Assert.False(invocation.IsValid);
        Assert.Equal(string.Empty, invocation.RepositoryRoot);
    }

    public static TheoryData<string[]> InvalidInternalInvocationCases => new()
        {
            { [SupervisorConstants.InternalServeFlag] },
            { [SupervisorConstants.InternalServeFlag, "--unknown", "/repo"] },
            { [SupervisorConstants.InternalServeFlag, SupervisorConstants.RepositoryRootOption] },
            { [SupervisorConstants.InternalServeFlag, SupervisorConstants.RepositoryRootOption, " "] },
            { [SupervisorConstants.InternalServeFlag, SupervisorConstants.RepositoryRootOption, "/repo", "extra"] },
        };
}
