using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

/// <summary> Represents one operation metadata entry resolved from the catalog provider. </summary>
internal sealed record UcliOperationDescriptor
{
    public UcliOperationDescriptor (
        string Name,
        UcliOperationKind Kind,
        OperationPolicy Policy,
        string ArgsSchemaJson,
        Sha256Digest DescriptorDigest,
        UcliOperationVerdictContract? VerdictContract,
        string? ResultSchemaJson,
        UcliOperationExposure Exposure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(ArgsSchemaJson);
        ArgumentNullException.ThrowIfNull(DescriptorDigest);
        if (!TextVocabulary.IsDefined(Kind))
        {
            throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Operation kind must be defined.");
        }

        if (!TextVocabulary.IsDefined(Policy))
        {
            throw new ArgumentOutOfRangeException(nameof(Policy), Policy, "Operation policy must be defined.");
        }

        if (!TextVocabulary.IsDefined(Exposure))
        {
            throw new ArgumentOutOfRangeException(nameof(Exposure), Exposure, "Operation exposure must be defined.");
        }

        if (ResultSchemaJson != null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(ResultSchemaJson);
        }

        if (VerdictContract != null && ResultSchemaJson == null)
        {
            throw new ArgumentException(
                "A judging operation descriptor must contain its result schema.",
                nameof(ResultSchemaJson));
        }

        if (VerdictContract != null && Kind != UcliOperationKind.Query)
        {
            throw new ArgumentException(
                "Only a query operation descriptor may declare a verdict contract.",
                nameof(VerdictContract));
        }

        this.Name = Name;
        this.Kind = Kind;
        this.Policy = Policy;
        this.ArgsSchemaJson = ArgsSchemaJson;
        this.DescriptorDigest = DescriptorDigest;
        this.VerdictContract = VerdictContract;
        this.ResultSchemaJson = ResultSchemaJson;
        this.Exposure = Exposure;
    }

    /// <summary> Gets the unique operation name. </summary>
    public string Name { get; }

    /// <summary> Gets the operation kind. </summary>
    public UcliOperationKind Kind { get; }

    /// <summary> Gets the required operation policy. </summary>
    public OperationPolicy Policy { get; }

    /// <summary> Gets the operation argument schema as JSON text. </summary>
    public string ArgsSchemaJson { get; }

    /// <summary> Gets the RFC 8785 digest of the semantic operation descriptor. </summary>
    public Sha256Digest DescriptorDigest { get; }

    /// <summary> Gets the condition judged from a successful Call result, or <see langword="null" />. </summary>
    public UcliOperationVerdictContract? VerdictContract { get; }

    /// <summary> Gets the operation result schema as JSON text, or <see langword="null" /> when no result is emitted. </summary>
    public string? ResultSchemaJson { get; }

    /// <summary> Gets whether the operation is reachable from public request surfaces. </summary>
    public UcliOperationExposure Exposure { get; }
}
