namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Represents one established validity scope for a ready claim. </summary>
internal abstract record ReadyClaimValidityOutput
{
    private protected ReadyClaimValidityOutput ()
    {
    }

    /// <summary> Creates validity limited to the completed readiness probe. </summary>
    public static ReadyClaimValidityOutput ProbeOnly ()
    {
        return new ProbeOnlyReadyClaimValidityOutput();
    }

    /// <summary> Creates validity bound to the resolved daemon session. </summary>
    public static ReadyClaimValidityOutput SessionBound (bool guaranteesReusableSession)
    {
        return new SessionBoundReadyClaimValidityOutput(guaranteesReusableSession);
    }
}

/// <summary> Represents validity limited to the completed readiness probe. </summary>
internal sealed record ProbeOnlyReadyClaimValidityOutput : ReadyClaimValidityOutput
{
    public ProbeOnlyReadyClaimValidityOutput ()
    {
    }
}

/// <summary> Represents validity bound to the resolved daemon session. </summary>
internal sealed record SessionBoundReadyClaimValidityOutput : ReadyClaimValidityOutput
{
    public SessionBoundReadyClaimValidityOutput (bool guaranteesReusableSession)
    {
        GuaranteesReusableSession = guaranteesReusableSession;
    }

    public bool GuaranteesReusableSession { get; }
}
