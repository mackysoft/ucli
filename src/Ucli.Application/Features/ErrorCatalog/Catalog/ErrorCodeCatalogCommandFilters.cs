namespace MackySoft.Ucli.Application.Features.ErrorCatalog.Catalog;

/// <summary> Defines command identifiers accepted by error-code catalog command filtering. </summary>
internal static class ErrorCodeCatalogCommandFilters
{
    private static readonly HashSet<UcliCommand> KnownCommandSet = new(UcliPublicCommandCatalog.KnownCommands);

    /// <summary> Determines whether the specified command identifier is accepted for catalog filtering. </summary>
    /// <param name="command"> The command identifier to check. </param>
    /// <returns> <see langword="true" /> when the identifier is a known command or command group; otherwise <see langword="false" />. </returns>
    public static bool Contains (UcliCommand command)
    {
        return command.IsValid && KnownCommandSet.Contains(command);
    }
}
