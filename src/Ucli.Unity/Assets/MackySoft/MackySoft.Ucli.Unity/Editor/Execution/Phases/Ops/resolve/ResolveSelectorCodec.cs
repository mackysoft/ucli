using System;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Decodes and validates selector arguments for <c>ucli.resolve</c>. </summary>
    internal static class ResolveSelectorCodec
    {
        private static readonly (string Name, SelectorPropertyKind Kind)[] PropertyDefinitions =
        {
            (IpcResolveSelectorPropertyNames.GlobalObjectId, SelectorPropertyKind.GlobalObjectId),
            (IpcResolveSelectorPropertyNames.AssetGuid, SelectorPropertyKind.AssetGuid),
            (IpcResolveSelectorPropertyNames.AssetPath, SelectorPropertyKind.AssetPath),
            (IpcResolveSelectorPropertyNames.ProjectAssetPath, SelectorPropertyKind.ProjectAssetPath),
            (IpcResolveSelectorPropertyNames.Scene, SelectorPropertyKind.Scene),
            (IpcResolveSelectorPropertyNames.Prefab, SelectorPropertyKind.Prefab),
            (IpcResolveSelectorPropertyNames.HierarchyPath, SelectorPropertyKind.HierarchyPath),
            (IpcResolveSelectorPropertyNames.ComponentType, SelectorPropertyKind.ComponentType),
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
                    $"Operation 'args' requires hierarchy selectors to specify either '{IpcResolveSelectorPropertyNames.Scene}' or '{IpcResolveSelectorPropertyNames.Prefab}' together with '{IpcResolveSelectorPropertyNames.HierarchyPath}'.";
                return false;
            }

            if (state.HasScenePath && state.HasPrefabPath)
            {
                errorMessage =
                    $"Operation 'args' must not specify both '{IpcResolveSelectorPropertyNames.Scene}' and '{IpcResolveSelectorPropertyNames.Prefab}'.";
                return false;
            }

            if (state.HasComponentType && !(state.HasScenePath || state.HasPrefabPath))
            {
                errorMessage =
                    $"Operation 'args' property '{IpcResolveSelectorPropertyNames.ComponentType}' requires '{IpcResolveSelectorPropertyNames.Scene}' or '{IpcResolveSelectorPropertyNames.Prefab}' with '{IpcResolveSelectorPropertyNames.HierarchyPath}'.";
                return false;
            }

            var selectorCount = CountSpecifiedSelectorKinds(in state);
            if (selectorCount != 1)
            {
                errorMessage =
                    $"Operation 'args' must specify exactly one selector: '{IpcResolveSelectorPropertyNames.GlobalObjectId}', '{IpcResolveSelectorPropertyNames.AssetGuid}', '{IpcResolveSelectorPropertyNames.AssetPath}', '{IpcResolveSelectorPropertyNames.ProjectAssetPath}', '{IpcResolveSelectorPropertyNames.Scene}' + '{IpcResolveSelectorPropertyNames.HierarchyPath}', or '{IpcResolveSelectorPropertyNames.Prefab}' + '{IpcResolveSelectorPropertyNames.HierarchyPath}'.";
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
                        IpcResolveSelectorPropertyNames.GlobalObjectId,
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
                        IpcResolveSelectorPropertyNames.AssetGuid,
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
                        IpcResolveSelectorPropertyNames.AssetPath,
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
                        IpcResolveSelectorPropertyNames.ProjectAssetPath,
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
                        IpcResolveSelectorPropertyNames.Scene,
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
                        IpcResolveSelectorPropertyNames.Prefab,
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
                        IpcResolveSelectorPropertyNames.HierarchyPath,
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
                        IpcResolveSelectorPropertyNames.ComponentType,
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
