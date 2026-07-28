using System.Text.Json.Serialization.Metadata;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Schemas;

namespace MackySoft.Ucli.Hosting.Composition.Schemas;

/// <summary> Binds one product-owned schema identity to its effective serializer contract. </summary>
internal sealed record UcliStaticSchemaRegistration
{
    /// <summary> Initializes one static schema registration. </summary>
    public UcliStaticSchemaRegistration (
        string name,
        RootRelativePath path,
        UcliStaticSchemaKind kind,
        JsonTypeInfo typeInfo,
        UcliStaticSchemaManifestMetadata? manifestMetadata = null)
    {
        Name = !string.IsNullOrWhiteSpace(name)
            ? name
            : throw new ArgumentException("Schema logical name must not be empty.", nameof(name));
        Path = path ?? throw new ArgumentNullException(nameof(path));
        Kind = kind;
        TypeInfo = typeInfo ?? throw new ArgumentNullException(nameof(typeInfo));
        Command = manifestMetadata?.Command;
        Status = manifestMetadata?.Status;
        Usages = Snapshot(manifestMetadata?.Usages);
        StaticDependencies = Snapshot(manifestMetadata?.StaticDependencies);
        DynamicValidationSources = Snapshot(manifestMetadata?.DynamicValidationSources);
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

    /// <summary> Gets the serializer contract used by the actual product boundary. </summary>
    public JsonTypeInfo TypeInfo { get; }

    /// <summary> Gets the public document delivery sites. </summary>
    public IReadOnlyList<UcliStaticSchemaUsage> Usages { get; }

    /// <summary> Gets direct static dependencies by logical name. </summary>
    public IReadOnlyList<string> StaticDependencies { get; }

    /// <summary> Gets dynamic validation sources. </summary>
    public IReadOnlyList<string> DynamicValidationSources { get; }

    private static IReadOnlyList<T> Snapshot<T> (IReadOnlyList<T>? values)
    {
        return values is null
            ? Array.Empty<T>()
            : Array.AsReadOnly(values.ToArray());
    }
}

/// <summary> Describes the optional product delivery relationships recorded in the schema manifest. </summary>
internal sealed record UcliStaticSchemaManifestMetadata (
    string? Command,
    CommandResultStatus? Status,
    IReadOnlyList<UcliStaticSchemaUsage>? Usages = null,
    IReadOnlyList<string>? StaticDependencies = null,
    IReadOnlyList<string>? DynamicValidationSources = null);
