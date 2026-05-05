using MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;
using MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query.Projection;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Tests.Features.Requests.Query.UseCases.Query;

public sealed class QueryAssetsFindOperationRequestFactoryTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenNoFilterIsProvided_ReturnsInvalidArgument ()
    {
        var result = QueryAssetsFindOperationRequestFactory.Create(
            commandName: "query assets find",
            operationId: "query.assets.find",
            operationName: "ucli.assets.find",
            type: null,
            pathPrefix: null,
            nameContains: null,
            all: false,
            limit: null,
            after: null);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Operation);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Contains("at least one filter", result.Error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(" type-id", "type")]
    [InlineData("Assets/ ", "pathPrefix")]
    [InlineData(" Player", "nameContains")]
    public void Create_WhenFilterContainsOuterWhitespace_ReturnsInvalidArgument (
        string value,
        string expectedOptionName)
    {
        var result = QueryAssetsFindOperationRequestFactory.Create(
            commandName: "query assets find",
            operationId: "query.assets.find",
            operationName: "ucli.assets.find",
            type: expectedOptionName == "type" ? value : "Texture2D",
            pathPrefix: expectedOptionName == "pathPrefix" ? value : null,
            nameContains: expectedOptionName == "nameContains" ? value : null,
            all: false,
            limit: null,
            after: null);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Contains($"--{expectedOptionName}", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("leading or trailing whitespace", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenFiltersAreValid_ReturnsNormalizedOperationWithDefaultWindow ()
    {
        var result = QueryAssetsFindOperationRequestFactory.Create(
            commandName: "query assets find",
            operationId: "query.assets.find",
            operationName: "ucli.assets.find",
            type: "Texture2D",
            pathPrefix: "Assets/UI",
            nameContains: "Button",
            all: false,
            limit: null,
            after: null);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        var operation = Assert.IsType<QueryAssetsFindOperationRequest>(result.Operation);
        Assert.Equal("query assets find", operation.CommandName);
        Assert.Equal("query.assets.find", operation.OperationId);
        Assert.Equal("ucli.assets.find", operation.OperationName);
        Assert.Equal("Texture2D", operation.Filter.TypeId);
        Assert.Equal("Assets/UI", operation.Filter.PathPrefix);
        Assert.Equal("Button", operation.Filter.NameContains);
        Assert.False(operation.WindowOptions.All);
        Assert.Equal(QueryWindowOptionsFactory.DefaultLimit, operation.WindowOptions.Limit);
        Assert.Null(operation.WindowOptions.After);
        Assert.Equal(0, operation.WindowOptions.Offset);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenAllIsCombinedWithLimit_ReturnsWindowingError ()
    {
        var result = QueryAssetsFindOperationRequestFactory.Create(
            commandName: "query assets find",
            operationId: "query.assets.find",
            operationName: "ucli.assets.find",
            type: "Texture2D",
            pathPrefix: null,
            nameContains: null,
            all: true,
            limit: 10,
            after: null);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Contains("'--all' cannot be combined", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenAllIsTrue_ReturnsUnboundedWindow ()
    {
        var result = QueryAssetsFindOperationRequestFactory.Create(
            commandName: "query assets find",
            operationId: "query.assets.find",
            operationName: "ucli.assets.find",
            type: "Texture2D",
            pathPrefix: null,
            nameContains: null,
            all: true,
            limit: null,
            after: null);

        Assert.True(result.IsSuccess);
        var operation = Assert.IsType<QueryAssetsFindOperationRequest>(result.Operation);
        Assert.True(operation.WindowOptions.All);
        Assert.Equal(0, operation.WindowOptions.Limit);
        Assert.Null(operation.WindowOptions.After);
        Assert.Equal(0, operation.WindowOptions.Offset);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenLimitIsProvided_ReturnsBoundedWindow ()
    {
        var result = QueryAssetsFindOperationRequestFactory.Create(
            commandName: "query assets find",
            operationId: "query.assets.find",
            operationName: "ucli.assets.find",
            type: "Texture2D",
            pathPrefix: null,
            nameContains: null,
            all: false,
            limit: 42,
            after: null);

        Assert.True(result.IsSuccess);
        var operation = Assert.IsType<QueryAssetsFindOperationRequest>(result.Operation);
        Assert.False(operation.WindowOptions.All);
        Assert.Equal(42, operation.WindowOptions.Limit);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenAfterCursorIsValid_ReturnsDecodedOffset ()
    {
        var cursor = QueryWindowCursorCodec.Encode(123);

        var result = QueryAssetsFindOperationRequestFactory.Create(
            commandName: "query assets find",
            operationId: "query.assets.find",
            operationName: "ucli.assets.find",
            type: "Texture2D",
            pathPrefix: null,
            nameContains: null,
            all: false,
            limit: 50,
            after: cursor);

        Assert.True(result.IsSuccess);
        var operation = Assert.IsType<QueryAssetsFindOperationRequest>(result.Operation);
        Assert.Equal(cursor, operation.WindowOptions.After);
        Assert.Equal(123, operation.WindowOptions.Offset);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(0)]
    [InlineData(QueryWindowOptionsFactory.MaxLimit + 1)]
    public void Create_WhenLimitIsOutOfRange_ReturnsWindowingError (int limit)
    {
        var result = QueryAssetsFindOperationRequestFactory.Create(
            commandName: "query assets find",
            operationId: "query.assets.find",
            operationName: "ucli.assets.find",
            type: "Texture2D",
            pathPrefix: null,
            nameContains: null,
            all: false,
            limit: limit,
            after: null);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Contains("limit must be between", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenAfterCursorIsInvalid_ReturnsWindowingError ()
    {
        var result = QueryAssetsFindOperationRequestFactory.Create(
            commandName: "query assets find",
            operationId: "query.assets.find",
            operationName: "ucli.assets.find",
            type: "Texture2D",
            pathPrefix: null,
            nameContains: null,
            all: false,
            limit: null,
            after: "not-a-cursor");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Contains("after cursor is invalid", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenAllIsCombinedWithAfter_ReturnsWindowingError ()
    {
        var result = QueryAssetsFindOperationRequestFactory.Create(
            commandName: "query assets find",
            operationId: "query.assets.find",
            operationName: "ucli.assets.find",
            type: "Texture2D",
            pathPrefix: null,
            nameContains: null,
            all: true,
            limit: null,
            after: QueryWindowCursorCodec.Encode(1));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Contains("'--all' cannot be combined", result.Error.Message, StringComparison.Ordinal);
    }
}
