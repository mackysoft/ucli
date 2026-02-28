using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc.Validation;

/// <summary> Provides primitive readers for individual <c>expect</c> constraint values. </summary>
internal static class ExpectationConstraintValueReader
{
    /// <summary> Reads one optional boolean constraint from an <c>expect</c> object. </summary>
    /// <param name="expectationElement"> The <c>expect</c> object element. </param>
    /// <param name="propertyName"> The boolean property name. </param>
    /// <param name="error"> The machine-readable contract-read error on failure. </param>
    /// <returns> The parsed boolean value, or <see langword="null" /> when the property is absent. </returns>
    public static bool? TryReadOptionalBoolean (
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

    /// <summary> Reads one optional non-negative integer constraint from an <c>expect</c> object. </summary>
    /// <param name="expectationElement"> The <c>expect</c> object element. </param>
    /// <param name="propertyName"> The integer property name. </param>
    /// <param name="error"> The machine-readable contract-read error on failure. </param>
    /// <returns> The parsed integer value, or <see langword="null" /> when the property is absent. </returns>
    public static int? TryReadOptionalNonNegativeInteger (
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