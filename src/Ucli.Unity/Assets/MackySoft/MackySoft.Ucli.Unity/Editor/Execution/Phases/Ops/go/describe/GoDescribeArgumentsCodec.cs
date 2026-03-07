using System;
using System.Text.Json;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Parses and validates argument contracts for <c>ucli.go.describe</c>. </summary>
    internal static class GoDescribeArgumentsCodec
    {
        /// <summary> Parses one <c>ucli.go.describe</c> argument object. </summary>
        /// <param name="args"> The operation argument element. </param>
        /// <param name="parsedArguments"> The parsed arguments when successful. </param>
        /// <param name="errorMessage"> The parse error message when parsing fails. </param>
        /// <returns> <see langword="true" /> when parsing succeeds; otherwise <see langword="false" />. </returns>
        public static bool TryParse (
            JsonElement args,
            out GoDescribeArguments parsedArguments,
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
            var hasDepth = false;
            var targetReference = default(UnityObjectReference);
            var depth = default(int?);
            foreach (var property in args.EnumerateObject())
            {
                if (string.Equals(property.Name, GoOperationPropertyNames.Target, StringComparison.Ordinal))
                {
                    if (hasTarget)
                    {
                        errorMessage = $"Operation 'args' contains duplicated property: {GoOperationPropertyNames.Target}.";
                        return false;
                    }

                    if (!UnityObjectReferenceCodec.TryParse(
                        property.Value,
                        "args.target",
                        out targetReference,
                        out errorMessage))
                    {
                        return false;
                    }

                    hasTarget = true;
                    continue;
                }

                if (string.Equals(property.Name, GoOperationPropertyNames.Depth, StringComparison.Ordinal))
                {
                    if (hasDepth)
                    {
                        errorMessage = $"Operation 'args' contains duplicated property: {GoOperationPropertyNames.Depth}.";
                        return false;
                    }

                    if (!OperationArgumentValueReader.TryReadNonNegativeInt32OrNull(property, out depth, out errorMessage))
                    {
                        return false;
                    }

                    hasDepth = true;
                    continue;
                }

                errorMessage = $"Operation 'args' contains an unknown property: {property.Name}.";
                return false;
            }

            if (!hasTarget)
            {
                errorMessage = $"Operation 'args' requires property '{GoOperationPropertyNames.Target}'.";
                return false;
            }

            parsedArguments = new GoDescribeArguments(targetReference, depth);
            return true;
        }
    }
}