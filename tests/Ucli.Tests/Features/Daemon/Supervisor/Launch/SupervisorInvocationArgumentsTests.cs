namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorInvocationArgumentsTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Build_ReturnsParserCompatibleArgumentSequence ()
    {
        const string repositoryRoot = "/repo";

        var arguments = SupervisorInvocationArguments.Build(repositoryRoot);

        Assert.Equal(
            [
                SupervisorConstants.InternalServeFlag,
                SupervisorConstants.RepositoryRootOption,
                repositoryRoot,
            ],
            arguments);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Build_WhenRepositoryRootIsEmpty_ThrowsArgumentException ()
    {
        Assert.Throws<ArgumentException>(() => SupervisorInvocationArguments.Build(" "));
    }
}
