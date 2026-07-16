using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Parses operation-argument references that can point to Unity objects through aliases or selectors. </summary>
    internal static class UnityObjectReferenceCodec
    {
        private const string AliasPropertyName = "var";

        /// <summary> Parses one Unity-object reference from a JSON element. </summary>
        /// <param name="element"> The JSON element to parse. </param>
        /// <param name="propertyPath"> The logical property path used in diagnostics. </param>
        /// <param name="reference"> The parsed reference when successful. </param>
        /// <param name="errorMessage"> The parse error message when parsing fails. </param>
        /// <returns> <see langword="true" /> when parsing succeeds; otherwise <see langword="false" />. </returns>
        public static bool TryParse (
            JsonElement element,
            string propertyPath,
            OperationAliasReferenceMap aliasReferences,
            [NotNullWhen(true)] out UnityObjectReference? reference,
            out string errorMessage)
        {
            reference = null;
            errorMessage = string.Empty;
            if (aliasReferences == null)
            {
                throw new ArgumentNullException(nameof(aliasReferences));
            }

            if (element.ValueKind != JsonValueKind.Object)
            {
                errorMessage = $"Operation '{propertyPath}' must be an object.";
                return false;
            }

            var hasAlias = false;
            var hasSelectorProperty = false;
            string? alias = null;
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, AliasPropertyName, StringComparison.Ordinal))
                {
                    if (!TryReadUniqueAlias(property, propertyPath, ref hasAlias, out alias, out errorMessage))
                    {
                        return false;
                    }

                    continue;
                }

                if (IsSelectorProperty(property.Name))
                {
                    hasSelectorProperty = true;
                    continue;
                }

                errorMessage = $"Operation '{propertyPath}' contains an unknown property: {property.Name}.";
                return false;
            }

            if (hasAlias)
            {
                if (hasSelectorProperty)
                {
                    errorMessage =
                        $"Operation '{propertyPath}' must specify exactly one reference form: '{{ \"var\": string }}' or selector properties.";
                    return false;
                }

                try
                {
                    reference = UnityObjectReference.FromAlias(aliasReferences.Resolve(new UcliPlanAlias(alias!)));
                }
                catch (ArgumentException)
                {
                    errorMessage = $"Operation '{propertyPath}.{AliasPropertyName}' must be a valid request-local alias.";
                    return false;
                }

                return true;
            }

            if (!hasSelectorProperty)
            {
                errorMessage =
                    $"Operation '{propertyPath}' must specify exactly one reference form: '{{ \"var\": string }}' or selector properties.";
                return false;
            }

            if (!ResolveSelectorCodec.TryParse(element, out var selector, out errorMessage))
            {
                errorMessage = RewriteArgsPath(errorMessage, propertyPath);
                return false;
            }

            reference = UnityObjectReference.FromSelector(selector);
            return true;
        }

        /// <summary> Determines whether one raw property name belongs to selector syntax. </summary>
        /// <param name="propertyName"> The raw property name. </param>
        /// <returns> <see langword="true" /> when the property name belongs to selector syntax; otherwise <see langword="false" />. </returns>
        private static bool IsSelectorProperty (string propertyName)
        {
            return string.Equals(propertyName, IpcResolveSelectorPropertyNames.GlobalObjectId, StringComparison.Ordinal)
                || string.Equals(propertyName, IpcResolveSelectorPropertyNames.AssetGuid, StringComparison.Ordinal)
                || string.Equals(propertyName, IpcResolveSelectorPropertyNames.AssetPath, StringComparison.Ordinal)
                || string.Equals(propertyName, IpcResolveSelectorPropertyNames.ProjectAssetPath, StringComparison.Ordinal)
                || string.Equals(propertyName, IpcResolveSelectorPropertyNames.Scene, StringComparison.Ordinal)
                || string.Equals(propertyName, IpcResolveSelectorPropertyNames.Prefab, StringComparison.Ordinal)
                || string.Equals(propertyName, IpcResolveSelectorPropertyNames.HierarchyPath, StringComparison.Ordinal)
                || string.Equals(propertyName, IpcResolveSelectorPropertyNames.ComponentType, StringComparison.Ordinal);
        }

        /// <summary> Reads one unique alias property. </summary>
        /// <param name="property"> The JSON property. </param>
        /// <param name="propertyPath"> The logical property path used in diagnostics. </param>
        /// <param name="hasAlias"> The alias-presence flag. </param>
        /// <param name="alias"> The alias value when successful. </param>
        /// <param name="errorMessage"> The parse error message when parsing fails. </param>
        /// <returns> <see langword="true" /> when the alias property is valid; otherwise <see langword="false" />. </returns>
        private static bool TryReadUniqueAlias (
            JsonProperty property,
            string propertyPath,
            ref bool hasAlias,
            out string? alias,
            out string errorMessage)
        {
            alias = null;
            if (hasAlias)
            {
                errorMessage = $"Operation '{propertyPath}' contains duplicated property: {AliasPropertyName}.";
                return false;
            }

            if (!OperationArgumentValueReader.TryReadRequiredString(
                property.Value,
                $"{propertyPath}.{AliasPropertyName}",
                expectedTypeDescription: "a string",
                out alias,
                out errorMessage))
            {
                return false;
            }

            hasAlias = true;
            return true;
        }

        /// <summary> Rewrites <c>ResolveSelectorCodec</c> diagnostics to the specified property path. </summary>
        /// <param name="errorMessage"> The original error message. </param>
        /// <param name="propertyPath"> The target property path. </param>
        /// <returns> The rewritten error message. </returns>
        private static string RewriteArgsPath (
            string errorMessage,
            string propertyPath)
        {
            return errorMessage.Replace("Operation 'args'", $"Operation '{propertyPath}'");
        }
    }
}
