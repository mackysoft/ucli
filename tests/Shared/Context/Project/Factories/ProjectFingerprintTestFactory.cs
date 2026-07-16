using System.Security.Cryptography;
using System.Text;

namespace MackySoft.Ucli.TestSupport;

internal static class ProjectFingerprintTestFactory
{
    public static ProjectFingerprint Create (string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(label));
        return new ProjectFingerprint(Convert.ToHexString(digest).ToLowerInvariant());
    }
}
