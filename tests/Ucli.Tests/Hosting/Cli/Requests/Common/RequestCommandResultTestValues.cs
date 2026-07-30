using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Tests;

internal static class RequestCommandResultTestValues
{
    public static readonly Sha256Digest OperationDescriptorDigest = Sha256Digest.Parse(
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");
}
