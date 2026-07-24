using MackySoft.Ucli.Application.Features.Play.Common.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Hosting.Cli.Play;

/// <summary> Creates public transition payload fragments for Play Mode commands. </summary>
internal static class PlayTransitionPayloadProjector
{
    /// <summary> Creates the JSON-serializable transition payload. </summary>
    /// <param name="transition"> The projected transition. </param>
    /// <returns> The anonymous payload object serialized by the command-result writer. </returns>
    public static object Create (PlayTransitionOutput transition)
    {
        ArgumentNullException.ThrowIfNull(transition);

        var transitionLiteral = TextVocabulary.GetText(transition.Transition);
        var resultLiteral = TextVocabulary.GetText(transition.Result);

        return transition.Result switch
        {
            IpcPlayTransitionOutcome.Entered
                or IpcPlayTransitionOutcome.AlreadyEntered
                or IpcPlayTransitionOutcome.Exited
                or IpcPlayTransitionOutcome.AlreadyExited => new
                {
                    transition = transitionLiteral,
                    result = resultLiteral,
                    transition.Before,
                    transition.After,
                },
            IpcPlayTransitionOutcome.Timeout or IpcPlayTransitionOutcome.Blocked => new
            {
                transition = transitionLiteral,
                result = resultLiteral,
                transition.Before,
                transition.Observed,
                applicationState = TextVocabulary.GetText(transition.ApplicationState!.Value),
            },
            _ => throw new ArgumentOutOfRangeException(nameof(PlayTransitionOutput.Result), transition.Result, null),
        };
    }
}
