using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

/// <summary> Defines build profile input-kind literals. </summary>
internal enum BuildProfileInputsKind
{
    /// <summary> Uses build inputs declared directly in the profile. </summary>
    [UcliContractLiteral("explicit")]
    Explicit = 0,
}
