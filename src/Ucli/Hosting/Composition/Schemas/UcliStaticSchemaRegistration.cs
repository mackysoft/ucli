using System.Text.Json.Serialization.Metadata;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Schemas;

namespace MackySoft.Ucli.Hosting.Composition.Schemas;

/// <summary> Binds one product-owned schema identity to its effective serializer contract. </summary>
internal sealed record UcliStaticSchemaRegistration
{
    private UcliStaticSchemaRegistration (
        string name,
        RootRelativePath path,
        UcliStaticSchemaKind kind,
        JsonTypeInfo typeInfo,
        string? command,
        CommandResultStatus? status,
        bool hasOperationApplicationStateConstraints,
        LifecycleExecutionKind? lifecycleExecutionKind,
        IReadOnlyList<UcliStaticSchemaUsage> usages,
        IReadOnlyList<string> staticDependencies,
        IReadOnlyList<string> dynamicValidationSources)
    {
        Name = !string.IsNullOrWhiteSpace(name)
            ? name
            : throw new ArgumentException("Schema logical name must not be empty.", nameof(name));
        Path = path ?? throw new ArgumentNullException(nameof(path));
        Kind = kind;
        TypeInfo = typeInfo ?? throw new ArgumentNullException(nameof(typeInfo));
        Command = command;
        Status = status;
        HasOperationApplicationStateConstraints =
            hasOperationApplicationStateConstraints;
        LifecycleExecutionKind = lifecycleExecutionKind;
        Usages = Snapshot(usages);
        StaticDependencies = Snapshot(staticDependencies);
        DynamicValidationSources = Snapshot(dynamicValidationSources);
    }

    /// <summary> Gets the stable logical schema name. </summary>
    public string Name { get; }

    /// <summary> Gets the product-assigned provider contract identifier. </summary>
    public string ContractId => "ucli.schema/" + Name;

    /// <summary> Gets the guarded path relative to the static schema-set root. </summary>
    public RootRelativePath Path { get; }

    /// <summary> Gets the manifest delivery kind. </summary>
    public UcliStaticSchemaKind Kind { get; }

    /// <summary> Gets the matching CLI command, or null for a non-payload schema. </summary>
    public string? Command { get; }

    /// <summary> Gets the matching command-result status, or null for a non-payload schema. </summary>
    public CommandResultStatus? Status { get; }

    /// <summary>
    /// Gets whether this contract contains operation-only application-state
    /// constraints.
    /// </summary>
    public bool HasOperationApplicationStateConstraints { get; }

    /// <summary>
    /// Gets the Lifecycle Execution action whose CLI output contract is refined,
    /// or null when the payload is not action-specific.
    /// </summary>
    public LifecycleExecutionKind? LifecycleExecutionKind { get; }

    /// <summary> Gets the serializer contract used by the actual product boundary. </summary>
    public JsonTypeInfo TypeInfo { get; }

    /// <summary> Gets the public document delivery sites. </summary>
    public IReadOnlyList<UcliStaticSchemaUsage> Usages { get; }

    /// <summary> Gets direct static dependencies by logical name. </summary>
    public IReadOnlyList<string> StaticDependencies { get; }

    /// <summary> Gets dynamic validation sources. </summary>
    public IReadOnlyList<string> DynamicValidationSources { get; }

    /// <summary> Registers the schema-set manifest contract. </summary>
    public static UcliStaticSchemaRegistration SchemaSetMetadata (
        string name,
        RootRelativePath path,
        JsonTypeInfo typeInfo)
    {
        return CreateDocument(
            name,
            path,
            UcliStaticSchemaKind.SchemaSetMetadata,
            typeInfo);
    }

    /// <summary> Registers the common CLI result envelope contract. </summary>
    public static UcliStaticSchemaRegistration CliOutputEnvelope (
        string name,
        RootRelativePath path,
        JsonTypeInfo typeInfo)
    {
        return CreateDocument(
            name,
            path,
            UcliStaticSchemaKind.CliOutputEnvelope,
            typeInfo);
    }

    /// <summary> Registers a reusable common definition referenced by public schemas. </summary>
    public static UcliStaticSchemaRegistration CommonDefinition (
        string name,
        RootRelativePath path,
        JsonTypeInfo typeInfo)
    {
        return CreateDocument(
            name,
            path,
            UcliStaticSchemaKind.CommonDefinition,
            typeInfo);
    }

    /// <summary> Registers one command payload contract with its command and status identity. </summary>
    public static UcliStaticSchemaRegistration CliOutputPayload (
        string name,
        RootRelativePath path,
        JsonTypeInfo typeInfo,
        string command,
        CommandResultStatus status)
    {
        return CreateCliOutputPayload(
            name,
            path,
            typeInfo,
            command,
            status,
            hasOperationApplicationStateConstraints: false);
    }

    /// <summary>
    /// Registers one operation-execution command payload with its operation-only
    /// application-state constraints.
    /// </summary>
    public static UcliStaticSchemaRegistration OperationExecutionCliOutputPayload (
        string name,
        RootRelativePath path,
        JsonTypeInfo typeInfo,
        string command,
        CommandResultStatus status)
    {
        return CreateCliOutputPayload(
            name,
            path,
            typeInfo,
            command,
            status,
            hasOperationApplicationStateConstraints: true);
    }

    private static UcliStaticSchemaRegistration CreateCliOutputPayload (
        string name,
        RootRelativePath path,
        JsonTypeInfo typeInfo,
        string command,
        CommandResultStatus status,
        bool hasOperationApplicationStateConstraints)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        if (!TextVocabulary.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Command result status must be defined.");
        }

        return new UcliStaticSchemaRegistration(
            name,
            path,
            UcliStaticSchemaKind.CliOutputPayload,
            typeInfo,
            command,
            status,
            hasOperationApplicationStateConstraints,
            lifecycleExecutionKind: null,
            Array.Empty<UcliStaticSchemaUsage>(),
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    /// <summary>
    /// Registers one action-specific Lifecycle Execution command payload.
    /// </summary>
    public static UcliStaticSchemaRegistration LifecycleExecutionCliOutputPayload (
        string name,
        RootRelativePath path,
        JsonTypeInfo typeInfo,
        string command,
        LifecycleExecutionKind executionKind,
        CommandResultStatus status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        if (!TextVocabulary.IsDefined(executionKind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(executionKind),
                executionKind,
                "Lifecycle Execution kind must be defined.");
        }
        if (!TextVocabulary.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Command result status must be defined.");
        }
        if (!string.Equals(
            command,
            TextVocabulary.GetText(executionKind),
            StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The command must match the registered Lifecycle Execution kind.",
                nameof(command));
        }

        return new UcliStaticSchemaRegistration(
            name,
            path,
            UcliStaticSchemaKind.CliOutputPayload,
            typeInfo,
            command,
            status,
            hasOperationApplicationStateConstraints: false,
            lifecycleExecutionKind: executionKind,
            Array.Empty<UcliStaticSchemaUsage>(),
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    /// <summary> Registers one user-input document contract with its delivery relationships. </summary>
    public static UcliStaticSchemaRegistration UserInputDocument (
        string name,
        RootRelativePath path,
        JsonTypeInfo typeInfo,
        IReadOnlyList<UcliStaticSchemaUsage> usages,
        IReadOnlyList<string> staticDependencies,
        IReadOnlyList<string> dynamicValidationSources)
    {
        ArgumentNullException.ThrowIfNull(usages);
        ArgumentNullException.ThrowIfNull(staticDependencies);
        ArgumentNullException.ThrowIfNull(dynamicValidationSources);
        return new UcliStaticSchemaRegistration(
            name,
            path,
            UcliStaticSchemaKind.UserInputDocument,
            typeInfo,
            command: null,
            status: null,
            hasOperationApplicationStateConstraints: false,
            lifecycleExecutionKind: null,
            usages,
            staticDependencies,
            dynamicValidationSources);
    }

    private static UcliStaticSchemaRegistration CreateDocument (
        string name,
        RootRelativePath path,
        UcliStaticSchemaKind kind,
        JsonTypeInfo typeInfo)
    {
        return new UcliStaticSchemaRegistration(
            name,
            path,
            kind,
            typeInfo,
            command: null,
            status: null,
            hasOperationApplicationStateConstraints: false,
            lifecycleExecutionKind: null,
            Array.Empty<UcliStaticSchemaUsage>(),
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private static IReadOnlyList<T> Snapshot<T> (IReadOnlyList<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.Count == 0
            ? Array.Empty<T>()
            : Array.AsReadOnly(values.ToArray());
    }
}
