namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents generation validity for build artifacts. </summary>
internal sealed record BuildGenerationsOutput (
    BuildGenerationSnapshotOutput Before,
    BuildGenerationSnapshotOutput After,
    BuildGenerationSnapshotOutput ValidFor);
