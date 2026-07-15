using System.Text;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.TestSupport;

internal static class Sha256DigestTestFactory
{
    public static Sha256Digest Create (char hexadecimalDigit)
    {
        return Sha256Digest.Parse(new string(hexadecimalDigit, 64));
    }

    public static Sha256Digest Compute (string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Sha256Digest.Compute(Encoding.UTF8.GetBytes(value));
    }
}
