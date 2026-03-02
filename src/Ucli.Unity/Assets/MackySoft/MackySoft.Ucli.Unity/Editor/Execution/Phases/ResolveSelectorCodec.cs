using System;
using System.Collections.Generic;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Decodes and validates selector arguments for <c>ucli.resolve</c>. </summary>
    internal static class ResolveSelectorCodec
    {
        private static readonly HashSet<string> AllowedPropertyNames = new HashSet<string>(StringComparer.Ordinal)
        {
            ResolveSelectorPropertyNames.GlobalObjectId,
            ResolveSelectorPropertyNames.AssetGuid,
            ResolveSelectorPropertyNames.AssetPath,
            ResolveSelectorPropertyNames.Scene,
            ResolveSelectorPropertyNames.HierarchyPath,
        };

        /// <summary> Parses one selector from operation arguments. </summary>
        /// <param name="args"> The operation arguments element. </param>
        /// <param name="selector"> The parsed selector when successful. </param>
        /// <param name="errorMessage"> The parse error message when failed. </param>
        /// <returns> <see langword="true" /> when selector was parsed successfully; otherwise <see langword="false" />. </returns>
        public static bool TryParse (
            JsonElement args,
            out ResolveSelector selector,
            out string errorMessage)
        {
            selector = default;
            errorMessage = string.Empty;
            if (args.ValueKind != JsonValueKind.Object)
            {
                errorMessage = "Operation 'args' must be an object.";
                return false;
            }

            var hasGlobalObjectId = false;
            var hasAssetGuid = false;
            var hasAssetPath = false;
            var hasScenePath = false;
            var hasHierarchyPath = false;
            string? globalObjectId = null;
            string? assetGuid = null;
            string? assetPath = null;
            string? scenePath = null;
            string? hierarchyPath = null;

            foreach (var property in args.EnumerateObject())
            {
                if (!AllowedPropertyNames.Contains(property.Name))
                {
                    errorMessage = $"Operation 'args' contains an unknown property: {property.Name}.";
                    return false;
                }

                switch (property.Name)
                {
                    case ResolveSelectorPropertyNames.GlobalObjectId:
                        if (hasGlobalObjectId)
                        {
                            errorMessage = $"Operation 'args' contains duplicated property: {ResolveSelectorPropertyNames.GlobalObjectId}.";
                            return false;
                        }

                        if (!TryReadRequiredString(property, out globalObjectId, out errorMessage))
                        {
                            return false;
                        }

                        hasGlobalObjectId = true;
                        break;

                    case ResolveSelectorPropertyNames.AssetGuid:
                        if (hasAssetGuid)
                        {
                            errorMessage = $"Operation 'args' contains duplicated property: {ResolveSelectorPropertyNames.AssetGuid}.";
                            return false;
                        }

                        if (!TryReadRequiredString(property, out assetGuid, out errorMessage))
                        {
                            return false;
                        }

                        hasAssetGuid = true;
                        break;

                    case ResolveSelectorPropertyNames.AssetPath:
                        if (hasAssetPath)
                        {
                            errorMessage = $"Operation 'args' contains duplicated property: {ResolveSelectorPropertyNames.AssetPath}.";
                            return false;
                        }

                        if (!TryReadRequiredString(property, out assetPath, out errorMessage))
                        {
                            return false;
                        }

                        hasAssetPath = true;
                        break;

                    case ResolveSelectorPropertyNames.Scene:
                        if (hasScenePath)
                        {
                            errorMessage = $"Operation 'args' contains duplicated property: {ResolveSelectorPropertyNames.Scene}.";
                            return false;
                        }

                        if (!TryReadRequiredString(property, out scenePath, out errorMessage))
                        {
                            return false;
                        }

                        hasScenePath = true;
                        break;

                    case ResolveSelectorPropertyNames.HierarchyPath:
                        if (hasHierarchyPath)
                        {
                            errorMessage = $"Operation 'args' contains duplicated property: {ResolveSelectorPropertyNames.HierarchyPath}.";
                            return false;
                        }

                        if (!TryReadRequiredString(property, out hierarchyPath, out errorMessage))
                        {
                            return false;
                        }

                        hasHierarchyPath = true;
                        break;
                }
            }

            if (hasScenePath != hasHierarchyPath)
            {
                errorMessage = $"Operation 'args' requires both '{ResolveSelectorPropertyNames.Scene}' and '{ResolveSelectorPropertyNames.HierarchyPath}' when one is specified.";
                return false;
            }

            var selectorCount = 0;
            if (hasGlobalObjectId)
            {
                selectorCount++;
            }

            if (hasAssetGuid)
            {
                selectorCount++;
            }

            if (hasAssetPath)
            {
                selectorCount++;
            }

            if (hasScenePath)
            {
                selectorCount++;
            }

            if (selectorCount != 1)
            {
                errorMessage =
                    $"Operation 'args' must specify exactly one selector: '{ResolveSelectorPropertyNames.GlobalObjectId}', '{ResolveSelectorPropertyNames.AssetGuid}', '{ResolveSelectorPropertyNames.AssetPath}', or '{ResolveSelectorPropertyNames.Scene}' + '{ResolveSelectorPropertyNames.HierarchyPath}'.";
                return false;
            }

            if (hasGlobalObjectId)
            {
                selector = ResolveSelector.FromGlobalObjectId(globalObjectId!);
                return true;
            }

            if (hasAssetGuid)
            {
                selector = ResolveSelector.FromAssetGuid(assetGuid!);
                return true;
            }

            if (hasAssetPath)
            {
                selector = ResolveSelector.FromAssetPath(assetPath!);
                return true;
            }

            selector = ResolveSelector.FromSceneHierarchy(scenePath!, hierarchyPath!);
            return true;
        }

        /// <summary> Reads one required strict string property from selector arguments. </summary>
        /// <param name="property"> The JSON property to parse. </param>
        /// <param name="value"> The parsed string when successful. </param>
        /// <param name="errorMessage"> The parse error message when failed. </param>
        /// <returns> <see langword="true" /> when property is a valid strict string; otherwise <see langword="false" />. </returns>
        private static bool TryReadRequiredString (
            JsonProperty property,
            out string? value,
            out string errorMessage)
        {
            value = null;
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
    }
}
