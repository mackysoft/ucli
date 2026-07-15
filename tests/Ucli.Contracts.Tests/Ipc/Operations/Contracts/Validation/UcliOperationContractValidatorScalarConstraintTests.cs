using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Contracts.Tests.Ipc.Operations.UcliOperationContractValidatorTestContracts;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Operations;

public sealed class UcliOperationContractValidatorScalarConstraintTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_WhenRangeValueIsBelowMinimum_ReturnsFalse ()
    {
        var args = new RangeArgs(-1);

        var isValid = UcliOperationContractValidator.TryValidate(args, typeof(RangeArgs), out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Operation 'args.depth' must be greater than or equal to 0.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_WhenRangeMaximumIsOmitted_DoesNotApplyDefaultMaximum ()
    {
        var args = new RangeArgs(1);

        var isValid = UcliOperationContractValidator.TryValidate(args, typeof(RangeArgs), out var errorMessage);

        Assert.True(isValid);
        Assert.Empty(errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_WhenRangeMinimumIsOmitted_DoesNotApplyDefaultMinimum ()
    {
        var args = new MaximumRangeArgs(-1);

        var isValid = UcliOperationContractValidator.TryValidate(args, typeof(MaximumRangeArgs), out var errorMessage);

        Assert.True(isValid);
        Assert.Empty(errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidate_WhenCursorIsInvalid_ReturnsFalse ()
    {
        var args = new CursorArgs(" " + BoundedWindowCursorCodec.Encode(1));

        var isValid = UcliOperationContractValidator.TryValidate(args, typeof(CursorArgs), out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Operation 'args.cursor' must be a valid cursor.", errorMessage);
    }
}
