namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Defines the closed <c>build.run</c> stream event set. </summary>
public static class BuildRunProgressEventNames
{
    /// <summary> Gets the event emitted after build run identity, artifacts, and execution target are established. </summary>
    public const string Started = "build.started";

    /// <summary> Gets the event emitted after the final build payload has been built. </summary>
    public const string Completed = "build.completed";
}
