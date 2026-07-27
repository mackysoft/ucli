namespace MackySoft.Ucli.Hosting.Cli.Codes;

/// <summary> Identifies the source that supplied one public code catalog. </summary>
[VocabularyDefinition]
internal enum CodeCatalogSource
{
    /// <summary> The catalog was bundled with this uCLI installation. </summary>
    [VocabularyText("bundled")]
    Bundled = 0,
}
