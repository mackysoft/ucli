using System;
using System.Text.Json;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Parses and validates argument contracts for <c>ucli.comp.ensure</c>. </summary>
    internal static class CompEnsureArgumentsCodec
    {
        public static bool TryParse (
            JsonElement args,
            out CompEnsureArguments parsedArguments,
            out string errorMessage)
        {
            parsedArguments = default;
            errorMessage = string.Empty;
            if (args.ValueKind != JsonValueKind.Object)
            {
                errorMessage = "Operation 'args' must be an object.";
                return false;
            }

            var hasTarget = false;
            var hasType = false;
            var targetReference = default(UnityObjectReference);
            string? typeId = null;
            foreach (var property in args.EnumerateObject())
            {
                if (string.Equals(property.Name, CompOperationPropertyNames.Target, StringComparison.Ordinal))
                {
                    if (hasTarget)
                    {
                        errorMessage = $"Operation 'args' contains duplicated property: {CompOperationPropertyNames.Target}.";
                        return false;
                    }

                    if (!UnityObjectReferenceCodec.TryParse(property.Value, "args.target", out targetReference, out errorMessage))
                    {
                        return false;
                    }

                    hasTarget = true;
                    continue;
                }

                if (string.Equals(property.Name, CompOperationPropertyNames.Type, StringComparison.Ordinal))
                {
                    if (hasType)
                    {
                        errorMessage = $"Operation 'args' contains duplicated property: {CompOperationPropertyNames.Type}.";
                        return false;
                    }

                    if (!OperationArgumentValueReader.TryReadRequiredString(property, out typeId, out errorMessage))
                    {
                        return false;
                    }

                    hasType = true;
                    continue;
                }

                errorMessage = $"Operation 'args' contains an unknown property: {property.Name}.";
                return false;
            }

            if (!hasTarget)
            {
                errorMessage = $"Operation 'args' requires property '{CompOperationPropertyNames.Target}'.";
                return false;
            }

            if (!hasType)
            {
                errorMessage = $"Operation 'args' requires property '{CompOperationPropertyNames.Type}'.";
                return false;
            }

            parsedArguments = new CompEnsureArguments(targetReference, typeId!);
            return true;
        }
    }
}