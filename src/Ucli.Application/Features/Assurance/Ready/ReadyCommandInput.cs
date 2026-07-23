using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Represents normalized input for one <c>ready</c> command execution. </summary>
internal sealed record ReadyCommandInput
{
    public ReadyCommandInput (
        string? ProjectPath,
        ReadyTarget Target,
        UnityExecutionMode? Mode,
        int? TimeoutMilliseconds,
        ReadIndexMode? ReadIndexMode,
        bool IsReadIndexModeSpecified,
        bool FailFast)
    {
        if (!TextVocabulary.IsDefined(Target))
        {
            throw new ArgumentOutOfRangeException(nameof(Target), Target, "Ready target must be defined.");
        }

        this.ProjectPath = ProjectPath;
        this.Target = Target;
        this.Mode = Mode;
        this.TimeoutMilliseconds = TimeoutMilliseconds;
        this.ReadIndexMode = ReadIndexMode;
        this.IsReadIndexModeSpecified = IsReadIndexModeSpecified;
        this.FailFast = FailFast;
    }

    public string? ProjectPath { get; }

    public ReadyTarget Target { get; }

    public UnityExecutionMode? Mode { get; }

    public int? TimeoutMilliseconds { get; }

    public ReadIndexMode? ReadIndexMode { get; }

    public bool IsReadIndexModeSpecified { get; }

    public bool FailFast { get; }
}
