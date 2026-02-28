using System;
using System.Collections.Generic;
using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc.Validation;

/// <summary> Represents normalized <c>expect</c> constraint values. </summary>
/// <param name="NonNull"> The optional non-null constraint flag. </param>
/// <param name="Count"> The optional exact-count constraint. </param>
/// <param name="Min"> The optional minimum-count constraint. </param>
/// <param name="Max"> The optional maximum-count constraint. </param>
internal readonly record struct ExpectationConstraints (
    bool? NonNull,
    int? Count,
    int? Min,
    int? Max);

/// <summary> Defines machine-readable error kinds for <c>expect</c> contract reads. </summary>
internal enum ExpectationConstraintReadErrorKind
{
    /// <summary> No error. </summary>
    None = 0,

    /// <summary> The <c>expect</c> property must be an object when specified. </summary>
    ExpectationMustBeObject,

    /// <summary> The <c>expect</c> object contains one unknown property. </summary>
    ExpectationContainsUnknownProperty,

    /// <summary> The <c>expect</c> object must contain at least one constraint. </summary>
    ExpectationMustContainAtLeastOneConstraint,

    /// <summary> One boolean constraint has invalid value kind. </summary>
    BooleanConstraintMustBeBoolean,

    /// <summary> One integer constraint has invalid value kind. </summary>
    IntegerConstraintMustBeInteger,

    /// <summary> One integer constraint must be non-negative. </summary>
    IntegerConstraintMustBeNonNegative,

    /// <summary> <c>count</c> cannot be combined with <c>min</c> or <c>max</c>. </summary>
    CountCannotCombineWithMinOrMax,

    /// <summary> <c>min</c> must be less than or equal to <c>max</c>. </summary>
    MinMustBeLessThanOrEqualToMax,
}

/// <summary> Represents one <c>expect</c> contract-read error. </summary>
/// <param name="Kind"> The machine-readable error kind. </param>
/// <param name="PropertyPath"> The related property path (for example, <c>expect.count</c>). </param>
/// <param name="UnknownPropertyName"> The unknown property name when <see cref="Kind" /> is unknown-property related; otherwise <see langword="null" />. </param>
internal readonly record struct ExpectationConstraintReadError (
    ExpectationConstraintReadErrorKind Kind,
    string PropertyPath,
    string? UnknownPropertyName = null)
{
    /// <summary> Gets an empty error value that indicates success. </summary>
    public static ExpectationConstraintReadError None => new(ExpectationConstraintReadErrorKind.None, string.Empty);
}

/// <summary> Provides reusable readers for <c>expect</c> constraints in operation contracts. </summary>
internal static class ExpectationConstraintHelper
{
    private static readonly HashSet<string> AllowedExpectationProperties = new(StringComparer.Ordinal)
    {
        "nonNull",
        "count",
        "min",
        "max",
    };

    /// <summary> Reads optional <c>expect</c> constraints from one operation object. </summary>
    /// <param name="operationElement"> The operation JSON object. </param>
    /// <param name="constraints"> The normalized constraint values, or <see langword="null" /> when <c>expect</c> is absent. </param>
    /// <param name="error"> The machine-readable contract-read error on failure. </param>
    /// <returns> <see langword="true" /> when constraints are valid; otherwise <see langword="false" />. </returns>
    public static bool TryReadOptional (
        JsonElement operationElement,
        out ExpectationConstraints? constraints,
        out ExpectationConstraintReadError error)
    {
        constraints = null;
        error = ExpectationConstraintReadError.None;

        if (!operationElement.TryGetProperty("expect", out var expectationElement))
        {
            return true;
        }

        if (expectationElement.ValueKind != JsonValueKind.Object)
        {
            error = new ExpectationConstraintReadError(
                Kind: ExpectationConstraintReadErrorKind.ExpectationMustBeObject,
                PropertyPath: "expect");
            return false;
        }

        var unknownExpectationProperty = JsonPropertyGuard.FindUnknownProperty(expectationElement, AllowedExpectationProperties);
        if (unknownExpectationProperty is not null)
        {
            error = new ExpectationConstraintReadError(
                Kind: ExpectationConstraintReadErrorKind.ExpectationContainsUnknownProperty,
                PropertyPath: "expect",
                UnknownPropertyName: unknownExpectationProperty);
            return false;
        }

        if (!expectationElement.EnumerateObject().MoveNext())
        {
            error = new ExpectationConstraintReadError(
                Kind: ExpectationConstraintReadErrorKind.ExpectationMustContainAtLeastOneConstraint,
                PropertyPath: "expect");
            return false;
        }

        var nonNull = TryReadOptionalBooleanConstraint(expectationElement, "nonNull", out error);
        if (error.Kind != ExpectationConstraintReadErrorKind.None)
        {
            return false;
        }

        var count = TryReadOptionalNonNegativeIntegerConstraint(expectationElement, "count", out error);
        if (error.Kind != ExpectationConstraintReadErrorKind.None)
        {
            return false;
        }

        var min = TryReadOptionalNonNegativeIntegerConstraint(expectationElement, "min", out error);
        if (error.Kind != ExpectationConstraintReadErrorKind.None)
        {
            return false;
        }

        var max = TryReadOptionalNonNegativeIntegerConstraint(expectationElement, "max", out error);
        if (error.Kind != ExpectationConstraintReadErrorKind.None)
        {
            return false;
        }

        if (count.HasValue && (min.HasValue || max.HasValue))
        {
            error = new ExpectationConstraintReadError(
                Kind: ExpectationConstraintReadErrorKind.CountCannotCombineWithMinOrMax,
                PropertyPath: "expect");
            return false;
        }

        if (min.HasValue && max.HasValue && min.Value > max.Value)
        {
            error = new ExpectationConstraintReadError(
                Kind: ExpectationConstraintReadErrorKind.MinMustBeLessThanOrEqualToMax,
                PropertyPath: "expect");
            return false;
        }

        constraints = new ExpectationConstraints(
            NonNull: nonNull,
            Count: count,
            Min: min,
            Max: max);
        return true;
    }

    private static bool? TryReadOptionalBooleanConstraint (
        JsonElement expectationElement,
        string propertyName,
        out ExpectationConstraintReadError error)
    {
        error = ExpectationConstraintReadError.None;
        if (!expectationElement.TryGetProperty(propertyName, out var propertyElement))
        {
            return null;
        }

        if (propertyElement.ValueKind != JsonValueKind.True
            && propertyElement.ValueKind != JsonValueKind.False)
        {
            error = new ExpectationConstraintReadError(
                Kind: ExpectationConstraintReadErrorKind.BooleanConstraintMustBeBoolean,
                PropertyPath: $"expect.{propertyName}");
            return null;
        }

        return propertyElement.GetBoolean();
    }

    private static int? TryReadOptionalNonNegativeIntegerConstraint (
        JsonElement expectationElement,
        string propertyName,
        out ExpectationConstraintReadError error)
    {
        error = ExpectationConstraintReadError.None;
        if (!expectationElement.TryGetProperty(propertyName, out var propertyElement))
        {
            return null;
        }

        if (propertyElement.ValueKind != JsonValueKind.Number || !propertyElement.TryGetInt32(out var parsedValue))
        {
            error = new ExpectationConstraintReadError(
                Kind: ExpectationConstraintReadErrorKind.IntegerConstraintMustBeInteger,
                PropertyPath: $"expect.{propertyName}");
            return null;
        }

        if (parsedValue < 0)
        {
            error = new ExpectationConstraintReadError(
                Kind: ExpectationConstraintReadErrorKind.IntegerConstraintMustBeNonNegative,
                PropertyPath: $"expect.{propertyName}");
            return null;
        }

        return parsedValue;
    }
}