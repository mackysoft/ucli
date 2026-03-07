using System.Text.Json;
using MackySoft.Ucli.Contracts.Text;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Provides shared readers for strict operation-argument values. </summary>
    internal static class OperationArgumentValueReader
    {
        /// <summary> Reads one required strict string property. </summary>
        /// <param name="property"> The JSON property. </param>
        /// <param name="value"> The parsed value when successful. </param>
        /// <param name="errorMessage"> The parse error message when parsing fails. </param>
        /// <returns> <see langword="true" /> when the property is valid; otherwise <see langword="false" />. </returns>
        public static bool TryReadRequiredString (
            JsonProperty property,
            out string? value,
            out string errorMessage)
        {
            return TryReadRequiredString(
                property.Value,
                $"args.{property.Name}",
                expectedTypeDescription: "a string",
                out value,
                out errorMessage);
        }

        /// <summary> Reads one required strict string value using the specified logical property path. </summary>
        /// <param name="valueElement"> The JSON element that stores the value. </param>
        /// <param name="propertyPath"> The logical property path used in diagnostics. </param>
        /// <param name="expectedTypeDescription"> The expected type description used in diagnostics. </param>
        /// <param name="value"> The parsed value when successful. </param>
        /// <param name="errorMessage"> The parse error message when parsing fails. </param>
        /// <returns> <see langword="true" /> when the value is valid; otherwise <see langword="false" />. </returns>
        public static bool TryReadRequiredString (
            JsonElement valueElement,
            string propertyPath,
            string expectedTypeDescription,
            out string? value,
            out string errorMessage)
        {
            value = null;
            if (valueElement.ValueKind != JsonValueKind.String)
            {
                errorMessage = $"Operation '{propertyPath}' must be {expectedTypeDescription}.";
                return false;
            }

            var parsedValue = valueElement.GetString();
            if (string.IsNullOrWhiteSpace(parsedValue))
            {
                errorMessage = $"Operation '{propertyPath}' must not be empty or whitespace.";
                return false;
            }

            if (StringValueValidator.HasOuterWhitespace(parsedValue))
            {
                errorMessage = $"Operation '{propertyPath}' must not contain leading or trailing whitespace.";
                return false;
            }

            value = parsedValue;
            errorMessage = string.Empty;
            return true;
        }

        /// <summary> Reads one non-negative integer-or-null property. </summary>
        /// <param name="property"> The JSON property. </param>
        /// <param name="value"> The parsed value when successful. </param>
        /// <param name="errorMessage"> The parse error message when parsing fails. </param>
        /// <returns> <see langword="true" /> when the property is valid; otherwise <see langword="false" />. </returns>
        public static bool TryReadNonNegativeInt32OrNull (
            JsonProperty property,
            out int? value,
            out string errorMessage)
        {
            return TryReadNonNegativeInt32OrNull(
                property.Value,
                $"args.{property.Name}",
                out value,
                out errorMessage);
        }

        /// <summary> Reads one non-negative integer-or-null value using the specified logical property path. </summary>
        /// <param name="valueElement"> The JSON element that stores the value. </param>
        /// <param name="propertyPath"> The logical property path used in diagnostics. </param>
        /// <param name="value"> The parsed value when successful. </param>
        /// <param name="errorMessage"> The parse error message when parsing fails. </param>
        /// <returns> <see langword="true" /> when the value is valid; otherwise <see langword="false" />. </returns>
        public static bool TryReadNonNegativeInt32OrNull (
            JsonElement valueElement,
            string propertyPath,
            out int? value,
            out string errorMessage)
        {
            value = null;
            if (valueElement.ValueKind == JsonValueKind.Null)
            {
                errorMessage = string.Empty;
                return true;
            }

            if (valueElement.ValueKind != JsonValueKind.Number || !valueElement.TryGetInt32(out var parsedValue))
            {
                errorMessage = $"Operation '{propertyPath}' must be an integer or null.";
                return false;
            }

            if (parsedValue < 0)
            {
                errorMessage = $"Operation '{propertyPath}' must be greater than or equal to 0.";
                return false;
            }

            value = parsedValue;
            errorMessage = string.Empty;
            return true;
        }
    }
}
