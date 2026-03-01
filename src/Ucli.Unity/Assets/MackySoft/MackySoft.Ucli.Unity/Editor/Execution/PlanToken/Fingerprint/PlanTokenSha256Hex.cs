using System;
using System.Security.Cryptography;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.PlanToken
{
    /// <summary> Computes lowercase hexadecimal SHA-256 digest strings for plan-token workflows. </summary>
    internal static class PlanTokenSha256Hex
    {
        /// <summary> Computes SHA-256 digest and returns lowercase hexadecimal text. </summary>
        /// <param name="bytes"> The input bytes. </param>
        /// <returns> The lowercase hexadecimal digest string. </returns>
        public static string Compute (ReadOnlySpan<byte> bytes)
        {
            var inputBytes = bytes.ToArray();
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(inputBytes);

            var chars = new char[hashBytes.Length * 2];
            var charIndex = 0;
            for (var i = 0; i < hashBytes.Length; i++)
            {
                var value = hashBytes[i];
                chars[charIndex] = ToHexNibble(value >> 4);
                chars[charIndex + 1] = ToHexNibble(value & 0x0f);
                charIndex += 2;
            }

            return new string(chars);
        }

        /// <summary> Converts one nibble value to lowercase hexadecimal char. </summary>
        /// <param name="value"> The nibble value. </param>
        /// <returns> The lowercase hexadecimal char. </returns>
        private static char ToHexNibble (int value)
        {
            return (char)(value < 10 ? '0' + value : 'a' + (value - 10));
        }
    }
}