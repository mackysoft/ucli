namespace MackySoft.Ucli.Application.Features.Assurance.Build.Metadata;

/// <summary> Represents the runner invocation summary persisted in <c>build.json</c>. </summary>
internal sealed record BuildRunRunnerInvocationMetadata (
    IReadOnlyDictionary<string, string> Arguments,
    BuildRunRunnerInvocationEnvironmentMetadata Environment);
