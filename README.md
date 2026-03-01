# JustRDP

A lightweight WPF-based RDP connection manager for Windows. Organize remote desktop connections in folders, connect via tabbed sessions, and manage credentials — all from a clean Material Design interface.

## Features

- **Tabbed RDP sessions** — open multiple connections side-by-side with independent tabs
- **Folder organization** — group connections into folders with drag-and-drop reordering
- **Credential inheritance** — set credentials on a folder and all child connections inherit them automatically
- **DPAPI-encrypted passwords** — credentials are encrypted with Windows DPAPI (per-user scope), no external key management needed
- **Import / Export** — import and export connections as JSON or standard `.rdp` files compatible with mstsc.exe
- **Dark / Light theme** — toggle between themes with automatic persistence
- **Keyboard shortcuts** — Ctrl+N (new connection), Ctrl+Shift+N (new folder), F2 (rename), Delete, Ctrl+W (close tab)

### Connection Settings

Each connection supports the full range of RDP options:

| Category | Settings |
|---|---|
| **Connection** | Hostname, port, auto-reconnect, NLA, compression |
| **Display** | Resolution, color depth (up to 32-bit), resize behavior (scrollbars / smart sizing / smart reconnect) |
| **Redirection** | Clipboard, printers, drives, smart cards, ports, audio |
| **Gateway** | RD Gateway hostname, credentials, usage method |
| **Credentials** | Username, domain, password (with folder inheritance) |
| **Input** | Keyboard hook mode, Windows key forwarding, accelerator passthrough |

## Tech Stack

| Component | Technology |
|---|---|
| Framework | .NET 10 |
| UI | WPF + Material Design |
| RDP | [RoyalApps.Community.Rdp.WinForms](https://github.com/royalapps/rdp) |
| MVVM | CommunityToolkit.Mvvm |
| Database | SQLite via EF Core |
| Encryption | Windows DPAPI |
| Logging | Serilog (file sink, daily rolling) |

## Getting Started

### Prerequisites

- Windows 10 or later
- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Build & Run

```powershell
git clone https://github.com/your-username/JustRDP.git
cd JustRDP
dotnet run --project src/JustRDP.Presentation
```

The database is created automatically on first launch at `%LOCALAPPDATA%\JustRDP\justrdp.db`.

Logs are written to `%LOCALAPPDATA%\JustRDP\logs\`.

## Architecture

The project follows Clean Architecture with four layers:

```
src/
  JustRDP.Domain           Entities, value objects, interfaces
  JustRDP.Application      Services, DTOs, mapping
  JustRDP.Infrastructure   EF Core, SQLite, DPAPI, import/export
  JustRDP.Presentation     WPF views, view models, themes
```

## Configuration

Settings are stored in `appsettings.json`:

```json
{
  "Database": {
    "Path": "%LOCALAPPDATA%/JustRDP/justrdp.db"
  }
}
```

Logging levels, file paths, and retention can be configured in the `Serilog` section of the same file.

## License

This project is not yet licensed. All rights reserved.
