using System;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc.Validation;

/// <summary> Provides strict readers for public <c>ucli.scene.query</c> argument objects. </summary>
internal static class IpcSceneQueryArgsContractReader
{
    /// <summary>
    /// Parses one edit-local scene-query argument object.
    /// </summary>
    /// <param name="argsElement"> The source JSON argument object. </param>
    /// <param name="contract"> The parsed argument contract when parsing succeeds. </param>
    /// <param name="errorMessage"> The validation error message when parsing fails. </param>
    /// <returns> <see langword="true" /> when the argument object matches the edit-local scene-query contract; otherwise <see langword="false" />. </returns>
    public static bool TryReadForEditSelection (
        JsonElement argsElement,
        out IpcSceneQueryArgsContract contract,
        out string errorMessage)
    {
        return TryRead(
            argsElement,
            propertyRoot: "step.select.from.args",
            contextLabel: "Edit step property",
            allowScene: false,
            requireScene: false,
            out contract,
            out errorMessage);
    }

    /// <summary>
    /// Parses one public <c>ucli.scene.query</c> operation argument object.
    /// </summary>
    /// <param name="argsElement"> The source JSON argument object. </param>
    /// <param name="contract"> The parsed argument contract when parsing succeeds. </param>
    /// <param name="errorMessage"> The validation error message when parsing fails. </param>
    /// <returns> <see langword="true" /> when the argument object matches the public operation contract; otherwise <see langword="false" />. </returns>
    public static bool TryReadForOperation (
        JsonElement argsElement,
        out IpcSceneQueryArgsContract contract,
        out string errorMessage)
    {
        return TryRead(
            argsElement,
            propertyRoot: "args",
            contextLabel: "Operation",
            allowScene: true,
            requireScene: true,
            out contract,
            out errorMessage);
    }

    private static bool TryRead (
        JsonElement argsElement,
        string propertyRoot,
        string contextLabel,
        bool allowScene,
        bool requireScene,
        out IpcSceneQueryArgsContract contract,
        out string errorMessage)
    {
        contract = default!;
        errorMessage = string.Empty;
        if (argsElement.ValueKind != JsonValueKind.Object)
        {
            errorMessage = $"{contextLabel} '{propertyRoot}' must be an object.";
            return false;
        }

        var hasScene = false;
        var hasPathPrefix = false;
        var hasComponentType = false;
        string? scenePath = null;
        string? pathPrefix = null;
        string? componentType = null;
        foreach (var property in argsElement.EnumerateObject())
        {
            if (string.Equals(property.Name, "scene", StringComparison.Ordinal))
            {
                if (!allowScene)
                {
                    errorMessage = $"{contextLabel} '{propertyRoot}' cannot contain property 'scene'.";
                    return false;
                }

                if (!TryReadUniqueStringProperty(
                    property,
                    propertyRoot,
                    contextLabel,
                    ref hasScene,
                    out scenePath,
                    out errorMessage))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(property.Name, "pathPrefix", StringComparison.Ordinal))
            {
                if (!TryReadUniqueStringProperty(
                    property,
                    propertyRoot,
                    contextLabel,
                    ref hasPathPrefix,
                    out pathPrefix,
                    out errorMessage))
                {
                    return false;
                }

                continue;
            }

            if (string.Equals(property.Name, "componentType", StringComparison.Ordinal))
            {
                if (!TryReadUniqueStringProperty(
                    property,
                    propertyRoot,
                    contextLabel,
                    ref hasComponentType,
                    out componentType,
                    out errorMessage))
                {
                    return false;
                }

                continue;
            }

            errorMessage = $"{contextLabel} '{propertyRoot}' contains an unknown property: {property.Name}.";
            return false;
        }

        if (requireScene && !hasScene)
        {
            errorMessage = $"{contextLabel} '{propertyRoot}.scene' is required.";
            return false;
        }

        contract = new IpcSceneQueryArgsContract(scenePath, pathPrefix, componentType);
        errorMessage = string.Empty;
        return true;
    }

    private static bool TryReadUniqueStringProperty (
        JsonProperty property,
        string propertyRoot,
        string contextLabel,
        ref bool hasValue,
        out string? value,
        out string errorMessage)
    {
        value = null;
        if (hasValue)
        {
            errorMessage = $"{contextLabel} '{propertyRoot}.{property.Name}' is duplicated.";
            return false;
        }

        if (property.Value.ValueKind != JsonValueKind.String)
        {
            errorMessage = $"{contextLabel} '{propertyRoot}.{property.Name}' must be a string.";
            return false;
        }

        value = property.Value.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            errorMessage = $"{contextLabel} '{propertyRoot}.{property.Name}' must not be empty.";
            return false;
        }

        if (StringValueValidator.HasOuterWhitespace(value))
        {
            errorMessage = $"{contextLabel} '{propertyRoot}.{property.Name}' must not contain leading or trailing whitespace.";
            return false;
        }

        hasValue = true;
        errorMessage = string.Empty;
        return true;
    }
}