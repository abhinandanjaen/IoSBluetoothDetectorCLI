# IoSBluetoothDetectorCLI

A lightweight Windows console application that detects nearby Apple devices (iPhone, iPad, Apple Watch, AirPods, and more) using Bluetooth Low Energy (BLE).

It passively listens for BLE advertisement packets, identifies Apple-made devices, and shows them live in the terminal with signal strength, inferred device type, and approximate distance.

Built with .NET 10 (C#) using Microsoft WinRT Bluetooth APIs, with privacy and security best practices.

> This is my first project on GitHub. Feedback and stars are welcome.

## Features

- Real-time scanning for nearby BLE devices
- Apple-only filtering using manufacturer ID 0x004C
- Device-type inference (Nearby iPhone/iPad, AirPods, Find My, and more)
- Live RSSI and rough proximity estimation
- First-seen and last-seen tracking with sighting counts
- Fully local execution (no internet calls, no telemetry)

## Sample Output

```text
========================================================
 IoSBluetoothDetectorCLI (Windows)

[09:55:21] Apple devices in range: 4

Address              Inferred kind                         RSSI    Best     Seen  Approx. distance

4B:CE:23:XX:XX:XX    Nearby (likely iPhone/iPad)           -40d    -40d       5x  Very close (<1 m)
DD:2D:6B:XX:XX:XX    Find My                               -51d    -51d       3x  Very close (<1 m)
4F:9E:68:XX:XX:XX    Nearby (likely iPhone/iPad)           -97d    -92d       2x  Far (>10 m)
6E:0F:CB:XX:XX:XX    Nearby (likely iPhone/iPad)           -99d    -99d       1x  Far (>10 m)
```

## How It Works

Apple devices broadcast small BLE advertisement packets as part of Continuity features (Handoff, AirDrop, Find My, and related services). This application processes those public broadcasts:

1. Scan: a BluetoothLEAdvertisementWatcher runs in active scanning mode.
2. Filter: only packets with Apple Bluetooth company identifier 0x004C are retained.
3. Classify: the first byte of Apple manufacturer payload (Continuity message type) is mapped to friendly categories.
4. Track: devices are tracked in memory by Bluetooth address with RSSI, best signal, counts, and timestamps.

## Project Structure

| File | Responsibility |
| --- | --- |
| Program.cs | Console UI, live table rendering, consent prompts, CSV export |
| BleAppleScanner.cs | BLE watcher, Apple filtering, device-type classification |
| DetectedDevice.cs | In-memory model and privacy-preserving address masking |
| CliOptions.cs | Defensive command-line argument parsing and validation |
| iPhoneBluetoothDetector.csproj | Project config and secure build settings |
| README.md | Documentation |

## Security and Privacy

- Least privilege: runs as a normal user (no admin rights required)
- No network access: zero upload or telemetry behavior
- No silent storage: writes files only when export is explicitly requested
- Data minimization: addresses masked by default
- Passive behavior only: no pairing, no connections, no private data reads
- Hardened build: nullable references, analyzers, warnings-as-errors
- Input validation: all CLI arguments are validated defensively

## Limitations

- iPhone Bluetooth addresses are randomized and rotate periodically.
- Device type classification is heuristic, not guaranteed exact.
- BLE range depends on adapter and physical environment.
- Target device must have Bluetooth enabled.

Legal and ethical note: Bluetooth addresses may be personal data under privacy regulations. Use only where legally authorized.

## Getting Started

### Requirements

- Windows 10 (build 19041 / version 2004) or Windows 11
- Bluetooth adapter with Bluetooth turned on
- .NET 10 SDK (for building from source)

### Clone

```powershell
git clone https://github.com/Abhinandanjaen/IoSBluetoothDetectorCLI.git
cd IoSBluetoothDetectorCLI
```

### Build

```powershell
dotnet build -c Release
```

### Run

```powershell
# Run until Ctrl+C
dotnet run -c Release

# Run for 30 seconds
dotnet run -c Release -- --seconds 30

# Export CSV (with confirmation prompt)
dotnet run -c Release -- --seconds 60 --export .\apple-devices.csv

# Show full addresses (only when authorized)
dotnet run -c Release -- --show-full-address
```

### Publish Standalone EXE

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -o .\publish
# If your project file is still iPhoneBluetoothDetector.csproj, the exe name remains iPhoneBluetoothDetector.exe
```

## Command-Line Options

| Option | Description |
| --- | --- |
| -s, --seconds <N> | Scan for N seconds (1 to 86400), then stop |
| --show-full-address | Show full Bluetooth addresses (disables masking) |
| --export <path> | Export results to CSV after scanning (with confirmation) |
| -h, --help | Show help |

## Tech Stack

- Language: C#
- Framework: .NET 10 (net10.0-windows10.0.19041.0)
- Bluetooth API: Windows.Devices.Bluetooth.Advertisement (WinRT)
- Platform: Windows 10/11
- App type: Console

## Development Notes

Application runtime behavior is standard C# and .NET code and does not depend on any AI model at runtime.

This project was developed with AI-assisted coding support. The final source, testing, and usage responsibility remain with the author.

## Future Improvements

- WinUI 3 graphical interface with radar-style visualization
- System tray mode with optional authorized logging
- Signal-strength trend charts over time
- Advanced device-type filtering options

## License

Released under the MIT License. Provided as-is, for lawful and authorized use only.

## Acknowledgements

- Apple BLE Continuity advertisement references (publicly documented formats)
- Microsoft WinRT Bluetooth documentation
- GitHub Copilot for development assistance
