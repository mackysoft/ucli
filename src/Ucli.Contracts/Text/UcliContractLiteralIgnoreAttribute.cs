namespace MackySoft.Ucli.Contracts.Text;

/// <summary> Marks an enum member that intentionally has no contract literal. </summary>
[AttributeUsage(AttributeTargets.Field)]
internal sealed class UcliContractLiteralIgnoreAttribute : Attribute
{
}
