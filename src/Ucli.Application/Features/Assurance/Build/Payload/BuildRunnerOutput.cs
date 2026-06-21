namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents the resolved build runner emitted in public output. </summary>
internal sealed record BuildRunnerOutput (
    string Kind,
    string? Method,
    BuildRunnerInvocationOutput Invocation);
