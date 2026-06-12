using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Metadata;

/// <summary> Represents lifecycle snapshots captured around BuildPipeline execution. </summary>
internal sealed record BuildRunLifecycleMetadata (
    IpcBuildLifecycleSnapshot Before,
    IpcBuildLifecycleSnapshot After);
