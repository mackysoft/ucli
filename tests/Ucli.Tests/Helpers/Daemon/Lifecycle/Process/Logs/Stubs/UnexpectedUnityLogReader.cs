namespace MackySoft.Ucli.Tests.Helpers.Process;

internal sealed class UnexpectedUnityLogReader : IUnityLogReader
{
    private readonly string reason;

    public UnexpectedUnityLogReader (string reason)
    {
        this.reason = string.IsNullOrWhiteSpace(reason)
            ? "Unity log should not be read."
            : reason;
    }

    public ValueTask<UnityLogReadResult> ReadTailAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        int maxBytes = IUnityLogReader.DefaultMaxBytes,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(reason);
    }
}
