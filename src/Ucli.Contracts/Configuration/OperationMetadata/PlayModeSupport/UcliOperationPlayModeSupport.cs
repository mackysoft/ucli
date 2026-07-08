using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Configuration;

/// <summary> Defines whether a raw operation can be executed through Play Mode mutation requests. </summary>
public enum UcliOperationPlayModeSupport
{
    /// <summary> Disallows raw operation execution when <c>--allowPlayMode</c> is specified. </summary>
    [UcliContractLiteral("disallowed")]
    Disallowed = 0,

    /// <summary> Allows raw operation execution both outside and inside Play Mode mutation requests. </summary>
    [UcliContractLiteral("allowed")]
    Allowed = 1,

    /// <summary> Requires raw operation execution to use Play Mode mutation admission. </summary>
    [UcliContractLiteral("required")]
    Required = 2,
}
