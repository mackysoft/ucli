using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Shared.Context.Project;

/// <summary> Identifies the component of a Unity host project identity that differs from the requested project. </summary>
internal enum ProjectIdentityMismatchKind
{
    /// <summary> The project fingerprint differs. </summary>
    [UcliContractLiteral("projectFingerprint")]
    ProjectFingerprint = 1,

    /// <summary> The Unity project root path differs. </summary>
    [UcliContractLiteral("projectPath")]
    ProjectPath = 2,

    /// <summary> The Unity editor version differs. </summary>
    [UcliContractLiteral("unityVersion")]
    UnityVersion = 3,
}
