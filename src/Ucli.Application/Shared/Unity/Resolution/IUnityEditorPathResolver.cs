namespace MackySoft.Ucli.Application.Shared.Unity.Resolution;

/// <summary> Resolves Unity editor executable paths for one target Unity version. </summary>
internal interface IUnityEditorPathResolver
{
    /// <summary> Resolves one Unity editor executable path. </summary>
    /// <param name="unityVersion"> The target Unity version. </param>
    /// <param name="preferredUnityEditorPath"> The preferred editor path value. </param>
    /// <returns> The editor-path resolution result. </returns>
    UnityEditorPathResolutionResult Resolve (
        string unityVersion,
        string? preferredUnityEditorPath);
}
