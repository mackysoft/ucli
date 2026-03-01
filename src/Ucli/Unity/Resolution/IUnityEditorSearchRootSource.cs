namespace MackySoft.Ucli.UnityProject.Resolution;

/// <summary> Provides Unity editor search-root candidates for one specific platform. </summary>
internal interface IUnityEditorSearchRootSource
{
    /// <summary> Gets a value indicating whether this source supports the current platform. </summary>
    bool IsSupportedCurrentPlatform { get; }

    /// <summary> Gets candidate root directories for Unity editor installations. </summary>
    /// <returns> Candidate root directories. </returns>
    IReadOnlyList<string> GetSearchRoots ();
}