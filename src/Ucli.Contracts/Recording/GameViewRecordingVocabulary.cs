namespace MackySoft.Ucli.Contracts.Recording;

/// <summary>Defines the severity of a GameView recording diagnostic.</summary>
[VocabularyDefinition]
public enum GameViewRecordingDiagnosticSeverity
{
    [VocabularyText("warning")]
    Warning = 1,

    [VocabularyText("error")]
    Error,
}

/// <summary>Defines Recorder package observation states.</summary>
[VocabularyDefinition]
public enum GameViewRecordingPackageState
{
    [VocabularyText("missing")]
    Missing = 1,

    [VocabularyText("resolved")]
    Resolved,

    [VocabularyText("indeterminate")]
    Indeterminate,
}

/// <summary>Defines Recorder package compatibility states.</summary>
[VocabularyDefinition]
public enum GameViewRecordingCompatibilityState
{
    [VocabularyText("notApplicable")]
    NotApplicable = 1,

    [VocabularyText("supported")]
    Supported,

    [VocabularyText("unsupported")]
    Unsupported,

    [VocabularyText("indeterminate")]
    Indeterminate,
}

/// <summary>Defines Recorder adapter registration states.</summary>
[VocabularyDefinition]
public enum GameViewRecordingAdapterState
{
    [VocabularyText("notApplicable")]
    NotApplicable = 1,

    [VocabularyText("registered")]
    Registered,

    [VocabularyText("missing")]
    Missing,

    [VocabularyText("unobserved")]
    Unobserved,
}

/// <summary>Defines current runtime admission states.</summary>
[VocabularyDefinition]
public enum GameViewRecordingRuntimeAdmissionState
{
    [VocabularyText("ready")]
    Ready = 1,

    [VocabularyText("blocked")]
    Blocked,

    [VocabularyText("unobserved")]
    Unobserved,
}

/// <summary>Defines the fixed recording container.</summary>
[VocabularyDefinition]
public enum GameViewRecordingContainer
{
    [VocabularyText("mp4")]
    Mp4 = 1,
}

/// <summary>Defines the fixed recording video codec.</summary>
[VocabularyDefinition]
public enum GameViewRecordingCodec
{
    [VocabularyText("h264")]
    H264 = 1,
}

/// <summary>Defines the capture timing mode.</summary>
[VocabularyDefinition]
public enum GameViewRecordingTimingMode
{
    [VocabularyText("constantFrameRateCapture")]
    ConstantFrameRateCapture = 1,
}

/// <summary>Defines the durable GameView recording state.</summary>
[VocabularyDefinition]
public enum GameViewRecordingState
{
    [VocabularyText("preparing")]
    Preparing = 1,

    [VocabularyText("recording")]
    Recording,

    [VocabularyText("finalizing")]
    Finalizing,

    [VocabularyText("completed")]
    Completed,

    [VocabularyText("failed")]
    Failed,

    [VocabularyText("indeterminate")]
    Indeterminate,
}

/// <summary>Defines why a recording interval ended.</summary>
[VocabularyDefinition]
public enum GameViewRecordingStopReason
{
    [VocabularyText("manual")]
    Manual = 1,

    [VocabularyText("maxDurationReached")]
    MaxDurationReached,

    [VocabularyText("playModeExited")]
    PlayModeExited,

    [VocabularyText("domainReload")]
    DomainReload,

    [VocabularyText("unityExited")]
    UnityExited,

    [VocabularyText("adapterUnloaded")]
    AdapterUnloaded,

    [VocabularyText("encoderFailure")]
    EncoderFailure,

    [VocabularyText("internalFailure")]
    InternalFailure,

    [VocabularyText("unconfirmed")]
    Unconfirmed,
}

/// <summary>Defines whether the finalized recording video is available.</summary>
[VocabularyDefinition]
public enum GameViewRecordingVideoDisposition
{
    [VocabularyText("available")]
    Available = 1,

    [VocabularyText("missing")]
    Missing,

    [VocabularyText("unconfirmed")]
    Unconfirmed,
}

/// <summary>Defines the aggregate cleanup result.</summary>
[VocabularyDefinition]
public enum GameViewRecordingCleanupDisposition
{
    [VocabularyText("complete")]
    Complete = 1,

    [VocabularyText("failed")]
    Failed,

    [VocabularyText("unconfirmed")]
    Unconfirmed,
}

/// <summary>Defines the public recording payload branch.</summary>
[VocabularyDefinition]
public enum GameViewRecordingPayloadKind
{
    [VocabularyText("status")]
    Status = 1,

    [VocabularyText("active")]
    Active,

    [VocabularyText("recovery")]
    Recovery,

    [VocabularyText("terminal")]
    Terminal,
}

/// <summary>Defines the status recording-selection branch.</summary>
[VocabularyDefinition]
public enum GameViewRecordingSelectionKind
{
    [VocabularyText("none")]
    None = 1,

    [VocabularyText("selected")]
    Selected,
}

/// <summary>Defines each state restored after GameView recording.</summary>
[VocabularyDefinition]
public enum GameViewRecordingStateRestorationKind
{
    [VocabularyText("playModeView")]
    PlayModeView = 1,

    [VocabularyText("gameView")]
    GameView,

    [VocabularyText("display")]
    Display,

    [VocabularyText("resolutionSelection")]
    ResolutionSelection,

    [VocabularyText("presentation")]
    Presentation,

    [VocabularyText("timeState")]
    TimeState,
}

/// <summary>Defines the result of restoring one owned state.</summary>
[VocabularyDefinition]
public enum GameViewRecordingStateRestorationDisposition
{
    [VocabularyText("unchanged")]
    Unchanged = 1,

    [VocabularyText("restored")]
    Restored,

    [VocabularyText("failed")]
    Failed,

    [VocabularyText("unconfirmed")]
    Unconfirmed,
}

/// <summary>Defines each resource released after GameView recording.</summary>
[VocabularyDefinition]
public enum GameViewRecordingResourceKind
{
    [VocabularyText("captureSession")]
    CaptureSession = 1,

    [VocabularyText("temporaryOutput")]
    TemporaryOutput,

    [VocabularyText("lifecycleSubscriptions")]
    LifecycleSubscriptions,

    [VocabularyText("runtimeRegistration")]
    RuntimeRegistration,

    [VocabularyText("recordingExclusion")]
    RecordingExclusion,
}

/// <summary>Defines the result of releasing one owned resource.</summary>
[VocabularyDefinition]
public enum GameViewRecordingResourceReleaseDisposition
{
    [VocabularyText("notAcquired")]
    NotAcquired = 1,

    [VocabularyText("released")]
    Released,

    [VocabularyText("failed")]
    Failed,

    [VocabularyText("unconfirmed")]
    Unconfirmed,
}
