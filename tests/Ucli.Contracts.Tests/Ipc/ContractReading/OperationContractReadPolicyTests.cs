using MackySoft.Ucli.Contracts.Ipc.ContractReading;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.ContractReading;

public sealed class OperationContractReadPolicyTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void StrictExecute_RequiresOperationIdentifiers ()
    {
        var policy = OperationContractReadPolicy.StrictExecute;

        Assert.True(policy.RequireOperationId);
        Assert.True(policy.RequireOperationName);
        Assert.True(policy.RequireNonEmptyOperationId);
        Assert.True(policy.RequireNonEmptyOperationName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PermissivePreflight_AllowsMissingOperationIdentifiers ()
    {
        var policy = OperationContractReadPolicy.PermissivePreflight;

        Assert.False(policy.RequireOperationId);
        Assert.False(policy.RequireOperationName);
        Assert.False(policy.RequireNonEmptyOperationId);
        Assert.False(policy.RequireNonEmptyOperationName);
    }
}
