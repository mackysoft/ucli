namespace MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

/// <summary> Represents the build runner resolved from a build profile. </summary>
internal sealed record ResolvedBuildRunner (
    BuildProfileRunnerKind Kind,
    string? Method,
    ResolvedBuildRunnerInvocation Invocation);
