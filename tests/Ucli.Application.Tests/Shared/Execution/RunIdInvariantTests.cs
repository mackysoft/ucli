namespace MackySoft.Ucli.Application.Tests.Shared.Execution;

public sealed class RunIdInvariantTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void RunIdGenerator_Generate_ReturnsNonEmptyGuid ()
    {
        Assert.NotEqual(Guid.Empty, new RunIdGenerator().Generate());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TestRunServiceResult_WhenRunIdIsEmpty_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => TestRunServiceResult.Pass(
            "Tests passed.",
            Guid.Empty,
            "/tmp/artifacts",
            "/tmp/artifacts/summary.json"));

        Assert.Equal("runId", exception.ParamName);
    }
}
