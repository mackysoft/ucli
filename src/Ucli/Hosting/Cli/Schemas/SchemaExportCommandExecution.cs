using MackySoft.FileSystem;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;

namespace MackySoft.Ucli.Hosting.Cli.Schemas;

/// <summary> Resolves and executes the filesystem boundary owned by <c>schema export</c>. </summary>
internal static class SchemaExportCommandExecution
{
    public static CommandResult Export (
        UcliStaticSchemaSet schemaSet,
        AbsolutePath destination)
    {
        try
        {
            var export = UcliStaticSchemaSetExporter.Export(schemaSet, destination);
            return CreateSuccess(schemaSet, export);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException)
        {
            return CommandResult.InvalidArgument(
                UcliCommandNames.SchemaExport,
                exception.Message);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return CommandResult.InternalError(
                UcliCommandNames.SchemaExport,
                $"Static schema export failed. {exception.Message}");
        }
    }

    private static CommandResult CreateSuccess (
        UcliStaticSchemaSet schemaSet,
        UcliStaticSchemaExportResult export)
    {
        return CommandResult.Success(
            UcliCommandNames.SchemaExport,
            "Installed static schemas exported.",
            new UcliSchemaExportPayload(
                export.Destination.Value,
                schemaSet.Manifest.SchemaSet,
                schemaSet.Manifest.PackageVersion,
                export.FileCount));
    }
}
