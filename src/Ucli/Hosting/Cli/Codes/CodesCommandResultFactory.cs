using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Codes;

/// <summary> Creates command-level JSON results for <c>codes</c> commands. </summary>
internal static class CodesCommandResultFactory
{
    /// <summary> Creates one command result for <c>codes list</c>. </summary>
    /// <param name="result"> The application list result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult CreateList (CodeCatalogListResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess
            ? CommandResult.Success(
                UcliCommandNames.CodesList,
                "Code catalog returned.",
                CodeCatalogPayloadProjector.CreateListPayload(result))
            : CommandResultFactory.FromExecutionError(UcliCommandNames.CodesList, result.Error!);
    }

    /// <summary> Creates one command result for <c>codes describe</c>. </summary>
    /// <param name="result"> The application describe result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult CreateDescribe (CodeCatalogDescribeResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess
            ? CommandResult.Success(
                UcliCommandNames.CodesDescribe,
                "Code description returned.",
                CodeCatalogPayloadProjector.CreateDescribePayload(result))
            : CommandResultFactory.FromExecutionError(UcliCommandNames.CodesDescribe, result.Error!);
    }
}
