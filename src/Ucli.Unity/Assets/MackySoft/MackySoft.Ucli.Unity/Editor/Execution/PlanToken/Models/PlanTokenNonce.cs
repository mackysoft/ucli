using System;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using MackySoft.Ucli.Contracts.Text;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.PlanToken
{
    /// <summary> Represents the canonical unpadded base64url encoding of a 16-byte plan-token nonce. </summary>
    internal sealed class PlanTokenNonce : IEquatable<PlanTokenNonce>
    {
        private const int ByteLength = 16;

        private const int EncodedLength = 22;

        private readonly string value;

        /// <summary> Initializes a nonce from canonical unpadded base64url text. </summary>
        /// <param name="value"> The canonical 22-character nonce text. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="value" /> is <see langword="null" />. </exception>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="value" /> is not the canonical encoding of 16 bytes. </exception>
        internal PlanTokenNonce (string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (!IsCanonical(value))
            {
                throw new ArgumentException(
                    "Plan-token nonce must be the canonical unpadded base64url encoding of exactly 16 bytes.",
                    nameof(value));
            }

            this.value = value;
        }

        /// <summary> Creates a cryptographically random 16-byte nonce. </summary>
        /// <returns> The generated nonce. </returns>
        internal static PlanTokenNonce Create ()
        {
            var bytes = new byte[ByteLength];
            RandomNumberGenerator.Fill(bytes);
            return new PlanTokenNonce(Base64UrlCodec.Encode(bytes));
        }

        /// <summary> Attempts to parse canonical nonce text. </summary>
        /// <param name="value"> The candidate nonce text. </param>
        /// <param name="nonce"> The parsed nonce when successful; otherwise <see langword="null" />. </param>
        /// <returns> <see langword="true" /> when <paramref name="value" /> is canonical nonce text; otherwise <see langword="false" />. </returns>
        internal static bool TryParse (
            string? value,
            [NotNullWhen(true)] out PlanTokenNonce? nonce)
        {
            nonce = null;
            if (!IsCanonical(value))
            {
                return false;
            }

            nonce = new PlanTokenNonce(value);
            return true;
        }

        /// <inheritdoc />
        public bool Equals (PlanTokenNonce? other)
        {
            return ReferenceEquals(this, other)
                || (other is not null && string.Equals(value, other.value, StringComparison.Ordinal));
        }

        /// <inheritdoc />
        public override bool Equals (object? obj)
        {
            return obj is PlanTokenNonce other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode ()
        {
            return StringComparer.Ordinal.GetHashCode(value);
        }

        /// <summary> Determines whether two nonce values are equal. </summary>
        public static bool operator == (PlanTokenNonce? left, PlanTokenNonce? right)
        {
            return left is null
                ? right is null
                : left.Equals(right);
        }

        /// <summary> Determines whether two nonce values differ. </summary>
        public static bool operator != (PlanTokenNonce? left, PlanTokenNonce? right)
        {
            return !(left == right);
        }

        /// <summary> Returns the canonical nonce text. </summary>
        public override string ToString ()
        {
            return value;
        }

        private static bool IsCanonical ([NotNullWhen(true)] string? value)
        {
            return value is { Length: EncodedLength }
                && Base64UrlCodec.IsCanonical(value);
        }
    }
}
