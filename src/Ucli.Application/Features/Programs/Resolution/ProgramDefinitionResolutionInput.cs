namespace MackySoft.Ucli.Application.Features.Programs.Resolution;

/// <summary> Identifies one closed root Program input and its reference boundary. </summary>
internal abstract record ProgramDefinitionResolutionInput
{
    /// <summary> Gets the decoded root Program JSON. </summary>
    public abstract string Json { get; }

    /// <summary> Gets the source recorded by the source manifest. </summary>
    public abstract ProgramRootSource RootSource { get; }

    /// <summary> Gets the file path recorded by the source manifest, when applicable. </summary>
    public virtual AbsolutePath? RootPath => null;

    /// <summary> Gets the preset identifier recorded by the source manifest, when applicable. </summary>
    public virtual string? PresetId => null;

    /// <summary> Gets the boundary used for referenced request documents, when available. </summary>
    public virtual AbsolutePath? ReferenceRoot => null;
}

/// <summary> Identifies Program JSON received from standard input. </summary>
internal sealed record StdinProgramDefinitionResolutionInput : ProgramDefinitionResolutionInput
{
    /// <summary> Initializes a standard-input Program definition. </summary>
    public StdinProgramDefinitionResolutionInput (string json)
    {
        Json = json;
    }

    /// <inheritdoc />
    public override string Json { get; }

    /// <inheritdoc />
    public override ProgramRootSource RootSource => ProgramRootSource.Stdin;
}

/// <summary> Identifies Program JSON read from one physical file selection. </summary>
internal sealed record FileProgramDefinitionResolutionInput : ProgramDefinitionResolutionInput
{
    /// <summary> Initializes a file Program definition from one root-file receipt. </summary>
    public FileProgramDefinitionResolutionInput (ProgramDefinitionRootFileReceipt receipt)
    {
        Receipt = receipt ?? throw new ArgumentNullException(nameof(receipt));
    }

    /// <summary> Gets the receipt that owns the root document and path facts. </summary>
    public ProgramDefinitionRootFileReceipt Receipt { get; }

    /// <inheritdoc />
    public override string Json => Receipt.Json;

    /// <inheritdoc />
    public override ProgramRootSource RootSource => ProgramRootSource.File;

    /// <inheritdoc />
    public override AbsolutePath RootPath => Receipt.RequestedPath.Target;

    /// <inheritdoc />
    public override AbsolutePath ReferenceRoot => Receipt.RequestedParent;
}

/// <summary> Identifies Program JSON resolved from a preset file. </summary>
internal sealed record PresetProgramDefinitionResolutionInput : ProgramDefinitionResolutionInput
{
    /// <summary> Initializes a preset Program definition from one root-file receipt. </summary>
    public PresetProgramDefinitionResolutionInput (string id, ProgramDefinitionRootFileReceipt receipt)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Receipt = receipt ?? throw new ArgumentNullException(nameof(receipt));
    }

    /// <summary> Gets the preset identifier. </summary>
    public string Id { get; }

    /// <summary> Gets the receipt that owns the root document and path facts. </summary>
    public ProgramDefinitionRootFileReceipt Receipt { get; }

    /// <inheritdoc />
    public override string Json => Receipt.Json;

    /// <inheritdoc />
    public override ProgramRootSource RootSource => ProgramRootSource.Preset;

    /// <inheritdoc />
    public override string PresetId => Id;

    /// <inheritdoc />
    public override AbsolutePath ReferenceRoot => Receipt.PhysicalParent;
}

/// <summary> Identifies the origin of a Program root. </summary>
internal enum ProgramRootSource
{
    Stdin,
    File,
    Preset,
}
