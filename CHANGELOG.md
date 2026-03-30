# Changelog

All notable changes to DCS Manager will be documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.0.0] - 2026-03-30

### Added
- **Plugin Browser** — Browse and install DCS plugins from a curated catalog with one click
- **Auto-Update Manager** — Background service checks GitHub for new releases every 20 minutes
- **DCS-Aware Updates** — Detects when DCS.exe is running; queues updates and applies them automatically the moment DCS closes
- **DCS Install Detector** — Automatically finds DCS World (Stable) and DCS World Open Beta installations via registry and common paths
- **System Tray** — Runs silently in the background with a tray icon; double-click to open, close button minimizes to tray
- **Start with Windows** — Optional Windows startup registration via the Settings tab
- **Update History** — Full log of every install and update with timestamps, version diffs, and success/failure status
- **Plugin Catalog** — JSON-based catalog hosted on GitHub; app fetches latest catalog on every update cycle without needing an app update
- **Supported plugins on launch:**
  - SimpleRadioStandalone (SRS) — auto-update via GitHub releases
  - DCS Data Transfer Cartridge (DTC) — auto-update via GitHub releases
  - UH-60L Black Hawk community mod — auto-update via GitHub releases (v1.4 and below)
  - KC-30 MRTT — manual download link
  - MiG-17F — manual download link
  - SAM Sites Asset Pack — manual download link
  - Tacview — manual download link
- **Windows toast notifications** — notified when an update is found or applied
- **Settings tab** — configure update interval, DCS install paths, startup toggle, and open log/data folders
- **Self-contained installer** — single `.exe` installer, no .NET runtime required
- **GitHub Actions CI/CD** — automated build and release pipeline; pushing a version tag publishes the installer automatically

[1.0.0]: https://github.com/dillen198/DCS-Manager/releases/tag/v1.0.0
