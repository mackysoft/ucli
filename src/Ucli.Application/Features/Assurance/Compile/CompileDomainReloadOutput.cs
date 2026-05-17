namespace MackySoft.Ucli.Application.Features.Assurance.Compile;

/// <summary> Represents domain reload evidence grouped under <c>payload.compile.domainReload</c>. </summary>
internal sealed record CompileDomainReloadOutput (
    bool ReloadRequired,
    bool ReloadObserved,
    string GenerationBefore,
    string GenerationAfter,
    bool Settled);
