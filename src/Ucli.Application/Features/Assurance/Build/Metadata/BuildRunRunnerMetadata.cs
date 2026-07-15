using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Metadata;

/// <summary> Represents the resolved runner metadata persisted in <c>build.json</c>. </summary>
internal sealed record BuildRunRunnerMetadata (
    BuildRunnerKind Kind,
    string? Method,
    BuildRunRunnerInvocationMetadata Invocation,
    IpcBuildOutputLayout? OutputLayout);
