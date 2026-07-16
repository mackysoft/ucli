using System;
using System.Security.Cryptography;
using System.Text;
using MackySoft.Ucli.Contracts;

namespace MackySoft.Ucli.Unity.Tests
{
    internal static class ProjectFingerprintTestFactory
    {
        public static ProjectFingerprint Create (string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                throw new ArgumentException("Fingerprint label must not be empty.", nameof(label));
            }

            using (var sha256 = SHA256.Create())
            {
                var digest = sha256.ComputeHash(Encoding.UTF8.GetBytes(label));
                return new ProjectFingerprint(BitConverter.ToString(digest).Replace("-", string.Empty).ToLowerInvariant());
            }
        }
    }
}
