using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Assurance.Build;

/// <summary> Defines uCLI buildTarget stable-name literals. </summary>
public enum BuildTargetStableName
{
    /// <summary> macOS standalone player build target. </summary>
    [UcliContractLiteral("standaloneOSX")]
    StandaloneOsx = 1,

    /// <summary> Windows 32-bit standalone player build target. </summary>
    [UcliContractLiteral("standaloneWindows")]
    StandaloneWindows = 2,

    /// <summary> Windows 64-bit standalone player build target. </summary>
    [UcliContractLiteral("standaloneWindows64")]
    StandaloneWindows64 = 3,

    /// <summary> Linux x64 standalone player build target. </summary>
    [UcliContractLiteral("standaloneLinux64")]
    StandaloneLinux64 = 4,

    /// <summary> iOS player build target. </summary>
    [UcliContractLiteral("ios")]
    Ios = 5,

    /// <summary> Android player build target. </summary>
    [UcliContractLiteral("android")]
    Android = 6,

    /// <summary> WebGL player build target. </summary>
    [UcliContractLiteral("webgl")]
    Webgl = 7,

    /// <summary> Windows Store Apps player build target. </summary>
    [UcliContractLiteral("wsaPlayer")]
    WsaPlayer = 8,

    /// <summary> tvOS player build target. </summary>
    [UcliContractLiteral("tvos")]
    Tvos = 9,

    /// <summary> Nintendo Switch player build target. </summary>
    [UcliContractLiteral("switch")]
    Switch = 10,

    /// <summary> Linux headless simulation build target. </summary>
    [UcliContractLiteral("linuxHeadlessSimulation")]
    LinuxHeadlessSimulation = 11,

    /// <summary> Game Core Xbox Series build target. </summary>
    [UcliContractLiteral("gameCoreXboxSeries")]
    GameCoreXboxSeries = 12,

    /// <summary> Game Core Xbox One build target. </summary>
    [UcliContractLiteral("gameCoreXboxOne")]
    GameCoreXboxOne = 13,

    /// <summary> PlayStation 4 player build target. </summary>
    [UcliContractLiteral("ps4")]
    Ps4 = 14,

    /// <summary> PlayStation 5 player build target. </summary>
    [UcliContractLiteral("ps5")]
    Ps5 = 15,

    /// <summary> Xbox One player build target. </summary>
    [UcliContractLiteral("xboxOne")]
    XboxOne = 16,

    /// <summary> Embedded Linux player build target. </summary>
    [UcliContractLiteral("embeddedLinux")]
    EmbeddedLinux = 17,

    /// <summary> QNX player build target. </summary>
    [UcliContractLiteral("qnx")]
    Qnx = 18,

    /// <summary> visionOS player build target. </summary>
    [UcliContractLiteral("visionOS")]
    VisionOs = 19,
}
