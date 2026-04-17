# Home Device Emulator
This android application provides emulation for home devices that sends and receives the packets compatible with KS X 4506.

## Target Devices
The emulator has been tested on several android 12 devices such as phone, tablet and Raspberry Pi 4.

## USB Serial Dongle
This application communicates with home devices by using the usb-to-serial converter and java driver written in java.
Please refer to [usb-serial-for-android](https://github.com/mik3y/usb-serial-for-android) page, it also lists compatable devices.

## Android Developing Environment
To build and run this android project, you need the developing environment for android, the detail is out of scope.
(see. https://developer.android.com)

---

## Windows Portable Version

A portable Windows desktop application is available in the `windows/` directory.
It ports the KS X 4506 emulator logic to a WinForms GUI application and communicates via USB Serial (COM port).

### Features
- **COM Port selection** dropdown – lists all USB Serial ports detected by Windows Device Manager
- **Baudrate selection** dropdown – standard values (1200, 2400, 4800, **9600 default**, 14400, 19200, 38400, 57600, 115200)
- **Protocol** selector – KS X 4506 / KD
- **Mode** selector – Slave (Device Emulator) or Master (Controller)
- **Start / Stop** buttons with live status indicator
- **Device list** showing emulated device states (thermostat, light, boiler, gas valve, etc.)
- **Log pane** for RX/TX traffic
- **Portable** – copy the output folder to any Windows 7+ machine and run directly; no installer required

### Requirements
- Windows 7 SP1 or later (x86 / x64)
- .NET Framework 4.8 (pre-installed on Windows 10/11; available as a free download for Windows 7/8/8.1)
  - Download: https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48

### Build

**Option 1 – Visual Studio 2019 / 2022 (GUI)**
1. Open `windows/SmartHomeEmulator.sln`
2. Set configuration to `Release`
3. Build → Build Solution (Ctrl+Shift+B)
4. Output: `windows/SmartHomeEmulator/bin/Release/SmartHomeEmulator.exe`

**Option 2 – Command line (build.bat)**
```cmd
cd windows
build.bat
```
Requires Visual Studio Build Tools or any edition of Visual Studio 2019/2022.
Download Build Tools: https://visualstudio.microsoft.com/downloads/ → "Build Tools for Visual Studio"

### Portable Distribution
After building, copy the entire `bin\Release\` folder to the target machine:
```
SmartHomeEmulator.exe    ← main executable
*.dll                    ← runtime libraries (if any)
```
No registry entries or installation steps are needed.

### Usage
1. Plug in your USB Serial adapter (the device should appear in Windows Device Manager under "Ports (COM & LPT)").
2. Launch `SmartHomeEmulator.exe`.
3. Select the COM port from the dropdown (click **↺ Refresh** if it does not appear).
4. Select the baudrate (default: **9600**, matching the KS X 4506 standard).
5. Select Protocol and Mode (default: KS X 4506/KD, Slave).
6. Click **▶ Start** to begin emulation.
7. Click **■ Stop** to stop.

### Serial Parameters
The following serial parameters are used (same as the Android version):

| Parameter  | Value  |
|------------|--------|
| Baudrate   | 9600 (selectable) |
| Data bits  | 8      |
| Parity     | None   |
| Stop bits  | 1      |

