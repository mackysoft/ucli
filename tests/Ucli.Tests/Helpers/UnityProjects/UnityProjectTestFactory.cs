namespace MackySoft.Tests;

/// <summary> Provides helper methods for creating minimal UnityProject directory structures in tests. </summary>
internal static class UnityProjectTestFactory
{
    private const string DefaultProjectVersionContent = "m_EditorVersion: 6000.1.4f1";

    /// <summary> Creates a minimal valid UnityProject structure with <c>ProjectVersion.txt</c>. </summary>
    /// <param name="scope"> The root test-directory scope. </param>
    /// <param name="projectRelativePath"> The relative project path under the scope root. </param>
    /// <param name="projectVersionContent"> Optional <c>ProjectVersion.txt</c> contents. </param>
    /// <returns> The absolute UnityProject root path. </returns>
    internal static string CreateMinimalUnityProject (
        TestDirectoryScope scope,
        string projectRelativePath,
        string projectVersionContent = DefaultProjectVersionContent)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRelativePath);

        scope.CreateDirectory(projectRelativePath);
        scope.CreateDirectory(Path.Combine(projectRelativePath, "ProjectSettings"));
        scope.WriteFile(
            Path.Combine(projectRelativePath, "ProjectSettings", "ProjectVersion.txt"),
            projectVersionContent);

        return scope.GetPath(projectRelativePath);
    }
}
