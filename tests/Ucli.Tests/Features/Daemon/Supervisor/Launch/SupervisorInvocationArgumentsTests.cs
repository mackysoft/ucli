using MackySoft.FileSystem;
namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorInvocationArgumentsTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Build_ReturnsParserCompatibleArgumentSequence ()
    {
        const string repositoryRoot = "/repo";

        var arguments = SupervisorInvocationArguments.Build(AbsolutePath.Parse(repositoryRoot));

        Assert.Equal(
            [
                SupervisorConstants.InternalServeFlag,
                SupervisorConstants.RepositoryRootOption,
                repositoryRoot,
            ],
            arguments);
    }

}
