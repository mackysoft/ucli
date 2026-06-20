namespace MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

/// <summary> Represents process environment entries requested by an executeMethod runner invocation. </summary>
internal sealed record ResolvedBuildRunnerEnvironment (
    IReadOnlyList<string> Variables,
    IReadOnlyList<string> Secrets)
{
    /// <summary> Gets an empty runner environment request. </summary>
    public static ResolvedBuildRunnerEnvironment Empty { get; } = new(
        Array.Empty<string>(),
        Array.Empty<string>());
}
