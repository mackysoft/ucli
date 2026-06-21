namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents the build runner invocation summary emitted in public output. </summary>
internal sealed record BuildRunnerInvocationOutput (
    IReadOnlyDictionary<string, string> Arguments,
    BuildRunnerInvocationEnvironmentOutput Environment);
