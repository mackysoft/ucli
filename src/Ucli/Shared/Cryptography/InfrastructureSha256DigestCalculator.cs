using MackySoft.Ucli.Application.Shared.Cryptography;
using MackySoft.Ucli.Infrastructure.Cryptography;

namespace MackySoft.Ucli.Shared.Cryptography;

/// <summary> Adapts the infrastructure SHA-256 implementation to application digest contracts. </summary>
internal sealed class InfrastructureSha256DigestCalculator : ISha256DigestCalculator
{
    /// <inheritdoc />
    public string Compute (ReadOnlySpan<byte> bytes)
    {
        return Sha256LowerHex.Compute(bytes);
    }
}
