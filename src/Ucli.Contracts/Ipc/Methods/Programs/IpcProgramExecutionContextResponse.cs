using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Returns one same-host Editor generation observation for Program registration. </summary>
public sealed record IpcProgramExecutionContextResponse
{
    /// <summary> Initializes the response from the host identity and a current generation snapshot. </summary>
    [JsonConstructor]
    public IpcProgramExecutionContextResponse (
        LifecycleExecutionHostRegistration host,
        UnityEditorGenerationSnapshot generation,
        IpcProgramEffectiveAuthorizationSnapshot authorization,
        IpcProgramEffectiveConfigurationSnapshot configuration)
    {
        Host = host ?? throw new ArgumentNullException(nameof(host));
        Generation = generation ?? throw new ArgumentNullException(nameof(generation));
        Authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary> Gets the fixed host registration observed by the responding endpoint. </summary>
    [JsonInclude]
    [JsonRequired]
    public LifecycleExecutionHostRegistration Host { get; private init; }

    /// <summary> Gets the current Editor generation of that same host. </summary>
    [JsonInclude]
    [JsonRequired]
    public UnityEditorGenerationSnapshot Generation { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public IpcProgramEffectiveAuthorizationSnapshot Authorization { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public IpcProgramEffectiveConfigurationSnapshot Configuration { get; private init; }
}
