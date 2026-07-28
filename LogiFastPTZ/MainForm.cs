using System.Drawing;
using System.Windows.Forms;

public sealed class MainForm : Form
{
    private readonly CameraController cameraController = new();
    private readonly AppSettingsStore settingsStore = new();
    private readonly Label statusLabel = new();
    private readonly Label zoomLabel = new();
    private readonly Label previewStatusLabel = new();
    private readonly Label feedbackLabel = new();
    private readonly CheckBox alwaysOnTopCheckBox = new();
    private readonly CheckBox mirrorPreviewCheckBox = new();
    private readonly CheckBox previewCheckBox = new();
    private readonly Panel previewPanel = new();
    private readonly System.Windows.Forms.Timer repeatTimer = new();
    private readonly CameraPreview cameraPreview;
    private AppSettings savedSettings;
    private Action? repeatAction;

    public MainForm()
    {
        bool hasSavedSettings = settingsStore.TryLoad(out savedSettings, out string? settingsError);

        Text = "LogiFastPTZ";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(420, 610);
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(248, 249, 251);
        KeyPreview = true;

        BuildUi();
        cameraPreview = new CameraPreview(previewPanel);
        cameraPreview.IsMirrored = savedSettings.MirrorPreview;
        WireEvents();

        repeatTimer.Interval = 175;
        repeatTimer.Tick += (_, _) => RunCameraAction(repeatAction);

        TopMost = savedSettings.AlwaysOnTop;
        cameraController.Connect();

        if (hasSavedSettings && savedSettings.Zoom is int savedZoom)
        {
            bool restored = cameraController.SetZoom(savedZoom);
            SetFeedback(restored ? $"Restored zoom {savedZoom}" : "Could not restore saved zoom");
        }
        else if (settingsError is not null)
        {
            SetFeedback("Could not read the saved preset");
        }

        RefreshStatus();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        StartPreview();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            repeatTimer.Dispose();
            cameraPreview.Dispose();
        }

        base.Dispose(disposing);
    }

    private void BuildUi()
    {
        statusLabel.AutoSize = false;
        statusLabel.Location = new Point(16, 12);
        statusLabel.Size = new Size(280, 24);
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        statusLabel.AutoEllipsis = true;
        statusLabel.Font = new Font(Font, FontStyle.Bold);
        Controls.Add(statusLabel);

        zoomLabel.AutoSize = false;
        zoomLabel.Location = new Point(304, 12);
        zoomLabel.Size = new Size(100, 24);
        zoomLabel.TextAlign = ContentAlignment.MiddleRight;
        zoomLabel.Font = new Font(Font, FontStyle.Bold);
        Controls.Add(zoomLabel);

        previewPanel.BackColor = Color.Black;
        previewPanel.Location = new Point(16, 70);
        previewPanel.Size = new Size(388, 220);
        previewPanel.BorderStyle = BorderStyle.FixedSingle;
        Controls.Add(previewPanel);

        previewStatusLabel.AutoSize = false;
        previewStatusLabel.Dock = DockStyle.Fill;
        previewStatusLabel.ForeColor = Color.LightGray;
        previewStatusLabel.Text = savedSettings.LivePreview ? "Starting preview…" : "Preview off";
        previewStatusLabel.TextAlign = ContentAlignment.MiddleCenter;
        previewPanel.Controls.Add(previewStatusLabel);

        previewCheckBox.Text = "Live preview";
        previewCheckBox.Location = new Point(16, 42);
        previewCheckBox.Size = new Size(110, 24);
        previewCheckBox.Checked = savedSettings.LivePreview;
        Controls.Add(previewCheckBox);

        mirrorPreviewCheckBox.Text = "Mirror preview";
        mirrorPreviewCheckBox.Location = new Point(145, 42);
        mirrorPreviewCheckBox.Size = new Size(122, 24);
        mirrorPreviewCheckBox.Checked = savedSettings.MirrorPreview;
        Controls.Add(mirrorPreviewCheckBox);

        alwaysOnTopCheckBox.Text = "Always on top";
        alwaysOnTopCheckBox.Location = new Point(292, 42);
        alwaysOnTopCheckBox.Size = new Size(112, 24);
        alwaysOnTopCheckBox.Checked = savedSettings.AlwaysOnTop;
        Controls.Add(alwaysOnTopCheckBox);

        Controls.Add(CreateSectionLabel("PAN & TILT", 26, 306, 190));
        Controls.Add(CreateSectionLabel("ZOOM", 238, 306, 166));

        Button upButton = CreateButton("▲", 92, 330, width: 60, height: 42);
        Button leftButton = CreateButton("◀", 26, 378, width: 60, height: 42);
        Button stopButton = CreateButton("●", 92, 378, width: 60, height: 42);
        Button rightButton = CreateButton("▶", 158, 378, width: 60, height: 42);
        Button downButton = CreateButton("▼", 92, 426, width: 60, height: 42);

        Button zoomOutButton = CreateButton("−", 238, 350, width: 76, height: 58);
        zoomOutButton.Font = new Font(Font.FontFamily, 16F);
        Button zoomInButton = CreateButton("+", 328, 350, width: 76, height: 58);
        zoomInButton.Font = new Font(Font.FontFamily, 16F);

        var zoomHintLabel = new Label
        {
            Text = "Hold to adjust",
            Location = new Point(238, 418),
            Size = new Size(166, 22),
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = SystemColors.GrayText,
        };
        Controls.Add(zoomHintLabel);

        AddRepeatBehavior(upButton, cameraController.TiltUp);
        AddRepeatBehavior(leftButton, cameraController.PanLeft);
        AddRepeatBehavior(rightButton, cameraController.PanRight);
        AddRepeatBehavior(downButton, cameraController.TiltDown);
        AddRepeatBehavior(zoomOutButton, cameraController.ZoomOut);
        AddRepeatBehavior(zoomInButton, cameraController.ZoomIn);
        stopButton.Click += (_, _) => StopRepeating();

        Button saveButton = CreateButton("Save preset", 16, 484, width: 132, height: 40);
        saveButton.Font = new Font(Font, FontStyle.Bold);
        saveButton.FlatStyle = FlatStyle.Flat;
        saveButton.FlatAppearance.BorderSize = 0;
        saveButton.BackColor = SystemColors.Highlight;
        saveButton.ForeColor = SystemColors.HighlightText;
        saveButton.Click += (_, _) => SavePreset();

        Button restoreButton = CreateButton("Restore", 158, 484, width: 106, height: 40);
        restoreButton.Click += (_, _) => RestorePreset();

        feedbackLabel.AutoSize = false;
        feedbackLabel.Location = new Point(274, 484);
        feedbackLabel.Size = new Size(130, 40);
        feedbackLabel.TextAlign = ContentAlignment.MiddleRight;
        feedbackLabel.ForeColor = SystemColors.GrayText;
        feedbackLabel.AutoEllipsis = true;
        Controls.Add(feedbackLabel);

        var separator = new Label
        {
            BorderStyle = BorderStyle.Fixed3D,
            Location = new Point(16, 540),
            Size = new Size(388, 2),
        };
        Controls.Add(separator);

        Button reconnectButton = CreateButton("Reconnect", 16, 556, width: 110, height: 38);
        reconnectButton.Click += (_, _) => ConnectCamera();

        Button exitButton = CreateButton("Exit", 314, 556, width: 90, height: 38);
        exitButton.Click += (_, _) => Close();
    }

    private Label CreateSectionLabel(string text, int x, int y, int width)
    {
        return new Label
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, 18),
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = SystemColors.GrayText,
            Font = new Font(Font, FontStyle.Bold),
        };
    }

    private Button CreateButton(string text, int x, int y, int width, int height)
    {
        var button = new Button
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, height),
            TabStop = false,
            UseVisualStyleBackColor = true,
        };

        Controls.Add(button);
        return button;
    }

    private void WireEvents()
    {
        alwaysOnTopCheckBox.CheckedChanged += (_, _) => TopMost = alwaysOnTopCheckBox.Checked;
        mirrorPreviewCheckBox.CheckedChanged += (_, _) =>
            cameraPreview.IsMirrored = mirrorPreviewCheckBox.Checked;
        previewCheckBox.CheckedChanged += (_, _) =>
        {
            if (previewCheckBox.Checked)
            {
                StartPreview();
            }
            else
            {
                StopPreview();
            }
        };

        KeyDown += HandleKeyDown;
        KeyUp += (_, _) => StopRepeating();
        FormClosing += (_, _) =>
        {
            StopRepeating();
            cameraPreview.Stop();
        };
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
        cameraPreview.Stop();
        cameraController.Connect();

        if (previewCheckBox.Checked)
        {
            StartPreview();
        }

        RefreshStatus();
    }

    private void StartPreview()
    {
        if (!previewCheckBox.Checked || !IsHandleCreated)
        {
            return;
        }

        bool started = cameraPreview.Start(cameraController.DeviceName);
        previewStatusLabel.Text = cameraPreview.StatusMessage;
        previewStatusLabel.Visible = !started;
    }

    private void StopPreview()
    {
        cameraPreview.Stop();
        previewStatusLabel.Text = cameraPreview.StatusMessage;
        previewStatusLabel.Visible = true;
    }

    private void SavePreset()
    {
        try
        {
            if (!cameraController.EnsureConnected() || cameraController.CurrentZoom is not int zoom)
            {
                SetFeedback("Connect the camera first");
                return;
            }

            savedSettings = new AppSettings
            {
                Zoom = zoom,
                LivePreview = previewCheckBox.Checked,
                MirrorPreview = mirrorPreviewCheckBox.Checked,
                AlwaysOnTop = alwaysOnTopCheckBox.Checked,
            };

            settingsStore.Save(savedSettings);
            SetFeedback($"Saved zoom {zoom}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not save preset", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void RestorePreset()
    {
        if (!settingsStore.TryLoad(out AppSettings settings, out string? error))
        {
            SetFeedback(error is null ? "No preset saved yet" : "Could not read preset");
            return;
        }

        savedSettings = settings;
        previewCheckBox.Checked = settings.LivePreview;
        mirrorPreviewCheckBox.Checked = settings.MirrorPreview;
        alwaysOnTopCheckBox.Checked = settings.AlwaysOnTop;

        if (settings.Zoom is int zoom)
        {
            bool restored = cameraController.SetZoom(zoom);
            SetFeedback(restored ? $"Restored zoom {zoom}" : "Could not restore zoom");
        }

        RefreshStatus();
    }

    private void SetFeedback(string message)
    {
        feedbackLabel.Text = message;
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

    private void RefreshStatus()
    {
        statusLabel.Text = cameraController.StatusMessage;
        zoomLabel.Text = cameraController.CurrentZoom is { } zoom ? $"Zoom {zoom}" : "Zoom —";
    }
}
