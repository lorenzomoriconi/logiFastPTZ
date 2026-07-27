# LogiFastPTZ

A tiny Windows .NET app for controlling Logitech motorized webcams connected via USB. It provides a minimal floating WinForms remote for pan, tilt, zoom, reconnect, and status feedback.

## Features

- **Tiny WinForms remote** with directional controls, zoom buttons, camera status, and reconnect/exit actions
- **Pan & Tilt Control** via on-screen buttons or keyboard shortcuts
  - `W` / `▲` - Tilt up
  - `S` / `▼` - Tilt down
  - `A` / `◀` - Pan left
  - `D` / `▶` - Pan right
- **Zoom Control** via on-screen buttons or keyboard shortcuts
  - `Q` / `Zoom −` - Zoom out
  - `E` / `Zoom +` - Zoom in
- **Hold-to-repeat controls** for smoother movement from the UI buttons
- Adjustable move and zoom step sizes
- Editable camera device name, defaulting to `BCC950 ConferenceCam`
- Optional always-on-top mode
- Real-time USB communication with Logitech webcams

## Requirements

- .NET 8.0 or later
- Windows operating system
- Compatible Logitech motorized webcam (tested with BCC950)
- USB connection to the webcam
- The `Logitech-BCC950-PTZ-Lib` project available next to this project, as referenced by the solution

## Installation

1. Clone this repository together with its PTZ library submodule:
   ```bash
   git clone --recurse-submodules https://github.com/lorenzomoriconi/logiFastPTZ.git
   cd logiFastPTZ
   ```

2. If you already cloned the repository without submodules, initialize the PTZ library now:
   ```bash
   git submodule update --init --recursive
   ```

   The command must create this project inside the repository root:
   ```text
   Logitech-BCC950-PTZ-Lib/PTZDevice.csproj
   ```

3. Build the application:
   ```bash
   dotnet build LogiFastPTZ.sln -c Release
   ```

4. Run the application:
   ```bash
   dotnet run --project LogiFastPTZ
   ```

5. To produce a self-contained 64-bit Windows build:
   ```powershell
   dotnet publish .\LogiFastPTZ\LogiFastPTZ.csproj `
     -c Release `
     -r win-x64 `
     --self-contained true
   ```

   The executable is written to:
   ```text
   LogiFastPTZ\bin\Release\net8.0-windows\win-x64\publish\LogiFastPTZ.exe
   ```

### Missing `PTZDevice.csproj` or `PTZ` namespace

If the build reports that `..\Logitech-BCC950-PTZ-Lib\PTZDevice.csproj` does not exist, the Git submodule was not downloaded. From the repository root, run:

```bash
git submodule update --init --recursive
```

Then repeat the build or publish command. Do not clone the library one directory above this repository: the solution expects it in the `Logitech-BCC950-PTZ-Lib` directory at the repository root.

## Usage

1. Connect your Logitech motorized webcam via USB.
2. Run the application.
3. Use the on-screen controls or keyboard shortcuts while the window is focused:
   - `W/A/S/D` - Control pan and tilt
   - `Q/E` - Control zoom
   - `R` - Reconnect to the camera
   - `ESC` or the Exit button - Exit the application
4. Optionally change the move/zoom step sizes or camera device name in the small settings area.
5. Enable **Always on top** if you want the controller to float over other windows.

## Credits

This project uses the PTZ control library created by **Scott Hanselman**:
- Repository: [Logitech-BCC950-PTZ-Lib](https://github.com/shanselman/Logitech-BCC950-PTZ-Lib)
- The library handles the low-level USB communication and DirectShow integration with Logitech webcams

## Compatible Devices

- Logitech BCC950 ConferenceCam
- Other Logitech motorized webcams (compatibility may vary)

## License

This project is open source. Please check the original library license for the PTZ control components.
