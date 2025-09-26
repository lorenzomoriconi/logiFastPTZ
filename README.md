# LogiFastPTZ

A .NET console application for controlling Logitech motorized webcams connected via USB. This tool provides simple keyboard controls for pan, tilt, and zoom operations.

## Features

- **Pan & Tilt Control**: Use WASD keys to control camera movement
  - `W` - Tilt up
  - `S` - Tilt down  
  - `A` - Pan left
  - `D` - Pan right
- **Zoom Control**: Use Q and E keys for zoom operations
  - `Q` - Zoom out
  - `E` - Zoom in
- Real-time USB communication with Logitech webcams
- Console-based interface for quick camera adjustments

## Requirements

- .NET 8.0 or later
- Windows operating system
- Compatible Logitech motorized webcam (tested with BCC950)
- USB connection to the webcam

## Installation

1. Clone this repository:
   ```bash
   git clone https://github.com/lorenzomoriconi/logiFastPTZ.git
   cd logiFastPTZ
   ```

2. Build the application:
   ```bash
   dotnet build
   ```

3. Run the application:
   ```bash
   dotnet run --project LogiFastPTZ
   ```

## Usage

1. Connect your Logitech motorized webcam via USB
2. Run the application
3. Use the following keyboard controls:
   - `W/A/S/D` - Control pan and tilt
   - `Q/E` - Control zoom
   - `ESC` or `Ctrl+C` - Exit the application

## Credits

This project uses the PTZ control library created by **Scott Hanselman**:
- Repository: [Logitech-BCC950-PTZ-Lib](https://github.com/shanselman/Logitech-BCC950-PTZ-Lib)
- The library handles the low-level USB communication and DirectShow integration with Logitech webcams

## Compatible Devices

- Logitech BCC950 ConferenceCam
- Other Logitech motorized webcams (compatibility may vary)

## License

This project is open source. Please check the original library license for the PTZ control components.
