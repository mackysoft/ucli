using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> References a Unity project asset outside <c>Assets/</c>. </summary>
[Description("Project-scoped Unity asset-path reference.")]
public sealed record ProjectAssetPathReferenceArgs : AssetReferenceArgs, ResolveSelectorArgs
{
    /// <summary> Initializes a new project-scoped Unity asset-path reference. </summary>
    /// <param name="projectAssetPath"> The project-scoped asset path. </param>
    [JsonConstructor]
    public ProjectAssetPathReferenceArgs (ProjectSettingsAssetPath projectAssetPath)
    {
        ProjectAssetPath = ContractArgumentGuard.RequireNotNull(projectAssetPath, nameof(projectAssetPath));
    }

    /// <summary> Gets the project-scoped asset path. </summary>
    [JsonInclude]
    [JsonRequired]
    [Description("Project-scoped asset path selector.")]
    [UcliAssetExists(UcliOperationAssetKind.ProjectSettings)]
    public ProjectSettingsAssetPath ProjectAssetPath { get; private init; }
}
