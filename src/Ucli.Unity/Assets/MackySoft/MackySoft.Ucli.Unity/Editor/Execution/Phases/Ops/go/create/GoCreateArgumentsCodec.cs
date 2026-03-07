using System;
using System.Text.Json;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Parses and validates argument contracts for <c>ucli.go.create</c>. </summary>
    internal static class GoCreateArgumentsCodec
    {
        /// <summary> Parses one <c>ucli.go.create</c> argument object. </summary>
        /// <param name="args"> The operation argument element. </param>
        /// <param name="parsedArguments"> The parsed arguments when successful. </param>
        /// <param name="errorMessage"> The parse error message when parsing fails. </param>
        /// <returns> <see langword="true" /> when parsing succeeds; otherwise <see langword="false" />. </returns>
        public static bool TryParse (
            JsonElement args,
            out GoCreateArguments parsedArguments,
            out string errorMessage)
        {
            parsedArguments = default;
            errorMessage = string.Empty;
            if (args.ValueKind != JsonValueKind.Object)
            {
                errorMessage = "Operation 'args' must be an object.";
                return false;
            }

            var hasName = false;
            var hasScene = false;
            var hasParent = false;
            string? name = null;
            string? scenePath = null;
            var parentReference = default(UnityObjectReference);
            foreach (var property in args.EnumerateObject())
            {
                if (string.Equals(property.Name, GoOperationPropertyNames.Name, StringComparison.Ordinal))
                {
                    if (!TryReadUniqueRequiredString(property, ref hasName, out name, out errorMessage))
                    {
                        return false;
                    }

                    continue;
                }

                if (string.Equals(property.Name, GoOperationPropertyNames.Scene, StringComparison.Ordinal))
                {
                    if (!TryReadUniqueRequiredString(property, ref hasScene, out scenePath, out errorMessage))
                    {
                        return false;
                    }

                    continue;
                }

                if (string.Equals(property.Name, GoOperationPropertyNames.Parent, StringComparison.Ordinal))
                {
                    if (hasParent)
                    {
                        errorMessage = $"Operation 'args' contains duplicated property: {GoOperationPropertyNames.Parent}.";
                        return false;
                    }

                    if (!UnityObjectReferenceCodec.TryParse(
                        property.Value,
                        "args.parent",
                        out parentReference,
                        out errorMessage))
                    {
                        return false;
                    }

                    hasParent = true;
                    continue;
                }

                errorMessage = $"Operation 'args' contains an unknown property: {property.Name}.";
                return false;
            }

            if (!hasName)
            {
                errorMessage = $"Operation 'args' requires property '{GoOperationPropertyNames.Name}'.";
                return false;
            }

            if (hasScene == hasParent)
            {
                errorMessage =
                    $"Operation 'args' must specify exactly one of '{GoOperationPropertyNames.Scene}' or '{GoOperationPropertyNames.Parent}'.";
                return false;
            }

            parsedArguments = new GoCreateArguments(
                name: name!,
                scenePath: scenePath,
                parentReference: parentReference,
                hasParentReference: hasParent);
            return true;
        }

        /// <summary> Reads one unique required string property. </summary>
        /// <param name="property"> The JSON property. </param>
        /// <param name="hasProperty"> The presence flag that guards against duplicates. </param>
        /// <param name="value"> The parsed property value when successful. </param>
        /// <param name="errorMessage"> The parse error message when parsing fails. </param>
        /// <returns> <see langword="true" /> when the property is valid; otherwise <see langword="false" />. </returns>
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