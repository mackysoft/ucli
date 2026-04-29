using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Contracts.Tests.Execution;

public sealed class UnityExecutionTargetContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void UnityExecutionTarget_HasStableEnumValues ()
    {
        Assert.Equal(0, (int)UnityExecutionTarget.Daemon);
        Assert.Equal(1, (int)UnityExecutionTarget.Oneshot);
    }
}
