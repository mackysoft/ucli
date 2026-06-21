namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents runner environment names emitted in public output. </summary>
internal sealed record BuildRunnerInvocationEnvironmentOutput (
    IReadOnlyList<string> Variables,
    IReadOnlyList<string> Secrets);
