namespace MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

/// <summary> Represents non-secret executeMethod runner invocation inputs resolved from a build profile. </summary>
internal sealed record ResolvedBuildRunnerInvocation (
    IReadOnlyDictionary<string, string> Arguments,
    ResolvedBuildRunnerEnvironment Environment)
{
    /// <summary> Gets the empty runner invocation used by buildPipeline profiles. </summary>
    public static ResolvedBuildRunnerInvocation Empty { get; } = new(
        new Dictionary<string, string>(StringComparer.Ordinal),
        new ResolvedBuildRunnerEnvironment(
            Array.Empty<string>(),
            Array.Empty<string>()));
}
