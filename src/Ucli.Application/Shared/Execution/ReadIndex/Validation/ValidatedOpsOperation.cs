using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Represents one operation whose catalog contract has been validated and projected into typed values. </summary>
internal sealed class ValidatedOpsOperation
{
    internal ValidatedOpsOperation (
        IndexOpEntryJsonContract contract,
        UcliOperationExposure exposure)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentException.ThrowIfNullOrWhiteSpace(contract.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(contract.Description);
        if (!contract.ArgsContract.HasValue
            || !contract.ArgsContract.Value.IsDefined)
        {
            throw new ArgumentException("Operation args contract must be defined.", nameof(contract));
        }
        ArgumentNullException.ThrowIfNull(contract.Assurance);
        if (!contract.Kind.HasValue || !TextVocabulary.IsDefined(contract.Kind.Value))
        {
            throw new ArgumentException("Operation kind must have a contract value.", nameof(contract));
        }

        if (!contract.Policy.HasValue || !TextVocabulary.IsDefined(contract.Policy.Value))
        {
            throw new ArgumentException("Operation policy must have a contract value.", nameof(contract));
        }

        if (!TextVocabulary.IsDefined(exposure))
        {
            throw new ArgumentOutOfRangeException(nameof(exposure), exposure, "Operation exposure must have a contract literal.");
        }

        if (!contract.PlayModeSupport.HasValue
            || !TextVocabulary.IsDefined(contract.PlayModeSupport.Value))
        {
            throw new ArgumentException("Operation Play Mode support must have a contract value.", nameof(contract));
        }

        Name = contract.Name;
        Kind = contract.Kind.Value;
        Policy = contract.Policy.Value;
        Exposure = exposure;
        PlayModeSupport = contract.PlayModeSupport.Value;
        Description = contract.Description;
        DescriptorDigest = contract.DescriptorDigest
            ?? throw new ArgumentException("Operation descriptor digest must be defined.", nameof(contract));
        ArgsContract = contract.ArgsContract.Value;
        ResultContract = contract.ResultContract;
        VerdictContract = contract.VerdictContract;
        Assurance = contract.Assurance;
        CodeContract = contract.CodeContract;
    }

    /// <summary> Gets the operation name. </summary>
    public string Name { get; }

    /// <summary> Gets the operation kind. </summary>
    public UcliOperationKind Kind { get; }

    /// <summary> Gets the operation policy. </summary>
    public OperationPolicy Policy { get; }

    /// <summary> Gets the operation exposure. </summary>
    public UcliOperationExposure Exposure { get; }

    /// <summary> Gets the Play Mode support contract. </summary>
    public UcliOperationPlayModeSupport PlayModeSupport { get; }

    /// <summary> Gets the operation purpose description. </summary>
    public string Description { get; }

    /// <summary> Gets the RFC 8785 digest of the semantic operation descriptor. </summary>
    public Sha256Digest DescriptorDigest { get; }

    /// <summary> Gets the generated operation argument contract. </summary>
    public UcliOperationJsonContract ArgsContract { get; }

    /// <summary> Gets the generated operation result contract, or <see langword="null" /> when no result is emitted. </summary>
    public UcliOperationJsonContract? ResultContract { get; }

    /// <summary> Gets the optional condition judged from a successful Call result. </summary>
    public UcliOperationVerdictContract? VerdictContract { get; }

    /// <summary> Gets the operation assurance contract. </summary>
    public UcliOperationAssuranceContract Assurance { get; }

    /// <summary> Gets the optional operation code contract. </summary>
    public UcliOperationCodeContract? CodeContract { get; }

    /// <summary> Projects this validated operation into its JSON persistence contract. </summary>
    public IndexOpEntryJsonContract ToJsonContract ()
    {
        return new IndexOpEntryJsonContract(
            Name: Name,
            Kind: Kind,
            Policy: Policy,
            ArgsContract: ArgsContract,
            ResultContract: ResultContract,
            Exposure: Exposure == UcliOperationExposure.Public
                ? null
                : Exposure,
            PlayModeSupport: PlayModeSupport,
            DescriptorDigest: DescriptorDigest,
            VerdictContract: VerdictContract)
        {
            Description = Description,
            Assurance = Assurance,
            CodeContract = CodeContract,
        };
    }
}
