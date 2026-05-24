using System.Runtime.ExceptionServices;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Transport;

/// <summary> Wraps an exception thrown by a caller-owned IPC progress frame handler. </summary>
internal sealed class IpcProgressFrameHandlerException : Exception
{
    /// <summary> Initializes a new instance of the <see cref="IpcProgressFrameHandlerException" /> class. </summary>
    /// <param name="handlerException"> The exception thrown by the progress frame handler. </param>
    public IpcProgressFrameHandlerException (Exception handlerException)
        : base("IPC progress frame handler failed.", handlerException)
    {
        HandlerException = handlerException ?? throw new ArgumentNullException(nameof(handlerException));
    }

    /// <summary> Gets the original exception thrown by the caller-owned progress frame handler. </summary>
    public Exception HandlerException { get; }

    /// <summary> Throws the original progress handler exception while preserving its stack trace. </summary>
    public void ThrowHandlerException ()
    {
        ExceptionDispatchInfo.Capture(HandlerException).Throw();
    }
}
