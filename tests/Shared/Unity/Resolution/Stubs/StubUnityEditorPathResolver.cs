namespace MackySoft.Ucli.TestSupport;

internal sealed class StubUnityEditorPathResolver : IUnityEditorPathResolver
{
    public static string DefaultUnityEditorPath { get; } =
        Path.Combine(ProjectPathTestValues.WorkspaceRoot, "Unity", "Editor");

    private readonly UnityEditorPathResolutionResult result;

    public StubUnityEditorPathResolver ()
        : this(DefaultUnityEditorPath)
    {
    }

    public StubUnityEditorPathResolver (string unityEditorPath)
        : this(UnityEditorPathResolutionResult.Success(
            MackySoft.FileSystem.AbsolutePath.Parse(Path.GetFullPath(unityEditorPath))))
    {
    }

    public StubUnityEditorPathResolver (UnityEditorPathResolutionResult result)
    {
        this.result = result ?? throw new ArgumentNullException(nameof(result));
    }

    public UnityEditorPathResolutionResult Resolve (
        string unityVersion,
        AbsolutePath? preferredUnityEditorPath)
    {
        return result;
    }
}
