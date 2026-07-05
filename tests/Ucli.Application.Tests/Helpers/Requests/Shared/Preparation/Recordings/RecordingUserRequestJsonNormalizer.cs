using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class RecordingUserRequestJsonNormalizer : IUserRequestJsonNormalizer
{
    private readonly List<Invocation> invocations = [];

    public IReadOnlyList<Invocation> Invocations => invocations;

    public UserRequestJsonNormalizationResult? Result { get; set; }

    public UserRequestJsonNormalizationResult Normalize (string requestJson)
    {
        invocations.Add(new Invocation(requestJson));

        if (Result is null)
        {
            throw new InvalidOperationException("User request JSON normalization result is not configured.");
        }

        return Result;
    }

    internal readonly record struct Invocation (string RequestJson);
}
