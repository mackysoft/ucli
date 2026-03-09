using System;
using System.Text.Json;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Parses and validates argument contracts for <c>ucli.comp.schema</c>. </summary>
    internal static class CompSchemaArgumentsCodec
    {
        public static bool TryParse (
            JsonElement args,
            out CompSchemaArguments parsedArguments,
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
            string? typeId = null;
            foreach (var property in args.EnumerateObject())
            {
                if (!string.Equals(property.Name, CompOperationPropertyNames.Type, StringComparison.Ordinal))
                {
                    errorMessage = $"Operation 'args' contains an unknown property: {property.Name}.";
                    return false;
                }

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
            }

            if (!hasType)
            {
                errorMessage = $"Operation 'args' requires property '{CompOperationPropertyNames.Type}'.";
                return false;
            }

            parsedArguments = new CompSchemaArguments(typeId!);
            return true;
        }
    }
}