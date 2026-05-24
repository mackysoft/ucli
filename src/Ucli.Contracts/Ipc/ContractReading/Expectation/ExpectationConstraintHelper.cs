using System.Text.Json;
using MackySoft.Ucli.Contracts.Json;

namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

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
        if (!TryReadExpectationElement(operationElement, out var expectationElement, out error))
        {
            return false;
        }

        if (expectationElement == null)
        {
            return true;
        }

        if (!TryReadValues(expectationElement.Value, out var values, out error))
        {
            return false;
        }

        constraints = new ExpectationConstraints(
            NonNull: values.NonNull,
            Count: values.Count,
            Min: values.Min,
            Max: values.Max);
        error = ExpectationConstraintReadError.None;
        return true;
    }

    private static bool TryReadExpectationElement (
        JsonElement operationElement,
        out JsonElement? expectationElement,
        out ExpectationConstraintReadError error)
    {
        expectationElement = null;
        error = ExpectationConstraintReadError.None;
        if (!operationElement.TryGetProperty(ExpectationConstraintSchema.ExpectationPropertyName, out var rawElement))
        {
            return true;
        }

        if (rawElement.ValueKind != JsonValueKind.Object)
        {
            error = CreateError(ExpectationConstraintReadErrorKind.ExpectationMustBeObject);
            return false;
        }

        var unknownProperty = JsonObjectPropertyReader.FindUnknownProperty(rawElement, ExpectationConstraintSchema.AllowedProperties);
        if (unknownProperty is not null)
        {
            error = new ExpectationConstraintReadError(
                Kind: ExpectationConstraintReadErrorKind.ExpectationContainsUnknownProperty,
                PropertyPath: ExpectationConstraintSchema.ExpectationPropertyName,
                UnknownPropertyName: unknownProperty);
            return false;
        }

        if (!rawElement.EnumerateObject().MoveNext())
        {
            error = CreateError(ExpectationConstraintReadErrorKind.ExpectationMustContainAtLeastOneConstraint);
            return false;
        }

        expectationElement = rawElement;
        return true;
    }

    private static bool TryReadValues (
        JsonElement expectationElement,
        out ExpectationConstraintValues values,
        out ExpectationConstraintReadError error)
    {
        var nonNull = ExpectationConstraintValueReader.TryReadOptionalBoolean(
            expectationElement,
            ExpectationConstraintSchema.NonNullPropertyName,
            out error);
        if (error.Kind != ExpectationConstraintReadErrorKind.None)
        {
            values = default;
            return false;
        }

        var count = ExpectationConstraintValueReader.TryReadOptionalNonNegativeInteger(
            expectationElement,
            ExpectationConstraintSchema.CountPropertyName,
            out error);
        if (error.Kind != ExpectationConstraintReadErrorKind.None)
        {
            values = default;
            return false;
        }

        var min = ExpectationConstraintValueReader.TryReadOptionalNonNegativeInteger(
            expectationElement,
            ExpectationConstraintSchema.MinPropertyName,
            out error);
        if (error.Kind != ExpectationConstraintReadErrorKind.None)
        {
            values = default;
            return false;
        }

        var max = ExpectationConstraintValueReader.TryReadOptionalNonNegativeInteger(
            expectationElement,
            ExpectationConstraintSchema.MaxPropertyName,
            out error);
        if (error.Kind != ExpectationConstraintReadErrorKind.None)
        {
            values = default;
            return false;
        }

        values = new ExpectationConstraintValues(nonNull, count, min, max);
        return TryValidateCombination(values, out error);
    }

    private static bool TryValidateCombination (
        ExpectationConstraintValues values,
        out ExpectationConstraintReadError error)
    {
        if (values.Count.HasValue && (values.Min.HasValue || values.Max.HasValue))
        {
            error = CreateError(ExpectationConstraintReadErrorKind.CountCannotCombineWithMinOrMax);
            return false;
        }

        if (values.Min.HasValue && values.Max.HasValue && values.Min.Value > values.Max.Value)
        {
            error = CreateError(ExpectationConstraintReadErrorKind.MinMustBeLessThanOrEqualToMax);
            return false;
        }

        error = ExpectationConstraintReadError.None;
        return true;
    }

    private static ExpectationConstraintReadError CreateError (ExpectationConstraintReadErrorKind kind)
    {
        return new ExpectationConstraintReadError(
            Kind: kind,
            PropertyPath: ExpectationConstraintSchema.ExpectationPropertyName);
    }

    private readonly struct ExpectationConstraintValues
    {
        public ExpectationConstraintValues (
            bool? nonNull,
            int? count,
            int? min,
            int? max)
        {
            NonNull = nonNull;
            Count = count;
            Min = min;
            Max = max;
        }

        public bool? NonNull { get; }

        public int? Count { get; }

        public int? Min { get; }

        public int? Max { get; }
    }
}
