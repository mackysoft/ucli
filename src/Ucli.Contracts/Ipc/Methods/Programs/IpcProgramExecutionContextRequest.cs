using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Requests immutable host facts and independently verified authorization for one Program Run. </summary>
public sealed record IpcProgramExecutionContextRequest
{
    [JsonConstructor]
    public IpcProgramExecutionContextRequest (IpcProgramEffectiveAuthorizationSnapshot authorization)
    {
        Authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
    }

    [JsonInclude]
    [JsonRequired]
    public IpcProgramEffectiveAuthorizationSnapshot Authorization { get; private init; }
}
