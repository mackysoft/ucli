namespace MackySoft.Ucli.UnityIntegration.Resolution;

/// <summary> Provides Unity editor search-root candidates for one specific platform. </summary>
internal interface IUnityEditorSearchRootSource
{
    /// <summary> Gets a value indicating whether this source supports the current platform. </summary>
    bool IsSupportedCurrentPlatform { get; }

    /// <summary> Appends candidate root directories for Unity editor installations. </summary>
    /// <param name="searchRootBuilder"> The destination builder that stores de-duplicated roots. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="searchRootBuilder" /> is <see langword="null" />. </exception>
    void AppendSearchRoots (UnityEditorSearchRootBuilder searchRootBuilder);
}