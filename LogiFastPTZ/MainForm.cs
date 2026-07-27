using System.Drawing;
using System.Windows.Forms;

public sealed class MainForm : Form
{
    private readonly CameraController cameraController = new();
    private readonly Label statusLabel = new();
    private readonly Label zoomLabel = new();
    private readonly TextBox deviceNameTextBox = new();
    private readonly NumericUpDown moveStepInput = new();
    private readonly NumericUpDown zoomStepInput = new();
    private readonly CheckBox alwaysOnTopCheckBox = new();
    private readonly System.Windows.Forms.Timer repeatTimer = new();
    private Action? repeatAction;

    public MainForm()
    {
        Text = "LogiFastPTZ";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(300, 315);
        KeyPreview = true;

        BuildUi();
        WireEvents();

        repeatTimer.Interval = 175;
        repeatTimer.Tick += (_, _) => RunCameraAction(repeatAction);

        cameraController.Connect();
        RefreshStatus();
    }

    private void BuildUi()
    {
        statusLabel.AutoSize = false;
        statusLabel.Location = new Point(12, 12);
        statusLabel.Size = new Size(276, 24);
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        Controls.Add(statusLabel);

        zoomLabel.AutoSize = false;
        zoomLabel.Location = new Point(12, 36);
        zoomLabel.Size = new Size(276, 20);
        Controls.Add(zoomLabel);

        var upButton = CreateButton("▲", 117, 66, (_, _) => { });
        var leftButton = CreateButton("◀", 62, 111, (_, _) => { });
        var stopButton = CreateButton("●", 117, 111, (_, _) => StopRepeating());
        var rightButton = CreateButton("▶", 172, 111, (_, _) => { });
        var downButton = CreateButton("▼", 117, 156, (_, _) => { });
        var zoomOutButton = CreateButton("Zoom −", 62, 207, (_, _) => { }, width: 80);
        var zoomInButton = CreateButton("Zoom +", 158, 207, (_, _) => { }, width: 80);

        AddRepeatBehavior(upButton, cameraController.TiltUp);
        AddRepeatBehavior(leftButton, cameraController.PanLeft);
        AddRepeatBehavior(rightButton, cameraController.PanRight);
        AddRepeatBehavior(downButton, cameraController.TiltDown);
        AddRepeatBehavior(zoomOutButton, cameraController.ZoomOut);
        AddRepeatBehavior(zoomInButton, cameraController.ZoomIn);

        var reconnectButton = CreateButton("Reconnect", 12, 247, (_, _) => ConnectCamera(), width: 92);

        var exitButton = CreateButton("Exit", 196, 247, (_, _) => Close(), width: 92);

        alwaysOnTopCheckBox.Text = "Always on top";
        alwaysOnTopCheckBox.Location = new Point(110, 252);
        alwaysOnTopCheckBox.Size = new Size(110, 20);
        Controls.Add(alwaysOnTopCheckBox);

        var deviceLabel = new Label
        {
            Text = "Camera",
            Location = new Point(12, 283),
            Size = new Size(48, 20),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        Controls.Add(deviceLabel);

        deviceNameTextBox.Location = new Point(63, 281);
        deviceNameTextBox.Size = new Size(225, 23);
        deviceNameTextBox.Text = cameraController.DeviceName;
        Controls.Add(deviceNameTextBox);

        var moveStepLabel = new Label
        {
            Text = "Move",
            Location = new Point(12, 188),
            Size = new Size(38, 20),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        Controls.Add(moveStepLabel);

        moveStepInput.Location = new Point(50, 186);
        moveStepInput.Minimum = 1;
        moveStepInput.Maximum = 100;
        moveStepInput.Value = cameraController.MoveStep;
        moveStepInput.Size = new Size(58, 23);
        Controls.Add(moveStepInput);

        var zoomStepLabel = new Label
        {
            Text = "Zoom",
            Location = new Point(194, 188),
            Size = new Size(42, 20),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        Controls.Add(zoomStepLabel);

        zoomStepInput.Location = new Point(236, 186);
        zoomStepInput.Minimum = 1;
        zoomStepInput.Maximum = 20;
        zoomStepInput.Value = cameraController.ZoomStep;
        zoomStepInput.Size = new Size(52, 23);
        Controls.Add(zoomStepInput);
    }

    private Button CreateButton(string text, int x, int y, EventHandler clickHandler, int width = 52, int height = 36)
    {
        var button = new Button
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, height),
            TabStop = false,
        };

        button.Click += clickHandler;
        Controls.Add(button);
        return button;
    }

    private void WireEvents()
    {
        alwaysOnTopCheckBox.CheckedChanged += (_, _) => TopMost = alwaysOnTopCheckBox.Checked;
        deviceNameTextBox.TextChanged += (_, _) => cameraController.DeviceName = deviceNameTextBox.Text;
        moveStepInput.ValueChanged += (_, _) => cameraController.MoveStep = (int)moveStepInput.Value;
        zoomStepInput.ValueChanged += (_, _) => cameraController.ZoomStep = (int)zoomStepInput.Value;
        KeyDown += HandleKeyDown;
        KeyUp += (_, _) => StopRepeating();
    }

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            Close();
            return;
        }

        Action? action = e.KeyCode switch
        {
            Keys.W => () => cameraController.TiltUp(),
            Keys.A => () => cameraController.PanLeft(),
            Keys.S => () => cameraController.TiltDown(),
            Keys.D => () => cameraController.PanRight(),
            Keys.Q => () => cameraController.ZoomOut(),
            Keys.E => () => cameraController.ZoomIn(),
            Keys.R => ConnectCamera,
            _ => null,
        };

        if (action is null)
        {
            return;
        }

        e.Handled = true;
        RunCameraAction(action);
    }

    private void AddRepeatBehavior(Button button, Func<bool> action)
    {
        button.MouseDown += (_, _) => StartRepeating(() => action());
        button.MouseUp += (_, _) => StopRepeating();
        button.MouseLeave += (_, _) => StopRepeating();
    }

    private void StartRepeating(Action action)
    {
        repeatAction = action;
        RunCameraAction(repeatAction);
        repeatTimer.Start();
    }

    private void StopRepeating()
    {
        repeatTimer.Stop();
        repeatAction = null;
    }

    private void ConnectCamera()
    {
        cameraController.Connect();
        RefreshStatus();
    }

    private void RunCameraAction(Action? action)
    {
        if (action is null)
        {
            return;
        }

        try
        {
            action();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Camera error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        RefreshStatus();
    }

    private void RunCameraAction(Func<bool> action)
    {
        RunCameraAction(() => action());
    }

    private void RefreshStatus()
    {
        statusLabel.Text = $"Status: {cameraController.StatusMessage}";
        zoomLabel.Text = cameraController.CurrentZoom is { } zoom ? $"Zoom: {zoom}" : "Zoom: —";
    }
}
