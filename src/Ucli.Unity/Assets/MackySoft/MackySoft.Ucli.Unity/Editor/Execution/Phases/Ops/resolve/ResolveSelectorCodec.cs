using System;
using System.Text.Json;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Decodes and validates selector arguments for <c>ucli.resolve</c>. </summary>
    internal static class ResolveSelectorCodec
    {
        private static readonly (string Name, SelectorPropertyKind Kind)[] PropertyDefinitions =
        {
            (ResolveSelectorPropertyNames.GlobalObjectId, SelectorPropertyKind.GlobalObjectId),
            (ResolveSelectorPropertyNames.AssetGuid, SelectorPropertyKind.AssetGuid),
            (ResolveSelectorPropertyNames.AssetPath, SelectorPropertyKind.AssetPath),
            (ResolveSelectorPropertyNames.ProjectAssetPath, SelectorPropertyKind.ProjectAssetPath),
            (ResolveSelectorPropertyNames.Scene, SelectorPropertyKind.Scene),
            (ResolveSelectorPropertyNames.Prefab, SelectorPropertyKind.Prefab),
            (ResolveSelectorPropertyNames.HierarchyPath, SelectorPropertyKind.HierarchyPath),
            (ResolveSelectorPropertyNames.ComponentType, SelectorPropertyKind.ComponentType),
        };

        private enum SelectorPropertyKind
        {
            GlobalObjectId,
            AssetGuid,
            AssetPath,
            ProjectAssetPath,
            Scene,
            Prefab,
            HierarchyPath,
            ComponentType,
        }

        private struct SelectorParseState
        {
            public bool HasGlobalObjectId;
            public bool HasAssetGuid;
            public bool HasAssetPath;
            public bool HasProjectAssetPath;
            public bool HasScenePath;
            public bool HasPrefabPath;
            public bool HasHierarchyPath;
            public bool HasComponentType;
            public string? GlobalObjectId;
            public string? AssetGuid;
            public string? AssetPath;
            public string? ProjectAssetPath;
            public string? ScenePath;
            public string? PrefabPath;
            public string? HierarchyPath;
            public string? ComponentType;
        }

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

            var state = default(SelectorParseState);

            foreach (var property in args.EnumerateObject())
            {
                if (!TryResolvePropertyKind(property.Name, out var propertyKind))
                {
                    errorMessage = $"Operation 'args' contains an unknown property: {property.Name}.";
                    return false;
                }

                if (!TryAssignPropertyValue(
                    propertyKind,
                    property,
                    ref state,
                    out errorMessage))
                {
                    return false;
                }
            }

            if ((state.HasScenePath || state.HasPrefabPath) != state.HasHierarchyPath)
            {
                errorMessage =
                    $"Operation 'args' requires hierarchy selectors to specify either '{ResolveSelectorPropertyNames.Scene}' or '{ResolveSelectorPropertyNames.Prefab}' together with '{ResolveSelectorPropertyNames.HierarchyPath}'.";
                return false;
            }

            if (state.HasScenePath && state.HasPrefabPath)
            {
                errorMessage =
                    $"Operation 'args' must not specify both '{ResolveSelectorPropertyNames.Scene}' and '{ResolveSelectorPropertyNames.Prefab}'.";
                return false;
            }

            if (state.HasComponentType && !(state.HasScenePath || state.HasPrefabPath))
            {
                errorMessage =
                    $"Operation 'args' property '{ResolveSelectorPropertyNames.ComponentType}' requires '{ResolveSelectorPropertyNames.Scene}' or '{ResolveSelectorPropertyNames.Prefab}' with '{ResolveSelectorPropertyNames.HierarchyPath}'.";
                return false;
            }

            var selectorCount = CountSpecifiedSelectorKinds(in state);
            if (selectorCount != 1)
            {
                errorMessage =
                    $"Operation 'args' must specify exactly one selector: '{ResolveSelectorPropertyNames.GlobalObjectId}', '{ResolveSelectorPropertyNames.AssetGuid}', '{ResolveSelectorPropertyNames.AssetPath}', '{ResolveSelectorPropertyNames.ProjectAssetPath}', '{ResolveSelectorPropertyNames.Scene}' + '{ResolveSelectorPropertyNames.HierarchyPath}', or '{ResolveSelectorPropertyNames.Prefab}' + '{ResolveSelectorPropertyNames.HierarchyPath}'.";
                return false;
            }

            if (state.HasGlobalObjectId)
            {
                selector = ResolveSelector.FromGlobalObjectId(state.GlobalObjectId!);
                return true;
            }

            if (state.HasAssetGuid)
            {
                selector = ResolveSelector.FromAssetGuid(state.AssetGuid!);
                return true;
            }

            if (state.HasAssetPath)
            {
                selector = ResolveSelector.FromAssetPath(state.AssetPath!);
                return true;
            }

            if (state.HasProjectAssetPath)
            {
                selector = ResolveSelector.FromProjectAssetPath(state.ProjectAssetPath!);
                return true;
            }

            if (state.HasScenePath)
            {
                selector = ResolveSelector.FromSceneHierarchy(state.ScenePath!, state.HierarchyPath!, state.ComponentType);
                return true;
            }

            selector = ResolveSelector.FromPrefabHierarchy(state.PrefabPath!, state.HierarchyPath!, state.ComponentType);
            return true;
        }

        /// <summary> Resolves selector property kind from one raw property name. </summary>
        /// <param name="propertyName"> The property name. </param>
        /// <param name="kind"> The resolved property kind. </param>
        /// <returns> <see langword="true" /> when property name is supported; otherwise <see langword="false" />. </returns>
        private static bool TryResolvePropertyKind (
            string propertyName,
            out SelectorPropertyKind kind)
        {
            foreach (var definition in PropertyDefinitions)
            {
                if (!string.Equals(definition.Name, propertyName, StringComparison.Ordinal))
                {
                    continue;
                }

                kind = definition.Kind;
                return true;
            }

            kind = default;
            return false;
        }

        /// <summary> Assigns one selector property value while validating duplicate declarations. </summary>
        /// <param name="kind"> The selector property kind. </param>
        /// <param name="property"> The source JSON property. </param>
        /// <param name="state"> The mutable selector parse state. </param>
        /// <param name="errorMessage"> The parse error message when assignment fails. </param>
        /// <returns> <see langword="true" /> when assignment succeeds; otherwise <see langword="false" />. </returns>
        private static bool TryAssignPropertyValue (
            SelectorPropertyKind kind,
            JsonProperty property,
            ref SelectorParseState state,
            out string errorMessage)
        {
            errorMessage = string.Empty;
            switch (kind)
            {
                case SelectorPropertyKind.GlobalObjectId:
                    if (!TryReadUniqueRequiredString(
                        property,
                        ResolveSelectorPropertyNames.GlobalObjectId,
                        ref state.HasGlobalObjectId,
                        out var globalObjectId,
                        out errorMessage))
                    {
                        return false;
                    }

                    state.GlobalObjectId = globalObjectId;
                    return true;
                case SelectorPropertyKind.AssetGuid:
                    if (!TryReadUniqueRequiredString(
                        property,
                        ResolveSelectorPropertyNames.AssetGuid,
                        ref state.HasAssetGuid,
                        out var assetGuid,
                        out errorMessage))
                    {
                        return false;
                    }

                    state.AssetGuid = assetGuid;
                    return true;
                case SelectorPropertyKind.AssetPath:
                    if (!TryReadUniqueRequiredString(
                        property,
                        ResolveSelectorPropertyNames.AssetPath,
                        ref state.HasAssetPath,
                        out var assetPath,
                        out errorMessage))
                    {
                        return false;
                    }

                    state.AssetPath = assetPath;
                    return true;
                case SelectorPropertyKind.ProjectAssetPath:
                    if (!TryReadUniqueRequiredString(
                        property,
                        ResolveSelectorPropertyNames.ProjectAssetPath,
                        ref state.HasProjectAssetPath,
                        out var projectAssetPath,
                        out errorMessage))
                    {
                        return false;
                    }

                    state.ProjectAssetPath = projectAssetPath;
                    return true;
                case SelectorPropertyKind.Scene:
                    if (!TryReadUniqueRequiredString(
                        property,
                        ResolveSelectorPropertyNames.Scene,
                        ref state.HasScenePath,
                        out var scenePath,
                        out errorMessage))
                    {
                        return false;
                    }

                    state.ScenePath = scenePath;
                    return true;
                case SelectorPropertyKind.Prefab:
                    if (!TryReadUniqueRequiredString(
                        property,
                        ResolveSelectorPropertyNames.Prefab,
                        ref state.HasPrefabPath,
                        out var prefabPath,
                        out errorMessage))
                    {
                        return false;
                    }

                    state.PrefabPath = prefabPath;
                    return true;
                case SelectorPropertyKind.HierarchyPath:
                    if (!TryReadUniqueRequiredString(
                        property,
                        ResolveSelectorPropertyNames.HierarchyPath,
                        ref state.HasHierarchyPath,
                        out var hierarchyPath,
                        out errorMessage))
                    {
                        return false;
                    }

                    state.HierarchyPath = hierarchyPath;
                    return true;
                case SelectorPropertyKind.ComponentType:
                    if (!TryReadUniqueRequiredString(
                        property,
                        ResolveSelectorPropertyNames.ComponentType,
                        ref state.HasComponentType,
                        out var componentType,
                        out errorMessage))
                    {
                        return false;
                    }

                    state.ComponentType = componentType;
                    return true;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported selector property kind.");
            }
        }

        /// <summary> Reads one unique required string selector property. </summary>
        /// <param name="property"> The source JSON property. </param>
        /// <param name="propertyName"> The canonical property name used by diagnostics. </param>
        /// <param name="hasProperty"> The property-presence flag for duplicate validation. </param>
        /// <param name="value"> The parsed value when successful. </param>
        /// <param name="errorMessage"> The parse error message when failed. </param>
        /// <returns> <see langword="true" /> when property is unique and valid; otherwise <see langword="false" />. </returns>
        private static bool TryReadUniqueRequiredString (
            JsonProperty property,
            string propertyName,
            ref bool hasProperty,
            out string? value,
            out string errorMessage)
        {
            if (hasProperty)
            {
                value = null;
                errorMessage = $"Operation 'args' contains duplicated property: {propertyName}.";
                return false;
            }

            if (!OperationArgumentValueReader.TryReadRequiredString(property, out value, out errorMessage))
            {
                return false;
            }

            hasProperty = true;
            return true;
        }

        /// <summary> Counts how many selector kinds are specified in one parse state. </summary>
        /// <param name="state"> The selector parse state. </param>
        /// <returns> The number of selector kinds currently specified. </returns>
        private static int CountSpecifiedSelectorKinds (in SelectorParseState state)
        {
            var selectorCount = 0;
            if (state.HasGlobalObjectId)
            {
                selectorCount++;
            }

            if (state.HasAssetGuid)
            {
                selectorCount++;
            }

            if (state.HasAssetPath)
            {
                selectorCount++;
            }

            if (state.HasProjectAssetPath)
            {
                selectorCount++;
            }

            if (state.HasScenePath || state.HasPrefabPath || state.HasHierarchyPath || state.HasComponentType)
            {
                selectorCount++;
            }

            return selectorCount;
        }
    }
}
