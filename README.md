# JustRDP

A lightweight WPF-based remote connection manager for Windows. Organize RDP and SSH connections in folders, connect via tabbed sessions, and manage credentials — all from a clean Material Design interface.

## Features

- **Tabbed RDP sessions** — open multiple connections side-by-side with independent tabs
- **SSH terminal sessions**  — connect to Linux/Unix hosts with a built-in VT100/xterm terminal emulator (SSH.NET + VtNetCore); supports password and private-key authentication
- **Quick Connect** — type a `host:port` in the toolbar and connect instantly without creating a tree entry; use `ssh://user@host:port` for SSH
- **Folder organization** — group connections into folders with drag-and-drop reordering and alphabetical sorting
- **Tree filter** — real-time search box above the tree to find connections by name
- **Multi-select** — check multiple connections and connect to all of them at once
- **Credential inheritance** — set credentials on a folder and all child connections inherit them automatically
- **DPAPI-encrypted passwords** — credentials are encrypted with Windows DPAPI (per-user scope), no external key management needed
- **Availability monitor** — toggle a background monitor that periodically pings all connections (ICMP + TCP fallback) and shows green/red indicators in the tree; status bar displays an availability summary (e.g. "12/18 available"); pauses automatically when minimized; persisted across sessions
- **Network scan** — scan a CIDR range for RDP-enabled hosts, view results with existing-host detection, and selectively import discovered hosts into the tree
- **Import / Export** — import and export connections as JSON or standard `.rdp` files compatible with mstsc.exe
- **Dark / Light theme** — toggle between themes with automatic persistence
- **Keyboard shortcuts** — Ctrl+N (new connection), Ctrl+Shift+N (new folder), F2 (rename), Delete, Ctrl+W (close tab)
- **Persistent layout** — window position, size, state, and panel widths are remembered across sessions
- **Auto-close on disconnect** — tabs close automatically when the remote session ends

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

SSH connections  support:

| Category | Settings |
|---|---|
| **Connection** | Hostname, port (default 22), terminal type (xterm-256color) |
| **Authentication** | Username, password, private key file (PEM/OpenSSH), optional passphrase |
| **Terminal** | Font family, font size, color scheme, scrollback buffer |

## Tech Stack

| Component | Technology |
|---|---|
| Framework | .NET 10 |
| UI | WPF + Material Design |
| RDP | [RoyalApps.Community.Rdp.WinForms](https://github.com/royalapps/rdp) |
| SSH | [SSH.NET](https://github.com/sshnet/SSH.NET) + [VtNetCore](https://github.com/nickvdyck/VtNetCore)  |
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
git clone https://github.com/itsChris/JustRDP.git
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
