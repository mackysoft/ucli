namespace MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun.Preflight;

/// <summary> Represents one established Test Run preflight outcome. </summary>
internal abstract record TestRunPreflightResult
{
    private TestRunPreflightResult ()
    {
    }

    public static TestRunPreflightSuccess Success (TestRunExecutionContext context)
    {
        return new TestRunPreflightSuccess(context);
    }

    public static TestRunPreflightFailure Failed (
        TestRunBeforeCreationCommandErrorServiceResult failure)
    {
        return new TestRunPreflightFailure(failure);
    }

    internal sealed record TestRunPreflightSuccess : TestRunPreflightResult
    {
        internal TestRunPreflightSuccess (TestRunExecutionContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public TestRunExecutionContext Context { get; }
    }

    internal sealed record TestRunPreflightFailure : TestRunPreflightResult
    {
        internal TestRunPreflightFailure (
            TestRunBeforeCreationCommandErrorServiceResult failure)
        {
            CommandError = failure ?? throw new ArgumentNullException(nameof(failure));
        }

        public TestRunBeforeCreationCommandErrorServiceResult CommandError { get; }
    }
}
