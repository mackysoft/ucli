using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.Tests.Shared.Execution.UnityExecutionMode.Decision;

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
