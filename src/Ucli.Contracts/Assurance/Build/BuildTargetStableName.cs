using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Assurance.Build;

/// <summary> Defines uCLI buildTarget stable-name literals. </summary>
public enum BuildTargetStableName
{
    /// <summary> macOS standalone player build target. </summary>
    [UcliContractLiteral("standaloneOSX")]
    StandaloneOsx = 0,

    /// <summary> Windows 32-bit standalone player build target. </summary>
    [UcliContractLiteral("standaloneWindows")]
    StandaloneWindows = 1,

    /// <summary> Windows 64-bit standalone player build target. </summary>
    [UcliContractLiteral("standaloneWindows64")]
    StandaloneWindows64 = 2,

    /// <summary> Linux x64 standalone player build target. </summary>
    [UcliContractLiteral("standaloneLinux64")]
    StandaloneLinux64 = 3,

    /// <summary> iOS player build target. </summary>
    [UcliContractLiteral("ios")]
    Ios = 4,

    /// <summary> Android player build target. </summary>
    [UcliContractLiteral("android")]
    Android = 5,

    /// <summary> WebGL player build target. </summary>
    [UcliContractLiteral("webgl")]
    Webgl = 6,

    /// <summary> Windows Store Apps player build target. </summary>
    [UcliContractLiteral("wsaPlayer")]
    WsaPlayer = 7,

    /// <summary> tvOS player build target. </summary>
    [UcliContractLiteral("tvos")]
    Tvos = 8,

    /// <summary> Nintendo Switch player build target. </summary>
    [UcliContractLiteral("switch")]
    Switch = 9,

    /// <summary> Linux headless simulation build target. </summary>
    [UcliContractLiteral("linuxHeadlessSimulation")]
    LinuxHeadlessSimulation = 10,

    /// <summary> Game Core Xbox Series build target. </summary>
    [UcliContractLiteral("gameCoreXboxSeries")]
    GameCoreXboxSeries = 11,

    /// <summary> Game Core Xbox One build target. </summary>
    [UcliContractLiteral("gameCoreXboxOne")]
    GameCoreXboxOne = 12,

    /// <summary> PlayStation 4 player build target. </summary>
    [UcliContractLiteral("ps4")]
    Ps4 = 13,

    /// <summary> PlayStation 5 player build target. </summary>
    [UcliContractLiteral("ps5")]
    Ps5 = 14,

    /// <summary> Xbox One player build target. </summary>
    [UcliContractLiteral("xboxOne")]
    XboxOne = 15,

    /// <summary> Embedded Linux player build target. </summary>
    [UcliContractLiteral("embeddedLinux")]
    EmbeddedLinux = 16,

    /// <summary> QNX player build target. </summary>
    [UcliContractLiteral("qnx")]
    Qnx = 17,

    /// <summary> visionOS player build target. </summary>
    [UcliContractLiteral("visionOS")]
    VisionOs = 18,
}
