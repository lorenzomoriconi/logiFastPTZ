using PTZ;

public sealed class CameraController
{
    private const string DefaultDeviceName = "BCC950 ConferenceCam";
    private const int DefaultMoveStep = 25;
    private const int DefaultZoomStep = 2;
    private const int DefaultZoomMax = 120;

    private PTZDevice? camera;

    public string DeviceName { get; set; } = DefaultDeviceName;

    public int MoveStep { get; set; } = DefaultMoveStep;

    public int ZoomStep { get; set; } = DefaultZoomStep;

    public string StatusMessage { get; private set; } = "Not connected";

    public int? CurrentZoom => camera?.GetCurrentZoom();

    public bool IsConnected => camera?.IsConnected() == true;

    public bool Connect()
    {
        try
        {
            camera = PTZDevice.GetDevice(DeviceName, PTZType.Relative);
            StatusMessage = "Connected";
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

    public bool TiltUp() => Move(0, MoveStep);

    public bool TiltDown() => Move(0, -MoveStep);

    public bool PanLeft() => Move(MoveStep, 0);

    public bool PanRight() => Move(-MoveStep, 0);

    public bool ZoomOut()
    {
        if (!EnsureConnected() || camera is null)
        {
            return false;
        }

        if (camera.GetCurrentZoom() > camera.ZoomMin)
        {
            camera.Zoom(-ZoomStep);
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

        if (camera.GetCurrentZoom() < DefaultZoomMax)
        {
            camera.Zoom(ZoomStep);
        }

        StatusMessage = "Connected";
        return true;
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
