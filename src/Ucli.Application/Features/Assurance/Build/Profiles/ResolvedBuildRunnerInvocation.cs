namespace MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

/// <summary> Represents non-secret executeMethod runner invocation inputs resolved from a build profile. </summary>
internal sealed record ResolvedBuildRunnerInvocation (
    IReadOnlyDictionary<string, string> Arguments,
    IReadOnlyList<string> EnvironmentNames)
{
    /// <summary> Gets the empty runner invocation used by buildPipeline profiles. </summary>
    public static ResolvedBuildRunnerInvocation Empty { get; } = new(
        new Dictionary<string, string>(StringComparer.Ordinal),
        Array.Empty<string>());
}
