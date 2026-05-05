namespace MackySoft.Ucli.Application.Shared.Unity.Resolution;

/// <summary> Resolves one Unity editor version string from preferred input and project metadata. </summary>
internal interface IUnityVersionResolver
{
    /// <summary> Resolves the effective Unity version from one preferred value and project files. </summary>
    /// <param name="projectPath"> The Unity project root path. </param>
    /// <param name="preferredUnityVersion"> The preferred Unity version value. </param>
    /// <returns> The Unity-version resolution result. </returns>
    UnityVersionResolutionResult Resolve (
        string projectPath,
        string? preferredUnityVersion);
}
