using System;
using System.Text.Json;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Parses and validates argument contracts for <c>ucli.asset.create</c>. </summary>
    internal static class AssetCreateArgumentsCodec
    {
        public static bool TryParse (
            JsonElement args,
            out AssetCreateArguments parsedArguments,
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
            var hasPath = false;
            string? typeId = null;
            string? assetPath = null;
            foreach (var property in args.EnumerateObject())
            {
                if (string.Equals(property.Name, AssetOperationPropertyNames.Type, StringComparison.Ordinal))
                {
                    if (!TryReadUniqueRequiredString(property, ref hasType, out typeId, out errorMessage))
                    {
                        return false;
                    }

                    continue;
                }

                if (string.Equals(property.Name, AssetOperationPropertyNames.Path, StringComparison.Ordinal))
                {
                    if (!TryReadUniqueRequiredString(property, ref hasPath, out assetPath, out errorMessage))
                    {
                        return false;
                    }

                    continue;
                }

                errorMessage = $"Operation 'args' contains an unknown property: {property.Name}.";
                return false;
            }

            if (!hasType)
            {
                errorMessage = $"Operation 'args' requires property '{AssetOperationPropertyNames.Type}'.";
                return false;
            }

            if (!hasPath)
            {
                errorMessage = $"Operation 'args' requires property '{AssetOperationPropertyNames.Path}'.";
                return false;
            }

            parsedArguments = new AssetCreateArguments(typeId!, assetPath!);
            return true;
        }

        private static bool TryReadUniqueRequiredString (
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