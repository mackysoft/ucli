using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc.Validation;

/// <summary> Provides reusable readers for <c>expect</c> constraints in operation contracts. </summary>
internal static class ExpectationConstraintHelper
{
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

        if (!operationElement.TryGetProperty(ExpectationConstraintSchema.ExpectationPropertyName, out var expectationElement))
        {
            return true;
        }

        if (expectationElement.ValueKind != JsonValueKind.Object)
        {
            error = new ExpectationConstraintReadError(
                Kind: ExpectationConstraintReadErrorKind.ExpectationMustBeObject,
                PropertyPath: ExpectationConstraintSchema.ExpectationPropertyName);
            return false;
        }

        var unknownExpectationProperty = JsonPropertyGuard.FindUnknownProperty(
            expectationElement,
            ExpectationConstraintSchema.AllowedProperties);
        if (unknownExpectationProperty is not null)
        {
            error = new ExpectationConstraintReadError(
                Kind: ExpectationConstraintReadErrorKind.ExpectationContainsUnknownProperty,
                PropertyPath: ExpectationConstraintSchema.ExpectationPropertyName,
                UnknownPropertyName: unknownExpectationProperty);
            return false;
        }

        if (!expectationElement.EnumerateObject().MoveNext())
        {
            error = new ExpectationConstraintReadError(
                Kind: ExpectationConstraintReadErrorKind.ExpectationMustContainAtLeastOneConstraint,
                PropertyPath: ExpectationConstraintSchema.ExpectationPropertyName);
            return false;
        }

        var nonNull = ExpectationConstraintValueReader.TryReadOptionalBoolean(
            expectationElement,
            ExpectationConstraintSchema.NonNullPropertyName,
            out error);
        if (error.Kind != ExpectationConstraintReadErrorKind.None)
        {
            return false;
        }

        var count = ExpectationConstraintValueReader.TryReadOptionalNonNegativeInteger(
            expectationElement,
            ExpectationConstraintSchema.CountPropertyName,
            out error);
        if (error.Kind != ExpectationConstraintReadErrorKind.None)
        {
            return false;
        }

        var min = ExpectationConstraintValueReader.TryReadOptionalNonNegativeInteger(
            expectationElement,
            ExpectationConstraintSchema.MinPropertyName,
            out error);
        if (error.Kind != ExpectationConstraintReadErrorKind.None)
        {
            return false;
        }

        var max = ExpectationConstraintValueReader.TryReadOptionalNonNegativeInteger(
            expectationElement,
            ExpectationConstraintSchema.MaxPropertyName,
            out error);
        if (error.Kind != ExpectationConstraintReadErrorKind.None)
        {
            return false;
        }

        if (count.HasValue && (min.HasValue || max.HasValue))
        {
            error = new ExpectationConstraintReadError(
                Kind: ExpectationConstraintReadErrorKind.CountCannotCombineWithMinOrMax,
                PropertyPath: ExpectationConstraintSchema.ExpectationPropertyName);
            return false;
        }

        if (min.HasValue && max.HasValue && min.Value > max.Value)
        {
            error = new ExpectationConstraintReadError(
                Kind: ExpectationConstraintReadErrorKind.MinMustBeLessThanOrEqualToMax,
                PropertyPath: ExpectationConstraintSchema.ExpectationPropertyName);
            return false;
        }

        constraints = new ExpectationConstraints(
            NonNull: nonNull,
            Count: count,
            Min: min,
            Max: max);
        return true;
    }
}
