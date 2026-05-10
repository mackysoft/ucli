using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Daemon;

/// <summary> Converts daemon startup-blocked process policies to canonical contract literals. </summary>
public static class DaemonStartupBlockedProcessPolicyCodec
{
    private static readonly (DaemonStartupBlockedProcessPolicy Value, string Literal)[] Mappings =
    {
        (DaemonStartupBlockedProcessPolicy.Auto, DaemonStartupBlockedProcessPolicyValues.Auto),
        (DaemonStartupBlockedProcessPolicy.Keep, DaemonStartupBlockedProcessPolicyValues.Keep),
        (DaemonStartupBlockedProcessPolicy.Terminate, DaemonStartupBlockedProcessPolicyValues.Terminate),
    };

    /// <summary> Converts one startup-blocked process policy enum value to a canonical contract literal. </summary>
    /// <param name="policy"> The startup-blocked process policy enum value. </param>
    /// <returns> The canonical contract literal. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="policy" /> is unsupported. </exception>
    public static string ToValue (DaemonStartupBlockedProcessPolicy policy)
    {
        return LiteralCodecUtilities.ToValue(
            policy,
            Mappings,
            nameof(policy),
            "Unsupported daemon startup-blocked process policy.");
    }

    /// <summary> Tries to parse one raw startup-blocked process policy literal. </summary>
    /// <param name="value"> The optional raw literal. </param>
    /// <param name="policy"> The parsed process policy enum value when parsing succeeds; otherwise default value. </param>
    /// <returns> <see langword="true" /> when one supported literal is parsed; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        out DaemonStartupBlockedProcessPolicy policy)
    {
        return LiteralCodecUtilities.TryParseTrimmed(
            value,
            Mappings,
            StringComparison.Ordinal,
            out policy);
    }
}
