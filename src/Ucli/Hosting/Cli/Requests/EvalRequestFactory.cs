using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Creates internal call requests for the <c>eval</c> command. </summary>
internal static class EvalRequestFactory
{
    private const string OperationId = "eval";

    /// <summary> Creates one request JSON document that invokes <c>ucli.cs.eval</c>. </summary>
    /// <param name="source"> The C# source text to evaluate. </param>
    /// <returns> The internal request JSON. </returns>
    public static string Create (string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        return JsonSerializer.Serialize(
            new
            {
                steps = new[]
                {
                    new
                    {
                        kind = "op",
                        id = OperationId,
                        op = UcliPrimitiveOperationNames.CsEval,
                        args = IpcPayloadCodec.SerializeToElement(new CsEvalArgs(source)),
                    },
                },
            },
            IpcJsonSerializerOptions.Default);
    }
}
