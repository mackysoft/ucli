namespace MackySoft.Ucli.UnityProject.Resolution;

/// <summary> Provides platform-aware path comparer policies used for search-root de-duplication. </summary>
internal sealed class UnityPathComparerProvider : IUnityPathComparerProvider
{
    /// <summary> Gets the comparer used for path de-duplication on the current platform. </summary>
    /// <returns> The comparer used for path de-duplication. </returns>
    public StringComparer GetComparer ()
    {
        return OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
    }
}