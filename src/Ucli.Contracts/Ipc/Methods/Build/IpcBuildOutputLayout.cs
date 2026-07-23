using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents the command-derived BuildPipeline output layout for one <c>build.run</c> request. </summary>
public sealed record IpcBuildOutputLayout
{
    /// <summary> Initializes one BuildPipeline output layout. </summary>
    [JsonConstructor]
    public IpcBuildOutputLayout (
        IpcBuildOutputLayoutShape Shape,
        string LocationPathName)
    {
        if (!TextVocabulary.IsDefined(Shape))
        {
            throw new ArgumentOutOfRangeException(nameof(Shape), Shape, "Build output layout shape must be specified.");
        }

        this.Shape = Shape;
        this.LocationPathName = ContractArgumentGuard.RequireValue(LocationPathName, nameof(LocationPathName));
    }

    public IpcBuildOutputLayoutShape Shape { get; }

    public string LocationPathName { get; }
}
