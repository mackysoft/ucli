
namespace MackySoft.Ucli.Contracts.Daemon;

/// <summary> Defines daemon startup-blocking reason literals observed before endpoint registration. </summary>
[VocabularyDefinition]
public enum DaemonStartupBlockingReason
{
    /// <summary> Unity Editor safe mode blocked startup. </summary>
    [VocabularyText("safeMode")]
    SafeMode = 0,

    /// <summary> Unity script compilation blocked startup. </summary>
    [VocabularyText("compile")]
    Compile = 1,

    /// <summary> Unity package resolution blocked startup. </summary>
    [VocabularyText("packageResolution")]
    PackageResolution = 2,

    /// <summary> The uCLI Unity plugin blocked startup. </summary>
    [VocabularyText("ucliPlugin")]
    UcliPlugin = 3,

    /// <summary> A precompiled assembly conflict blocked startup. </summary>
    [VocabularyText("precompiledAssemblyConflict")]
    PrecompiledAssemblyConflict = 4,

    /// <summary> A Unity modal dialog blocked startup. </summary>
    [VocabularyText("modalDialog")]
    ModalDialog = 5,

    /// <summary> Endpoint registration did not complete. </summary>
    [VocabularyText("endpointNotRegistered")]
    EndpointNotRegistered = 6,

    /// <summary> The Unity process exited before startup completed. </summary>
    [VocabularyText("processExit")]
    ProcessExit = 7,

    /// <summary> Startup was blocked by an unknown condition. </summary>
    [VocabularyText("unknown")]
    Unknown = 8,
}
