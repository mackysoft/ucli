using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Represents one operation descriptor whose persisted field contracts have been validated. </summary>
internal sealed record ValidatedOpsCatalogEntry
{
    public ValidatedOpsCatalogEntry (
        string name,
        UcliOperationKind kind,
        OperationPolicy policy,
        string description,
        Sha256Digest describeKey,
        Sha256Digest describeHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        if (!TextVocabulary.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Operation kind must have a canonical contract literal.");
        }

        if (!TextVocabulary.IsDefined(policy))
        {
            throw new ArgumentOutOfRangeException(nameof(policy), policy, "Operation policy must have a canonical contract literal.");
        }

        Name = name;
        Kind = kind;
        Policy = policy;
        Description = description;
        DescribeKey = describeKey ?? throw new ArgumentNullException(nameof(describeKey));
        DescribeHash = describeHash ?? throw new ArgumentNullException(nameof(describeHash));
    }

    public string Name { get; }

    public UcliOperationKind Kind { get; }

    public OperationPolicy Policy { get; }

    public string Description { get; }

    public Sha256Digest DescribeKey { get; }

    public Sha256Digest DescribeHash { get; }
}
