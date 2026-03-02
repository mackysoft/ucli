using System;
using System.IO;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Unity.Execution.Requests;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.RequestIdempotency
{
    /// <summary> Computes deterministic request digests for request-id idempotency matching. </summary>
    internal static class ExecuteRequestIdempotencyDigestCalculator
    {
        /// <summary> Computes one deterministic digest from execute request command, arguments and plan-token. </summary>
        /// <param name="request"> The execute request payload. </param>
        /// <returns> The lowercase hexadecimal SHA-256 digest string. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="request" /> is <see langword="null" />. </exception>
        public static string ComputeDigest (IpcExecuteRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var normalizedPlanToken = StringValueNormalizer.TrimToNull(request.PlanToken);

            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
            {
                Indented = false,
            });

            writer.WriteStartObject();
            writer.WritePropertyName("arguments");
            CanonicalRequestWriter.WriteCanonicalJsonValue(writer, request.Arguments);
            writer.WriteString("command", request.Command);
            if (normalizedPlanToken is null)
            {
                writer.WriteNull("planToken");
            }
            else
            {
                writer.WriteString("planToken", normalizedPlanToken);
            }

            writer.WriteEndObject();
            writer.Flush();

            return Sha256LowerHex.Compute(stream.ToArray());
        }
    }
}
