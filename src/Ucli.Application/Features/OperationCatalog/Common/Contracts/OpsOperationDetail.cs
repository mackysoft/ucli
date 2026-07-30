using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Application.Features.OperationCatalog.Common.Contracts;

/// <summary> Represents one detailed operation payload entry. </summary>
internal sealed class OpsOperationDetail
{
    /// <summary> Initializes a new instance of the <see cref="OpsOperationDetail" /> class. </summary>
    public OpsOperationDetail (
        string name,
        UcliOperationKind kind,
        OperationPolicy policy,
        UcliOperationPlayModeSupport playModeSupport,
        Sha256Digest descriptorDigest,
        string description,
        UcliOperationJsonContract argsContract,
        UcliOperationJsonContract? resultContract,
        UcliOperationVerdictContract? verdictContract,
        UcliOperationAssuranceContract assurance,
        UcliOperationCodeContract? codeContract)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(descriptorDigest);
        ArgumentNullException.ThrowIfNull(assurance);
        if (!TextVocabulary.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Operation kind must be defined.");
        }

        if (!TextVocabulary.IsDefined(policy))
        {
            throw new ArgumentOutOfRangeException(nameof(policy), policy, "Operation policy must be defined.");
        }

        if (!TextVocabulary.IsDefined(playModeSupport))
        {
            throw new ArgumentOutOfRangeException(
                nameof(playModeSupport),
                playModeSupport,
                "Operation Play Mode support must be defined.");
        }

        if (!argsContract.IsDefined)
        {
            throw new ArgumentException(
                "An operation detail requires a defined arguments contract.",
                nameof(argsContract));
        }

        if (resultContract.HasValue && !resultContract.Value.IsDefined)
        {
            throw new ArgumentException(
                "An operation detail result contract must be defined when present.",
                nameof(resultContract));
        }

        if (verdictContract != null && resultContract == null)
        {
            throw new ArgumentException(
                "A judging operation detail must contain its result contract.",
                nameof(resultContract));
        }

        if (verdictContract != null && kind != UcliOperationKind.Query)
        {
            throw new ArgumentException(
                "Only a query operation detail may declare a verdict contract.",
                nameof(verdictContract));
        }

        Name = name;
        Kind = kind;
        Policy = policy;
        PlayModeSupport = playModeSupport;
        DescriptorDigest = descriptorDigest;
        Description = description;
        ArgsContract = argsContract;
        ResultContract = resultContract;
        VerdictContract = verdictContract;
        Assurance = assurance;
        CodeContract = codeContract;
    }

    /// <summary> Gets the operation name. </summary>
    public string Name { get; }

    /// <summary> Gets the operation kind. </summary>
    public UcliOperationKind Kind { get; }

    /// <summary> Gets the operation policy. </summary>
    public OperationPolicy Policy { get; }

    /// <summary> Gets the Play Mode support for public raw operation execution. </summary>
    public UcliOperationPlayModeSupport PlayModeSupport { get; }

    /// <summary> Gets the RFC 8785 digest of the semantic operation descriptor. </summary>
    public Sha256Digest DescriptorDigest { get; }

    /// <summary> Gets the operation purpose description. </summary>
    public string Description { get; }

    /// <summary> Gets the generated operation argument contract. </summary>
    public UcliOperationJsonContract ArgsContract { get; }

    /// <summary> Gets the generated operation result contract, or <see langword="null" /> when no result is emitted. </summary>
    public UcliOperationJsonContract? ResultContract { get; }

    /// <summary> Gets the optional condition judged from a successful Call result. </summary>
    public UcliOperationVerdictContract? VerdictContract { get; }

    /// <summary> Gets machine-readable assurance metadata. </summary>
    public UcliOperationAssuranceContract Assurance { get; }

    /// <summary> Gets optional source-facing code metadata. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UcliOperationCodeContract? CodeContract { get; }

}
