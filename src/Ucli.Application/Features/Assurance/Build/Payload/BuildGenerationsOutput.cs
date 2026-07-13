using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents generation validity for build artifacts. </summary>
internal sealed record BuildGenerationsOutput (
    IpcUnityGenerationSnapshot? Before,
    IpcUnityGenerationSnapshot? After,
    IpcUnityGenerationSnapshot? ValidFor);
