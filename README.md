# DCS Manager

A Windows application for DCS World players to browse, install, and automatically update plugins and mods — all in one place.

![DCS Manager](https://img.shields.io/badge/platform-Windows-blue) ![Version](https://img.shields.io/github/v/release/dillen198/DCS-Manager) ![License](https://img.shields.io/github/license/dillen198/DCS-Manager)

---

## Features

- **Plugin Browser** — Browse a curated catalog of DCS plugins and mods. Install anything with one click.
- **Auto-Update** — Checks GitHub every 20 minutes for new releases. Updates are applied automatically when DCS is not running.
- **DCS-Aware** — Detects when DCS is running and queues updates. The moment you close DCS, queued updates apply automatically.
- **Supports Stable & Open Beta** — Automatically detects both DCS World (Stable) and DCS World Open Beta installations.
- **System Tray** — Runs silently in the background. No window cluttering your desktop unless you need it.
- **Starts with Windows** — Optional startup with Windows so updates are always ready before you fly.
- **Update History** — Full log of every install and update with timestamps and version diffs.

---

## Supported Plugins

| Plugin | Source | Auto-Update |
|--------|--------|-------------|
| SimpleRadioStandalone (SRS) | [GitHub](https://github.com/ciribob/DCS-SimpleRadioStandalone) | ✅ |
| DCS Data Transfer Cartridge (DTC) | [GitHub](https://github.com/the-paid-actor/dcs-dtc) | ✅ |
| UH-60L Black Hawk (Community Mod) | [GitHub](https://github.com/Kinkkujuustovoileipa/uh-60l) | ✅ (v1.4 and below) |
| KC-30 MRTT | DCS Forums | 🔗 Link |
| MiG-17F | DCS Forums | 🔗 Link |
| SAM Sites Asset Pack | DCS Forums | 🔗 Link |
| Tacview | tacview.net | 🔗 Link |

> Want a plugin added? [Open an issue](https://github.com/dillen198/DCS-Manager/issues) or submit a pull request to `catalog/plugins.json`.

---

## Installation

1. Download the latest `DCSManager-Setup-x.x.x.exe` from the [Releases page](https://github.com/dillen198/DCS-Manager/releases)
2. Run the installer — it will ask for admin rights (needed to install SRS and DTC)
3. DCS Manager starts in your system tray
4. Double-click the tray icon to open the main window

**Requirements:** Windows 10/11 (64-bit). No .NET installation needed — it's fully self-contained.

---

## How It Works

```
DCS Running?
    YES → Queue updates, wait for DCS to close → Apply automatically
    NO  → Apply updates immediately
```

DCS Manager checks for updates every 20 minutes (configurable in Settings). When it finds a new version:
- If DCS is **closed** → installs immediately and shows a tray notification
- If DCS is **running** → shows a badge on the Updates tab and installs the moment you close DCS

---

## Adding Plugins to the Catalog

The plugin catalog lives in [`catalog/plugins.json`](catalog/plugins.json). To add a new plugin:

1. Fork this repo
2. Add an entry to `catalog/plugins.json` following the existing format
3. Open a pull request

The app fetches the latest catalog from GitHub on every update cycle, so new plugins become available to all users without needing an app update.

---

## Building from Source

**Requirements:** .NET 10 SDK, Windows

```bash
git clone https://github.com/dillen198/DCS-Manager.git
cd DCS-Manager
dotnet build DCSManager.slnx
```

To publish a self-contained installer:
```bash
dotnet publish src/DCSManager.App/DCSManager.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/
# Then compile installer/setup.iss with Inno Setup
```

---

## Project Structure

```
src/
  DCSManager.Core/        Models and interfaces
  DCSManager.Services/    GitHub API, update logic, DCS detection, installers
  DCSManager.UI/          WPF views and ViewModels
  DCSManager.App/         Entry point, DI, system tray
catalog/
  plugins.json            Plugin registry
installer/
  setup.iss               Inno Setup packaging script
```

---

## Contributing

Pull requests are welcome! If you want to add a plugin, fix a bug, or improve the UI:

1. Fork the repo
2. Create a branch: `git checkout -b feature/your-feature`
3. Commit your changes
4. Open a pull request

---

## License

MIT — see [LICENSE](LICENSE) for details.
