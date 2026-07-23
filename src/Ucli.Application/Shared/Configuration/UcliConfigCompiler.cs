using System.Text.Json;
using MackySoft.Ucli.Contracts.Configuration;

using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Shared.Configuration;

/// <summary> Compiles config JSON values and creates serializable config documents. </summary>
internal sealed class UcliConfigCompiler
{
    private const string UnsupportedSchemaVersionCode = "config.save.unsupportedSchemaVersion";
    private const string UnsupportedEnumCode = "config.save.unsupportedEnum";
    private const string NullAllowlistCode = "config.save.nullAllowlist";
    private const string EmptyAllowlistPatternCode = "config.save.emptyAllowlistPattern";
    private const string InvalidRegexPatternCode = "config.save.invalidRegexPattern";
    private const string InvalidTimeoutCode = "config.save.invalidTimeout";
    private const string NullTimeoutOverridesCode = "config.save.nullTimeoutOverrides";
    private const string UnsupportedTimeoutCommandCode = "config.save.unsupportedTimeoutCommand";

    private readonly UcliConfigSchemaValidator schemaValidator;

    private readonly UcliEffectiveConfigBuilder effectiveConfigBuilder;

    /// <summary> Initializes a new instance of the <see cref="UcliConfigCompiler" /> class. </summary>
    /// <param name="schemaValidator"> The schema validator dependency. </param>
    /// <param name="effectiveConfigBuilder"> The effective config builder dependency. </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="schemaValidator" /> or <paramref name="effectiveConfigBuilder" /> is <see langword="null" />.
    /// </exception>
    public UcliConfigCompiler (
        UcliConfigSchemaValidator schemaValidator,
        UcliEffectiveConfigBuilder effectiveConfigBuilder)
    {
        this.schemaValidator = schemaValidator ?? throw new ArgumentNullException(nameof(schemaValidator));
        this.effectiveConfigBuilder = effectiveConfigBuilder ?? throw new ArgumentNullException(nameof(effectiveConfigBuilder));
    }

    /// <summary> Creates a compiler with default dependencies. </summary>
    /// <returns> The created compiler. </returns>
    public static UcliConfigCompiler CreateDefault ()
    {
        return new UcliConfigCompiler(
            new UcliConfigSchemaValidator(),
            new UcliEffectiveConfigBuilder());
    }

    /// <summary> Compiles one config JSON root into effective config values. </summary>
    /// <param name="root"> The config JSON root element. </param>
    /// <param name="sourcePath"> The source config path used in diagnostics. </param>
    /// <returns> The config build result. </returns>
    public UcliConfigBuildResult Compile (
        JsonElement root,
        string sourcePath)
    {
        var schemaResult = schemaValidator.Validate(root, sourcePath);
        if (!schemaResult.IsSuccess)
        {
            return UcliConfigBuildResult.Failure(schemaResult.Diagnostics);
        }

        return effectiveConfigBuilder.Build(schemaResult.Document!.Value, sourcePath);
    }

    /// <summary> Builds a serializable config document from typed config values. </summary>
    /// <param name="config"> The typed config values. </param>
    /// <param name="sourcePath"> The destination config path used in diagnostics. </param>
    /// <returns> The document build result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="config" /> is <see langword="null" />. </exception>
    public UcliConfigDocumentBuildResult CreateDocument (
        UcliConfig config,
        string sourcePath)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var diagnostics = ValidateConfigForSave(config, sourcePath);
        if (diagnostics.Count > 0)
        {
            return UcliConfigDocumentBuildResult.Failure(diagnostics);
        }

        var ipcTimeoutMillisecondsByCommand = IpcTimeoutConfigValidator.CreateSerializableCommandTimeoutOverrides(
            config.IpcTimeoutMillisecondsByCommand);

        return UcliConfigDocumentBuildResult.Success(new UcliConfigDocument(
            SchemaVersion: config.SchemaVersion,
            OperationPolicy: TextVocabulary.GetText(config.OperationPolicy),
            PlanTokenMode: TextVocabulary.GetText(config.PlanTokenMode),
            ReadIndexDefaultMode: TextVocabulary.GetText(config.ReadIndexDefaultMode),
            OperationAllowlist: config.OperationAllowlist.ToArray(),
            IpcDefaultTimeoutMilliseconds: config.IpcDefaultTimeoutMilliseconds,
            IpcTimeoutMillisecondsByCommand: ipcTimeoutMillisecondsByCommand));
    }

    private static IReadOnlyList<UcliConfigDiagnostic> ValidateConfigForSave (
        UcliConfig config,
        string sourcePath)
    {
        var diagnostics = new List<UcliConfigDiagnostic>();

        if (config.SchemaVersion != UcliConfig.CurrentSchemaVersion)
        {
            AddDiagnostic(diagnostics, CreateDiagnostic(
                UnsupportedSchemaVersionCode,
                UcliConfigJsonPropertyNames.SchemaVersion,
                sourcePath,
                $"Config schemaVersion must be {UcliConfig.CurrentSchemaVersion}. Actual: {config.SchemaVersion}."));
        }

        AddUnsupportedEnumDiagnostic(
            diagnostics,
            UcliConfigJsonPropertyNames.OperationPolicy,
            config.OperationPolicy,
            sourcePath);
        AddUnsupportedEnumDiagnostic(
            diagnostics,
            UcliConfigJsonPropertyNames.PlanTokenMode,
            config.PlanTokenMode,
            sourcePath);
        AddUnsupportedEnumDiagnostic(
            diagnostics,
            UcliConfigJsonPropertyNames.ReadIndexDefaultMode,
            config.ReadIndexDefaultMode,
            sourcePath);
        AddAllowlistDiagnostics(config.OperationAllowlist, sourcePath, diagnostics);
        AddTimeoutDiagnostics(config, sourcePath, diagnostics);

        return diagnostics;
    }

    private static void AddAllowlistDiagnostics (
        IReadOnlyList<string>? operationAllowlist,
        string sourcePath,
        List<UcliConfigDiagnostic> diagnostics)
    {
        if (operationAllowlist is null)
        {
            AddDiagnostic(diagnostics, CreateDiagnostic(
                NullAllowlistCode,
                UcliConfigJsonPropertyNames.OperationAllowlist,
                sourcePath,
                "Config operationAllowlist must not be null."));
            return;
        }

        UcliConfigOperationAllowlistValidator.AddSaveDiagnostics(
            operationAllowlist,
            sourcePath,
            EmptyAllowlistPatternCode,
            InvalidRegexPatternCode,
            diagnostics);
    }

    private static void AddTimeoutDiagnostics (
        UcliConfig config,
        string sourcePath,
        List<UcliConfigDiagnostic> diagnostics)
    {
        if (!IpcTimeoutConfigValidator.TryParseTimeoutMilliseconds(config.IpcDefaultTimeoutMilliseconds, out _))
        {
            AddDiagnostic(diagnostics, CreateDiagnostic(
                InvalidTimeoutCode,
                UcliConfigJsonPropertyNames.IpcDefaultTimeoutMilliseconds,
                sourcePath,
                $"Config ipcDefaultTimeoutMilliseconds must be a positive integer. Actual: {config.IpcDefaultTimeoutMilliseconds}."));
        }

        if (config.IpcTimeoutMillisecondsByCommand is null)
        {
            AddDiagnostic(diagnostics, CreateDiagnostic(
                NullTimeoutOverridesCode,
                UcliConfigJsonPropertyNames.IpcTimeoutMillisecondsByCommand,
                sourcePath,
                "Config ipcTimeoutMillisecondsByCommand must not be null."));
            return;
        }

        UcliConfigCommandTimeoutValidator.AddSaveDiagnostics(
            config.IpcTimeoutMillisecondsByCommand,
            sourcePath,
            UnsupportedTimeoutCommandCode,
            InvalidTimeoutCode,
            diagnostics);
    }

    private static void AddUnsupportedEnumDiagnostic<TEnum> (
        List<UcliConfigDiagnostic> diagnostics,
        string propertyPath,
        TEnum value,
        string sourcePath)
        where TEnum : struct, Enum
    {
        if (Enum.IsDefined(value))
        {
            return;
        }

        AddDiagnostic(diagnostics, CreateDiagnostic(
            UnsupportedEnumCode,
            propertyPath,
            sourcePath,
            $"Config {propertyPath} is unsupported: {value}."));
    }

    private static UcliConfigDiagnostic CreateDiagnostic (
        string code,
        string propertyPath,
        string sourcePath,
        string message)
    {
        return UcliConfigDiagnostic.Create(code, propertyPath, sourcePath, message);
    }

    private static bool AddDiagnostic (
        List<UcliConfigDiagnostic> diagnostics,
        UcliConfigDiagnostic diagnostic)
    {
        return UcliConfigDiagnosticList.Add(diagnostics, diagnostic);
    }
}
