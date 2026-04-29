using System;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Provides canonical helpers for daemon-log category values. </summary>
public static class IpcDaemonLogsCategoryCodec
{
    /// <summary> Gets the category literal that disables category filtering. </summary>
    public const string All = "all";

    /// <summary> Determines whether one optional category value represents <see cref="All" />. </summary>
    /// <param name="value"> The optional category literal. </param>
    /// <returns> <see langword="true" /> when <paramref name="value" /> maps to <see cref="All" />; otherwise <see langword="false" />. </returns>
    public static bool IsAll (string? value)
    {
        if (!StringValueNormalizer.TryTrimToNonEmpty(value, out var normalizedValue))
        {
            return false;
        }

        return string.Equals(normalizedValue, All, StringComparison.OrdinalIgnoreCase);
    }
}
