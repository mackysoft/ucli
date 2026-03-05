using System;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Parses and validates argument contracts for scene-domain operations. </summary>
    internal static class SceneOperationArgumentsCodec
    {
        /// <summary> Parses scene-path arguments used by <c>ucli.scene.open</c> and <c>ucli.scene.save</c>. </summary>
        /// <param name="args"> The operation arguments element. </param>
        /// <param name="scenePath"> The parsed scene path when successful. </param>
        /// <param name="errorMessage"> The parse error message when failed. </param>
        /// <returns> <see langword="true" /> when parsing succeeds; otherwise <see langword="false" />. </returns>
        public static bool TryParsePathArguments (
            JsonElement args,
            out string scenePath,
            out string errorMessage)
        {
            scenePath = string.Empty;
            errorMessage = string.Empty;
            if (args.ValueKind != JsonValueKind.Object)
            {
                errorMessage = "Operation 'args' must be an object.";
                return false;
            }

            var hasPath = false;
            foreach (var property in args.EnumerateObject())
            {
                if (string.Equals(property.Name, "path", StringComparison.Ordinal))
                {
                    if (hasPath)
                    {
                        errorMessage = "Operation 'args' contains duplicated property: path.";
                        return false;
                    }

                    if (!TryReadRequiredString(property, out scenePath, out errorMessage))
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
                errorMessage = "Operation 'args' requires property 'path'.";
                return false;
            }

            return true;
        }

        /// <summary> Parses scene-tree arguments used by <c>ucli.scene.tree</c>. </summary>
        /// <param name="args"> The operation arguments element. </param>
        /// <param name="scenePath"> The parsed scene path when successful. </param>
        /// <param name="depth"> The parsed depth. <see langword="null" /> means unlimited depth. </param>
        /// <param name="errorMessage"> The parse error message when failed. </param>
        /// <returns> <see langword="true" /> when parsing succeeds; otherwise <see langword="false" />. </returns>
        public static bool TryParseTreeArguments (
            JsonElement args,
            out string scenePath,
            out int? depth,
            out string errorMessage)
        {
            scenePath = string.Empty;
            depth = null;
            errorMessage = string.Empty;
            if (args.ValueKind != JsonValueKind.Object)
            {
                errorMessage = "Operation 'args' must be an object.";
                return false;
            }

            var hasPath = false;
            var hasDepth = false;
            foreach (var property in args.EnumerateObject())
            {
                if (string.Equals(property.Name, "path", StringComparison.Ordinal))
                {
                    if (hasPath)
                    {
                        errorMessage = "Operation 'args' contains duplicated property: path.";
                        return false;
                    }

                    if (!TryReadRequiredString(property, out scenePath, out errorMessage))
                    {
                        return false;
                    }

                    hasPath = true;
                    continue;
                }

                if (string.Equals(property.Name, "depth", StringComparison.Ordinal))
                {
                    if (hasDepth)
                    {
                        errorMessage = "Operation 'args' contains duplicated property: depth.";
                        return false;
                    }

                    if (!TryReadDepth(property, out depth, out errorMessage))
                    {
                        return false;
                    }

                    hasDepth = true;
                    continue;
                }

                errorMessage = $"Operation 'args' contains an unknown property: {property.Name}.";
                return false;
            }

            if (!hasPath)
            {
                errorMessage = "Operation 'args' requires property 'path'.";
                return false;
            }

            return true;
        }

        /// <summary> Reads one required strict string property. </summary>
        /// <param name="property"> The source JSON property. </param>
        /// <param name="value"> The parsed value when successful. </param>
        /// <param name="errorMessage"> The parse error message when failed. </param>
        /// <returns> <see langword="true" /> when property is valid; otherwise <see langword="false" />. </returns>
        private static bool TryReadRequiredString (
            JsonProperty property,
            out string value,
            out string errorMessage)
        {
            value = string.Empty;
            if (property.Value.ValueKind != JsonValueKind.String)
            {
                errorMessage = $"Operation 'args.{property.Name}' must be a string.";
                return false;
            }

            var parsedValue = property.Value.GetString();
            if (string.IsNullOrWhiteSpace(parsedValue))
            {
                errorMessage = $"Operation 'args.{property.Name}' must not be empty or whitespace.";
                return false;
            }

            if (StringValueValidator.HasOuterWhitespace(parsedValue))
            {
                errorMessage = $"Operation 'args.{property.Name}' must not contain leading or trailing whitespace.";
                return false;
            }

            value = parsedValue;
            errorMessage = string.Empty;
            return true;
        }

        /// <summary> Reads one scene-tree depth property. </summary>
        /// <param name="property"> The source JSON property. </param>
        /// <param name="depth"> The parsed depth value. <see langword="null" /> means unlimited depth. </param>
        /// <param name="errorMessage"> The parse error message when failed. </param>
        /// <returns> <see langword="true" /> when property is valid; otherwise <see langword="false" />. </returns>
        private static bool TryReadDepth (
            JsonProperty property,
            out int? depth,
            out string errorMessage)
        {
            depth = null;
            if (property.Value.ValueKind == JsonValueKind.Null)
            {
                errorMessage = string.Empty;
                return true;
            }

            if (property.Value.ValueKind != JsonValueKind.Number)
            {
                errorMessage = $"Operation 'args.{property.Name}' must be an integer or null.";
                return false;
            }

            if (!property.Value.TryGetInt32(out var parsedDepth))
            {
                errorMessage = $"Operation 'args.{property.Name}' must be an integer or null.";
                return false;
            }

            if (parsedDepth < 0)
            {
                errorMessage = $"Operation 'args.{property.Name}' must be greater than or equal to 0.";
                return false;
            }

            depth = parsedDepth;
            errorMessage = string.Empty;
            return true;
        }
    }
}
