
namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Defines normalized daemon diagnosis startup-phase literals. </summary>
[VocabularyDefinition]
public enum DaemonDiagnosisStartupPhase
{
    /// <summary> Unity script compilation blocked startup. </summary>
    [VocabularyText("scriptCompilation")]
    ScriptCompilation = 1,

    /// <summary> Unity package resolution blocked startup. </summary>
    [VocabularyText("packageResolution")]
    PackageResolution = 2,

    /// <summary> Unity Editor is waiting for user action. </summary>
    [VocabularyText("userAction")]
    UserAction = 3,

    /// <summary> Unity Editor exited before bootstrap completed. </summary>
    [VocabularyText("processExit")]
    ProcessExit = 4,

    /// <summary> Startup is waiting for GUI endpoint registration. </summary>
    [VocabularyText("endpointRegistration")]
    EndpointRegistration = 5,
}
