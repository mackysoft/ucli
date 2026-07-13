namespace MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;

/// <summary> Represents domain reload evidence grouped under <c>payload.compile.domainReload</c>. </summary>
internal sealed record CompileDomainReloadOutput (
    bool ReloadRequired,
    bool ReloadObserved,
    long? GenerationBefore,
    long? GenerationAfter,
    bool Settled);
