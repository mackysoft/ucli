using MackySoft.FileSystem;
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
        Assert.Null(invocation.RepositoryRoot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WhenInternalInvocationIsValid_ReturnsRepositoryRoot ()
    {
        var repositoryRoot = AbsolutePath.Parse(
            Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory)!, "repo"));

        var invocation = InternalSupervisorInvocationParser.Parse(
            [SupervisorConstants.InternalServeFlag, SupervisorConstants.RepositoryRootOption, repositoryRoot.Value]);

        Assert.True(invocation.IsMatched);
        Assert.True(invocation.IsValid);
        Assert.Equal(repositoryRoot, invocation.RepositoryRoot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WhenInternalInvocationIsInvalid_ReturnsMatchedWithoutRepositoryRoot ()
    {
        foreach (var args in InvalidInternalInvocationCases)
        {
            var invocation = InternalSupervisorInvocationParser.Parse(args);

            Assert.True(invocation.IsMatched);
            Assert.False(invocation.IsValid);
            Assert.Null(invocation.RepositoryRoot);
        }
    }
}
