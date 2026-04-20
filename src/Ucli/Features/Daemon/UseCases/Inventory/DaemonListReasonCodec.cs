namespace MackySoft.Ucli.Features.Daemon.UseCases.Inventory;

/// <summary> Defines daemon-list reason literals. </summary>
internal static class DaemonListReasonCodec
{
    /// <summary> Gets the reason literal for stale daemon sessions. </summary>
    public const string StaleSession = "staleSession";

    /// <summary> Gets the reason literal for invalid daemon sessions. </summary>
    public const string InvalidSession = "invalidSession";

    /// <summary> Gets the reason literal for daemon probe timeouts. </summary>
    public const string ProbeTimeout = "probeTimeout";

    /// <summary> Gets the reason literal for unexpected daemon probe failures. </summary>
    public const string ProbeFailed = "probeFailed";
}