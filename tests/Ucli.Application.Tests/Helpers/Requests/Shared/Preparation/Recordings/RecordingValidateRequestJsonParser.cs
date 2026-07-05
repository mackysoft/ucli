using MackySoft.Ucli.Application.Features.Requests.Shared.Validation.Parsing;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingValidateRequestJsonParser : IValidateRequestJsonParser
{
    private readonly List<Invocation> invocations = [];

    public IReadOnlyList<Invocation> Invocations => invocations;

    public ValidateRequestJsonParseResult? Result { get; set; }

    public ValidateRequestJsonParseResult Parse (string requestJson)
    {
        invocations.Add(new Invocation(requestJson));

        if (Result is null)
        {
            throw new InvalidOperationException("Validate request JSON parse result is not configured.");
        }

        return Result;
    }

    internal readonly record struct Invocation (string RequestJson);
}
