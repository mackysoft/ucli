namespace MackySoft.Ucli.UnityProject.Resolution;

/// <summary> Provides candidate root directories used for Unity editor installation discovery. </summary>
internal interface IUnityEditorSearchRootProvider
{
    /// <summary> Gets candidate root directory paths for Unity editor discovery. </summary>
    /// <returns> The candidate root directory paths. </returns>
    IReadOnlyList<string> GetSearchRoots ();
}