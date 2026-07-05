namespace MackySoft.Ucli.TestSupport;

internal sealed class StubUnityEditorPathResolver : IUnityEditorPathResolver
{
    public const string DefaultUnityEditorPath = "/Applications/Unity.app/Contents/MacOS/Unity";

    private readonly UnityEditorPathResolutionResult result;

    public StubUnityEditorPathResolver ()
        : this(DefaultUnityEditorPath)
    {
    }

    public StubUnityEditorPathResolver (string unityEditorPath)
        : this(UnityEditorPathResolutionResult.Success(unityEditorPath))
    {
    }

    public StubUnityEditorPathResolver (UnityEditorPathResolutionResult result)
    {
        this.result = result ?? throw new ArgumentNullException(nameof(result));
    }

    public UnityEditorPathResolutionResult Resolve (
        string unityVersion,
        string? preferredUnityEditorPath)
    {
        return result;
    }
}
