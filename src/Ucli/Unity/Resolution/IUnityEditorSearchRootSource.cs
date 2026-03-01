namespace MackySoft.Ucli.UnityProject.Resolution;

/// <summary> Provides Unity editor search-root candidates for one specific platform. </summary>
internal interface IUnityEditorSearchRootSource
{
    /// <summary> Gets a value indicating whether this source supports the current platform. </summary>
    bool IsSupportedCurrentPlatform { get; }

    /// <summary> Appends candidate root directories for Unity editor installations. </summary>
    /// <param name="searchRootSet"> The destination set that stores de-duplicated roots. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="searchRootSet" /> is <see langword="null" />. </exception>
    void AppendSearchRoots (UnityEditorSearchRootSet searchRootSet);
}