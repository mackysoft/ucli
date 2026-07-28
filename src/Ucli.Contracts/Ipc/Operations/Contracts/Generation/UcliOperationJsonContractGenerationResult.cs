using MackySoft.JsonSchema.Generation;
using MackySoft.JsonSchema.Generation.ContractModel;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary>
/// Carries the provider generation results for one operation's args and optional result contracts.
/// </summary>
public sealed class UcliOperationJsonContractGenerationResult
{
    private readonly JsonContractGenerationResult argsGenerationResult;
    private readonly JsonContractGenerationResult? resultGenerationResult;

    internal UcliOperationJsonContractGenerationResult (
        JsonContractGenerationResult argsGenerationResult,
        JsonContractGenerationResult? resultGenerationResult)
    {
        this.argsGenerationResult = argsGenerationResult ?? throw new ArgumentNullException(nameof(argsGenerationResult));
        this.resultGenerationResult = resultGenerationResult;
        ArgsContract = new UcliOperationJsonContract(argsGenerationResult);
        ResultContract = resultGenerationResult == null
            ? null
            : new UcliOperationJsonContract(resultGenerationResult);
    }

    /// <summary> Gets the generated uCLI args contract derived from the authoritative provider result. </summary>
    public UcliOperationJsonContract ArgsContract { get; }

    /// <summary>
    /// Gets the generated uCLI result contract, or <see langword="null" /> when the
    /// operation declares <see cref="UcliNoResult" />.
    /// </summary>
    public UcliOperationJsonContract? ResultContract { get; }

    /// <summary> Returns a caller-owned copy of the generated args JSON Schema projection. </summary>
    public byte[] GetArgsJsonSchemaUtf8 ()
    {
        return argsGenerationResult.GetJsonSchemaUtf8();
    }

    /// <summary>
    /// Returns a caller-owned copy of the generated result JSON Schema projection, or <see langword="null" /> when
    /// the operation declares <see cref="UcliNoResult" />.
    /// </summary>
    public byte[]? GetResultJsonSchemaUtf8 ()
    {
        return resultGenerationResult?.GetJsonSchemaUtf8();
    }

    internal JsonContractModel ArgsContractModel => argsGenerationResult.Model;
}
