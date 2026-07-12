using System.Reflection;
using MackySoft.Ucli.Application.Features.Requests.Call.Common.Contracts;
using MackySoft.Ucli.Application.Features.Requests.Plan.Common.Contracts;
using MackySoft.Ucli.Application.Features.Requests.Query.UseCases.Query;
using MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;
using MackySoft.Ucli.Application.Features.Requests.Validate.Common.Contracts;

namespace MackySoft.Ucli.Application.Tests.Execution.Results;

public sealed class RequestServiceResultConstructionInvariantTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ResultTypes_DoNotExposeDirectConstructors ()
    {
        Type[] resultTypes =
        [
            typeof(PlanServiceResult),
            typeof(CallServiceResult),
            typeof(QueryServiceResult),
            typeof(ResolveServiceResult),
            typeof(ValidateServiceResult),
            typeof(OperationExecuteResult),
        ];

        for (var i = 0; i < resultTypes.Length; i++)
        {
            var constructors = resultTypes[i].GetConstructors(
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic);

            Assert.DoesNotContain(constructors, static constructor => !constructor.IsPrivate);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Success_WhenRequiredOutputIsMissing_Throws ()
    {
        Assert.Throws<ArgumentNullException>(() => PlanServiceResult.Success(null!, "uCLI plan completed."));
        Assert.Throws<ArgumentNullException>(() => CallServiceResult.Success(null!, "uCLI call completed."));
        Assert.Throws<ArgumentNullException>(() => ValidateServiceResult.Success(null!, "Static validation passed."));
        Assert.Throws<ArgumentNullException>(() => QueryServiceResultFactory.Success("query assets find", RequestServiceResultInvariantTestSupport.RequestId, [], null!, ProjectIdentityInfoTestFactory.Create()));
        Assert.Throws<ArgumentNullException>(() => QueryServiceResultFactory.Success("query assets find", RequestServiceResultInvariantTestSupport.RequestId, [], RequestServiceResultInvariantTestSupport.CreateReadIndexInfo(), null!));
        Assert.Throws<ArgumentNullException>(() => ResolveServiceResultFactory.Success(RequestServiceResultInvariantTestSupport.RequestId, [], null!, ProjectIdentityInfoTestFactory.Create()));
        Assert.Throws<ArgumentNullException>(() => ResolveServiceResultFactory.Success(RequestServiceResultInvariantTestSupport.RequestId, [], RequestServiceResultInvariantTestSupport.CreateReadIndexInfo(), null!));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CorrelatedResult_WhenRequestIdIsEmpty_Throws ()
    {
        var readIndex = RequestServiceResultInvariantTestSupport.CreateReadIndexInfo();
        var project = ProjectIdentityInfoTestFactory.Create();

        var exceptions = new[]
        {
            Assert.Throws<ArgumentException>(() => QueryServiceResultFactory.Success("query assets find", Guid.Empty, [], readIndex, project)),
            Assert.Throws<ArgumentException>(() => ResolveServiceResultFactory.Success(Guid.Empty, [], readIndex, project)),
            Assert.Throws<ArgumentException>(() => OperationExecuteResultFactory.Success(Guid.Empty, [], "Operation execution completed.", readPostcondition: null, project)),
        };

        Assert.All(exceptions, static exception => Assert.Equal("requestId", exception.ParamName));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Failure_Errors_AreReturnedAsReadOnlySnapshot ()
    {
        var inputErrors = new List<ApplicationFailure>(RequestServiceResultInvariantTestSupport.CreateErrors());
        var result = PlanServiceResult.Failure(
            "Plan failed.",
            inputErrors);

        inputErrors[0] = ApplicationFailure.InvalidInput("Changed message.");

        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
        var collection = Assert.IsAssignableFrom<ICollection<ApplicationFailure>>(result.Errors);
        Assert.True(collection.IsReadOnly);
    }
}
