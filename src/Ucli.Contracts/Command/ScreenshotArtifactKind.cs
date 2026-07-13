using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts;

/// <summary> Defines semantic artifact kinds produced by screenshot commands. </summary>
public enum ScreenshotArtifactKind
{
    /// <summary> Identifies a committed screenshot image artifact. </summary>
    [UcliContractLiteral("screenshot")]
    Screenshot = 0,
}
