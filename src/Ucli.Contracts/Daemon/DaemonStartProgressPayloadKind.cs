
namespace MackySoft.Ucli.Contracts.Daemon;

/// <summary> Defines daemon-start progress payload-kind literals. </summary>
[VocabularyDefinition]
public enum DaemonStartProgressPayloadKind
{
    /// <summary> Endpoint-registration startup observation payload. </summary>
    [VocabularyText("startupObservation")]
    StartupObservation = 0,

    /// <summary> Endpoint-registered lifecycle snapshot payload. </summary>
    [VocabularyText("lifecycleSnapshot")]
    LifecycleSnapshot = 1,
}
