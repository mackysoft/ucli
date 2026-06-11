using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

/// <summary> Defines build profile scene-source literals. </summary>
internal enum BuildProfileSceneSource
{
    /// <summary> Uses enabled Unity Editor Build Settings scenes. </summary>
    [UcliContractLiteral("editorBuildSettings")]
    EditorBuildSettings = 0,

    /// <summary> Uses explicit profile scene paths. </summary>
    [UcliContractLiteral("explicit")]
    Explicit = 1,
}
