using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Infrastructure.Execution.ReadPostcondition;

/// <summary> Identifies the verified plan-token binding that may start one eval call. </summary>
public sealed record EvalCallAdmission
{
    /// <summary> Initializes an eval-call admission candidate after plan-token verification. </summary>
    public EvalCallAdmission (
        string nonce,
        Sha256Digest tokenDigest,
        Guid requestId,
        Sha256Digest sourceDigest,
        Sha256Digest executionDigest,
        long editorGeneration,
        DateTimeOffset issuedAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(nonce))
        {
            throw new ArgumentException("Eval plan-token nonce must be specified.", nameof(nonce));
        }

        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("Eval request ID must not be empty.", nameof(requestId));
        }

        if (issuedAtUtc.Offset != TimeSpan.Zero || expiresAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Eval plan-token timestamps must be UTC.");
        }

        if (expiresAtUtc <= issuedAtUtc)
        {
            throw new ArgumentException("Eval plan-token expiration must be later than its issue time.", nameof(expiresAtUtc));
        }

        Nonce = nonce;
        TokenDigest = tokenDigest ?? throw new ArgumentNullException(nameof(tokenDigest));
        RequestId = requestId;
        SourceDigest = sourceDigest ?? throw new ArgumentNullException(nameof(sourceDigest));
        ExecutionDigest = executionDigest ?? throw new ArgumentNullException(nameof(executionDigest));
        EditorGeneration = editorGeneration;
        IssuedAtUtc = issuedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    /// <summary> Gets the token-unique nonce. </summary>
    public string Nonce { get; }

    /// <summary> Gets the SHA-256 digest of the compact token; the token itself is never persisted. </summary>
    public Sha256Digest TokenDigest { get; }

    /// <summary> Gets the correlated IPC request identifier. </summary>
    public Guid RequestId { get; }

    /// <summary> Gets the verified source digest. </summary>
    public Sha256Digest SourceDigest { get; }

    /// <summary> Gets the verified compiled execution digest. </summary>
    public Sha256Digest ExecutionDigest { get; }

    /// <summary> Gets the verified Editor execution generation. </summary>
    public long EditorGeneration { get; }

    /// <summary> Gets the token issue timestamp. </summary>
    public DateTimeOffset IssuedAtUtc { get; }

    /// <summary> Gets the token expiration timestamp. </summary>
    public DateTimeOffset ExpiresAtUtc { get; }
}
