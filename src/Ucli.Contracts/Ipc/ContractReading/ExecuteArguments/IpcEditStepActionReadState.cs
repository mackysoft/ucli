using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary> Holds raw public edit action fields after property-level reading. </summary>
internal readonly record struct IpcEditStepActionReadState (
    IpcEditStepContract.ActionKind ActionKind,
    string? Target,
    string? Alias,
    string? Type,
    string? Name,
    string? Path,
    string? Parent,
    string? TargetAssetPath,
    IReadOnlyList<string>? PropertyPaths,
    bool HasValues,
    JsonElement Values)
{
    public IpcEditStepContract.EditAction ToAction ()
    {
        return new IpcEditStepContract.EditAction(
            Kind: ActionKind,
            Target: Target,
            Alias: Alias,
            Type: Type,
            Name: Name,
            Path: Path,
            Parent: Parent,
            TargetAssetPath: TargetAssetPath,
            PropertyPaths: PropertyPaths,
            Values: HasValues ? Values : default);
    }

    public bool HasRequiredString (string propertyName)
    {
        return propertyName switch
        {
            "target" => Target is not null,
            "type" => Type is not null,
            "name" => Name is not null,
            "path" => Path is not null,
            "parent" => Parent is not null,
            "targetAssetPath" => TargetAssetPath is not null,
            _ => false,
        };
    }
}
