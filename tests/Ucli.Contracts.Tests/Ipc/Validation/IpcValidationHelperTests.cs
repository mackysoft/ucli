using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc.Validation;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Validation;

public sealed class IpcValidationHelperTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void RequestSchemaPolicy_ExposesExpectedStrictAndPermissiveFlags ()
    {
        var strict = RequestSchemaPolicy.StrictExecute;
        var permissive = RequestSchemaPolicy.PermissivePreflight;

        Assert.True(strict.RequireOperationObject);
        Assert.True(strict.RequireOperationId);
        Assert.True(strict.RequireOperationName);
        Assert.True(strict.RequireNonEmptyOperationId);
        Assert.True(strict.RequireNonEmptyOperationName);

        Assert.False(permissive.RequireOperationObject);
        Assert.False(permissive.RequireOperationId);
        Assert.False(permissive.RequireOperationName);
        Assert.False(permissive.RequireNonEmptyOperationId);
        Assert.False(permissive.RequireNonEmptyOperationName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void JsonPropertyGuard_ReturnsUnknownPropertyName_WhenObjectContainsUnknownProperty ()
    {
        var jsonObject = JsonSerializer.SerializeToElement(new
        {
            protocolVersion = 1,
            requestId = "req-1",
            unknown = true,
        });
        var allowedPropertyNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "protocolVersion",
            "requestId",
        };

        var result = JsonPropertyGuard.FindUnknownProperty(jsonObject, allowedPropertyNames);

        Assert.Equal("unknown", result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void JsonPropertyGuard_ReturnsNull_WhenAllPropertiesAreAllowed ()
    {
        var jsonObject = JsonSerializer.SerializeToElement(new
        {
            protocolVersion = 1,
            requestId = "req-1",
        });
        var allowedPropertyNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "protocolVersion",
            "requestId",
        };

        var result = JsonPropertyGuard.FindUnknownProperty(jsonObject, allowedPropertyNames);

        Assert.Null(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void JsonStringContractReader_ReturnsMissingError_WhenRequiredPropertyIsAbsent ()
    {
        var jsonObject = JsonSerializer.SerializeToElement(new
        {
            protocolVersion = 1,
        });

        var result = JsonStringContractReader.TryRead(
            jsonObject,
            "requestId",
            JsonStringPresenceRequirement.Required,
            rejectEmptyOrWhitespace: true,
            rejectOuterWhitespace: true,
            out var value,
            out var error);

        Assert.False(result);
        Assert.Null(value);
        AssertJsonStringReadError(error, JsonStringReadErrorKind.Missing, "requestId");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void JsonStringContractReader_ReturnsNullWithoutError_WhenOptionalLoosePropertyIsNonString ()
    {
        var jsonObject = JsonSerializer.SerializeToElement(new
        {
            requestId = 123,
        });

        var result = JsonStringContractReader.TryRead(
            jsonObject,
            "requestId",
            JsonStringPresenceRequirement.OptionalLoose,
            rejectEmptyOrWhitespace: true,
            rejectOuterWhitespace: true,
            out var value,
            out var error);

        Assert.True(result);
        Assert.Null(value);
        AssertJsonStringReadError(error, JsonStringReadErrorKind.None, string.Empty);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void JsonStringContractReader_ReturnsTypeMismatch_WhenOptionalStrictPropertyIsNonString ()
    {
        using var parsed = JsonDocument.Parse("""{"as":10}""");

        var result = JsonStringContractReader.TryRead(
            parsed.RootElement,
            "as",
            JsonStringPresenceRequirement.OptionalStrict,
            rejectEmptyOrWhitespace: true,
            rejectOuterWhitespace: true,
            out _,
            out var error);

        Assert.False(result);
        AssertJsonStringReadError(error, JsonStringReadErrorKind.TypeMismatch, "as");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void JsonStringContractReader_ReturnsOuterWhitespaceError_WhenOuterWhitespaceIsRejected ()
    {
        var jsonObject = JsonSerializer.SerializeToElement(new
        {
            op = " ucli.scene.open ",
        });

        var result = JsonStringContractReader.TryRead(
            jsonObject,
            "op",
            JsonStringPresenceRequirement.Required,
            rejectEmptyOrWhitespace: true,
            rejectOuterWhitespace: true,
            out _,
            out var error);

        Assert.False(result);
        AssertJsonStringReadError(error, JsonStringReadErrorKind.OuterWhitespace, "op");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ExpectationConstraintHelper_ReturnsNullConstraints_WhenExpectationIsAbsent ()
    {
        var operationElement = JsonSerializer.SerializeToElement(new
        {
            id = "op-1",
            op = "ucli.scene.open",
            args = new { },
        });

        var result = ExpectationConstraintHelper.TryReadOptional(operationElement, out var constraints, out var error);

        Assert.True(result);
        Assert.False(constraints.HasValue);
        AssertExpectationReadError(error, ExpectationConstraintReadErrorKind.None, string.Empty);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ExpectationConstraintHelper_ReturnsUnknownPropertyError_WhenExpectationContainsUnknownProperty ()
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
    public void ExpectationConstraintHelper_ReturnsCountCombinationError_WhenCountAndMinAreCombined ()
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
    public void ExpectationConstraintHelper_ReturnsRangeError_WhenMinIsGreaterThanMax ()
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
    public void ExpectationConstraintHelper_ReturnsIntegerTypeError_WhenCountIsNotInteger ()
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
    public void ExpectationConstraintHelper_ReturnsConstraints_WhenExpectationIsValid ()
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

    [Fact]
    [Trait("Size", "Small")]
    public void OperationContractReader_ReturnsMissingError_WhenArgsIsAbsent ()
    {
        var operationElement = JsonSerializer.SerializeToElement(new
        {
            id = "op-1",
            op = "ucli.scene.open",
        });

        var result = OperationContractReader.TryReadOperationArgs(operationElement, out _, out var errorKind);

        Assert.False(result);
        Assert.Equal(OperationObjectReadErrorKind.Missing, errorKind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void OperationContractReader_ReturnsTypeMismatch_WhenArgsIsNotObject ()
    {
        var operationElement = JsonSerializer.SerializeToElement(new
        {
            args = new[] { 1, 2, 3 },
        });

        var result = OperationContractReader.TryReadOperationArgs(operationElement, out _, out var errorKind);

        Assert.False(result);
        Assert.Equal(OperationObjectReadErrorKind.TypeMismatch, errorKind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void OperationContractReader_ReturnsTypeMismatchError_WhenAliasIsNotString ()
    {
        using var parsed = JsonDocument.Parse("""{"as":123}""");

        var result = OperationContractReader.TryReadOperationAlias(parsed.RootElement, out _, out var error);

        Assert.False(result);
        AssertJsonStringReadError(error, JsonStringReadErrorKind.TypeMismatch, "as");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void OperationContractReader_AllowsMissingOrNonStringOperationName_WhenPolicyIsPermissivePreflight ()
    {
        var operationElement = JsonSerializer.SerializeToElement(new
        {
            op = 100,
        });

        var result = OperationContractReader.TryReadOperationName(
            operationElement,
            RequestSchemaPolicy.PermissivePreflight,
            out var operationName,
            out var error);

        Assert.True(result);
        Assert.Null(operationName);
        AssertJsonStringReadError(error, JsonStringReadErrorKind.None, string.Empty);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void OperationContractReader_RejectsEmptyOperationId_WhenPolicyIsStrictExecute ()
    {
        var operationElement = JsonSerializer.SerializeToElement(new
        {
            id = "",
        });

        var result = OperationContractReader.TryReadOperationId(
            operationElement,
            RequestSchemaPolicy.StrictExecute,
            out _,
            out var error);

        Assert.False(result);
        AssertJsonStringReadError(error, JsonStringReadErrorKind.EmptyOrWhitespace, "id");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void OperationContractReader_ReturnsMissingError_WhenOperationNameIsMissingInStrictMode ()
    {
        var operationElement = JsonSerializer.SerializeToElement(new
        {
            id = "op-1",
        });

        var result = OperationContractReader.TryReadOperationName(
            operationElement,
            RequestSchemaPolicy.StrictExecute,
            out _,
            out var error);

        Assert.False(result);
        AssertJsonStringReadError(error, JsonStringReadErrorKind.Missing, "op");
    }

    private static void AssertJsonStringReadError (
        JsonStringReadError error,
        JsonStringReadErrorKind expectedKind,
        string expectedPropertyName)
    {
        JsonAssert.For(JsonSerializer.SerializeToElement(error))
            .HasInt32("Kind", (int)expectedKind)
            .HasString("PropertyName", expectedPropertyName);
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