namespace MackySoft.Tests;

/// <summary> Provides helper methods for creating minimal UnityProject directory structures in tests. </summary>
internal static class UnityProjectTestFactory
{
    private const string DefaultProjectVersionContent = "m_EditorVersion: 6000.1.4f1";

    private const string UcliUnityPluginMarkerContent =
        """
        {
          "pluginId": "com.mackysoft.ucli.unity",
          "protocolVersion": 1
        }
        """;

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

    /// <summary> Writes the uCLI Unity plugin marker under a minimal Unity project. </summary>
    /// <param name="scope"> The root test-directory scope. </param>
    /// <param name="projectRelativePath"> The relative project path under the scope root. </param>
    /// <returns> A task that resolves to the written marker file path. </returns>
    internal static Task<string> WriteUcliUnityPluginMarkerAsync (
        TestDirectoryScope scope,
        string projectRelativePath)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRelativePath);

        return scope.WriteFileAsync(
            Path.Combine(
                projectRelativePath,
                "Assets",
                "MackySoft",
                "MackySoft.Ucli.Unity",
                "ucli-plugin.json"),
            UcliUnityPluginMarkerContent);
    }
}
