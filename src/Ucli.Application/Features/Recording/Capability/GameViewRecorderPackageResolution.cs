namespace MackySoft.Ucli.Application.Features.Recording.Capability;

/// <summary>Describes whether the resolved Unity package graph contains Recorder.</summary>
internal enum GameViewRecorderPackageResolutionState
{
    Missing,
    Resolved,
    Indeterminate,
}

/// <summary>Contains one Recorder package observation from the resolved Unity dependency graph.</summary>
internal sealed record GameViewRecorderPackageResolution
{
    private GameViewRecorderPackageResolution (
        GameViewRecorderPackageResolutionState state,
        string? version,
        string? diagnostic)
    {
        State = state;
        Version = version;
        Diagnostic = diagnostic;
    }

    public GameViewRecorderPackageResolutionState State { get; }

    public string? Version { get; }

    public string? Diagnostic { get; }

    public static GameViewRecorderPackageResolution Missing () =>
        new(GameViewRecorderPackageResolutionState.Missing, version: null, diagnostic: null);

    public static GameViewRecorderPackageResolution Resolved (string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        return new GameViewRecorderPackageResolution(
            GameViewRecorderPackageResolutionState.Resolved,
            version,
            diagnostic: null);
    }

    public static GameViewRecorderPackageResolution Indeterminate (string diagnostic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(diagnostic);
        return new GameViewRecorderPackageResolution(
            GameViewRecorderPackageResolutionState.Indeterminate,
            version: null,
            diagnostic);
    }
}
