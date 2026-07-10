using MackySoft.Ucli.Application.Shared.Execution.Timeout;

namespace MackySoft.Ucli.Application.Tests.Execution.Timeout;

public sealed class ExecutionTimeoutBudgetTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Start_WhenTimeProviderIsNull_ThrowsArgumentNullException ()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = ExecutionTimeoutBudget.Start(TimeSpan.FromSeconds(1), null!);
        });
    }
}
