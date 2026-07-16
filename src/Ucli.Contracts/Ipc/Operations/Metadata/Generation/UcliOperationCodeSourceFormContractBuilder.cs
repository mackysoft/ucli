using MackySoft.Ucli.Contracts.Operations;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

internal static class UcliOperationCodeSourceFormContractBuilder
{
    public static IReadOnlyList<UcliCodeSourceFormContract> Create (IReadOnlyList<UcliCodeSourceFormContract> sourceForms)
    {
        var contracts = new UcliCodeSourceFormContract[sourceForms.Count];
        for (var i = 0; i < sourceForms.Count; i++)
        {
            contracts[i] = CreateOne(sourceForms[i], nameof(sourceForms));
        }

        return contracts;
    }

    private static UcliCodeSourceFormContract CreateOne (
        UcliCodeSourceFormContract sourceForm,
        string paramName)
    {
        if (sourceForm == null
            || !sourceForm.Kind.HasValue
            || !ContractLiteralCodec.IsDefined(sourceForm.Kind.Value)
            || string.IsNullOrWhiteSpace(sourceForm.Description))
        {
            throw new ArgumentException("Source form kind and description must not be empty.", paramName);
        }

        return new UcliCodeSourceFormContract(sourceForm.Kind, sourceForm.Description);
    }
}
