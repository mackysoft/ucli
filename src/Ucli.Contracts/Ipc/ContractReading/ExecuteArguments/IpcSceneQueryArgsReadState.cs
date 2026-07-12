using System.Text.Json;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

internal sealed class IpcSceneQueryArgsReadState
{
    private readonly string propertyRoot;

    private readonly string contextLabel;

    private readonly bool allowScene;

    private bool hasScene;

    private bool hasPathPrefix;

    private bool hasComponentType;

    private string? scenePath;

    private string? pathPrefix;

    private string? componentType;

    public IpcSceneQueryArgsReadState (
        string propertyRoot,
        string contextLabel,
        bool allowScene)
    {
        this.propertyRoot = propertyRoot;
        this.contextLabel = contextLabel;
        this.allowScene = allowScene;
    }

    public bool TryRead (
        JsonProperty property,
        out string errorMessage)
    {
        return property.Name switch
        {
            "scene" => TryReadScene(property, out errorMessage),
            "pathPrefix" => TryReadStringProperty(property, ref hasPathPrefix, ref pathPrefix, out errorMessage),
            "componentType" => TryReadStringProperty(property, ref hasComponentType, ref componentType, out errorMessage),
            _ => Fail($"contains an unknown property: {property.Name}.", out errorMessage),
        };
    }

    public bool TryBuild (
        bool requireScene,
        out IpcSceneQueryArgsContract contract,
        out string errorMessage)
    {
        if (requireScene && !hasScene)
        {
            contract = default!;
            errorMessage = $"{contextLabel} '{propertyRoot}.scene' is required.";
            return false;
        }

        contract = new IpcSceneQueryArgsContract(scenePath, pathPrefix, componentType);
        errorMessage = string.Empty;
        return true;
    }

    private bool TryReadScene (
        JsonProperty property,
        out string errorMessage)
    {
        if (!allowScene)
        {
            return Fail("cannot contain property 'scene'.", out errorMessage);
        }

        return TryReadStringProperty(property, ref hasScene, ref scenePath, out errorMessage);
    }

    private bool TryReadStringProperty (
        JsonProperty property,
        ref bool hasValue,
        ref string? value,
        out string errorMessage)
    {
        if (hasValue)
        {
            errorMessage = $"{contextLabel} '{propertyRoot}.{property.Name}' is duplicated.";
            return false;
        }

        if (!TryReadStringValue(property, out value, out errorMessage))
        {
            return false;
        }

        hasValue = true;
        return true;
    }

    private bool TryReadStringValue (
        JsonProperty property,
        out string? value,
        out string errorMessage)
    {
        value = null;
        if (property.Value.ValueKind != JsonValueKind.String)
        {
            errorMessage = $"{contextLabel} '{propertyRoot}.{property.Name}' must be a string.";
            return false;
        }

        value = property.Value.GetString();
        return TryValidateStringValue(property.Name, value, out errorMessage);
    }

    private bool TryValidateStringValue (
        string propertyName,
        string? value,
        out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errorMessage = $"{contextLabel} '{propertyRoot}.{propertyName}' must not be empty.";
            return false;
        }

        if (StringValueValidator.HasOuterWhitespace(value))
        {
            errorMessage = $"{contextLabel} '{propertyRoot}.{propertyName}' must not contain leading or trailing whitespace.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    private bool Fail (
        string message,
        out string errorMessage)
    {
        errorMessage = $"{contextLabel} '{propertyRoot}' {message}";
        return false;
    }
}
