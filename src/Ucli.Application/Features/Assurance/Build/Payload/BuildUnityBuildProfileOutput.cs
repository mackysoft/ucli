using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents the Unity Build Profile asset resolved for a build run. </summary>
internal sealed record BuildUnityBuildProfileOutput (
    string Path,
    string Digest,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IpcUnityBuildProfileApplyAudit? ApplyAudit);
