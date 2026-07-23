using System.ComponentModel;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

namespace MackySoft.Ucli.ScreenshotFidelityOracle.Windows;

internal static class WindowCaptureOracle
{
    private const int DwmWindowAttributeCloaked = 14;
    private const int DwmWindowAttributeExtendedFrameBounds = 9;
    private static readonly IntPtr PerMonitorAwareV2 = new(-4);

    internal static void Capture (
        int processId,
        string windowTitle,
        string outputPath,
        string metadataPath)
    {
        if (processId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(windowTitle);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(metadataPath);

        using var dpiScope = new DpiAwarenessScope();
        IntPtr windowHandle = FindUniqueWindow(processId, windowTitle);
        WindowObservation before = ObserveWindow(windowHandle, processId, windowTitle);
        DateTimeOffset capturedAtUtc = DateTimeOffset.UtcNow;
        var clientCrop = new Rectangle(
            before.ClientBounds.Left - before.CaptureBounds.Left,
            before.ClientBounds.Top - before.CaptureBounds.Top,
            before.ClientBounds.Width,
            before.ClientBounds.Height);
        using Bitmap bitmap = WindowsGraphicsCapture.CaptureClient(
            windowHandle,
            new Size(before.CaptureBounds.Width, before.CaptureBounds.Height),
            clientCrop);
        WindowObservation after = ObserveWindow(windowHandle, processId, windowTitle);
        EnsureStable(before, after);

        var metadata = new CaptureMetadata(
            2,
            processId,
            windowTitle,
            $"0x{windowHandle.ToInt64():X}",
            ToBounds(before.ClientBounds),
            ToBounds(before.CaptureBounds),
            new Bounds(clientCrop.X, clientCrop.Y, clientCrop.Width, clientCrop.Height),
            "windows-graphics-capture",
            capturedAtUtc);
        CommitOutputs(bitmap, metadata, outputPath, metadataPath);
    }

    private static IntPtr FindUniqueWindow (int processId, string windowTitle)
    {
        var matches = new List<IntPtr>();
        var processWindows = new List<string>();
        Exception? callbackException = null;
        EnumWindowsProcedure callback = (windowHandle, ignoredParameter) =>
        {
            _ = ignoredParameter;
            try
            {
                _ = GetWindowThreadProcessId(windowHandle, out uint candidateProcessId);
                if (candidateProcessId == (uint)processId)
                {
                    string candidateTitle = ReadWindowTitle(windowHandle);
                    processWindows.Add(
                        $"0x{windowHandle.ToInt64():X} title='{candidateTitle}' visible={IsWindowVisible(windowHandle)}");
                    if (string.Equals(candidateTitle, windowTitle, StringComparison.Ordinal))
                    {
                        matches.Add(windowHandle);
                    }
                }

                return true;
            }
            catch (Exception exception)
            {
                callbackException = exception;
                return false;
            }
        };

        bool completed = EnumWindows(callback, IntPtr.Zero);
        GC.KeepAlive(callback);
        if (callbackException != null)
        {
            throw new OracleFailureException($"Could not enumerate windows: {callbackException.Message}");
        }

        if (!completed)
        {
            throw CreateWin32Failure("EnumWindows");
        }

        if (matches.Count == 0)
        {
            string candidates = processWindows.Count == 0
                ? "none"
                : string.Join("; ", processWindows);
            throw new OracleFailureException(
                $"No top-level window exactly matched process {processId} and title '{windowTitle}'. "
                + $"Observed top-level windows for that process: {candidates}.");
        }

        if (matches.Count > 1)
        {
            throw new OracleFailureException(
                $"More than one top-level window matched process {processId} and title '{windowTitle}'.");
        }

        return matches[0];
    }

    private static WindowObservation ObserveWindow (
        IntPtr windowHandle,
        int processId,
        string windowTitle)
    {
        if (!IsWindow(windowHandle))
        {
            throw new OracleFailureException("The selected window no longer exists.");
        }

        uint threadId = GetWindowThreadProcessId(windowHandle, out uint observedProcessId);
        if (threadId == 0)
        {
            throw CreateWin32Failure("GetWindowThreadProcessId");
        }

        if (observedProcessId != (uint)processId)
        {
            throw new OracleFailureException(
                $"The selected window process changed from {processId} to {observedProcessId}.");
        }

        string observedTitle = ReadWindowTitle(windowHandle);
        if (!string.Equals(observedTitle, windowTitle, StringComparison.Ordinal))
        {
            throw new OracleFailureException(
                $"The selected window title changed from '{windowTitle}' to '{observedTitle}'.");
        }

        if (!IsWindowVisible(windowHandle))
        {
            throw new OracleFailureException("The selected window is not visible.");
        }

        if (IsIconic(windowHandle))
        {
            throw new OracleFailureException("The selected window is minimized.");
        }

        int hResult = DwmGetWindowAttribute(
            windowHandle,
            DwmWindowAttributeCloaked,
            out int cloaked,
            Marshal.SizeOf<int>());
        if (hResult < 0)
        {
            throw new OracleFailureException(
                $"DwmGetWindowAttribute(DWMWA_CLOAKED) failed with HRESULT 0x{hResult:X8}.");
        }

        if (cloaked != 0)
        {
            throw new OracleFailureException("The selected window is cloaked by the Desktop Window Manager.");
        }

        if (!GetClientRect(windowHandle, out NativeRectangle clientRectangle))
        {
            throw CreateWin32Failure("GetClientRect");
        }

        var topLeft = new NativePoint(clientRectangle.Left, clientRectangle.Top);
        var bottomRight = new NativePoint(clientRectangle.Right, clientRectangle.Bottom);
        if (!ClientToScreen(windowHandle, ref topLeft) || !ClientToScreen(windowHandle, ref bottomRight))
        {
            throw CreateWin32Failure("ClientToScreen");
        }

        var clientBounds = new NativeRectangle(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
        if (clientBounds.Width <= 0 || clientBounds.Height <= 0)
        {
            throw new OracleFailureException(
                $"The selected window client area is empty: {clientBounds.Width}x{clientBounds.Height}.");
        }

        int extendedBoundsResult = DwmGetWindowAttribute(
            windowHandle,
            DwmWindowAttributeExtendedFrameBounds,
            out NativeRectangle captureBounds,
            Marshal.SizeOf<NativeRectangle>());
        if (extendedBoundsResult < 0)
        {
            throw new OracleFailureException(
                "DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS) failed with "
                + $"HRESULT 0x{extendedBoundsResult:X8}.");
        }

        if (captureBounds.Width <= 0 || captureBounds.Height <= 0)
        {
            throw new OracleFailureException(
                $"The selected window capture area is empty: {captureBounds.Width}x{captureBounds.Height}.");
        }

        if (clientBounds.Left < captureBounds.Left
            || clientBounds.Top < captureBounds.Top
            || clientBounds.Right > captureBounds.Right
            || clientBounds.Bottom > captureBounds.Bottom)
        {
            throw new OracleFailureException(
                "The selected window client area is not contained within its capture bounds.");
        }

        return new WindowObservation(clientBounds, captureBounds);
    }

    private static void EnsureStable (WindowObservation before, WindowObservation after)
    {
        if (!before.ClientBounds.Equals(after.ClientBounds))
        {
            throw new OracleFailureException("The window client bounds changed while the reference was captured.");
        }

        if (!before.CaptureBounds.Equals(after.CaptureBounds))
        {
            throw new OracleFailureException("The window capture bounds changed while the reference was captured.");
        }
    }

    private static void CommitOutputs (
        Bitmap bitmap,
        CaptureMetadata metadata,
        string outputPath,
        string metadataPath)
    {
        string outputFullPath = Path.GetFullPath(outputPath);
        string metadataFullPath = Path.GetFullPath(metadataPath);
        string outputDirectory = Path.GetDirectoryName(outputFullPath)
            ?? throw new IOException($"The capture output path has no parent directory: {outputFullPath}");
        string metadataDirectory = Path.GetDirectoryName(metadataFullPath)
            ?? throw new IOException($"The metadata path has no parent directory: {metadataFullPath}");
        if (string.Equals(outputFullPath, metadataFullPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new OracleFailureException("The capture output and metadata paths must be different.");
        }

        Directory.CreateDirectory(outputDirectory);
        Directory.CreateDirectory(metadataDirectory);
        if (File.Exists(outputFullPath) || File.Exists(metadataFullPath))
        {
            throw new OracleFailureException(
                "The capture output and metadata paths must not exist before capture publication.");
        }

        string temporaryOutput = Path.Combine(
            outputDirectory,
            $".{Path.GetFileName(outputFullPath)}.{Guid.NewGuid():N}.tmp");
        string temporaryMetadata = Path.Combine(
            metadataDirectory,
            $".{Path.GetFileName(metadataFullPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            bitmap.Save(temporaryOutput, ImageFormat.Png);
            JsonFile.WriteAtomic(temporaryMetadata, metadata);
            File.Move(temporaryOutput, outputFullPath);
            try
            {
                File.Move(temporaryMetadata, metadataFullPath);
            }
            catch
            {
                DeleteIfPresent(outputFullPath);
                throw;
            }
        }
        catch (Exception exception)
        {
            throw new OracleFailureException($"Could not commit the window reference capture: {exception.Message}");
        }
        finally
        {
            DeleteIfPresent(temporaryOutput);
            DeleteIfPresent(temporaryMetadata);
        }
    }

    private static void DeleteIfPresent (string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static Bounds ToBounds (NativeRectangle rectangle)
    {
        return new Bounds(rectangle.Left, rectangle.Top, rectangle.Width, rectangle.Height);
    }

    private static string ReadWindowTitle (IntPtr windowHandle)
    {
        int titleLength = GetWindowTextLength(windowHandle);
        if (titleLength <= 0)
        {
            return string.Empty;
        }

        var title = new StringBuilder(titleLength + 1);
        int charactersCopied = GetWindowText(windowHandle, title, title.Capacity);
        return charactersCopied > 0 ? title.ToString() : string.Empty;
    }

    private static OracleFailureException CreateWin32Failure (string operation)
    {
        int error = Marshal.GetLastWin32Error();
        string message = new Win32Exception(error).Message;
        return new OracleFailureException($"{operation} failed with Win32 error {error}: {message}");
    }

    private sealed class DpiAwarenessScope : IDisposable
    {
        private readonly IntPtr previousContext;
        private bool disposed;

        internal DpiAwarenessScope ()
        {
            previousContext = SetThreadDpiAwarenessContext(PerMonitorAwareV2);
            if (previousContext == IntPtr.Zero)
            {
                throw CreateWin32Failure("SetThreadDpiAwarenessContext(PER_MONITOR_AWARE_V2)");
            }
        }

        public void Dispose ()
        {
            if (disposed)
            {
                return;
            }

            _ = SetThreadDpiAwarenessContext(previousContext);
            disposed = true;
        }
    }

    private sealed record WindowObservation (
        NativeRectangle ClientBounds,
        NativeRectangle CaptureBounds);

    private sealed record CaptureMetadata (
        int SchemaVersion,
        int ProcessId,
        string WindowTitle,
        string WindowHandle,
        Bounds ClientBounds,
        Bounds CaptureBounds,
        Bounds ClientCrop,
        string CaptureMethod,
        DateTimeOffset CapturedAtUtc);

    private sealed record Bounds (
        int X,
        int Y,
        int Width,
        int Height);

    private delegate bool EnumWindowsProcedure (IntPtr windowHandle, IntPtr parameter);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        internal NativePoint (int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        internal int x;
        internal int y;

        internal readonly int X => x;

        internal readonly int Y => y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle
    {
        internal NativeRectangle (int left, int top, int right, int bottom)
        {
            this.left = left;
            this.top = top;
            this.right = right;
            this.bottom = bottom;
        }

        internal int left;
        internal int top;
        internal int right;
        internal int bottom;

        internal readonly int Left => left;

        internal readonly int Top => top;

        internal readonly int Right => right;

        internal readonly int Bottom => bottom;

        internal readonly int Width => checked(right - left);

        internal readonly int Height => checked(bottom - top);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows (EnumWindowsProcedure callback, IntPtr parameter);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow (IntPtr windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible (IntPtr windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic (IntPtr windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId (IntPtr windowHandle, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowTextLength (IntPtr windowHandle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowText (
        IntPtr windowHandle,
        StringBuilder text,
        int maximumCharacterCount);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect (IntPtr windowHandle, out NativeRectangle rectangle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen (IntPtr windowHandle, ref NativePoint point);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetThreadDpiAwarenessContext (IntPtr dpiContext);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute (
        IntPtr windowHandle,
        int attribute,
        out int attributeValue,
        int attributeSize);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute (
        IntPtr windowHandle,
        int attribute,
        out NativeRectangle attributeValue,
        int attributeSize);
}
