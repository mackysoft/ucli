using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("Scene query operation result.")]
public sealed record SceneQueryResult
{
    /// <summary> Initializes a scene query result with an owned snapshot of the matches. </summary>
    /// <param name="scene"> The non-null scene asset path that was queried. </param>
    /// <param name="matches"> The non-null matches, none of which may be null. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="scene" /> or <paramref name="matches" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="matches" /> contains a <see langword="null" /> item. </exception>
    [JsonConstructor]
    public SceneQueryResult (
        SceneAssetPath scene,
        IReadOnlyList<SceneQueryMatch> matches)
    {
        Scene = scene ?? throw new ArgumentNullException(nameof(scene));
        Matches = ContractArgumentGuard.RequireItems(matches, nameof(matches));
    }

    [UcliRequired]
    [UcliDescription("Scene asset path that was queried.")]
    public SceneAssetPath Scene { get; }

    [UcliRequired]
    [UcliDescription("Matched scene objects or components.")]
    public IReadOnlyList<SceneQueryMatch> Matches { get; }
}
