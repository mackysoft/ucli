using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Metadata;

/// <summary> Represents Unity Build Profile input evidence persisted in <c>build.json</c>. </summary>
internal sealed record BuildRunUnityBuildProfileInputMetadata (
    string Path,
    string Digest,
    IpcUnityBuildProfileApplyAudit ApplyAudit);
