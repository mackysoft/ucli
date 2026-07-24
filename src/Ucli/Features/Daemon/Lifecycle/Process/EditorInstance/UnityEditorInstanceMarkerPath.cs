using MackySoft.FileSystem;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Process.EditorInstance;

/// <summary> Resolves the Unity <c>Library/EditorInstance.json</c> marker path for one project. </summary>
internal static class UnityEditorInstanceMarkerPath
{
    private const string LibraryDirectoryName = "Library";

    private const string MarkerFileName = "EditorInstance.json";

    /// <summary> Resolves the absolute marker path under the specified Unity project root. </summary>
    /// <param name="unityProjectRoot"> The Unity project root path. </param>
    /// <returns> The marker path. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="unityProjectRoot" /> is empty. </exception>
    public static AbsolutePath Resolve (AbsolutePath unityProjectRoot)
    {
        ArgumentNullException.ThrowIfNull(unityProjectRoot);
        return ContainedPath.Create(
            unityProjectRoot,
            RootRelativePath.Parse(
                $"{LibraryDirectoryName}/{MarkerFileName}")).Target;
    }
}
