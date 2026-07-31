using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Contracts.Execution.Lifecycle;

/// <summary>
/// Binds one Lifecycle Execution to an Editor process generation and its accepted endpoint registrations.
/// </summary>
public sealed record LifecycleExecutionHostRegistration
{
    /// <summary> Initializes one validated host registration. </summary>
    /// <param name="process"> The operating-system process generation hosting the Editor. </param>
    /// <param name="editorInstanceId"> The stable Editor instance identifier within that process. </param>
    /// <param name="firstEndpointRegistrationGenerationId">
    /// The first endpoint registration generation accepted for the execution.
    /// </param>
    /// <param name="currentEndpointRegistrationGenerationId">
    /// The latest proven successor endpoint registration generation.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="process" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// An identifier is empty.
    /// </exception>
    [JsonConstructor]
    public LifecycleExecutionHostRegistration (
        ProcessIdentity process,
        Guid editorInstanceId,
        Guid firstEndpointRegistrationGenerationId,
        Guid currentEndpointRegistrationGenerationId)
    {
        Process = process ?? throw new ArgumentNullException(nameof(process));
        EditorInstanceId = ContractArgumentGuard.RequireNonEmptyGuid(
            editorInstanceId,
            nameof(editorInstanceId));
        FirstEndpointRegistrationGenerationId = ContractArgumentGuard.RequireNonEmptyGuid(
            firstEndpointRegistrationGenerationId,
            nameof(firstEndpointRegistrationGenerationId));
        CurrentEndpointRegistrationGenerationId = ContractArgumentGuard.RequireNonEmptyGuid(
            currentEndpointRegistrationGenerationId,
            nameof(currentEndpointRegistrationGenerationId));
    }

    /// <summary> Gets the operating-system process generation hosting the Editor. </summary>
    [JsonInclude]
    [JsonRequired]
    public ProcessIdentity Process { get; private init; }

    /// <summary> Gets the stable Editor instance identifier within <see cref="Process" />. </summary>
    [JsonInclude]
    [JsonRequired]
    public Guid EditorInstanceId { get; private init; }

    /// <summary> Gets the first endpoint registration generation accepted for the execution. </summary>
    [JsonInclude]
    [JsonRequired]
    public Guid FirstEndpointRegistrationGenerationId { get; private init; }

    /// <summary> Gets the latest proven successor endpoint registration generation. </summary>
    [JsonInclude]
    [JsonRequired]
    public Guid CurrentEndpointRegistrationGenerationId { get; private init; }
}
