using System;
using System.Text.Json;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Parses and validates argument contracts for <c>ucli.prefab.create</c>. </summary>
    internal static class PrefabCreateArgumentsCodec
    {
        /// <summary> Parses one <c>ucli.prefab.create</c> argument object. </summary>
        /// <param name="args"> The operation argument element. </param>
        /// <param name="parsedArguments"> The parsed arguments when successful. </param>
        /// <param name="errorMessage"> The parse error message when parsing fails. </param>
        /// <returns> <see langword="true" /> when parsing succeeds; otherwise <see langword="false" />. </returns>
        public static bool TryParse (
            JsonElement args,
            out PrefabCreateArguments parsedArguments,
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
            var hasPath = false;
            var targetReference = default(UnityObjectReference);
            string? prefabPath = null;
            foreach (var property in args.EnumerateObject())
            {
                if (string.Equals(property.Name, PrefabOperationPropertyNames.Target, StringComparison.Ordinal))
                {
                    if (hasTarget)
                    {
                        errorMessage = $"Operation 'args' contains duplicated property: {PrefabOperationPropertyNames.Target}.";
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

                if (string.Equals(property.Name, PrefabOperationPropertyNames.Path, StringComparison.Ordinal))
                {
                    if (hasPath)
                    {
                        errorMessage = $"Operation 'args' contains duplicated property: {PrefabOperationPropertyNames.Path}.";
                        return false;
                    }

                    if (!OperationArgumentValueReader.TryReadRequiredString(property, out prefabPath, out errorMessage))
                    {
                        return false;
                    }

                    hasPath = true;
                    continue;
                }

                errorMessage = $"Operation 'args' contains an unknown property: {property.Name}.";
                return false;
            }

            if (!hasTarget)
            {
                errorMessage = $"Operation 'args' requires property '{PrefabOperationPropertyNames.Target}'.";
                return false;
            }

            if (!hasPath)
            {
                errorMessage = $"Operation 'args' requires property '{PrefabOperationPropertyNames.Path}'.";
                return false;
            }

            parsedArguments = new PrefabCreateArguments(targetReference, prefabPath!);
            return true;
        }
    }
}