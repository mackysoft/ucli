using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one dirty build input item. </summary>
public sealed record IpcBuildDirtyStateItem
{
    /// <summary> Initializes one dirty build input item whose kind matches its canonical project path. </summary>
    /// <param name="Kind"> The item kind derived from <paramref name="Path" />. </param>
    /// <param name="Path"> The canonical path of the dirty project item. </param>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="Kind" /> is not a defined contract literal. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="Path" /> is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when a known <paramref name="Kind" /> contradicts <paramref name="Path" />. </exception>
    [JsonConstructor]
    public IpcBuildDirtyStateItem (
        IpcBuildDirtyStateItemKind Kind,
        ProjectMutationAuditPath Path)
    {
        if (!TextVocabulary.IsDefined(Kind))
        {
            throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Dirty-state item kind must be specified.");
        }

        var path = Path ?? throw new ArgumentNullException(nameof(Path));
        if (Kind != ClassifyPath(path))
        {
            throw new ArgumentException(
                "Dirty-state item kind must match the canonical classification of Path.",
                nameof(Kind));
        }

        this.Kind = Kind;
        this.Path = path;
    }

    public IpcBuildDirtyStateItemKind Kind { get; }

    public ProjectMutationAuditPath Path { get; }

    /// <summary> Classifies a canonical project mutation audit path into a known dirty-state item kind. </summary>
    /// <param name="path"> The canonical project mutation audit path. </param>
    /// <returns> The known kind derived from the path root and file extension. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="path" /> is <see langword="null" />. </exception>
    internal static IpcBuildDirtyStateItemKind ClassifyPath (ProjectMutationAuditPath path)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        if (path.Value.StartsWith("ProjectSettings/", StringComparison.Ordinal))
        {
            return IpcBuildDirtyStateItemKind.ProjectSettings;
        }

        if (path.Value.EndsWith(UnityAssetPathContract.SceneAssetExtension, StringComparison.OrdinalIgnoreCase))
        {
            return IpcBuildDirtyStateItemKind.Scene;
        }

        if (path.Value.EndsWith(UnityAssetPathContract.PrefabAssetExtension, StringComparison.OrdinalIgnoreCase))
        {
            return IpcBuildDirtyStateItemKind.Prefab;
        }

        return IpcBuildDirtyStateItemKind.Asset;
    }
}
