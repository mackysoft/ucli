namespace MackySoft.Ucli.Application.Features.Assurance.Build.Metadata;

/// <summary> Represents runner environment names persisted in <c>build.json</c>. </summary>
internal sealed record BuildRunRunnerInvocationEnvironmentMetadata (
    IReadOnlyList<string> Variables,
    IReadOnlyList<string> Secrets);
