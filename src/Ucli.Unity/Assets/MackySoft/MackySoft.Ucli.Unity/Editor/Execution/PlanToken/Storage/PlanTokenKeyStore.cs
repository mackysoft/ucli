using System;
using System.IO;
using System.Security.Cryptography;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Storage;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.PlanToken
{
    /// <summary> Provides load/create behavior for plan-token signing keys. </summary>
    internal static class PlanTokenKeyStore
    {
        /// <summary> Loads one existing signing key or creates a new key file on demand. </summary>
        /// <param name="snapshot"> The runtime environment snapshot. </param>
        /// <param name="key"> The loaded or generated key bytes. </param>
        /// <param name="errorMessage"> The error message when load/create fails. </param>
        /// <returns> <see langword="true" /> when key is available; otherwise <see langword="false" />. </returns>
        public static bool TryLoadOrCreate (
            PlanTokenEnvironmentSnapshot snapshot,
            out byte[] key,
            out string? errorMessage)
        {
            try
            {
                var keyFilePath = UcliStoragePathResolver.ResolvePlanTokenKeyPath(snapshot.RepositoryRoot, snapshot.ProjectFingerprint);
                if (!keyFilePath.TryGetParent(out var parentDirectory))
                {
                    key = Array.Empty<byte>();
                    errorMessage = "Failed to resolve plan-token key directory.";
                    return false;
                }

                UcliLocalStorageBootstrapper.EnsureInitialized(parentDirectory);
                Directory.CreateDirectory(parentDirectory.Value);

                if (File.Exists(keyFilePath.Value))
                {
                    var encodedKey = StringValueNormalizer.TrimOrEmpty(File.ReadAllText(keyFilePath.Value));
                    if (TryDecodeKey(encodedKey, out key))
                    {
                        errorMessage = null;
                        return true;
                    }
                }

                key = CreateRandomKey();
                var encoded = Convert.ToBase64String(key);
                File.WriteAllText(keyFilePath.Value, encoded);
                errorMessage = null;
                return true;
            }
            catch (Exception exception)
            {
                key = Array.Empty<byte>();
                errorMessage = $"Failed to initialize plan-token key. {exception.Message}";
                return false;
            }
        }

        /// <summary> Attempts to decode one stored key string. </summary>
        /// <param name="encoded"> The encoded key string. </param>
        /// <param name="key"> The decoded key bytes. </param>
        /// <returns> <see langword="true" /> when decode succeeds and size is valid; otherwise <see langword="false" />. </returns>
        private static bool TryDecodeKey (
            string encoded,
            out byte[] key)
        {
            key = Array.Empty<byte>();
            if (string.IsNullOrWhiteSpace(encoded))
            {
                return false;
            }

            try
            {
                var decoded = Convert.FromBase64String(encoded);
                if (decoded.Length < PlanTokenCompactCodec.MinimumSigningKeyByteLength)
                {
                    return false;
                }

                key = decoded;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary> Creates one new random signing key. </summary>
        /// <returns> The generated key bytes. </returns>
        private static byte[] CreateRandomKey ()
        {
            var key = new byte[PlanTokenCompactCodec.MinimumSigningKeyByteLength];
            RandomNumberGenerator.Fill(key);
            return key;
        }
    }
}
