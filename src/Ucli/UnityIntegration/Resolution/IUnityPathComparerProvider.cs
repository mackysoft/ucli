namespace MackySoft.Ucli.UnityIntegration.Resolution;

/// <summary> Provides path comparer policies used for search-root de-duplication. </summary>
internal interface IUnityPathComparerProvider
{
    /// <summary> Gets the comparer used for path de-duplication on the current platform. </summary>
    /// <returns> The comparer used for path de-duplication. </returns>
    StringComparer GetComparer ();
}