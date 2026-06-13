namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents one opaque generation snapshot. </summary>
internal sealed record BuildGenerationSnapshotOutput (
    string CompileGeneration,
    string DomainReloadGeneration,
    string AssetRefreshGeneration);
