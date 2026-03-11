using System;
using System.Text.Json;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Parses and validates argument contracts for <c>ucli.asset.schema</c>. </summary>
    internal static class AssetSchemaArgumentsCodec
    {
        public static bool TryParse (
            JsonElement args,
            out AssetSchemaArguments parsedArguments,
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
            var hasTarget = false;
            string? typeId = null;
            var targetReference = default(UnityObjectReference);
            foreach (var property in args.EnumerateObject())
            {
                if (string.Equals(property.Name, AssetOperationPropertyNames.Type, StringComparison.Ordinal))
                {
                    if (hasType)
                    {
                        errorMessage = $"Operation 'args' contains duplicated property: {AssetOperationPropertyNames.Type}.";
                        return false;
                    }

                    if (!OperationArgumentValueReader.TryReadRequiredString(property, out typeId, out errorMessage))
                    {
                        return false;
                    }

                    hasType = true;
                    continue;
                }

                if (string.Equals(property.Name, AssetOperationPropertyNames.Target, StringComparison.Ordinal))
                {
                    if (hasTarget)
                    {
                        errorMessage = $"Operation 'args' contains duplicated property: {AssetOperationPropertyNames.Target}.";
                        return false;
                    }

                    if (!UnityObjectReferenceCodec.TryParse(property.Value, "args.target", out targetReference, out errorMessage))
                    {
                        return false;
                    }

                    hasTarget = true;
                    continue;
                }

                errorMessage = $"Operation 'args' contains an unknown property: {property.Name}.";
                return false;
            }

            if (hasType == hasTarget)
            {
                errorMessage =
                    $"Operation 'args' must specify exactly one of '{AssetOperationPropertyNames.Type}' or '{AssetOperationPropertyNames.Target}'.";
                return false;
            }

            parsedArguments = new AssetSchemaArguments(typeId, targetReference, hasTarget);
            return true;
        }
    }
}