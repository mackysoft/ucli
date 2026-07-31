namespace MackySoft.Ucli.Hosting.Cli.Play.Contracts;

/// <summary>
/// Represents a Play Mode transition failure before action-specific execution context exists.
/// </summary>
internal sealed record EmptyPlayTransitionErrorCommandPayload
    : PlayTransitionErrorCommandPayload;
