using System.Runtime.InteropServices;
using DirectShowLib;

public sealed class CameraPreview : IDisposable
{
    private const double PreferredAspectRatio = 16d / 9d;

    private readonly Control host;
    private readonly MirrorSampleGrabberCallback mirrorCallback = new();
    private IGraphBuilder? graphBuilder;
    private ICaptureGraphBuilder2? captureGraphBuilder;
    private IBaseFilter? sourceFilter;
    private IBaseFilter? sampleGrabberFilter;
    private ISampleGrabber? sampleGrabber;
    private IMediaControl? mediaControl;
    private IVideoWindow? videoWindow;
    private double videoAspectRatio = PreferredAspectRatio;
    private bool disposed;

    public CameraPreview(Control host)
    {
        this.host = host;
        this.host.Resize += HandleHostResize;
    }

    public bool IsRunning { get; private set; }

    public bool IsMirrored
    {
        get => mirrorCallback.MirrorEnabled;
        set => mirrorCallback.MirrorEnabled = value;
    }

    public string StatusMessage { get; private set; } = "Preview off";

    public bool Start(string deviceName)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        StopGraph();

        if (string.IsNullOrWhiteSpace(deviceName))
        {
            StatusMessage = "Enter a camera name";
            return false;
        }

        DsDevice[] devices = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);

        try
        {
            DsDevice? device = devices.FirstOrDefault(
                candidate => string.Equals(candidate.Name, deviceName, StringComparison.Ordinal));

            if (device is null)
            {
                StatusMessage = $"Preview device not found: {deviceName}";
                return false;
            }

            graphBuilder = (IGraphBuilder)new FilterGraph();
            captureGraphBuilder = (ICaptureGraphBuilder2)new CaptureGraphBuilder2();

            DsError.ThrowExceptionForHR(captureGraphBuilder.SetFiltergraph(graphBuilder));

            var graphBuilder2 = (IFilterGraph2)graphBuilder;
            DsError.ThrowExceptionForHR(
                graphBuilder2.AddSourceFilterForMoniker(
                    device.Mon,
                    null,
                    device.Name,
                    out sourceFilter));

            ConfigureVideoFormat();
            ConfigureMirrorFilter(graphBuilder2);

            DsError.ThrowExceptionForHR(
                captureGraphBuilder.RenderStream(
                    PinCategory.Preview,
                    MediaType.Video,
                    sourceFilter,
                    sampleGrabberFilter,
                    null));

            ConfigureMirrorCallback();

            mediaControl = (IMediaControl)graphBuilder;
            videoWindow = (IVideoWindow)graphBuilder;

            DsError.ThrowExceptionForHR(videoWindow.put_Owner(host.Handle));
            DsError.ThrowExceptionForHR(
                videoWindow.put_WindowStyle(
                    WindowStyle.Child | WindowStyle.ClipChildren | WindowStyle.ClipSiblings));

            ResizeVideo();
            DsError.ThrowExceptionForHR(videoWindow.put_Visible(OABool.True));
            DsError.ThrowExceptionForHR(mediaControl.Run());

            IsRunning = true;
            StatusMessage = "Live preview";
            return true;
        }
        catch (Exception ex)
        {
            StopGraph();
            StatusMessage = $"Preview unavailable: {ex.Message}";
            return false;
        }
        finally
        {
            foreach (DsDevice device in devices)
            {
                device.Dispose();
            }
        }
    }

    public void Stop()
    {
        if (disposed)
        {
            return;
        }

        StopGraph();
        StatusMessage = "Preview off";
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        host.Resize -= HandleHostResize;
        StopGraph();
        disposed = true;
        GC.SuppressFinalize(this);
    }

    private void HandleHostResize(object? sender, EventArgs e)
    {
        ResizeVideo();
    }

    private void ResizeVideo()
    {
        if (videoWindow is null || host.ClientSize.Width <= 0 || host.ClientSize.Height <= 0)
        {
            return;
        }

        int hostWidth = host.ClientSize.Width;
        int hostHeight = host.ClientSize.Height;
        int videoWidth = hostWidth;
        int videoHeight = hostHeight;

        if (hostWidth / (double)hostHeight > videoAspectRatio)
        {
            videoWidth = Math.Max(1, (int)Math.Round(hostHeight * videoAspectRatio));
        }
        else
        {
            videoHeight = Math.Max(1, (int)Math.Round(hostWidth / videoAspectRatio));
        }

        int left = (hostWidth - videoWidth) / 2;
        int top = (hostHeight - videoHeight) / 2;
        videoWindow.SetWindowPosition(left, top, videoWidth, videoHeight);
    }

    private void ConfigureVideoFormat()
    {
        if (captureGraphBuilder is null || sourceFilter is null)
        {
            return;
        }

        object? streamConfigObject = null;
        int result = captureGraphBuilder.FindInterface(
            PinCategory.Capture,
            MediaType.Video,
            sourceFilter,
            typeof(IAMStreamConfig).GUID,
            out streamConfigObject);

        if (result < 0)
        {
            result = captureGraphBuilder.FindInterface(
                PinCategory.Preview,
                MediaType.Video,
                sourceFilter,
                typeof(IAMStreamConfig).GUID,
                out streamConfigObject);
        }

        if (result < 0 || streamConfigObject is not IAMStreamConfig streamConfig)
        {
            ReleaseComObject(ref streamConfigObject);
            return;
        }

        try
        {
            SelectPreferredFormat(streamConfig);
            ReadCurrentAspectRatio(streamConfig);
        }
        finally
        {
            streamConfigObject = null;
            if (Marshal.IsComObject(streamConfig))
            {
                Marshal.FinalReleaseComObject(streamConfig);
            }
        }
    }

    private void ConfigureMirrorFilter(IFilterGraph2 graphBuilder2)
    {
        sampleGrabberFilter = (IBaseFilter)new SampleGrabber();
        sampleGrabber = (ISampleGrabber)sampleGrabberFilter;

        DsError.ThrowExceptionForHR(
            graphBuilder2.AddFilter(sampleGrabberFilter, "Preview mirror"));

        var mediaType = new AMMediaType
        {
            majorType = MediaType.Video,
            subType = MediaSubType.RGB24,
            formatType = FormatType.VideoInfo,
        };

        try
        {
            DsError.ThrowExceptionForHR(sampleGrabber.SetMediaType(mediaType));
            DsError.ThrowExceptionForHR(sampleGrabber.SetOneShot(false));
            DsError.ThrowExceptionForHR(sampleGrabber.SetBufferSamples(false));
        }
        finally
        {
            DsUtils.FreeAMMediaType(mediaType);
        }
    }

    private void ConfigureMirrorCallback()
    {
        if (sampleGrabber is null)
        {
            return;
        }

        var connectedMediaType = new AMMediaType();

        try
        {
            DsError.ThrowExceptionForHR(sampleGrabber.GetConnectedMediaType(connectedMediaType));

            if (!TryGetBitmapInfoHeader(connectedMediaType, out BitmapInfoHeader? bitmapInfo) ||
                bitmapInfo is null)
            {
                throw new InvalidOperationException("Could not determine the preview pixel format.");
            }

            int bytesPerPixel = bitmapInfo.BitCount / 8;
            int stride = ((bitmapInfo.Width * bitmapInfo.BitCount + 31) / 32) * 4;

            if (bytesPerPixel is not 3 and not 4)
            {
                throw new NotSupportedException(
                    $"Preview mirroring does not support {bitmapInfo.BitCount}-bit video.");
            }

            mirrorCallback.Configure(
                Math.Abs(bitmapInfo.Width),
                Math.Abs(bitmapInfo.Height),
                Math.Abs(stride),
                bytesPerPixel);

            DsError.ThrowExceptionForHR(sampleGrabber.SetCallback(mirrorCallback, 1));
        }
        finally
        {
            DsUtils.FreeAMMediaType(connectedMediaType);
        }
    }

    private void SelectPreferredFormat(IAMStreamConfig streamConfig)
    {
        int result = streamConfig.GetNumberOfCapabilities(out int formatCount, out int capabilitySize);
        if (result < 0 || formatCount <= 0 || capabilitySize <= 0)
        {
            return;
        }

        IntPtr capabilities = Marshal.AllocCoTaskMem(capabilitySize);
        AMMediaType? preferredFormat = null;
        double preferredScore = double.MaxValue;

        try
        {
            for (int index = 0; index < formatCount; index++)
            {
                AMMediaType? format = null;

                try
                {
                    if (streamConfig.GetStreamCaps(index, out format, capabilities) < 0 ||
                        !TryGetVideoSize(format, out Size size))
                    {
                        continue;
                    }

                    double aspectDifference = Math.Abs(size.Width / (double)size.Height - PreferredAspectRatio);
                    double score =
                        aspectDifference * 1_000_000 +
                        Math.Abs(size.Width - 640) +
                        Math.Abs(size.Height - 360);

                    if (score >= preferredScore)
                    {
                        continue;
                    }

                    FreeMediaType(ref preferredFormat);
                    preferredFormat = format;
                    format = null;
                    preferredScore = score;
                }
                finally
                {
                    FreeMediaType(ref format);
                }
            }

            // Avoid replacing a valid native format unless the camera offers a
            // genuinely widescreen mode.
            if (preferredFormat is not null &&
                TryGetVideoSize(preferredFormat, out Size preferredSize) &&
                Math.Abs(preferredSize.Width / (double)preferredSize.Height - PreferredAspectRatio) < 0.03)
            {
                streamConfig.SetFormat(preferredFormat);
            }
        }
        finally
        {
            FreeMediaType(ref preferredFormat);
            Marshal.FreeCoTaskMem(capabilities);
        }
    }

    private void ReadCurrentAspectRatio(IAMStreamConfig streamConfig)
    {
        AMMediaType? format = null;

        try
        {
            if (streamConfig.GetFormat(out format) >= 0 &&
                TryGetVideoSize(format, out Size size))
            {
                videoAspectRatio = size.Width / (double)size.Height;
            }
        }
        finally
        {
            FreeMediaType(ref format);
        }
    }

    private static bool TryGetVideoSize(AMMediaType? format, out Size size)
    {
        size = Size.Empty;

        if (!TryGetBitmapInfoHeader(format, out BitmapInfoHeader? bitmapInfo) ||
            bitmapInfo is null)
        {
            return false;
        }

        size = new Size(Math.Abs(bitmapInfo.Width), Math.Abs(bitmapInfo.Height));
        return size.Width > 0 && size.Height > 0;
    }

    private static bool TryGetBitmapInfoHeader(
        AMMediaType? format,
        out BitmapInfoHeader? bitmapInfo)
    {
        bitmapInfo = null;

        if (format is null || format.formatPtr == IntPtr.Zero)
        {
            return false;
        }

        if (format.formatType == FormatType.VideoInfo)
        {
            var header = (VideoInfoHeader?)Marshal.PtrToStructure(
                format.formatPtr,
                typeof(VideoInfoHeader));

            if (header is not null)
            {
                bitmapInfo = header.BmiHeader;
            }
        }
        else if (format.formatType == FormatType.VideoInfo2)
        {
            var header = (VideoInfoHeader2?)Marshal.PtrToStructure(
                format.formatPtr,
                typeof(VideoInfoHeader2));

            if (header is not null)
            {
                bitmapInfo = header.BmiHeader;
            }
        }

        return bitmapInfo is not null;
    }

    private static void FreeMediaType(ref AMMediaType? mediaType)
    {
        if (mediaType is not null)
        {
            DsUtils.FreeAMMediaType(mediaType);
            mediaType = null;
        }
    }

    private void StopGraph()
    {
        IsRunning = false;

        if (videoWindow is not null)
        {
            TryComCall(() => videoWindow.put_Visible(OABool.False));
            TryComCall(() => videoWindow.put_Owner(IntPtr.Zero));
        }

        if (mediaControl is not null)
        {
            TryComCall(mediaControl.Stop);
        }

        if (sampleGrabber is not null)
        {
            TryComCall(() => sampleGrabber.SetCallback(null!, 0));
        }

        videoWindow = null;
        mediaControl = null;
        sampleGrabber = null;

        ReleaseComObject(ref sampleGrabberFilter);
        ReleaseComObject(ref sourceFilter);
        ReleaseComObject(ref captureGraphBuilder);
        ReleaseComObject(ref graphBuilder);
        videoAspectRatio = PreferredAspectRatio;
    }

    private static void TryComCall(Func<int> operation)
    {
        try
        {
            operation();
        }
        catch (COMException)
        {
            // The device may already have disappeared; cleanup should still continue.
        }
    }

    private static void ReleaseComObject<T>(ref T? value) where T : class
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }

        value = null;
    }
}

internal sealed class MirrorSampleGrabberCallback : ISampleGrabberCB
{
    private volatile bool mirrorEnabled;
    private int width;
    private int height;
    private int stride;
    private int bytesPerPixel;

    public bool MirrorEnabled
    {
        get => mirrorEnabled;
        set => mirrorEnabled = value;
    }

    public void Configure(int width, int height, int stride, int bytesPerPixel)
    {
        this.width = width;
        this.height = height;
        this.stride = stride;
        this.bytesPerPixel = bytesPerPixel;
    }

    public int SampleCB(double sampleTime, IMediaSample sample)
    {
        return 0;
    }

    public unsafe int BufferCB(double sampleTime, IntPtr buffer, int bufferLength)
    {
        if (!mirrorEnabled ||
            buffer == IntPtr.Zero ||
            width <= 1 ||
            height <= 0 ||
            stride <= 0 ||
            bytesPerPixel <= 0 ||
            (long)stride * height > bufferLength)
        {
            return 0;
        }

        byte* firstRow = (byte*)buffer;

        for (int y = 0; y < height; y++)
        {
            byte* row = firstRow + y * stride;

            for (int left = 0, right = width - 1; left < right; left++, right--)
            {
                byte* leftPixel = row + left * bytesPerPixel;
                byte* rightPixel = row + right * bytesPerPixel;

                for (int channel = 0; channel < bytesPerPixel; channel++)
                {
                    (leftPixel[channel], rightPixel[channel]) =
                        (rightPixel[channel], leftPixel[channel]);
                }
            }
        }

        return 0;
    }
}
