namespace MackySoft.Ucli.Hosting.Cli.Play.Contracts;

/// <summary> Defines the closed Play Mode transition error payload branches. </summary>
[VocabularyDefinition]
internal enum PlayTransitionErrorPayloadKind
{
    [VocabularyText("empty")]
    Empty = 0,

    [VocabularyText("start")]
    Start = 1,

    [VocabularyText("transitionFailure")]
    TransitionFailure = 2,

    [VocabularyText("terminalFailure")]
    TerminalFailure = 3,

    [VocabularyText("terminalPublicationFailure")]
    TerminalPublicationFailure = 4,
}
