using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.ContractReading;

public sealed class ExpectationConstraintHelperTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryReadOptional_ReturnsNullConstraints_WhenExpectationIsAbsent ()
    {
        var operationElement = JsonSerializer.SerializeToElement(new
        {
            id = "op-1",
            op = UcliPrimitiveOperationNames.SceneOpen,
            args = new { },
        });

        var result = ExpectationConstraintHelper.TryReadOptional(operationElement, out var constraints, out var error);

        Assert.True(result);
        Assert.False(constraints.HasValue);
        AssertExpectationReadError(error, ExpectationConstraintReadErrorKind.None, string.Empty);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryReadOptional_ReturnsUnknownPropertyError_WhenExpectationContainsUnknownProperty ()
    {
        var operationElement = JsonSerializer.SerializeToElement(new
        {
            expect = new
            {
                nonNull = true,
                unknown = 1,
            },
        });

        var result = ExpectationConstraintHelper.TryReadOptional(operationElement, out _, out var error);

        Assert.False(result);
        AssertExpectationReadError(
            error,
            ExpectationConstraintReadErrorKind.ExpectationContainsUnknownProperty,
            "expect",
            "unknown");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryReadOptional_ReturnsCountCombinationError_WhenCountAndMinAreCombined ()
    {
        var operationElement = JsonSerializer.SerializeToElement(new
        {
            expect = new
            {
                count = 1,
                min = 0,
            },
        });

        var result = ExpectationConstraintHelper.TryReadOptional(operationElement, out _, out var error);

        Assert.False(result);
        AssertExpectationReadError(error, ExpectationConstraintReadErrorKind.CountCannotCombineWithMinOrMax, "expect");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryReadOptional_ReturnsRangeError_WhenMinIsGreaterThanMax ()
    {
        var operationElement = JsonSerializer.SerializeToElement(new
        {
            expect = new
            {
                min = 5,
                max = 1,
            },
        });

        var result = ExpectationConstraintHelper.TryReadOptional(operationElement, out _, out var error);

        Assert.False(result);
        AssertExpectationReadError(error, ExpectationConstraintReadErrorKind.MinMustBeLessThanOrEqualToMax, "expect");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryReadOptional_ReturnsIntegerTypeError_WhenCountIsNotInteger ()
    {
        var operationElement = JsonSerializer.SerializeToElement(new
        {
            expect = new
            {
                count = "1",
            },
        });

        var result = ExpectationConstraintHelper.TryReadOptional(operationElement, out _, out var error);

        Assert.False(result);
        AssertExpectationReadError(error, ExpectationConstraintReadErrorKind.IntegerConstraintMustBeInteger, "expect.count");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryReadOptional_ReturnsConstraints_WhenExpectationIsValid ()
    {
        var operationElement = JsonSerializer.SerializeToElement(new
        {
            expect = new
            {
                nonNull = true,
                min = 1,
                max = 3,
            },
        });

        var result = ExpectationConstraintHelper.TryReadOptional(operationElement, out var constraints, out var error);

        Assert.True(result);
        AssertExpectationReadError(error, ExpectationConstraintReadErrorKind.None, string.Empty);
        Assert.True(constraints.HasValue);
        JsonAssert.For(JsonSerializer.SerializeToElement(constraints.Value))
            .HasBoolean("NonNull", true)
            .IsNull("Count")
            .HasInt32("Min", 1)
            .HasInt32("Max", 3);
    }

    private static void AssertExpectationReadError (
        ExpectationConstraintReadError error,
        ExpectationConstraintReadErrorKind expectedKind,
        string expectedPropertyPath,
        string? expectedUnknownPropertyName = null)
    {
        var assertion = JsonAssert.For(JsonSerializer.SerializeToElement(error))
            .HasInt32("Kind", (int)expectedKind)
            .HasString("PropertyPath", expectedPropertyPath);
        if (expectedUnknownPropertyName is null)
        {
            assertion.IsNull("UnknownPropertyName");
            return;
        }

        assertion.HasString("UnknownPropertyName", expectedUnknownPropertyName);
    }
}
