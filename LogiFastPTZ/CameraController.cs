using PTZ;

public sealed class CameraController
{
    private const string DefaultDeviceName = "BCC950 ConferenceCam";
    private const int DefaultMoveStep = 25;
    private const int EffectiveZoomMax = 120;

    private PTZDevice? camera;

    public string DeviceName { get; } = DefaultDeviceName;

    public string StatusMessage { get; private set; } = "Not connected";

    public int? CurrentZoom
    {
        get
        {
            try
            {
                return IsConnected && camera is not null
                    ? Math.Min(camera.GetCurrentZoom(), GetMaximumZoom())
                    : null;
            }
            catch
            {
                return null;
            }
        }
    }

    public bool IsConnected => camera?.IsConnected() == true;

    public bool Connect()
    {
        try
        {
            camera = PTZDevice.GetDevice(DeviceName, PTZType.Relative);
            StatusMessage = "Connected";

            if (camera.GetCurrentZoom() > GetMaximumZoom())
            {
                SetZoomCore(GetMaximumZoom());
            }

            return true;
        }
        catch (Exception ex)
        {
            camera = null;
            StatusMessage = $"Camera not found: {ex.Message}";
            return false;
        }
    }

    public bool EnsureConnected()
    {
        return IsConnected || Connect();
    }

    public bool TiltUp() => Move(0, DefaultMoveStep);

    public bool TiltDown() => Move(0, -DefaultMoveStep);

    public bool PanLeft() => Move(DefaultMoveStep, 0);

    public bool PanRight() => Move(-DefaultMoveStep, 0);

    public bool ZoomOut()
    {
        if (!EnsureConnected() || camera is null)
        {
            return false;
        }

        int currentZoom = camera.GetCurrentZoom();

        if (currentZoom > GetMaximumZoom())
        {
            SetZoomCore(GetMaximumZoom());
        }
        else if (currentZoom > camera.ZoomMin)
        {
            camera.Zoom(-1);
        }

        StatusMessage = "Connected";
        return true;
    }

    public bool ZoomIn()
    {
        if (!EnsureConnected() || camera is null)
        {
            return false;
        }

        int currentZoom = camera.GetCurrentZoom();
        int maximumZoom = GetMaximumZoom();

        if (currentZoom > maximumZoom)
        {
            SetZoomCore(maximumZoom);
        }
        else if (currentZoom < maximumZoom)
        {
            camera.Zoom(1);
        }

        StatusMessage = "Connected";
        return true;
    }

    public bool SetZoom(int zoom)
    {
        if (!EnsureConnected() || camera is null)
        {
            return false;
        }

        return SetZoomCore(zoom);
    }

    private bool SetZoomCore(int zoom)
    {
        if (camera is null)
        {
            return false;
        }

        int targetZoom = Math.Clamp(zoom, camera.ZoomMin, GetMaximumZoom());
        int currentZoom = camera.GetCurrentZoom();
        int remainingAttempts = Math.Abs(currentZoom - targetZoom) + 1;

        while (currentZoom != targetZoom && remainingAttempts-- > 0)
        {
            int nextZoom = camera.Zoom(Math.Sign(targetZoom - currentZoom));

            if (nextZoom == currentZoom)
            {
                break;
            }

            currentZoom = nextZoom;
        }

        StatusMessage = "Connected";
        return currentZoom == targetZoom;
    }

    private int GetMaximumZoom()
    {
        return camera is null ? EffectiveZoomMax : Math.Min(camera.ZoomMax, EffectiveZoomMax);
    }

    private bool Move(int pan, int tilt)
    {
        if (!EnsureConnected() || camera is null)
        {
            return false;
        }

        camera.Move(pan, tilt);
        StatusMessage = "Connected";
        return true;
    }
}
