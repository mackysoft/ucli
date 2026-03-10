using System;
using System.Text.Json;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Parses and validates argument contracts for prefab-domain path operations. </summary>
    internal static class PrefabOperationArgumentsCodec
    {
        /// <summary> Parses prefab-path arguments used by <c>ucli.prefab.open</c> and <c>ucli.prefab.save</c>. </summary>
        /// <param name="args"> The operation arguments element. </param>
        /// <param name="prefabPath"> The parsed prefab path when successful. </param>
        /// <param name="errorMessage"> The parse error message when failed. </param>
        /// <returns> <see langword="true" /> when parsing succeeds; otherwise <see langword="false" />. </returns>
        public static bool TryParsePathArguments (
            JsonElement args,
            out string prefabPath,
            out string errorMessage)
        {
            prefabPath = string.Empty;
            errorMessage = string.Empty;
            if (args.ValueKind != JsonValueKind.Object)
            {
                errorMessage = "Operation 'args' must be an object.";
                return false;
            }

            var hasPath = false;
            foreach (var property in args.EnumerateObject())
            {
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

            if (!hasPath)
            {
                errorMessage = $"Operation 'args' requires property '{PrefabOperationPropertyNames.Path}'.";
                return false;
            }

            return true;
        }
    }
}