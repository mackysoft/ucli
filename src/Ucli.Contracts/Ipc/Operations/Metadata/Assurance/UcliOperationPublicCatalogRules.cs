namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines machine-readable inclusion rules for the public raw operation catalog. </summary>
public static class UcliOperationPublicCatalogRules
{
    /// <summary> Gets a value indicating whether side effects contain a public raw-catalog exclusion marker. </summary>
    public static bool HasPublicRawCatalogExclusionMarker (IReadOnlyList<string>? sideEffects)
    {
        if (sideEffects == null)
        {
            return false;
        }

        for (var i = 0; i < sideEffects.Count; i++)
        {
            if (string.Equals(sideEffects[i], UcliOperationSideEffectValues.ArbitrarySourceExecution, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
