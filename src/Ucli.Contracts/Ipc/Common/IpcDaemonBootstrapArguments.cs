namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one Unity daemon bootstrap argument payload. </summary>
/// <param name="RepositoryRoot"> The repository root path. </param>
/// <param name="ProjectFingerprint"> The project fingerprint value. </param>
/// <param name="SessionPath"> The daemon session file path. </param>
/// <param name="EndpointTransportKind"> The endpoint transport kind literal. </param>
/// <param name="EndpointAddress"> The endpoint address value. </param>
public sealed record IpcDaemonBootstrapArguments (
    string RepositoryRoot,
    string ProjectFingerprint,
    string SessionPath,
    string EndpointTransportKind,
    string EndpointAddress)
    : IpcBatchmodeBootstrapArguments;