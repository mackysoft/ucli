namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Represents validity semantics for one ready claim. </summary>
internal sealed record ReadyClaimValidityOutput (
    string Kind,
    bool GuaranteesReusableSession);
