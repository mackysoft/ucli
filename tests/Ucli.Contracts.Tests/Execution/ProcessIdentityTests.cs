using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Contracts.Tests.Execution;

public sealed class ProcessIdentityTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [Trait("Size", "Small")]
    public void Constructor_WhenProcessIdIsNotPositive_ThrowsArgumentOutOfRangeException (int processId)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ProcessIdentity(processId, Generation: 1));

        Assert.Equal("ProcessId", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenGenerationIsZero_ThrowsArgumentOutOfRangeException ()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ProcessIdentity(ProcessId: 1, Generation: 0));

        Assert.Equal("Generation", exception.ParamName);
    }
}
