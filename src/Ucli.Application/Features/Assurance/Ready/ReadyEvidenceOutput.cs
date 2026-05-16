namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Represents one evidence entry in a ready claim. </summary>
internal sealed record ReadyEvidenceOutput (
    string Kind,
    object? Data = null,
    string? EvidenceRef = null);
