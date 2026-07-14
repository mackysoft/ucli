using MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Requests;

namespace MackySoft.Ucli.Tests.Hosting.Cli.Requests.Query;

public sealed class QueryAssetsFindOperationRequestFactoryTests
{
    private const string CommandName = "query assets find";

    private const string OperationId = "query.assets.find";

    private const string OperationName = "ucli.assets.find";

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenNoFilterIsProvided_ReturnsInvalidArgument ()
    {
        var result = Create(
            type: null,
            pathPrefix: null,
            nameContains: null);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Operation);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Contains("at least one filter", result.Error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(" type-id", "type", "leading or trailing whitespace")]
    [InlineData("Assets/ ", "pathPrefix", null)]
    [InlineData(" Player", "nameContains", "leading or trailing whitespace")]
    public void Create_WhenFilterContainsOuterWhitespace_ReturnsInvalidArgument (
        string value,
        string expectedOptionName,
        string? expectedDetail)
    {
        var result = Create(
            type: expectedOptionName == "type" ? value : "Texture2D",
            pathPrefix: expectedOptionName == "pathPrefix" ? value : null,
            nameContains: expectedOptionName == "nameContains" ? value : null);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Contains($"--{expectedOptionName}", result.Error.Message, StringComparison.Ordinal);
        if (expectedDetail is not null)
        {
            Assert.Contains(expectedDetail, result.Error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenFiltersAreValid_ReturnsNormalizedOperationWithDefaultWindow ()
    {
        var result = Create(
            type: "Texture2D",
            pathPrefix: "Assets/UI",
            nameContains: "Button");

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        var operation = Assert.IsType<QueryAssetsFindOperationRequest>(result.Operation);
        Assert.Equal(CommandName, operation.CommandName);
        Assert.Equal(OperationId, operation.OperationId);
        Assert.Equal(OperationName, operation.OperationName);
        Assert.Equal("Texture2D", operation.Query.TypeId!.Value);
        Assert.Equal("Assets/UI", operation.Query.PathPrefix!.Value);
        Assert.Equal("Button", operation.Query.NameContains);
        Assert.False(operation.WindowOptions.All);
        Assert.Equal(BoundedWindowConstants.DefaultLimit, operation.WindowOptions.Limit);
        Assert.Null(operation.WindowOptions.Cursor);
        Assert.Equal(0, operation.WindowOptions.Offset);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenAllIsTrue_ReturnsUnboundedWindow ()
    {
        var result = Create(
            all: true,
            limit: null);

        Assert.True(result.IsSuccess);
        var operation = Assert.IsType<QueryAssetsFindOperationRequest>(result.Operation);
        Assert.True(operation.WindowOptions.All);
        Assert.Equal(0, operation.WindowOptions.Limit);
        Assert.Null(operation.WindowOptions.Cursor);
        Assert.Equal(0, operation.WindowOptions.Offset);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenLimitIsProvided_ReturnsBoundedWindow ()
    {
        var result = Create(limit: 42);

        Assert.True(result.IsSuccess);
        var operation = Assert.IsType<QueryAssetsFindOperationRequest>(result.Operation);
        Assert.False(operation.WindowOptions.All);
        Assert.Equal(42, operation.WindowOptions.Limit);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenAfterCursorIsValid_ReturnsDecodedOffset ()
    {
        var cursor = BoundedWindowCursorCodec.Encode(123);

        var result = Create(
            limit: 50,
            after: cursor);

        Assert.True(result.IsSuccess);
        var operation = Assert.IsType<QueryAssetsFindOperationRequest>(result.Operation);
        Assert.Equal(cursor, operation.WindowOptions.Cursor);
        Assert.Equal(123, operation.WindowOptions.Offset);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(true, 10, null, "'--all' cannot be combined")]
    [InlineData(false, 0, null, "limit must be between")]
    [InlineData(false, BoundedWindowConstants.MaxLimit + 1, null, "limit must be between")]
    [InlineData(false, null, "not-a-cursor", "after cursor is invalid")]
    [InlineData(false, null, "outer-whitespace", "after cursor is invalid")]
    [InlineData(true, null, "valid-cursor", "'--all' cannot be combined")]
    public void Create_WhenWindowOptionsAreInvalid_ReturnsWindowingError (
        bool all,
        int? limit,
        string? afterCase,
        string expectedMessage)
    {
        var result = Create(
            all: all,
            limit: limit,
            after: CreateAfterCursor(afterCase));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Contains(expectedMessage, result.Error.Message, StringComparison.Ordinal);
    }

    private static QueryAssetsFindOperationRequestCreationResult Create (
        string? type = "Texture2D",
        string? pathPrefix = null,
        string? nameContains = null,
        bool all = false,
        int? limit = null,
        string? after = null)
    {
        return QueryAssetsFindOperationRequestFactory.Create(
            CommandName,
            OperationId,
            OperationName,
            type,
            pathPrefix,
            nameContains,
            all,
            limit,
            after);
    }

    private static string? CreateAfterCursor (string? afterCase)
    {
        return afterCase switch
        {
            null => null,
            "valid-cursor" => BoundedWindowCursorCodec.Encode(1),
            "outer-whitespace" => " " + BoundedWindowCursorCodec.Encode(1),
            _ => afterCase,
        };
    }
}
