using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace MackySoft.Ucli.ScreenshotFidelityOracle.Windows;

internal static class WindowsGraphicsCapture
{
    private const int D3DDriverTypeHardware = 1;
    private const uint D3D11CreateDeviceBgraSupport = 0x20;
    private const uint D3D11SdkVersion = 7;
    private const string GraphicsCaptureItemRuntimeClassName =
        "Windows.Graphics.Capture.GraphicsCaptureItem";

    private static readonly Guid GraphicsCaptureItemInteropId =
        new("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356");

    private static readonly Guid GraphicsCaptureItemId =
        new("79C3F95B-31F7-4EC2-A464-632EF5D30760");

    internal static Bitmap CaptureClient (
        IntPtr windowHandle,
        Size expectedFrameSize,
        Rectangle clientCrop)
    {
        if (windowHandle == IntPtr.Zero)
        {
            throw new ArgumentException("A window handle is required.", nameof(windowHandle));
        }

        if (expectedFrameSize.Width <= 0 || expectedFrameSize.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedFrameSize));
        }

        if (clientCrop.Width <= 0
            || clientCrop.Height <= 0
            || clientCrop.Left < 0
            || clientCrop.Top < 0
            || clientCrop.Right > expectedFrameSize.Width
            || clientCrop.Bottom > expectedFrameSize.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(clientCrop));
        }

        try
        {
            return CaptureClientAsync(windowHandle, expectedFrameSize, clientCrop)
                .GetAwaiter()
                .GetResult();
        }
        catch (OracleFailureException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new OracleFailureException(
                $"Windows Graphics Capture could not read the target window: {exception.Message}");
        }
    }

    private static async Task<Bitmap> CaptureClientAsync (
        IntPtr windowHandle,
        Size expectedFrameSize,
        Rectangle clientCrop)
    {
        if (!GraphicsCaptureSession.IsSupported())
        {
            throw new OracleFailureException("Windows Graphics Capture is not supported by this system.");
        }

        GraphicsCaptureItem item = CreateCaptureItemForWindow(windowHandle);

        if (item.Size.Width != expectedFrameSize.Width || item.Size.Height != expectedFrameSize.Height)
        {
            throw new OracleFailureException(
                "The target window capture bounds changed before Windows Graphics Capture started: "
                + $"expected {expectedFrameSize.Width}x{expectedFrameSize.Height}, "
                + $"observed {item.Size.Width}x{item.Size.Height}.");
        }

        using var device = Direct3DDevice.Create();
        using var framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            device.Value,
            DirectXPixelFormat.B8G8R8A8UIntNormalized,
            2,
            new SizeInt32(expectedFrameSize.Width, expectedFrameSize.Height));
        using GraphicsCaptureSession session = framePool.CreateCaptureSession(item);
        session.IsCursorCaptureEnabled = false;

        var completion = new TaskCompletionSource<Direct3D11CaptureFrame>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        global::Windows.Foundation.TypedEventHandler<Direct3D11CaptureFramePool, object>? frameArrived = null;
        frameArrived = (sender, ignoredArgument) =>
        {
            _ = ignoredArgument;
            try
            {
                Direct3D11CaptureFrame frame = sender.TryGetNextFrame();
                if (!completion.TrySetResult(frame))
                {
                    frame.Dispose();
                }
            }
            catch (Exception exception)
            {
                _ = completion.TrySetException(exception);
            }
        };

        framePool.FrameArrived += frameArrived;
        try
        {
            session.StartCapture();
            using Direct3D11CaptureFrame frame = await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
            if (frame.ContentSize.Width != expectedFrameSize.Width
                || frame.ContentSize.Height != expectedFrameSize.Height)
            {
                throw new OracleFailureException(
                    "The target window capture bounds changed while Windows Graphics Capture was reading it: "
                    + $"expected {expectedFrameSize.Width}x{expectedFrameSize.Height}, "
                    + $"observed {frame.ContentSize.Width}x{frame.ContentSize.Height}.");
            }

            using SoftwareBitmap softwareBitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface);
            using Bitmap fullWindow = await ConvertToBitmapAsync(softwareBitmap);
            if (fullWindow.Width != expectedFrameSize.Width || fullWindow.Height != expectedFrameSize.Height)
            {
                throw new OracleFailureException(
                    "The encoded Windows Graphics Capture frame has unexpected dimensions: "
                    + $"expected {expectedFrameSize.Width}x{expectedFrameSize.Height}, "
                    + $"observed {fullWindow.Width}x{fullWindow.Height}.");
            }

            var clientBitmap = new Bitmap(clientCrop.Width, clientCrop.Height, PixelFormat.Format32bppArgb);
            try
            {
                using Graphics graphics = Graphics.FromImage(clientBitmap);
                graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                graphics.DrawImage(
                    fullWindow,
                    new Rectangle(0, 0, clientCrop.Width, clientCrop.Height),
                    clientCrop,
                    GraphicsUnit.Pixel);
                return clientBitmap;
            }
            catch
            {
                clientBitmap.Dispose();
                throw;
            }
        }
        finally
        {
            framePool.FrameArrived -= frameArrived;
        }
    }

    private static GraphicsCaptureItem CreateCaptureItemForWindow (IntPtr windowHandle)
    {
        IntPtr runtimeClassName = IntPtr.Zero;
        IntPtr activationFactory = IntPtr.Zero;
        IntPtr captureItem = IntPtr.Zero;
        try
        {
            Marshal.ThrowExceptionForHR(
                WindowsCreateString(
                    GraphicsCaptureItemRuntimeClassName,
                    (uint)GraphicsCaptureItemRuntimeClassName.Length,
                    out runtimeClassName));

            Guid interopId = GraphicsCaptureItemInteropId;
            Marshal.ThrowExceptionForHR(
                RoGetActivationFactory(runtimeClassName, ref interopId, out activationFactory));

            IntPtr virtualMethodTable = Marshal.ReadIntPtr(activationFactory);
            IntPtr createForWindowPointer = Marshal.ReadIntPtr(
                virtualMethodTable,
                3 * IntPtr.Size);
            var createForWindow = Marshal.GetDelegateForFunctionPointer<CreateForWindowDelegate>(
                createForWindowPointer);

            Guid captureItemId = GraphicsCaptureItemId;
            Marshal.ThrowExceptionForHR(
                createForWindow(
                    activationFactory,
                    windowHandle,
                    ref captureItemId,
                    out captureItem));
            if (captureItem == IntPtr.Zero)
            {
                throw new OracleFailureException(
                    "Windows Graphics Capture rejected the target window handle.");
            }

            return GraphicsCaptureItem.FromAbi(captureItem);
        }
        finally
        {
            if (captureItem != IntPtr.Zero)
            {
                _ = Marshal.Release(captureItem);
            }

            if (activationFactory != IntPtr.Zero)
            {
                _ = Marshal.Release(activationFactory);
            }

            if (runtimeClassName != IntPtr.Zero)
            {
                Marshal.ThrowExceptionForHR(WindowsDeleteString(runtimeClassName));
            }
        }
    }

    private static async Task<Bitmap> ConvertToBitmapAsync (SoftwareBitmap softwareBitmap)
    {
        using var stream = new InMemoryRandomAccessStream();
        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetSoftwareBitmap(softwareBitmap);
        await encoder.FlushAsync();

        var encodedBytes = new byte[checked((int)stream.Size)];
        using (var reader = new DataReader(stream.GetInputStreamAt(0)))
        {
            await reader.LoadAsync((uint)encodedBytes.Length);
            reader.ReadBytes(encodedBytes);
        }

        using var encodedStream = new MemoryStream(encodedBytes, writable: false);
        using var encodedBitmap = new Bitmap(encodedStream);
        var materializedBitmap = new Bitmap(
            encodedBitmap.Width,
            encodedBitmap.Height,
            PixelFormat.Format32bppArgb);
        try
        {
            using Graphics graphics = Graphics.FromImage(materializedBitmap);
            graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            graphics.DrawImageUnscaled(encodedBitmap, 0, 0);
            return materializedBitmap;
        }
        catch
        {
            materializedBitmap.Dispose();
            throw;
        }
    }

    private sealed class Direct3DDevice : IDisposable
    {
        private IntPtr nativeDevice;
        private IntPtr nativeContext;
        private IntPtr dxgiDevice;
        private IntPtr inspectableDevice;
        private bool disposed;

        private Direct3DDevice (
            IDirect3DDevice value,
            IntPtr nativeDevice,
            IntPtr nativeContext,
            IntPtr dxgiDevice,
            IntPtr inspectableDevice)
        {
            Value = value;
            this.nativeDevice = nativeDevice;
            this.nativeContext = nativeContext;
            this.dxgiDevice = dxgiDevice;
            this.inspectableDevice = inspectableDevice;
        }

        internal IDirect3DDevice Value { get; }

        internal static Direct3DDevice Create ()
        {
            int result = D3D11CreateDevice(
                IntPtr.Zero,
                D3DDriverTypeHardware,
                IntPtr.Zero,
                D3D11CreateDeviceBgraSupport,
                IntPtr.Zero,
                0,
                D3D11SdkVersion,
                out IntPtr nativeDevice,
                out _,
                out IntPtr nativeContext);
            Marshal.ThrowExceptionForHR(result);

            IntPtr dxgiDevice = IntPtr.Zero;
            IntPtr inspectableDevice = IntPtr.Zero;
            try
            {
                var dxgiDeviceId = new Guid("54EC77FA-1377-44E6-8C32-88FD5F44C84C");
                Marshal.ThrowExceptionForHR(
                    Marshal.QueryInterface(nativeDevice, ref dxgiDeviceId, out dxgiDevice));
                Marshal.ThrowExceptionForHR(
                    CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice, out inspectableDevice));
                IDirect3DDevice value = WinRT.MarshalInterface<IDirect3DDevice>.FromAbi(inspectableDevice);
                return new Direct3DDevice(
                    value,
                    nativeDevice,
                    nativeContext,
                    dxgiDevice,
                    inspectableDevice);
            }
            catch
            {
                Release(ref inspectableDevice);
                Release(ref dxgiDevice);
                Release(ref nativeContext);
                Release(ref nativeDevice);
                throw;
            }
        }

        public void Dispose ()
        {
            if (disposed)
            {
                return;
            }

            Release(ref inspectableDevice);
            Release(ref dxgiDevice);
            Release(ref nativeContext);
            Release(ref nativeDevice);
            disposed = true;
        }

        private static void Release (ref IntPtr value)
        {
            if (value == IntPtr.Zero)
            {
                return;
            }

            _ = Marshal.Release(value);
            value = IntPtr.Zero;
        }
    }

    [DllImport("d3d11.dll", ExactSpelling = true)]
    private static extern int D3D11CreateDevice (
        IntPtr adapter,
        int driverType,
        IntPtr software,
        uint flags,
        IntPtr featureLevels,
        uint featureLevelCount,
        uint sdkVersion,
        out IntPtr device,
        out uint featureLevel,
        out IntPtr immediateContext);

    [DllImport("d3d11.dll", ExactSpelling = true)]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice (
        IntPtr dxgiDevice,
        out IntPtr graphicsDevice);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int WindowsCreateString (
        [MarshalAs(UnmanagedType.LPWStr)] string sourceString,
        uint length,
        out IntPtr value);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int WindowsDeleteString (IntPtr value);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int RoGetActivationFactory (
        IntPtr activatableClassId,
        ref Guid interfaceId,
        out IntPtr activationFactory);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateForWindowDelegate (
        IntPtr thisPointer,
        IntPtr windowHandle,
        ref Guid interfaceId,
        out IntPtr captureItem);
}
