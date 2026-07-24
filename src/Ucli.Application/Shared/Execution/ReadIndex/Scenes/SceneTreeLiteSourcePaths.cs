using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;

/// <summary> Carries the logical and guarded filesystem paths for one Assets scene source. </summary>
/// <remarks>
/// The filesystem values prove current-platform lexical normalization and containment only.
/// Existence, node kind, symbolic-link behavior, and file contents are observed by the I/O operation that needs them.
/// </remarks>
internal sealed class SceneTreeLiteSourcePaths
{
    private SceneTreeLiteSourcePaths (
        SceneAssetPath sceneAssetPath,
        ContainedPath sceneFilePath,
        ContainedPath metaFilePath)
    {
        SceneAssetPath = sceneAssetPath;
        SceneFilePath = sceneFilePath;
        MetaFilePath = metaFilePath;
    }

    /// <summary> Gets the Unity scene-asset path used by read-index contracts. </summary>
    public SceneAssetPath SceneAssetPath { get; }

    /// <summary> Gets the scene file path lexically contained by the Unity project root. </summary>
    public ContainedPath SceneFilePath { get; }

    /// <summary> Gets the companion meta-file path lexically contained by the Unity project root. </summary>
    public ContainedPath MetaFilePath { get; }

    /// <summary> Adapts one guarded Unity scene-asset path to its lexically contained current-platform filesystem paths. </summary>
    /// <param name="unityProjectRoot"> The guarded absolute boundary for both source files. </param>
    /// <param name="sceneAssetPath"> The guarded Unity scene-asset path whose companion meta-file path is derived. </param>
    /// <returns> Matching logical, scene-file, and meta-file paths below <paramref name="unityProjectRoot" />. </returns>
    /// <exception cref="ArgumentNullException"> Either argument is <see langword="null" />. </exception>
    /// <exception cref="PathValidationException">
    /// The Unity asset path or its companion meta-file path is not a valid root-relative path on the current platform.
    /// </exception>
    public static SceneTreeLiteSourcePaths Create (
        AbsolutePath unityProjectRoot,
        SceneAssetPath sceneAssetPath)
    {
        ArgumentNullException.ThrowIfNull(unityProjectRoot);
        ArgumentNullException.ThrowIfNull(sceneAssetPath);

        var sceneRelativePath = RootRelativePath.Parse(sceneAssetPath.Value);
        var metaRelativePath = RootRelativePath.Parse(
            sceneRelativePath.Value + UnityAssetPathContract.MetaFileExtension);
        return new SceneTreeLiteSourcePaths(
            sceneAssetPath,
            ContainedPath.Create(unityProjectRoot, sceneRelativePath),
            ContainedPath.Create(unityProjectRoot, metaRelativePath));
    }
}
