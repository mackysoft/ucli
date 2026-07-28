using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Creates a root GameObject in a scene. </summary>
public sealed record GoCreateInSceneArgs : GoCreateArgs
{
    /// <summary> Initializes a scene-root GameObject creation contract. </summary>
    /// <param name="name"> The name assigned to the created GameObject. </param>
    /// <param name="scene"> The scene that receives the new root GameObject. </param>
    [JsonConstructor]
    public GoCreateInSceneArgs (
        string name,
        SceneAssetPath scene)
        : base(name)
    {
        Scene = ContractArgumentGuard.RequireNotNull(scene, nameof(scene));
    }

    /// <summary> Gets the scene that receives the new root GameObject. </summary>
    [JsonInclude]
    [JsonRequired]
    [Description("Scene asset path that receives the new root GameObject.")]
    [UcliAssetExists(UcliOperationAssetKind.Scene)]
    public SceneAssetPath Scene { get; private init; }
}
