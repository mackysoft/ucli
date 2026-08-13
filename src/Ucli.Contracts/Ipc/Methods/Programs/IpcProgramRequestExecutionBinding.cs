using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Projects;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Fixes every fact that identifies one same-generation Program request execution. </summary>
public sealed record IpcProgramRequestExecutionBinding
{
    [JsonConstructor]
    public IpcProgramRequestExecutionBinding (
        UnityProjectIdentity project,
        LifecycleExecutionHostRegistration host,
        UnityEditorGenerationSnapshot generation,
        DateTimeOffset deadlineUtc,
        Sha256Digest requestDigest,
        Sha256Digest planDigest,
        Sha256Digest? planTokenDigest,
        IReadOnlyList<Sha256Digest> operationDescriptorDigests,
        Sha256Digest authorizationDigest,
        Sha256Digest configurationDigest)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        Host = host ?? throw new ArgumentNullException(nameof(host));
        Generation = generation ?? throw new ArgumentNullException(nameof(generation));
        if (deadlineUtc == default || deadlineUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Deadline must be a UTC audit timestamp.", nameof(deadlineUtc));
        }

        RequestDigest = requestDigest ?? throw new ArgumentNullException(nameof(requestDigest));
        PlanDigest = planDigest ?? throw new ArgumentNullException(nameof(planDigest));
        PlanTokenDigest = planTokenDigest;
        OperationDescriptorDigests = Copy(operationDescriptorDigests, nameof(operationDescriptorDigests));
        AuthorizationDigest = authorizationDigest ?? throw new ArgumentNullException(nameof(authorizationDigest));
        ConfigurationDigest = configurationDigest ?? throw new ArgumentNullException(nameof(configurationDigest));
        DeadlineUtc = deadlineUtc;
    }

    [JsonInclude, JsonRequired] public UnityProjectIdentity Project { get; private init; }
    [JsonInclude, JsonRequired] public LifecycleExecutionHostRegistration Host { get; private init; }
    [JsonInclude, JsonRequired] public UnityEditorGenerationSnapshot Generation { get; private init; }
    [JsonInclude, JsonRequired] public DateTimeOffset DeadlineUtc { get; private init; }
    [JsonInclude, JsonRequired] public Sha256Digest RequestDigest { get; private init; }
    [JsonInclude, JsonRequired] public Sha256Digest PlanDigest { get; private init; }
    [JsonInclude] public Sha256Digest? PlanTokenDigest { get; private init; }
    [JsonInclude, JsonRequired] public IReadOnlyList<Sha256Digest> OperationDescriptorDigests { get; private init; }
    [JsonInclude, JsonRequired] public Sha256Digest AuthorizationDigest { get; private init; }
    [JsonInclude, JsonRequired] public Sha256Digest ConfigurationDigest { get; private init; }

    private static IReadOnlyList<Sha256Digest> Copy (IReadOnlyList<Sha256Digest> values, string name)
    {
        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }
        var copy = new Sha256Digest[values.Count];
        for (var index = 0; index < values.Count; index++)
        {
            copy[index] = values[index] ?? throw new ArgumentException("Descriptor digests must not contain null.", name);
        }
        return Array.AsReadOnly(copy);
    }
}
