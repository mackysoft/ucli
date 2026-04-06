using System;
using System.Text.Json;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Parses and validates argument contracts for <c>ucli.assets.find</c>. </summary>
    internal static class AssetsFindArgumentsCodec
    {
        private const string NameContainsPropertyName = "nameContains";

        private const string PathPrefixPropertyName = "pathPrefix";

        public static bool TryParse (
            JsonElement args,
            out AssetsFindArguments parsedArguments,
            out string errorMessage)
        {
            parsedArguments = default;
            errorMessage = string.Empty;
            if (args.ValueKind != JsonValueKind.Object)
            {
                errorMessage = "Operation 'args' must be an object.";
                return false;
            }

            var hasType = false;
            var hasPathPrefix = false;
            var hasNameContains = false;
            string? typeId = null;
            string? pathPrefix = null;
            string? nameContains = null;
            foreach (var property in args.EnumerateObject())
            {
                if (string.Equals(property.Name, AssetOperationPropertyNames.Type, StringComparison.Ordinal))
                {
                    if (!TryReadUniqueOptionalString(property, ref hasType, out typeId, out errorMessage))
                    {
                        return false;
                    }

                    continue;
                }

                if (string.Equals(property.Name, PathPrefixPropertyName, StringComparison.Ordinal))
                {
                    if (!TryReadUniqueOptionalString(property, ref hasPathPrefix, out pathPrefix, out errorMessage))
                    {
                        return false;
                    }

                    continue;
                }

                if (string.Equals(property.Name, NameContainsPropertyName, StringComparison.Ordinal))
                {
                    if (!TryReadUniqueOptionalString(property, ref hasNameContains, out nameContains, out errorMessage))
                    {
                        return false;
                    }

                    continue;
                }

                errorMessage = $"Operation 'args' contains an unknown property: {property.Name}.";
                return false;
            }

            if (typeId == null
                && pathPrefix == null
                && nameContains == null)
            {
                errorMessage =
                    $"Operation 'args' must specify at least one of '{AssetOperationPropertyNames.Type}', '{PathPrefixPropertyName}', or '{NameContainsPropertyName}'.";
                return false;
            }

            parsedArguments = new AssetsFindArguments(typeId, pathPrefix, nameContains);
            return true;
        }

        private static bool TryReadUniqueOptionalString (
            JsonProperty property,
            ref bool hasProperty,
            out string? value,
            out string errorMessage)
        {
            value = null;
            if (hasProperty)
            {
                errorMessage = $"Operation 'args' contains duplicated property: {property.Name}.";
                return false;
            }

            if (!OperationArgumentValueReader.TryReadRequiredString(property, out value, out errorMessage))
            {
                return false;
            }

            hasProperty = true;
            return true;
        }
    }
}
