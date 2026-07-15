using MackySoft.Ucli.Contracts.Assurance.Build;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents the resolved build runner emitted in public output. </summary>
internal sealed record BuildRunnerOutput (
    BuildRunnerKind Kind,
    string? Method,
    BuildRunnerInvocationOutput Invocation);
