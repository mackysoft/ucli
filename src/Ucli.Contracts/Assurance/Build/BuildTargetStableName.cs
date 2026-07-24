
namespace MackySoft.Ucli.Contracts.Assurance.Build;

/// <summary> Defines uCLI buildTarget stable-name literals. </summary>
[VocabularyDefinition]
public enum BuildTargetStableName
{
    /// <summary> macOS standalone player build target. </summary>
    [VocabularyText("standaloneOSX")]
    StandaloneOsx = 1,

    /// <summary> Windows 32-bit standalone player build target. </summary>
    [VocabularyText("standaloneWindows")]
    StandaloneWindows = 2,

    /// <summary> Windows 64-bit standalone player build target. </summary>
    [VocabularyText("standaloneWindows64")]
    StandaloneWindows64 = 3,

    /// <summary> Linux x64 standalone player build target. </summary>
    [VocabularyText("standaloneLinux64")]
    StandaloneLinux64 = 4,

    /// <summary> iOS player build target. </summary>
    [VocabularyText("ios")]
    Ios = 5,

    /// <summary> Android player build target. </summary>
    [VocabularyText("android")]
    Android = 6,

    /// <summary> WebGL player build target. </summary>
    [VocabularyText("webgl")]
    Webgl = 7,

    /// <summary> Windows Store Apps player build target. </summary>
    [VocabularyText("wsaPlayer")]
    WsaPlayer = 8,

    /// <summary> tvOS player build target. </summary>
    [VocabularyText("tvos")]
    Tvos = 9,

    /// <summary> Nintendo Switch player build target. </summary>
    [VocabularyText("switch")]
    Switch = 10,

    /// <summary> Linux headless simulation build target. </summary>
    [VocabularyText("linuxHeadlessSimulation")]
    LinuxHeadlessSimulation = 11,

    /// <summary> Game Core Xbox Series build target. </summary>
    [VocabularyText("gameCoreXboxSeries")]
    GameCoreXboxSeries = 12,

    /// <summary> Game Core Xbox One build target. </summary>
    [VocabularyText("gameCoreXboxOne")]
    GameCoreXboxOne = 13,

    /// <summary> PlayStation 4 player build target. </summary>
    [VocabularyText("ps4")]
    Ps4 = 14,

    /// <summary> PlayStation 5 player build target. </summary>
    [VocabularyText("ps5")]
    Ps5 = 15,

    /// <summary> Xbox One player build target. </summary>
    [VocabularyText("xboxOne")]
    XboxOne = 16,

    /// <summary> Embedded Linux player build target. </summary>
    [VocabularyText("embeddedLinux")]
    EmbeddedLinux = 17,

    /// <summary> QNX player build target. </summary>
    [VocabularyText("qnx")]
    Qnx = 18,

    /// <summary> visionOS player build target. </summary>
    [VocabularyText("visionOS")]
    VisionOs = 19,
}
