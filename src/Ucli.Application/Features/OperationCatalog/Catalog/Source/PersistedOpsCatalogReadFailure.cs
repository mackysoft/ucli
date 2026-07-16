namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;

/// <summary> Represents one classified persisted ops-catalog read failure. </summary>
internal sealed record PersistedOpsCatalogReadFailure
{
    /// <summary> Initializes a new instance of the <see cref="PersistedOpsCatalogReadFailure" /> class. </summary>
    /// <param name="kind"> The policy-facing failure classification. </param>
    /// <param name="errorCode"> The machine-readable failure code. </param>
    /// <param name="message"> The user-facing failure message. </param>
    public PersistedOpsCatalogReadFailure (
        PersistedOpsCatalogReadFailureKind kind,
        UcliCode errorCode,
        string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Kind = kind;
        ErrorCode = errorCode ?? throw new ArgumentNullException(nameof(errorCode));
        Message = message;
    }

    /// <summary> Gets the policy-facing failure classification. </summary>
    public PersistedOpsCatalogReadFailureKind Kind { get; }

    /// <summary> Gets the machine-readable failure code. </summary>
    public UcliCode ErrorCode { get; }

    /// <summary> Gets the user-facing failure message. </summary>
    public string Message { get; }
}
