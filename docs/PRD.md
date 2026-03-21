# JustRDP - Product Requirements Document

## 1. Product Overview

### 1.1 Purpose
JustRDP is a WPF-based RDP connection manager inspired by Royal TS V7. It allows IT professionals to manage, organize, and connect to multiple Remote Desktop Protocol (RDP) sessions through a modern tabbed interface with drag-and-drop organization and credential inheritance.

### 1.2 Target Users
- IT Administrators managing multiple servers
- DevOps engineers accessing remote infrastructure
- Developers connecting to development/staging environments
- MSPs (Managed Service Providers) managing client infrastructure

### 1.3 Key Value Propositions
- Free, lightweight alternative to Royal TS for RDP-only workflows
- Folder-based organization with credential inheritance
- Detachable Chrome-style tabs for multi-monitor workflows
- DPAPI-encrypted credential storage with zero key management
- Import/Export of connections via JSON and standard .rdp files

---

## 2. Technology Stack

| Component | Package | Version |
|---|---|---|
| Framework | .NET 10 (net10.0-windows) | 10.0 |
| MVVM | CommunityToolkit.Mvvm | 8.* |
| RDP Control | RoyalApps.Community.Rdp.WinForms | 1.4.1 |
| Detachable Tabs | Dragablz | 0.0.3.234 |
| Theme | MaterialDesignThemes + MaterialDesignColors | 5.3.* / 5.* |
| ORM | Microsoft.EntityFrameworkCore.Sqlite | 10.0.* |
| DI | Microsoft.Extensions.Hosting | 10.0.* |
| Encryption | System.Security.Cryptography.ProtectedData (DPAPI) | 10.0.* |
| SSH | SSH.NET (Renci.SshNet) | 2025.1.0 |
| Terminal Emulation | VtNetCore | 1.0.9 |

---

## 3. Architecture

### 3.1 Solution Structure
Clean Architecture with 4 projects:
- **JustRDP.Domain** — Entities, Value Objects, Enums, Interfaces (no dependencies)
- **JustRDP.Application** — Services, DTOs, Mapping (depends on Domain)
- **JustRDP.Infrastructure** — Persistence (EF Core + SQLite), Security (DPAPI), Import/Export (depends on Application)
- **JustRDP.Presentation** — WPF app with MaterialDesign, Dragablz tabs, RDP control (depends on Infrastructure)

### 3.2 Database
- **SQLite** at `%LOCALAPPDATA%\JustRDP\justrdp.db`
- **TPH** (Table Per Hierarchy) — single `TreeEntries` table with `EntryType` discriminator ("Folder" / "Connection")
- Self-referencing `ParentId` FK with cascade delete
- `AppSettings` table (Key/Value) for theme preference and other settings

### 3.3 Dependency Injection
- `Microsoft.Extensions.Hosting` provides the DI container
- All services registered as Scoped, ViewModels as Transient
- Database created/updated via `MigrateAsync()` on startup (with legacy database detection for pre-migration installs)

---

## 4. Domain Model

### 4.1 TreeEntry (Abstract Base)
| Property | Type | Description |
|---|---|---|
| Id | Guid | Primary key |
| Name | string | Display name |
| ParentId | Guid? | FK to parent FolderEntry |
| SortOrder | int | Position within siblings |
| CreatedAt | DateTime | Creation timestamp |
| ModifiedAt | DateTime | Last modification timestamp |

### 4.2 FolderEntry (extends TreeEntry)
| Property | Type | Description |
|---|---|---|
| IsExpanded | bool | UI state for tree expansion |
| CredentialUsername | string? | Username for credential inheritance |
| CredentialDomain | string? | Domain for credential inheritance |
| CredentialPasswordEncrypted | byte[]? | DPAPI-encrypted password |
| Children | Collection | Child TreeEntry items |

### 4.3 ConnectionEntry (extends TreeEntry)
| Property | Type | Description |
|---|---|---|
| ConnectionType | ConnectionType | RDP (default) or SSH |
| HostName | string | Server hostname/IP |
| Port | int | Port (default 3389 for RDP, 22 for SSH) |
| CredentialUsername | string? | Optional own credentials |
| CredentialDomain | string? | Optional own domain |
| CredentialPasswordEncrypted | byte[]? | DPAPI-encrypted password |
| DesktopWidth | int | 0 = auto-size to container |
| DesktopHeight | int | 0 = auto-size to container |
| ColorDepth | int | Default 32 |
| ResizeBehavior | int | 0=Scrollbars, 1=SmartSizing, 2=SmartReconnect |
| AutoReconnect | bool | Default true |
| NetworkLevelAuthentication | bool | NLA, default true |
| Compression | bool | Default true |
| RedirectClipboard | bool | Default true |
| RedirectPrinters | bool | Default false |
| RedirectDrives | bool | Default false |
| RedirectSmartCards | bool | Default false |
| RedirectPorts | bool | Default false |
| AudioRedirectionMode | int | 0=Local, 1=Remote, 2=None |
| GatewayHostName | string? | RD Gateway host |
| GatewayUsageMethod | int | Gateway usage setting |
| GatewayUsername | string? | Gateway credentials |
| GatewayDomain | string? | Gateway domain |
| GatewayPasswordEncrypted | byte[]? | Gateway password |
| Notes | string? | Free-text notes |
| SshPrivateKeyPath | string? | Path to SSH private key file |
| SshPrivateKeyPassphraseEncrypted | byte[]? | DPAPI-encrypted key passphrase |
| SshTerminalFontFamily | string? | Terminal font (default "Consolas") |
| SshTerminalFontSize | double? | Terminal font size (default 14) |

### 4.4 Credential (Value Object)
| Property | Type | Description |
|---|---|---|
| Username | string? | Resolved username |
| Domain | string? | Resolved domain |
| Password | string? | Decrypted password |
| InheritedFromName | string? | Source folder name if inherited |

### 4.5 AppSetting
| Property | Type | Description |
|---|---|---|
| Key | string | Setting identifier (PK) |
| Value | string | Setting value |

---

## 5. Features

### 5.1 Tree View — Connection Organization

#### 5.1.1 Hierarchical Tree
- Folders and connections displayed in a tree with HierarchicalDataTemplate
- Folders show folder icon, connections show monitor icon (MaterialDesign PackIcons)
- Folders can be expanded/collapsed; expansion state persisted to database
- Entries sorted by SortOrder within their parent

#### 5.1.2 CRUD Operations
- **Create Folder**: Toolbar button or context menu, creates with name "New Folder"
- **Create Connection**: Toolbar button or context menu, creates with name "New Connection"
- **Rename**: F2 key or context menu → inline TextBox editing (Enter to commit, Escape to cancel)
- **Delete**: Delete key or context menu → removes entry and all descendants (cascade)

#### 5.1.3 Drag and Drop
- Drag entries to reorder or re-parent
- Dropping on a folder makes the item a child of that folder
- Dropping on a connection makes the item a sibling (same parent)
- Cannot drop an entry on itself or its descendants
- Sort order updated in database after drop

#### 5.1.4 Context Menu
- Available on right-click of any tree entry
- Items: New Folder, New Connection, Rename (F2), Delete

### 5.2 Properties — Viewing and Editing

#### 5.2.1 Properties Side Panel
- Right panel showing read-only summary of selected entry
- Shows: Name, Type, Host, Port, Credentials (with inheritance info), Notes
- "Properties..." button opens modal dialog

#### 5.2.2 Properties Dialog
- Modal window with tabbed interface
- **General tab**: Name, Host Name, Port (connection only)
- **Credentials tab**: Username, Domain, Password (PasswordBox), inheritance hint
- **Display tab**: Desktop Width/Height, Color Depth combo, Resize Behavior combo (connection only)
- **Redirection tab**: Clipboard, Printers, Drives, Smart Cards, Ports checkboxes + Audio combo (connection only)
- **Connection tab**: Auto Reconnect, NLA, Compression checkboxes + Gateway Host Name (connection only)
- **Notes tab**: Multi-line TextBox (connection only)
- OK saves to database, Cancel discards

### 5.3 RDP Connections

#### 5.3.1 Opening a Connection
- Double-click a connection in the tree → opens RDP session in a new tab
- If connection is already open, switches to existing tab
- Uses `RoyalApps.Community.Rdp.WinForms.Controls.RdpControl` hosted in `WindowsFormsHost`

#### 5.3.2 RDP Configuration
- Maps ConnectionEntry properties to `RdpClientConfiguration`:
  - `config.Server`, `config.Port` (top-level)
  - `config.Credentials.Username/Domain/Password` (SensitiveString)
  - `config.Credentials.NetworkLevelAuthentication`
  - `config.Display.DesktopWidth/Height/ColorDepth/ResizeBehavior`
  - `config.Redirection.*` (all redirection flags + AudioRedirectionMode)
  - `config.Connection.EnableAutoReconnect`, `config.Connection.Compression`
  - `config.Gateway.*` (if gateway configured)

#### 5.3.3 Connection Lifecycle
- **Connecting**: Spinner overlay with "Connecting..." message
- **Connected**: Overlay hidden, RDP session visible
- **Disconnected**: Status message shown; if error code != 0/1, error overlay with details and Reconnect button
- **Tab Close**: Disconnect, Dispose RdpControl, null out WindowsFormsHost.Child

#### 5.3.4 Credential Inheritance
Algorithm in `CredentialInheritanceService.ResolveCredentialAsync()`:
1. Check connection's own credentials → if non-null, use them
2. Walk ancestor folders (parent → grandparent → root) via `GetAncestorsAsync()`
3. First ancestor with non-null `CredentialUsername` wins
4. Set `InheritedFromName` for UI display ("Inherited from: Production Servers")

### 5.4 Detachable Tabs

#### 5.4.1 Dragablz TabablzControl
- Replaces standard WPF TabControl
- MaterialDesign theme integration via `materialdesign.xaml` resource dictionary
- Tab header bound to `TabTitle` property
- Built-in close button per tab

#### 5.4.2 Tab Tear-Out
- `IInterTabClient` implementation creates `TabHostWindow` for torn-out tabs
- Secondary windows close when their last tab is removed
- Main window never closes from tab emptying
- Partition "RdpTabs" groups all RDP tabs together

#### 5.4.3 Tab Lifecycle
- `ClosingItemCallback` handles tab close: disconnect RDP, remove from OpenTabs collection
- `Ctrl+W` closes the active tab

### 5.5 Import/Export

#### 5.5.1 Import
- File dialog with filter: JSON (*.json) and RDP (*.rdp) files
- Multi-select supported for batch import
- **.rdp files**: Parse `key:type:value` format → create ConnectionEntry with parsed settings
- **JSON files**: Recursive tree structure → create FolderEntry/ConnectionEntry hierarchy
- Passwords are NOT imported (security)
- Tree reloads after import

#### 5.5.2 Export
- Save dialog for JSON export
- Exports entire tree as pretty-printed JSON
- Recursive structure: folders contain children array
- Passwords NOT exported (security)
- Credentials exported as username/domain only

#### 5.5.3 .rdp File Format Support
Supported keys:
- `full address`, `server port`, `username`, `domain`
- `desktopwidth`, `desktopheight`, `session bpp`, `smart sizing`
- `authentication level`, `enablecredsspsupport`, `compression`
- `redirectclipboard`, `redirectprinters`, `redirectdrives`, `redirectsmartcards`, `redirectcomports`
- `audiomode`, `autoreconnection enabled`
- `gatewayhostname`, `gatewayusagemethod`

### 5.6 Network Scan (Post-MVP, Implemented)

#### 5.6.1 IP Range Discovery
- Toolbar "Scan" button opens a non-modal, single-instance scan window
- User enters a CIDR range (e.g. `192.168.1.0/24`) with live translation showing first-last IP and host count
- CIDR validation: rejects invalid notation, rejects ranges larger than /16, prompts confirmation for ranges larger than /20
- TCP port scanning (no ICMP) on configurable ports (default 3389) with configurable timeout (default 1500ms)
- 16 hosts scanned concurrently via `SemaphoreSlim`
- Reverse DNS resolution with silent failure (fallback to IP)
- Security disclaimer always visible: "Network scanning may trigger security alerts"

#### 5.6.2 Results & Import
- Results displayed in a DataGrid with columns: Checkbox, IP, Hostname, Open Ports, Port dropdown, Status
- Existing hosts detected via case-insensitive matching (IP, hostname, FQDN-to-short) — shown with green background + "Exists" label, checkboxes disabled
- New hosts shown with blue background + "New" label, checkboxes enabled
- "Select All New" selects all importable hosts
- "Import Selected (N)" creates connection entries in a user-selected target folder via `TreeService`
- Imported entries inherit credentials from parent folder (no credentials set on import)
- After import, rows update to "Exists" status in-place

#### 5.6.3 Architecture
- `CidrParser` (Application) — pure static utility for CIDR parsing, IP enumeration, port validation
- `INetworkScanner` (Domain) — interface for port scanning + DNS resolution
- `NetworkScanner` (Infrastructure) — TCP connect implementation with `TcpClient.ConnectAsync`
- `NetworkScanViewModel` (Presentation) — scan orchestration, results, import logic
- `NetworkScanWindow` (Presentation) — Material Design themed non-modal window

### 5.7 Theme Management

#### 5.7.1 Dark/Light Toggle
- Toolbar button with ThemeLightDark icon
- Uses MaterialDesignThemes `PaletteHelper.SetTheme()` for runtime switching
- Default: Dark theme (Blue primary, LightBlue secondary)

#### 5.7.2 Persistence
- Theme preference stored in `AppSettings` table (key: "Theme", value: "Dark"/"Light")
- Loaded on startup before main window shown
- `ThemeManager` service handles load/save/apply

### 5.10 SSH Terminal Connections

#### 5.10.1 Connection Type
- `ConnectionEntry` has a `ConnectionType` enum: `RDP` (default) or `SSH`
- Selectable in the Properties dialog via a "Type" dropdown on the General tab
- When type is SSH, RDP-specific tabs (Display, Redirection, Connection/Gateway) are hidden
- SSH-specific fields shown: Private Key Path (file picker), Private Key Passphrase, Terminal Font Family/Size

#### 5.10.2 SSH Authentication
- Password authentication via existing credential system (credential inheritance applies)
- Private key authentication with optional DPAPI-encrypted passphrase
- Private key path stored on `ConnectionEntry.SshPrivateKeyPath`

#### 5.10.3 SSH Terminal
- Fully native WPF terminal control (no browser, no external processes)
- Based on SSH.NET (connection) + VtNetCore (terminal emulation)
- xterm-256color support with VS Code Dark color palette
- Full keyboard support: arrow keys, function keys, Ctrl+key sequences, Tab, copy/paste
- Terminal resize with debounced SSH window change request
- Scrollback buffer with mouse wheel scrolling
- Text selection via mouse drag + clipboard copy
- CompositionTarget.Rendering for ~60fps rendering (only when dirty)

#### 5.10.4 SSH Tab Lifecycle
- Double-click SSH connection → opens terminal tab (same pattern as RDP)
- Tab auto-closes on disconnect (matching RDP behavior)
- Multiple SSH tabs can be open simultaneously
- SSH and RDP tabs coexist in the same tab bar

#### 5.10.5 Visual Distinction
- Tree icons: `MonitorDashboard` for RDP, `Console` for SSH
- Availability monitor works for both types (ICMP + TCP on configured port)

#### 5.10.6 Quick Connect
- `ssh://user@host:port` prefix opens SSH terminal
- `ssh://user@host` defaults port to 22
- Non-prefixed addresses default to RDP (existing behavior)

### 5.8 Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| Ctrl+N | New Connection |
| Ctrl+Shift+N | New Folder |
| F2 | Rename selected entry |
| Delete | Delete selected entry |
| Ctrl+W | Close active tab |

### 5.9 Status Bar
- Bottom bar showing entry count and open connections
- Format: "X entries" or "X entries | Y connection(s) open"
- Updates on add/delete/connect/disconnect

---

## 6. UI Layout

```
+-------------------------------------------------------------+
| [+Folder] [+Connection] [Import] [Export] [Scan] [Theme] [Info] |  <- Toolbar
+----------+------------------------------+-------------------+
| TreeView |  TabablzControl (Dragablz)   | Properties Panel  |
|          | +----+----+----+             |                   |
| Folder1  | |Tab1|Tab2|Tab3|             | Name: Server01    |
|  Conn1   | +----+----+----+             | Host: 10.0.0.1    |
|  Conn2   | |              |             | Port: 3389        |
| Folder2  | |  RDP Session |             | Creds: inherited  |
|  Conn3   | |  (WFHost)    |             |   from: Folder1   |
|          | |              |             |                   |
|          | |              |             | [Properties...]   |
+----------+------------------------------+-------------------+
| Status: 3 entries | 2 connection(s) open                    |
+-------------------------------------------------------------+
```

- **Left panel**: TreeView (250px default, resizable via GridSplitter)
- **Center**: Dragablz TabablzControl with RDP sessions
- **Right panel**: Properties panel (250px default, resizable)
- **Toolbar**: MaterialDesign ColorZone with icon buttons
- **Status bar**: MaterialDesign ColorZone with text

---

## 7. Security

### 7.1 Credential Encryption
- Passwords encrypted using **DPAPI** (`System.Security.Cryptography.ProtectedData`)
- `DataProtectionScope.CurrentUser` — encrypted data only accessible by the same Windows user on the same machine
- Zero key management required
- Stored as `byte[]` in SQLite

### 7.2 Export Security
- Passwords are **never** included in JSON or .rdp exports
- Only username and domain are exported
- Import does not expect passwords

---

## 8. Data Storage

### 8.1 Database Location
- `%LOCALAPPDATA%\JustRDP\justrdp.db`
- Directory created automatically on first run
- SQLite database created/updated via EF Core migrations (`MigrateAsync()` on startup)

### 8.2 Tables

#### TreeEntries (TPH)
| Column | Type | Notes |
|---|---|---|
| Id | GUID | PK |
| EntryType | TEXT | Discriminator: "Folder" or "Connection" |
| Name | TEXT | Required, max 256 |
| ParentId | GUID? | FK to self, cascade delete |
| SortOrder | INT | Default 0 |
| CreatedAt | DATETIME | |
| ModifiedAt | DATETIME | |
| IsExpanded | BOOL | Folder only |
| CredentialUsername | TEXT? | Folder + Connection |
| CredentialDomain | TEXT? | Folder + Connection |
| CredentialPasswordEncrypted | BLOB? | Folder + Connection |
| HostName | TEXT? | Connection only, max 256 |
| Port | INT | Connection only, default 3389 |
| ... (all connection fields) | ... | ... |
| Notes | TEXT? | Connection only, max 4000 |

#### AppSettings
| Column | Type | Notes |
|---|---|---|
| Key | TEXT | PK, max 128 |
| Value | TEXT | Max 1024 |

---

## 9. MVP Scope

### 9.1 In Scope (MVP)
- Tree view with folders and connections (CRUD, drag-drop, inline rename)
- Properties side panel and modal editing dialog
- RDP connections in tabs via RoyalApps.Community.Rdp.WinForms
- Credential inheritance from parent folders
- Detachable tabs via Dragablz
- Import/Export (JSON tree + .rdp files)
- Dark/Light theme toggle with persistence
- Keyboard shortcuts
- Status bar
- SQLite persistence with DPAPI credential encryption

### 9.2 Post-MVP Features

#### Implemented
- **Network Scan (IP Range Discovery)** — scan a CIDR range for RDP-enabled hosts via TCP port probing, view results with existing-host detection, and selectively import discovered hosts into the connection tree (see FEAT-109)
- **SSH terminal connections** — native WPF terminal with SSH.NET + VtNetCore, password and private key auth, full xterm-256color support, auto-close on disconnect, Quick Connect with `ssh://` prefix (see FEAT-111)
- **Connection search/filter** — real-time name filter above the tree (partial, see FEAT-103)
- **Bulk operations (multi-select)** — checkbox-based multi-select with bulk connect (partial, see FEAT-105)
- **Dashboard** — home view with stats summary (total/online/offline/open sessions), all connections table with availability status, recent connections list (top 10), usage tracking (LastConnectedAt, ConnectCount), permanent "Dashboard" tree node (see FEAT-113)

#### Planned
- RD Gateway full support (credentials, auth methods) — schema in place, UI deferred
- VNC/other protocol support
- Multi-document (multiple .justrdp files)
- Cloud sync / team sharing
- Connection history / full session logging — basic tracking (last connected, connect count) covered by FEAT-113, detailed session history table (connect/disconnect times, durations) remains planned (see FEAT-107)
- Custom tab colors / connection badges
- Auto-login / saved sessions
- Full-screen RDP mode
- Plugin/extension system

---

## 10. Non-Functional Requirements

### 10.1 Performance
- App startup < 2 seconds
- Tree view responsive with 1000+ entries
- RDP session connect time dominated by network (no UI delay)

### 10.2 Reliability
- Graceful handling of RDP disconnection errors
- Database operations wrapped in try/catch with user-friendly error messages
- No data loss on crash (SQLite WAL mode)

### 10.3 Usability
- Familiar tree + tabs paradigm (like Royal TS, mRemoteNG)
- MaterialDesign theming for modern look
- Keyboard-friendly workflow (shortcuts for common actions)

### 10.4 Compatibility
- Windows 10/11 only (WPF + WinForms + DPAPI)
- .NET 10 runtime required
- x64 architecture (RDP ActiveX control)
