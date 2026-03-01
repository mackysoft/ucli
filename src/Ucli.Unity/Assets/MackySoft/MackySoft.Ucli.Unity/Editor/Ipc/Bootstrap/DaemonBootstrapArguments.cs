namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents parsed daemon bootstrap command-line arguments. </summary>
    /// <param name="RepositoryRoot"> The repository root path. </param>
    /// <param name="ProjectFingerprint"> The project fingerprint value. </param>
    /// <param name="SessionPath"> The daemon session JSON path. </param>
    /// <param name="EndpointTransportKind"> The endpoint transport kind value. </param>
    /// <param name="EndpointAddress"> The endpoint address value. </param>
    internal sealed record DaemonBootstrapArguments (
        string RepositoryRoot,
        string ProjectFingerprint,
        string SessionPath,
        string EndpointTransportKind,
        string EndpointAddress);
}
