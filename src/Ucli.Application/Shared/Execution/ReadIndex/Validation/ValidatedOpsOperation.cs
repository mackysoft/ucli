using System.Text.Json;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Represents one operation whose catalog contract has been validated and projected into typed values. </summary>
internal sealed class ValidatedOpsOperation
{
    internal ValidatedOpsOperation (
        IndexOpEntryJsonContract contract,
        UcliOperationKind kind,
        OperationPolicy policy,
        UcliOperationExposure exposure,
        UcliOperationPlayModeSupport playModeSupport,
        JsonElement argsSchema,
        JsonElement? resultSchema)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentException.ThrowIfNullOrWhiteSpace(contract.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(contract.Description);
        ArgumentNullException.ThrowIfNull(contract.Inputs);
        ArgumentNullException.ThrowIfNull(contract.ResultContract);
        ArgumentNullException.ThrowIfNull(contract.Assurance);
        if (!TextVocabulary.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Operation kind must have a contract literal.");
        }

        if (!TextVocabulary.IsDefined(policy))
        {
            throw new ArgumentOutOfRangeException(nameof(policy), policy, "Operation policy must have a contract literal.");
        }

        if (!TextVocabulary.IsDefined(exposure))
        {
            throw new ArgumentOutOfRangeException(nameof(exposure), exposure, "Operation exposure must have a contract literal.");
        }

        if (!TextVocabulary.IsDefined(playModeSupport))
        {
            throw new ArgumentOutOfRangeException(
                nameof(playModeSupport),
                playModeSupport,
                "Operation Play Mode support must have a contract literal.");
        }

        if (argsSchema.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Operation argument schema must be a JSON object.", nameof(argsSchema));
        }

        if (resultSchema is { ValueKind: not JsonValueKind.Object })
        {
            throw new ArgumentException("Operation result schema must be a JSON object when specified.", nameof(resultSchema));
        }

        Name = contract.Name;
        Kind = kind;
        Policy = policy;
        Exposure = exposure;
        PlayModeSupport = playModeSupport;
        Description = contract.Description;
        Inputs = Array.AsReadOnly(contract.Inputs.ToArray());
        ResultContract = contract.ResultContract;
        Assurance = contract.Assurance;
        CodeContract = contract.CodeContract;
        ArgsSchema = argsSchema;
        ResultSchema = resultSchema;
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

    /// <summary> Gets the argument JSON schema. </summary>
    public JsonElement ArgsSchema { get; }

    /// <summary> Gets the result JSON schema, or <see langword="null" /> when the operation emits no result. </summary>
    public JsonElement? ResultSchema { get; }

    /// <summary> Gets input contracts used to build operation arguments. </summary>
    public IReadOnlyList<UcliOperationInputContract> Inputs { get; }

    /// <summary> Gets the operation result contract. </summary>
    public UcliOperationResultContract ResultContract { get; }

    /// <summary> Gets the operation assurance contract. </summary>
    public UcliOperationAssuranceContract Assurance { get; }

    /// <summary> Gets the optional operation code contract. </summary>
    public UcliOperationCodeContract? CodeContract { get; }

    /// <summary> Projects this validated operation into its JSON persistence contract. </summary>
    public IndexOpEntryJsonContract ToJsonContract ()
    {
        return new IndexOpEntryJsonContract(
            Name,
            TextVocabulary.GetText(Kind),
            TextVocabulary.GetText(Policy),
            ArgsSchema.GetRawText(),
            ResultSchema?.GetRawText(),
            TextVocabulary.GetText(Exposure),
            TextVocabulary.GetText(PlayModeSupport))
        {
            Description = Description,
            Inputs = Inputs,
            ResultContract = ResultContract,
            Assurance = Assurance,
            CodeContract = CodeContract,
        };
    }
}
