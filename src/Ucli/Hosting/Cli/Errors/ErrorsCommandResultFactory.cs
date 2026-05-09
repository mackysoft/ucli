using MackySoft.Ucli.Application.Features.ErrorCatalog.Catalog;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Errors;

/// <summary> Creates command-level JSON results for <c>errors</c> commands. </summary>
internal static class ErrorsCommandResultFactory
{
    /// <summary> Creates one command result for <c>errors list</c>. </summary>
    /// <param name="result"> The application list result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult CreateList (ErrorCodeCatalogListResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess
            ? CommandResult.Success(
                UcliCommandNames.ErrorsList,
                "Error code catalog returned.",
                ErrorCodeCatalogPayloadProjector.CreateListPayload(result))
            : CommandResultFactory.FromExecutionError(UcliCommandNames.ErrorsList, result.Error!);
    }

    /// <summary> Creates one command result for <c>errors describe</c>. </summary>
    /// <param name="result"> The application describe result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult CreateDescribe (ErrorCodeCatalogDescribeResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess
            ? CommandResult.Success(
                UcliCommandNames.ErrorsDescribe,
                "Error code description returned.",
                ErrorCodeCatalogPayloadProjector.CreateDescribePayload(result))
            : CommandResultFactory.FromExecutionError(UcliCommandNames.ErrorsDescribe, result.Error!);
    }
}
