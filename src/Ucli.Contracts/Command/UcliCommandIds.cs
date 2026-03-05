namespace MackySoft.Ucli.Contracts;

/// <summary> Defines canonical command identifiers used across CLI and IPC domains. </summary>
public static class UcliCommandIds
{
    /// <summary> Gets command identifier for <c>init</c>. </summary>
    public static UcliCommand Init { get; } = new("init");

    /// <summary> Gets command identifier for <c>status</c>. </summary>
    public static UcliCommand Status { get; } = new("status");

    /// <summary> Gets command identifier for <c>daemon</c>. </summary>
    public static UcliCommand Daemon { get; } = new("daemon");

    /// <summary> Gets command identifier for <c>daemon.start</c>. </summary>
    public static UcliCommand DaemonStart { get; } = new("daemon.start");

    /// <summary> Gets command identifier for <c>daemon.stop</c>. </summary>
    public static UcliCommand DaemonStop { get; } = new("daemon.stop");

    /// <summary> Gets command identifier for <c>daemon.status</c>. </summary>
    public static UcliCommand DaemonStatus { get; } = new("daemon.status");

    /// <summary> Gets command identifier for <c>test</c>. </summary>
    public static UcliCommand Test { get; } = new("test");

    /// <summary> Gets command identifier for <c>test.run</c>. </summary>
    public static UcliCommand TestRun { get; } = new("test.run");

    /// <summary> Gets command identifier for <c>test.profile.init</c>. </summary>
    public static UcliCommand TestProfileInit { get; } = new("test.profile.init");

    /// <summary> Gets command identifier for <c>validate</c>. </summary>
    public static UcliCommand Validate { get; } = new("validate");

    /// <summary> Gets command identifier for <c>plan</c>. </summary>
    public static UcliCommand Plan { get; } = new("plan");

    /// <summary> Gets command identifier for <c>call</c>. </summary>
    public static UcliCommand Call { get; } = new("call");

    /// <summary> Gets command identifier for <c>resolve</c>. </summary>
    public static UcliCommand Resolve { get; } = new("resolve");

    /// <summary> Gets command identifier for <c>query</c>. </summary>
    public static UcliCommand Query { get; } = new("query");

    /// <summary> Gets command identifier for <c>refresh</c>. </summary>
    public static UcliCommand Refresh { get; } = new("refresh");

    /// <summary> Gets command identifier for <c>ops</c>. </summary>
    public static UcliCommand Ops { get; } = new("ops");
}