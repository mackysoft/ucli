using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Contracts.Schemas;

/// <summary> Identifies the static schema set carried by a uCLI package. </summary>
[VocabularyDefinition]
public enum UcliStaticSchemaSetName
{
    [VocabularyText("ucli")]
    Ucli = 0,
}

/// <summary> Identifies the JSON Schema dialect used by the static schema set. </summary>
[VocabularyDefinition]
public enum UcliJsonSchemaDialect
{
    [VocabularyText("https://json-schema.org/draft/2020-12/schema")]
    Draft202012 = 0,
}

/// <summary> Classifies one static schema by its delivery responsibility. </summary>
[VocabularyDefinition]
public enum UcliStaticSchemaKind
{
    [VocabularyText("schemaSetMetadata")]
    SchemaSetMetadata = 0,

    [VocabularyText("cliOutputEnvelope")]
    CliOutputEnvelope,

    [VocabularyText("cliOutputPayload")]
    CliOutputPayload,

    [VocabularyText("commonDefinition")]
    CommonDefinition,

    [VocabularyText("userInputDocument")]
    UserInputDocument,
}

/// <summary> Identifies how a user-authored document crosses a public CLI boundary. </summary>
[VocabularyDefinition]
public enum UcliStaticSchemaDelivery
{
    [VocabularyText("standardInput")]
    StandardInput = 0,

    [VocabularyText("optionFile")]
    OptionFile,

    [VocabularyText("fixedProjectPath")]
    FixedProjectPath,

    [VocabularyText("generatedFile")]
    GeneratedFile,

    [VocabularyText("generatedFixedProjectPath")]
    GeneratedFixedProjectPath,
}

/// <summary> Indexes the static JSON Schema documents distributed with one uCLI package. </summary>
[Title("uCLI static schema manifest")]
[Description("Indexes the exact static JSON Schema files distributed with the installed uCLI package.")]
public readonly record struct UcliStaticSchemaManifest
{
    /// <summary> Initializes an empty value for JSON deserialization. </summary>
    public UcliStaticSchemaManifest ()
    {
        PackageVersion = string.Empty;
        Schemas = Array.Empty<UcliStaticSchemaEntry>();
    }

    /// <summary> Gets the fixed schema-set identifier. </summary>
    [Description("Fixed identifier for the uCLI static schema set.")]
    [JsonRequired]
    public UcliStaticSchemaSetName SchemaSet { get; init; }

    /// <summary> Gets the uCLI package version that carries this schema set. </summary>
    [Description("Version of the MackySoft.Ucli package that carries this schema set.")]
    [JsonRequired]
    public string PackageVersion { get; init; } = string.Empty;

    /// <summary> Gets the IPC and public execution-result protocol version. </summary>
    [Description("IPC and public execution-result protocol version implemented by the same uCLI package.")]
    [JsonRequired]
    public int ProtocolVersion { get; init; }

    /// <summary> Gets the JSON Schema dialect used by every document in the set. </summary>
    [Description("JSON Schema dialect used by every document in the set.")]
    [JsonRequired]
    public UcliJsonSchemaDialect JsonSchemaDialect { get; init; }

    /// <summary> Gets schema entries ordered by logical name. </summary>
    [Description("Schema entries ordered by logical name using Unicode code-point order.")]
    [JsonRequired]
    public IReadOnlyList<UcliStaticSchemaEntry> Schemas { get; init; } = Array.Empty<UcliStaticSchemaEntry>();
}

/// <summary> Identifies one static JSON Schema document and its delivery relationships. </summary>
[Description("Identifies one static JSON Schema document and its delivery relationships.")]
public sealed record UcliStaticSchemaEntry
{
    /// <summary> Gets the stable logical schema name. </summary>
    [Length(1, 256)]
    [Description("Stable logical name used by ucli schema get.")]
    [JsonRequired]
    public string Name { get; init; } = string.Empty;

    /// <summary> Gets the document's delivery classification. </summary>
    [Description("Delivery and validation responsibility of the schema document.")]
    [JsonRequired]
    public UcliStaticSchemaKind Kind { get; init; }

    /// <summary> Gets the normalized path relative to the static schema-set root. </summary>
    [Length(1, 512)]
    [Description("Normalized slash-separated path relative to the static schema-set root.")]
    [JsonRequired]
    public string Path { get; init; } = string.Empty;

    /// <summary> Gets the canonical schema identifier. </summary>
    [JsonPropertyName("$id")]
    [Description("Canonical identifier that exactly matches the schema document's $id.")]
    [JsonRequired]
    public string Id { get; init; } = string.Empty;

    /// <summary> Gets the SHA-256 digest of the exact schema file bytes. </summary>
    [Description("SHA-256 of the exact schema file bytes as 64 lowercase hexadecimal characters.")]
    [JsonRequired]
    public Sha256Digest Sha256 { get; init; } = null!;

    /// <summary> Gets the matching command for a CLI output payload schema. </summary>
    [Description("Matching CommandResult command for a CLI output payload schema; null for other kinds.")]
    [JsonRequired]
    public string? Command { get; init; }

    /// <summary> Gets the matching result status for a CLI output payload schema. </summary>
    [Description("Matching CommandResult status for a CLI output payload schema; null for other kinds.")]
    [JsonRequired]
    public CommandResultStatus? Status { get; init; }

    /// <summary> Gets public command delivery sites for user-authored documents. </summary>
    [Description("Public command delivery sites for user-authored documents.")]
    [JsonRequired]
    public IReadOnlyList<UcliStaticSchemaUsage> Usages { get; init; } = Array.Empty<UcliStaticSchemaUsage>();

    /// <summary> Gets directly required static schema logical names. </summary>
    [Description("Logical names of directly required documents in the same static schema set.")]
    [JsonRequired]
    public IReadOnlyList<string> StaticDependencies { get; init; } = Array.Empty<string>();

    /// <summary> Gets dynamic catalog commands used after static validation. </summary>
    [Description("Public catalog commands used for dynamic validation after static structure validation.")]
    [JsonRequired]
    public IReadOnlyList<string> DynamicValidationSources { get; init; } = Array.Empty<string>();
}

/// <summary> Describes one public command delivery site for a user-authored JSON document. </summary>
[Description("Describes one public command delivery site for a user-authored JSON document.")]
public sealed record UcliStaticSchemaUsage
{
    /// <summary> Gets the public command that consumes or creates the document. </summary>
    [Length(1, 256)]
    [Description("Public command that consumes or creates the document.")]
    [JsonRequired]
    public string Command { get; init; } = string.Empty;

    /// <summary> Gets the document delivery method. </summary>
    [Description("Method by which the command consumes or creates the document.")]
    [JsonRequired]
    public UcliStaticSchemaDelivery Delivery { get; init; }

    /// <summary> Gets the option name or fixed relative path used to locate the document. </summary>
    [Description("Option name or fixed relative path used to locate the document; null for standard input.")]
    [JsonRequired]
    public string? Locator { get; init; }
}
