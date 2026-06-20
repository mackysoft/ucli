namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents opaque Unity generation values captured at one point in time. </summary>
/// <param name="CompileGeneration"> The opaque compile generation. </param>
/// <param name="DomainReloadGeneration"> The opaque domain-reload generation. </param>
/// <param name="AssetRefreshGeneration"> The opaque asset-refresh generation. </param>
public sealed record IpcBuildGenerationSnapshot (
    string? CompileGeneration,
    string? DomainReloadGeneration,
    string? AssetRefreshGeneration);
