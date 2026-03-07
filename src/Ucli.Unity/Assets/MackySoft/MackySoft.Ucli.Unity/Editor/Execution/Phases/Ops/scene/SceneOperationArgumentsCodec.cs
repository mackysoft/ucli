using System;
using System.Text.Json;

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

                    if (!OperationArgumentValueReader.TryReadRequiredString(property, out scenePath, out errorMessage))
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

                    if (!OperationArgumentValueReader.TryReadRequiredString(property, out scenePath, out errorMessage))
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

            if (!hasPath)
            {
                errorMessage = "Operation 'args' requires property 'path'.";
                return false;
            }

            return true;
        }
    }
}