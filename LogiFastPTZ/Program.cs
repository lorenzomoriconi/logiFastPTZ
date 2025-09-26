using PTZ;


int moveStep = 25;
int zoomStep = 2;
void printInfo(PTZDevice cam)
{
    Console.WriteLine(cam.GetCurrentZoom());
}

PTZDevice cam = null;

Boolean ConnectCamera()
{
    try
    {
        cam = PTZDevice.GetDevice("BCC950 ConferenceCam", PTZType.Relative);

        Console.WriteLine(cam.ZoomStep);
        Console.WriteLine("Cam connected");
        return true;
    }
    catch (Exception)
    {
        Console.WriteLine("Cam not found");
        cam = null;
        return false;
    }
}

ConnectCamera();

while (true)
{
    try
    {
        if (cam is null || !cam.IsConnected())
        {
            if (!ConnectCamera())
            {
                continue;
            }
        }

        var key = Console.ReadKey(true).Key;
        switch (key)
        {
            case ConsoleKey.W:
                cam.Move(0, moveStep);
                break;
            case ConsoleKey.A:
                cam.Move(moveStep, 0);
                break;
            case ConsoleKey.S:
                cam.Move(0, -moveStep);
                break;
            case ConsoleKey.D:
                cam.Move(-moveStep, 0);
                break;
            case ConsoleKey.Q:
                if (cam.GetCurrentZoom() > cam.ZoomMin)
                {
                    cam.Zoom(-1 * zoomStep);
                }
                break;
            case ConsoleKey.E:
                if (cam.GetCurrentZoom() < 120)
                {
                    cam.Zoom(1 * zoomStep);
                }
                break;
            case ConsoleKey.R:
                {
                    ConnectCamera();
                    break;
                }
            default:
                break;
        }
        printInfo(cam);
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
    }

}

