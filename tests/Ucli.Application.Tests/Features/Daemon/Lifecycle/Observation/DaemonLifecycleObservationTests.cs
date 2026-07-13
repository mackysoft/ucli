using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonLifecycleObservationTests
{
    public static TheoryData<RequiredLiteralParameter, string?> InvalidRequiredLiteralCases => new()
    {
        { RequiredLiteralParameter.EditorMode, null },
        { RequiredLiteralParameter.EditorMode, "" },
        { RequiredLiteralParameter.EditorMode, " gui " },
        { RequiredLiteralParameter.EditorMode, "unsupported" },
        { RequiredLiteralParameter.LifecycleState, null },
        { RequiredLiteralParameter.LifecycleState, "" },
        { RequiredLiteralParameter.LifecycleState, " ready " },
        { RequiredLiteralParameter.LifecycleState, "unsupported" },
        { RequiredLiteralParameter.CompileState, null },
        { RequiredLiteralParameter.CompileState, "" },
        { RequiredLiteralParameter.CompileState, " ready " },
        { RequiredLiteralParameter.CompileState, "unsupported" },
    };

    public static TheoryData<OptionalStringParameter, string> InvalidOptionalStringCases => new()
    {
        { OptionalStringParameter.BlockingReason, " " },
        { OptionalStringParameter.BlockingReason, "unsupported" },
        { OptionalStringParameter.CompileGeneration, " " },
        { OptionalStringParameter.DomainReloadGeneration, " " },
        { OptionalStringParameter.ActionRequired, " " },
        { OptionalStringParameter.ActionRequired, "unsupported" },
        { OptionalStringParameter.ServerVersion, " " },
    };

    public static TheoryData<IpcPrimaryDiagnostic> InvalidPrimaryDiagnosticCases => new()
    {
        new IpcPrimaryDiagnostic(null, null, null, null, null, null),
        new IpcPrimaryDiagnostic(" ", null, null, null, null, null),
        new IpcPrimaryDiagnostic("unsupported", null, null, null, null, null),
        new IpcPrimaryDiagnostic(DaemonDiagnosisPrimaryDiagnosticKindValues.Compiler, " ", null, null, null, null),
        new IpcPrimaryDiagnostic(DaemonDiagnosisPrimaryDiagnosticKindValues.Compiler, null, " ", null, null, null),
        new IpcPrimaryDiagnostic(DaemonDiagnosisPrimaryDiagnosticKindValues.Compiler, null, null, 0, null, null),
        new IpcPrimaryDiagnostic(DaemonDiagnosisPrimaryDiagnosticKindValues.Compiler, null, null, null, 0, null),
        new IpcPrimaryDiagnostic(DaemonDiagnosisPrimaryDiagnosticKindValues.Compiler, null, null, null, null, " "),
    };

    public static TheoryData<IpcPlayModeSnapshot> InvalidPlayModeCases => new()
    {
        CreatePlayMode(state: null!),
        CreatePlayMode(state: " playing "),
        CreatePlayMode(state: "unsupported"),
        CreatePlayMode(transition: null!),
        CreatePlayMode(transition: " none "),
        CreatePlayMode(transition: "unsupported"),
        CreatePlayMode(generation: " "),
    };

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [Trait("Size", "Small")]
    public void Constructor_WhenProcessIdIsNotPositive_ThrowsArgumentOutOfRangeException (int processId)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new ObservationArguments
        {
            ProcessId = processId,
        }.Create());

        Assert.Equal("processId", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenEditorInstanceIdIsEmpty_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new ObservationArguments
        {
            EditorInstanceId = Guid.Empty,
        }.Create());

        Assert.Equal("editorInstanceId", exception.ParamName);
    }

    [Theory]
    [InlineData(ObservationTimestamp.ProcessStartedAtUtc)]
    [InlineData(ObservationTimestamp.ObservedAtUtc)]
    [Trait("Size", "Small")]
    public void Constructor_WhenTimestampIsDefault_ThrowsArgumentException (ObservationTimestamp timestamp)
    {
        var arguments = timestamp switch
        {
            ObservationTimestamp.ProcessStartedAtUtc => new ObservationArguments
            {
                ProcessStartedAtUtc = default,
            },
            ObservationTimestamp.ObservedAtUtc => new ObservationArguments
            {
                ObservedAtUtc = default,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(timestamp), timestamp, null),
        };

        var exception = Assert.Throws<ArgumentException>(arguments.Create);

        Assert.Equal(timestamp switch
        {
            ObservationTimestamp.ProcessStartedAtUtc => "processStartedAtUtc",
            ObservationTimestamp.ObservedAtUtc => "observedAtUtc",
            _ => throw new ArgumentOutOfRangeException(nameof(timestamp), timestamp, null),
        }, exception.ParamName);
    }

    [Theory]
    [MemberData(nameof(InvalidRequiredLiteralCases))]
    [Trait("Size", "Small")]
    public void Constructor_WhenRequiredLiteralIsNotCanonical_ThrowsArgumentException (
        RequiredLiteralParameter parameter,
        string? value)
    {
        var arguments = parameter switch
        {
            RequiredLiteralParameter.EditorMode => new ObservationArguments
            {
                EditorMode = value!,
            },
            RequiredLiteralParameter.LifecycleState => new ObservationArguments
            {
                LifecycleState = value!,
            },
            RequiredLiteralParameter.CompileState => new ObservationArguments
            {
                CompileState = value!,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(parameter), parameter, null),
        };

        var exception = Assert.Throws<ArgumentException>(arguments.Create);

        Assert.Equal(parameter switch
        {
            RequiredLiteralParameter.EditorMode => "editorMode",
            RequiredLiteralParameter.LifecycleState => "lifecycleState",
            RequiredLiteralParameter.CompileState => "compileState",
            _ => throw new ArgumentOutOfRangeException(nameof(parameter), parameter, null),
        }, exception.ParamName);
    }

    [Theory]
    [MemberData(nameof(InvalidOptionalStringCases))]
    [Trait("Size", "Small")]
    public void Constructor_WhenOptionalStringViolatesItsContract_ThrowsArgumentException (
        OptionalStringParameter parameter,
        string value)
    {
        var arguments = parameter switch
        {
            OptionalStringParameter.BlockingReason => new ObservationArguments
            {
                BlockingReason = value,
            },
            OptionalStringParameter.CompileGeneration => new ObservationArguments
            {
                CompileGeneration = value,
            },
            OptionalStringParameter.DomainReloadGeneration => new ObservationArguments
            {
                DomainReloadGeneration = value,
            },
            OptionalStringParameter.ActionRequired => new ObservationArguments
            {
                ActionRequired = value,
            },
            OptionalStringParameter.ServerVersion => new ObservationArguments
            {
                ServerVersion = value,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(parameter), parameter, null),
        };

        var exception = Assert.Throws<ArgumentException>(arguments.Create);

        Assert.Equal(parameter switch
        {
            OptionalStringParameter.BlockingReason => "blockingReason",
            OptionalStringParameter.CompileGeneration => "compileGeneration",
            OptionalStringParameter.DomainReloadGeneration => "domainReloadGeneration",
            OptionalStringParameter.ActionRequired => "actionRequired",
            OptionalStringParameter.ServerVersion => "serverVersion",
            _ => throw new ArgumentOutOfRangeException(nameof(parameter), parameter, null),
        }, exception.ParamName);
    }

    [Theory]
    [MemberData(nameof(InvalidPrimaryDiagnosticCases))]
    [Trait("Size", "Small")]
    public void Constructor_WhenPrimaryDiagnosticViolatesItsContract_ThrowsArgumentException (
        IpcPrimaryDiagnostic primaryDiagnostic)
    {
        var exception = Assert.Throws<ArgumentException>(() => new ObservationArguments
        {
            PrimaryDiagnostic = primaryDiagnostic,
        }.Create());

        Assert.Equal("primaryDiagnostic", exception.ParamName);
    }

    [Theory]
    [MemberData(nameof(InvalidPlayModeCases))]
    [Trait("Size", "Small")]
    public void Constructor_WhenPlayModeViolatesItsContract_ThrowsArgumentException (IpcPlayModeSnapshot playMode)
    {
        var exception = Assert.Throws<ArgumentException>(() => new ObservationArguments
        {
            PlayMode = playMode,
        }.Create());

        Assert.Equal("playMode", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithValidValues_PreservesEveryValue ()
    {
        var arguments = new ObservationArguments();

        var observation = arguments.Create();

        Assert.Equal(arguments.ProcessId, observation.ProcessId);
        Assert.Equal(arguments.ProcessStartedAtUtc, observation.ProcessStartedAtUtc);
        Assert.Equal(arguments.EditorMode, observation.EditorMode);
        Assert.Equal(arguments.LifecycleState, observation.LifecycleState);
        Assert.Equal(arguments.BlockingReason, observation.BlockingReason);
        Assert.Equal(arguments.CompileState, observation.CompileState);
        Assert.Equal(arguments.CompileGeneration, observation.CompileGeneration);
        Assert.Equal(arguments.DomainReloadGeneration, observation.DomainReloadGeneration);
        Assert.Equal(arguments.ObservedAtUtc, observation.ObservedAtUtc);
        Assert.Equal(arguments.ActionRequired, observation.ActionRequired);
        Assert.Same(arguments.PrimaryDiagnostic, observation.PrimaryDiagnostic);
        Assert.Equal(arguments.ServerVersion, observation.ServerVersion);
        Assert.Equal(arguments.CanAcceptExecutionRequests, observation.CanAcceptExecutionRequests);
        Assert.Equal(arguments.EditorInstanceId, observation.EditorInstanceId);
        Assert.Same(arguments.PlayMode, observation.PlayMode);
    }

    [Theory]
    [InlineData(nameof(DaemonLifecycleObservation.ProcessId))]
    [InlineData(nameof(DaemonLifecycleObservation.ProcessStartedAtUtc))]
    [InlineData(nameof(DaemonLifecycleObservation.EditorMode))]
    [InlineData(nameof(DaemonLifecycleObservation.LifecycleState))]
    [InlineData(nameof(DaemonLifecycleObservation.BlockingReason))]
    [InlineData(nameof(DaemonLifecycleObservation.CompileState))]
    [InlineData(nameof(DaemonLifecycleObservation.CompileGeneration))]
    [InlineData(nameof(DaemonLifecycleObservation.DomainReloadGeneration))]
    [InlineData(nameof(DaemonLifecycleObservation.ObservedAtUtc))]
    [InlineData(nameof(DaemonLifecycleObservation.ActionRequired))]
    [InlineData(nameof(DaemonLifecycleObservation.PrimaryDiagnostic))]
    [InlineData(nameof(DaemonLifecycleObservation.ServerVersion))]
    [InlineData(nameof(DaemonLifecycleObservation.CanAcceptExecutionRequests))]
    [InlineData(nameof(DaemonLifecycleObservation.EditorInstanceId))]
    [InlineData(nameof(DaemonLifecycleObservation.PlayMode))]
    [Trait("Size", "Small")]
    public void Property_DoesNotExposeASetter (string propertyName)
    {
        var property = typeof(DaemonLifecycleObservation).GetProperty(propertyName);

        Assert.NotNull(property);
        Assert.Null(property.SetMethod);
    }

    private static IpcPlayModeSnapshot CreatePlayMode (
        string? state = "playing",
        string? transition = "none",
        string? generation = "1")
    {
        return new IpcPlayModeSnapshot(
            State: state!,
            Transition: transition!,
            IsPlaying: true,
            IsPlayingOrWillChangePlaymode: true,
            Generation: generation);
    }

    public enum ObservationTimestamp
    {
        ProcessStartedAtUtc,
        ObservedAtUtc,
    }

    public enum RequiredLiteralParameter
    {
        EditorMode,
        LifecycleState,
        CompileState,
    }

    public enum OptionalStringParameter
    {
        BlockingReason,
        CompileGeneration,
        DomainReloadGeneration,
        ActionRequired,
        ServerVersion,
    }

    private sealed record ObservationArguments
    {
        public int ProcessId { get; init; } = 1234;

        public DateTimeOffset ProcessStartedAtUtc { get; init; } = DateTimeOffset.UnixEpoch;

        public string EditorMode { get; init; } = ContractLiteralCodec.ToValue(DaemonEditorMode.Batchmode);

        public string LifecycleState { get; init; } = IpcEditorLifecycleStateCodec.Ready;

        public string? BlockingReason { get; init; } = IpcEditorBlockingReasonCodec.Recovery;

        public string CompileState { get; init; } = IpcCompileStateCodec.Ready;

        public string? CompileGeneration { get; init; } = "compile-generation";

        public string? DomainReloadGeneration { get; init; } = "domain-reload-generation";

        public DateTimeOffset ObservedAtUtc { get; init; } = DateTimeOffset.UnixEpoch.AddSeconds(1);

        public string? ActionRequired { get; init; } = DaemonDiagnosisActionRequiredValues.FixCompileErrors;

        public IpcPrimaryDiagnostic? PrimaryDiagnostic { get; init; } = new(
            Kind: DaemonDiagnosisPrimaryDiagnosticKindValues.Compiler,
            Code: "CS0001",
            File: "Assets/Example.cs",
            Line: 10,
            Column: 20,
            Message: "Example diagnostic");

        public string? ServerVersion { get; init; } = "0.5.0";

        public bool CanAcceptExecutionRequests { get; init; } = true;

        public Guid EditorInstanceId { get; init; } = Guid.Parse("11111111-1111-1111-1111-111111111111");

        public IpcPlayModeSnapshot? PlayMode { get; init; } = CreatePlayMode();

        public DaemonLifecycleObservation Create ()
        {
            return new DaemonLifecycleObservation(
                processId: ProcessId,
                processStartedAtUtc: ProcessStartedAtUtc,
                editorMode: EditorMode,
                lifecycleState: LifecycleState,
                blockingReason: BlockingReason,
                compileState: CompileState,
                compileGeneration: CompileGeneration,
                domainReloadGeneration: DomainReloadGeneration,
                observedAtUtc: ObservedAtUtc,
                actionRequired: ActionRequired,
                primaryDiagnostic: PrimaryDiagnostic,
                serverVersion: ServerVersion,
                canAcceptExecutionRequests: CanAcceptExecutionRequests,
                editorInstanceId: EditorInstanceId,
                playMode: PlayMode);
        }
    }
}
