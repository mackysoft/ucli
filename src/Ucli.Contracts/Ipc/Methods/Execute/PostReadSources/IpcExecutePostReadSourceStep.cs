using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one public step source fact used by post-read verification. </summary>
/// <param name="OpId"> The public step identifier matching <c>opResults[].opId</c>. </param>
/// <param name="SourceKind"> The public mutation source kind. </param>
/// <param name="PlayModeMutation"> Whether the step mutated Play Mode state. </param>
/// <param name="Commit"> The requested edit commit kind, or <see langword="null" /> when not applicable. </param>
/// <param name="PersistenceExpected"> Whether the source is expected to touch a persistence unit when it changes state. </param>
/// <param name="ExpectedPostState"> The expected post-state availability for this source. </param>
public sealed record IpcExecutePostReadSourceStep
{
    /// <summary> Initializes one post-read source fact. </summary>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when a non-null literal-backed enum value is not defined by the contract. </exception>
    [JsonConstructor]
    public IpcExecutePostReadSourceStep (
        IpcExecuteStepId OpId,
        IpcExecutePostReadSourceKind SourceKind,
        bool PlayModeMutation,
        IpcExecutePostReadCommit? Commit,
        bool PersistenceExpected,
        IpcExecuteExpectedPostState ExpectedPostState)
    {
        if (!TextVocabulary.IsDefined(SourceKind))
        {
            throw new ArgumentOutOfRangeException(nameof(SourceKind), SourceKind, "Post-read source kind must be specified.");
        }

        if (Commit.HasValue && !TextVocabulary.IsDefined(Commit.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(Commit), Commit, "Post-read commit must be a contract value when specified.");
        }

        if (!TextVocabulary.IsDefined(ExpectedPostState))
        {
            throw new ArgumentOutOfRangeException(nameof(ExpectedPostState), ExpectedPostState, "Expected post-state must be specified.");
        }

        this.OpId = OpId ?? throw new ArgumentNullException(nameof(OpId));
        this.SourceKind = SourceKind;
        this.PlayModeMutation = PlayModeMutation;
        this.Commit = Commit;
        this.PersistenceExpected = PersistenceExpected;
        this.ExpectedPostState = ExpectedPostState;
    }

    public IpcExecuteStepId OpId { get; }

    public IpcExecutePostReadSourceKind SourceKind { get; }

    [JsonInclude]
    [JsonRequired]
    public bool PlayModeMutation { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public IpcExecutePostReadCommit? Commit { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public bool PersistenceExpected { get; private init; }

    public IpcExecuteExpectedPostState ExpectedPostState { get; }
}
