namespace MackySoft.Ucli.Hosting.Cli.Schemas;

/// <summary> Loads the static schema set distributed with the running uCLI package. </summary>
internal interface IInstalledStaticSchemaSetProvider
{
    /// <summary> Loads and validates the complete installed static schema set. </summary>
    /// <returns> The validated static schema set. </returns>
    UcliStaticSchemaSet Load ();
}
