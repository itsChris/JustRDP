# FEAT-111: SSH Terminal Connections

## Metadata

| Attribute | Value |
|-----------|-------|
| **Feature ID** | FEAT-111 |
| **Title** | SSH Terminal Connections |
| **Category** | Post-MVP |
| **Priority** | P1 (High) |
| **PRD Sections** | §5.10 (new), §9.2 |
| **Depends On** | FEAT-001 (data model), FEAT-003 (credential encryption), FEAT-005 (tree CRUD), FEAT-011 (tab lifecycle), FEAT-009 (properties dialog) |
| **Dependents** | — |
| **Estimated Complexity** | XL (2-3 weeks) |

---

## 1. Overview

### 1.1 Purpose
Adds SSH terminal support alongside RDP, enabling users to manage both graphical remote desktop and text-based SSH connections from the same tree-organized interface. The SSH terminal is a fully native WPF control — no browser engines, no embedded external processes.

### 1.2 Scope
**In Scope:**
- `ConnectionType` enum (`RDP` / `SSH`) on `ConnectionEntry` to distinguish protocol
- SSH-specific entity fields: `SshPrivateKeyPath`, `SshPrivateKeyPassphraseEncrypted`, `SshTerminalFontFamily`, `SshTerminalFontSize`
- SSH terminal UserControl ported from POC: `SshTerminalControl`, `TerminalSession`, `TerminalRenderer`, `TerminalInputHandler`, `TerminalColorScheme`, `TerminalOptions`
- `SshTabViewModel` with lifecycle matching RDP tabs (auto-close on disconnect)
- `SshTabView` hosting the `SshTerminalControl`
- Properties dialog: ConnectionType dropdown, SSH-specific fields shown/hidden conditionally
- Tree icon: `MonitorDashboard` for RDP, `Console` for SSH
- Quick Connect: `ssh://user@host:port` prefix opens SSH, else defaults to RDP
- Availability monitor: works for SSH connections (ICMP + TCP on configured port, no changes needed)
- EF Core migration for new columns
- NuGet packages: `SSH.NET` (2025.1.0), `VtNetCore` (1.0.9)

**Out of Scope:**
- SFTP / file transfer
- SSH agent forwarding or key management
- SSH tunneling / port forwarding
- Terminal multiplexing (handled by remote tmux/screen)
- Mouse tracking mode forwarding to remote (post-MVP of SSH itself)
- Custom color scheme configuration UI

### 1.3 Key Decisions
- **ConnectionType enum on ConnectionEntry** (not a separate entity): Reuses existing tree CRUD, drag-drop, filtering, credential inheritance, import/export. SSH-specific fields are nullable columns on the same TPH table.
- **Separate SshTabViewModel** (not extending ConnectionTabViewModel): RDP uses WinForms interop with a fundamentally different lifecycle. SSH is pure WPF. A shared `IConnectionTab` interface or base class provides common tab behavior (TabTitle, IsSelected, CloseRequested).
- **Terminal control in Presentation layer** (not a separate library): The control is tightly coupled to WPF and only consumed by this app. Avoids extra project complexity.
- **Auto-close on disconnect** (matching RDP behavior): Consistent UX. No auto-reconnect for v1.
- **Default port 22 for SSH**: When ConnectionType is SSH, Port defaults to 22 instead of 3389.
- **Private key passphrase encrypted with DPAPI**: Same encryption path as RDP passwords via `ICredentialEncryptor`.
- **EF Core migration** (not EnsureCreated): Preserves existing connection data.

### 1.4 Assumptions
> [!NOTE]
> **Implementation Assumptions:**
> - [ASSUMPTION] SSH.NET 2025.1.0 and VtNetCore 1.0.9 are compatible with .NET 10
> - [ASSUMPTION] Users have SSH server access with password or private key authentication
> - [ASSUMPTION] The POC terminal control is production-ready (tested with vim, nano, tmux, htop, mc)
> - [ASSUMPTION] VtNetCore handles all VT100/xterm escape sequences correctly for common use cases

---

## 2. User Stories & Acceptance Criteria

### US-111-01: Create SSH Connection
**As a** user
**I want to** create an SSH connection in the tree
**So that** I can connect to Linux/Unix servers via SSH

**Acceptance Criteria:**
- [ ] AC1: New connections default to ConnectionType = RDP (existing behavior unchanged)
- [ ] AC2: Properties dialog has a "Type" dropdown (RDP / SSH) on the General tab
- [ ] AC3: Switching to SSH hides RDP-specific tabs (Display, Redirection, Connection/Gateway) and shows SSH fields
- [ ] AC4: SSH-specific fields: HostName, Port (default 22), Private Key Path (file picker), Private Key Passphrase
- [ ] AC5: Credentials tab works for SSH (username + password authentication)
- [ ] AC6: Private key passphrase is DPAPI-encrypted like RDP passwords
- [ ] AC7: ConnectionType persisted to database via EF Core migration

### US-111-02: Open SSH Terminal Tab
**As a** user
**I want to** double-click an SSH connection to open a terminal
**So that** I can interact with the remote shell

**Acceptance Criteria:**
- [ ] AC1: Double-clicking an SSH connection opens a new tab with an embedded SSH terminal
- [ ] AC2: Tab header shows the connection name (same pattern as RDP)
- [ ] AC3: Terminal connects using password or private key (based on which is configured)
- [ ] AC4: Terminal supports xterm-256color with full color rendering
- [ ] AC5: Arrow keys, function keys, Ctrl+C, Ctrl+V, Tab work correctly
- [ ] AC6: Interactive programs work: vim, nano, htop, tmux, mc
- [ ] AC7: Terminal resizes dynamically when the tab area is resized
- [ ] AC8: Scrollback buffer accessible via mouse wheel
- [ ] AC9: Text selection via mouse drag + Ctrl+C to copy
- [ ] AC10: Ctrl+V pastes clipboard content (with bracketed paste mode support)
- [ ] AC11: Tab auto-closes when SSH session disconnects (matching RDP behavior)
- [ ] AC12: Multiple SSH tabs can be open simultaneously without interference

### US-111-03: Visual Distinction in Tree
**As a** user
**I want to** visually distinguish SSH and RDP connections in the tree
**So that** I can quickly identify connection types

**Acceptance Criteria:**
- [ ] AC1: RDP connections show `MonitorDashboard` icon (existing)
- [ ] AC2: SSH connections show `Console` (or `Terminal`) icon
- [ ] AC3: Availability monitor dots work for both types (ICMP + TCP on configured port)

### US-111-04: SSH Quick Connect
**As a** user
**I want to** quickly connect to an SSH server from the toolbar
**So that** I don't have to create a tree entry first

**Acceptance Criteria:**
- [ ] AC1: Typing `ssh://user@host` in Quick Connect opens an SSH tab
- [ ] AC2: Typing `ssh://user@host:2222` uses port 2222
- [ ] AC3: Typing `ssh://host` uses the host with no username (prompts or uses empty)
- [ ] AC4: Non-prefixed addresses (e.g. `10.0.0.1`) continue to open RDP (existing behavior)
- [ ] AC5: Quick connect SSH prompts for password if no key is available (in-terminal auth)

---

## 3. Functional Requirements

| Req ID | Requirement | Priority |
|--------|-------------|----------|
| FR-111-01 | Add `ConnectionType` enum to Domain: `RDP = 0`, `SSH = 1` | Must |
| FR-111-02 | Add `ConnectionType` property to `ConnectionEntry` (default `RDP`) | Must |
| FR-111-03 | Add SSH fields to `ConnectionEntry`: `SshPrivateKeyPath` (string?), `SshPrivateKeyPassphraseEncrypted` (byte[]) | Must |
| FR-111-04 | Add optional terminal font fields: `SshTerminalFontFamily` (string?, default "Consolas"), `SshTerminalFontSize` (double?, default 14) | Should |
| FR-111-05 | EF Core migration adding `ConnectionType`, SSH columns to TreeEntries table | Must |
| FR-111-06 | Add `SSH.NET` (2025.1.0) and `VtNetCore` (1.0.9) NuGet packages to Presentation project | Must |
| FR-111-07 | Port SSH terminal controls into `Presentation/Controls/Terminal/`: `SshTerminalControl`, `TerminalSession`, `TerminalRenderer`, `TerminalInputHandler`, `TerminalColorScheme`, `TerminalOptions` | Must |
| FR-111-08 | Adapt `TerminalSession` to use `ILogger` instead of `Debug.WriteLine` | Should |
| FR-111-09 | Create `SshTabViewModel` with: TabTitle, IsSelected, IsConnected, CloseRequested event, ConnectAsync, Disconnect | Must |
| FR-111-10 | Create `SshTabView` UserControl hosting `SshTerminalControl` | Must |
| FR-111-11 | Update `MainWindowViewModel`: OpenTabs collection holds both `ConnectionTabViewModel` and `SshTabViewModel` (shared base/interface) | Must |
| FR-111-12 | Update `OpenConnectionAsync` to branch on `ConnectionType` — open RDP tab or SSH tab | Must |
| FR-111-13 | Update `TreeEntryTypeToIconConverter` to return `Console` for SSH connections | Must |
| FR-111-14 | Update Properties dialog: ConnectionType dropdown, conditional visibility for protocol-specific fields | Must |
| FR-111-15 | Quick Connect: parse `ssh://` prefix → open SSH tab, else RDP | Must |
| FR-111-16 | Credential inheritance works for SSH (username/password from parent folder) | Must |
| FR-111-17 | Import/Export: include `ConnectionType` and SSH fields in JSON format | Must |
| FR-111-18 | Availability monitor: no changes needed (already checks configured port via TCP) | Must |
| FR-111-19 | Auto-close SSH tab on disconnect (Disconnected event → CloseRequested) | Must |

---

## 4. UI Layout

### 4.1 Tree View — Icon Differentiation

```
📁 Production Servers
  🖥️ web-srv-01           ← RDP (MonitorDashboard icon)
  🖥️ web-srv-02           ← RDP
  💻 db-srv-01             ← SSH (Console icon)
  💻 monitoring-01         ← SSH
📁 Development
  🖥️ dev-machine           ← RDP
  💻 build-agent            ← SSH
```

### 4.2 SSH Tab

```
┌────────────────────────────────────────────────────────────┐
│ [db-srv-01] [web-srv-01] [monitoring-01]   ← Tab headers   │
├────────────────────────────────────────────────────────────┤
│                                                            │
│  admin@db-srv-01:~$ ls -la                                │
│  total 48                                                  │
│  drwxr-xr-x  6 admin admin 4096 Mar  1 10:22 .            │
│  drwxr-xr-x  3 root  root  4096 Feb 15 08:00 ..           │
│  -rw-r--r--  1 admin admin  220 Feb 15 08:00 .bash_logout  │
│  admin@db-srv-01:~$ _                                      │
│                                                            │
├────────────────────────────────────────────────────────────┤
│ Connected to db-srv-01:22                        120x35    │
└────────────────────────────────────────────────────────────┘
```

### 4.3 Properties Dialog — SSH Mode

```
┌─────────────────────────────────────────────┐
│  Connection Properties                   [X] │
├─────────────────────────────────────────────┤
│  [General] [Credentials] [Notes]             │
│                                              │
│  General:                                    │
│    Name:        [ db-srv-01              ]   │
│    Type:        [ SSH              ▼     ]   │
│    Host Name:   [ 10.0.1.50              ]   │
│    Port:        [ 22                     ]   │
│                                              │
│    Private Key: [ ~/.ssh/id_rsa     ] [📁]   │
│    Passphrase:  [ ●●●●●●                ]   │
│                                              │
│    Font Family: [ Consolas               ]   │
│    Font Size:   [ 14                     ]   │
│                                              │
│                       [ Cancel ]  [ OK ]     │
└─────────────────────────────────────────────┘
```

When Type = RDP, the dialog shows the existing tabs (Display, Redirection, Connection). When Type = SSH, those tabs are hidden and SSH-specific fields appear on the General tab.

### 4.4 Quick Connect

```
[ ssh://admin@10.0.1.50:22  ] [▶ Connect]
```

Prefix parsing:
- `ssh://user@host:port` → SSH connection (user, host, port parsed)
- `ssh://user@host` → SSH connection (port defaults to 22)
- `ssh://host` → SSH connection (no user, port 22)
- `host:port` or `host` → RDP connection (existing behavior)

---

## 5. Technical Design

### 5.1 Architecture

```
Domain Layer:
  ConnectionType.cs (enum)                    ← NEW
  ConnectionEntry.cs                          ← MODIFIED (add ConnectionType + SSH fields)

Infrastructure Layer:
  Migrations/AddSshSupport.cs                 ← NEW (EF Core migration)

Presentation Layer:
  Controls/Terminal/                           ← NEW (ported from POC)
    SshTerminalControl.xaml(.cs)
    TerminalSession.cs
    TerminalRenderer.cs
    TerminalInputHandler.cs
    TerminalColorScheme.cs
    TerminalOptions.cs
  ViewModels/SshTabViewModel.cs               ← NEW
  Views/SshTabView.xaml(.cs)                  ← NEW
  ViewModels/MainWindowViewModel.cs           ← MODIFIED
  ViewModels/ConnectionTabViewModel.cs        ← MODIFIED (extract IConnectionTab)
  Views/MainWindow.xaml                       ← MODIFIED (tab DataTemplateSelector)
  Views/PropertiesDialog.xaml(.cs)            ← MODIFIED (ConnectionType dropdown, conditional fields)
  Converters/TreeEntryTypeToIconConverter.cs   ← MODIFIED (SSH icon)
```

### 5.2 Entity Changes

#### ConnectionType Enum (new)
```csharp
namespace JustRDP.Domain.Enums;

public enum ConnectionType
{
    RDP = 0,
    SSH = 1
}
```

#### ConnectionEntry (modified)
```csharp
// New properties
public ConnectionType ConnectionType { get; set; } = ConnectionType.RDP;
public string? SshPrivateKeyPath { get; set; }
public byte[]? SshPrivateKeyPassphraseEncrypted { get; set; }
public string? SshTerminalFontFamily { get; set; }
public double? SshTerminalFontSize { get; set; }
```

Default `Port` remains 3389 at the entity level. When `ConnectionType = SSH`, the properties dialog sets Port to 22 on type change (if still at 3389).

### 5.3 Tab Architecture

Extract a common interface for tab ViewModels:

```csharp
public interface IConnectionTab : INotifyPropertyChanged
{
    Guid ConnectionId { get; }
    string TabTitle { get; }
    bool IsSelected { get; set; }
    event Action<IConnectionTab>? CloseRequested;
    void Disconnect();
}
```

Both `ConnectionTabViewModel` (RDP) and `SshTabViewModel` (SSH) implement this interface. `MainWindowViewModel.OpenTabs` becomes `ObservableCollection<IConnectionTab>`.

### 5.4 SshTabViewModel

```csharp
public partial class SshTabViewModel : ObservableObject, IConnectionTab
{
    // Connection info for ConfigureAndConnect
    private readonly ConnectionEntry _connection;
    private readonly Credential _credential;

    [ObservableProperty] private string _tabTitle;
    [ObservableProperty] private bool _isSelected;

    public Guid ConnectionId => _connection.Id;
    public event Action<IConnectionTab>? CloseRequested;

    // Called by SshTabView once the control is loaded
    public TerminalOptions BuildOptions() { ... }

    public void OnDisconnected() => CloseRequested?.Invoke(this);
    public void Disconnect() { /* No-op — SshTabView handles terminal lifecycle */ }
}
```

### 5.5 Terminal Control Port

Files from `ssh-net-poc/SshTerminalPoc/Controls/Terminal/` are copied into `JustRDP.Presentation/Controls/Terminal/` with namespace changed to `JustRDP.Presentation.Controls.Terminal`. No functional changes — the POC implementation is production-ready.

Changes during port:
- Namespace: `SshTerminalPoc.Controls.Terminal` → `JustRDP.Presentation.Controls.Terminal`
- Status bar in `SshTerminalControl.xaml`: remove hardcoded colors, use theme-aware brushes
- `TerminalSession`: add `ILogger` parameter (optional, replace `Debug.WriteLine`)

### 5.6 Tab Template Selection

`MainWindow.xaml` uses a `DataTemplateSelector` to render the correct view for each tab type:

```csharp
public class ConnectionTabTemplateSelector : DataTemplateSelector
{
    public DataTemplate? RdpTemplate { get; set; }
    public DataTemplate? SshTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        return item switch
        {
            ConnectionTabViewModel => RdpTemplate,
            SshTabViewModel => SshTemplate,
            _ => base.SelectTemplate(item, container)
        };
    }
}
```

### 5.7 Quick Connect Parsing

```csharp
// In MainWindowViewModel.QuickConnect()
if (address.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase))
{
    // Parse: ssh://[user@]host[:port]
    var uri = address[6..]; // strip "ssh://"
    string? user = null;
    if (uri.Contains('@'))
    {
        var parts = uri.Split('@', 2);
        user = parts[0];
        uri = parts[1];
    }
    var hostParts = uri.Split(':', 2);
    var host = hostParts[0];
    var port = hostParts.Length > 1 && int.TryParse(hostParts[1], out var p) ? p : 22;

    var connection = new ConnectionEntry
    {
        Name = address, HostName = host, Port = port,
        ConnectionType = ConnectionType.SSH,
        CredentialUsername = user
    };
    // Open SSH tab...
}
```

### 5.8 Import/Export

JSON format extended with optional `ConnectionType` field:
```json
{
  "Name": "db-srv-01",
  "Type": "Connection",
  "ConnectionType": "SSH",
  "HostName": "10.0.1.50",
  "Port": 22,
  "SshPrivateKeyPath": "~/.ssh/id_rsa"
}
```

- Export: includes `ConnectionType` and SSH-specific fields
- Import: if `ConnectionType` is absent, defaults to `RDP` (backward compatibility)
- Private key passphrases are NOT exported (security, same as passwords)

### 5.9 EF Core Migration

```
dotnet ef migrations add AddSshSupport --project src/JustRDP.Infrastructure --startup-project src/JustRDP.Presentation
```

Migration adds to `TreeEntries` table:
- `ConnectionType` INT NOT NULL DEFAULT 0 (RDP)
- `SshPrivateKeyPath` TEXT NULL
- `SshPrivateKeyPassphraseEncrypted` BLOB NULL
- `SshTerminalFontFamily` TEXT NULL
- `SshTerminalFontSize` REAL NULL

### 5.10 DI Registration

```csharp
// No new DI registrations needed for SSH — terminal control is instantiated by the view
// SshTabViewModel is created manually (same pattern as ConnectionTabViewModel)
```

### 5.11 NuGet Packages

Add to `JustRDP.Presentation.csproj`:
```xml
<PackageReference Include="SSH.NET" Version="2025.1.0" />
<PackageReference Include="VtNetCore" Version="1.0.9" />
```

---

## 6. Testing

### 6.1 Unit Tests

| Test ID | Case | Expected |
|---------|------|----------|
| UT-111-01 | Create connection with ConnectionType.SSH | Port defaults to 3389 (entity level), properties dialog sets to 22 |
| UT-111-02 | Quick Connect `ssh://admin@10.0.1.50` | Parses as SSH, user=admin, host=10.0.1.50, port=22 |
| UT-111-03 | Quick Connect `ssh://admin@10.0.1.50:2222` | Parses as SSH, user=admin, host=10.0.1.50, port=2222 |
| UT-111-04 | Quick Connect `ssh://10.0.1.50` | Parses as SSH, user=null, host=10.0.1.50, port=22 |
| UT-111-05 | Quick Connect `10.0.1.50` | Parses as RDP (existing behavior) |
| UT-111-06 | Quick Connect `10.0.1.50:3390` | Parses as RDP, port=3390 (existing behavior) |
| UT-111-07 | Icon converter for SSH connection | Returns `Console` PackIconKind |
| UT-111-08 | Icon converter for RDP connection | Returns `MonitorDashboard` PackIconKind (unchanged) |
| UT-111-09 | JSON export includes ConnectionType for SSH | "ConnectionType": "SSH" in output |
| UT-111-10 | JSON import without ConnectionType | Defaults to RDP (backward compat) |

### 6.2 Integration Tests

| Test ID | Case | Expected |
|---------|------|----------|
| IT-111-01 | Double-click SSH connection | SSH terminal tab opens, connects |
| IT-111-02 | SSH terminal: type `ls` + Enter | Output rendered in terminal |
| IT-111-03 | SSH terminal: Ctrl+C | Interrupts running command |
| IT-111-04 | SSH terminal: arrow keys in bash | History navigation works |
| IT-111-05 | SSH terminal: resize tab area | Terminal columns/rows update (`stty size`) |
| IT-111-06 | SSH terminal: disconnect | Tab auto-closes |
| IT-111-07 | SSH + RDP tabs open simultaneously | No interference |
| IT-111-08 | SSH credential inheritance | Username from parent folder used |
| IT-111-09 | SSH with private key | Connects using key file |
| IT-111-10 | Availability monitor for SSH connection | Green/red dot on port 22 |

### 6.3 Manual Tests

| Test ID | Case | Expected |
|---------|------|----------|
| MT-111-01 | vim in SSH terminal | Full-screen editing works, colors correct |
| MT-111-02 | tmux in SSH terminal | Split panes render, keyboard shortcuts work |
| MT-111-03 | htop in SSH terminal | Live-updating display, colors, interactive |
| MT-111-04 | Mouse scroll in terminal | Scrollback buffer accessible |
| MT-111-05 | Ctrl+V paste into terminal | Text pasted correctly (bracketed paste mode) |
| MT-111-06 | Text selection + Ctrl+C copy | Selected text copied to clipboard |

---

## 7. Implementation Notes

### 7.1 Key Files

| File | Project | Purpose | Status |
|------|---------|---------|--------|
| `Domain/Enums/ConnectionType.cs` | Domain | RDP/SSH enum | NEW |
| `Domain/Entities/ConnectionEntry.cs` | Domain | Add ConnectionType + SSH fields | MODIFIED |
| `Infrastructure/Migrations/AddSshSupport.cs` | Infrastructure | EF Core migration | NEW |
| `Presentation/Controls/Terminal/SshTerminalControl.xaml(.cs)` | Presentation | SSH terminal UserControl | NEW (from POC) |
| `Presentation/Controls/Terminal/TerminalSession.cs` | Presentation | SSH.NET + VtNetCore bridge | NEW (from POC) |
| `Presentation/Controls/Terminal/TerminalRenderer.cs` | Presentation | DrawingVisual terminal rendering | NEW (from POC) |
| `Presentation/Controls/Terminal/TerminalInputHandler.cs` | Presentation | Keyboard → VT escape sequences | NEW (from POC) |
| `Presentation/Controls/Terminal/TerminalColorScheme.cs` | Presentation | 256-color palette | NEW (from POC) |
| `Presentation/Controls/Terminal/TerminalOptions.cs` | Presentation | SSH connection config | NEW (from POC) |
| `Presentation/ViewModels/IConnectionTab.cs` | Presentation | Shared tab interface | NEW |
| `Presentation/ViewModels/SshTabViewModel.cs` | Presentation | SSH tab lifecycle | NEW |
| `Presentation/Views/SshTabView.xaml(.cs)` | Presentation | SSH tab view | NEW |
| `Presentation/Views/ConnectionTabTemplateSelector.cs` | Presentation | RDP/SSH tab template selection | NEW |
| `Presentation/ViewModels/MainWindowViewModel.cs` | Presentation | Tab management, Quick Connect | MODIFIED |
| `Presentation/ViewModels/ConnectionTabViewModel.cs` | Presentation | Implement IConnectionTab | MODIFIED |
| `Presentation/Views/MainWindow.xaml` | Presentation | Tab templates, DataTemplateSelector | MODIFIED |
| `Presentation/Converters/TreeEntryTypeToIconConverter.cs` | Presentation | SSH icon | MODIFIED |
| `Presentation/Views/PropertiesDialog.xaml(.cs)` | Presentation | ConnectionType dropdown | MODIFIED |
| `Presentation/ViewModels/ConnectionPropertiesViewModel.cs` | Presentation | SSH fields, type switching | MODIFIED |

### 7.2 Dependencies
- `SSH.NET` (2025.1.0) — SSH connection, authentication, ShellStream
- `VtNetCore` (1.0.9) — VT100/xterm terminal emulation and ANSI parsing

### 7.3 Threading Model
- SSH read loop runs on a background thread (`Thread`, not Task — matching POC)
- VtNetCore data processing marshalled to UI thread via `Dispatcher.InvokeAsync`
- Terminal rendering via `CompositionTarget.Rendering` (~60fps, only when dirty)
- Resize events debounced at 100ms via `DispatcherTimer`

### 7.4 Known Limitations
- **No SSH agent forwarding**: Users must provide password or private key file directly
- **No mouse tracking**: Terminal mouse events are not forwarded to remote applications (text selection only)
- **No custom color schemes**: Uses VS Code Dark theme palette (hardcoded in `TerminalColorScheme`)
- **VtNetCore limitations**: Some rare escape sequences may not be implemented. Known to work with bash, zsh, vim, nano, tmux, htop, mc.
- **No host key verification**: SSH.NET's default behavior (accept all host keys). Security improvement for future.

### 7.5 Logging
Follow existing project pattern (`ILogger<T>`):
- **Information**: SSH connection opened (host, port, auth method), SSH session closed
- **Debug**: Terminal resize events, key mapping fallbacks
- **Warning**: Connection errors, unexpected disconnects
- **Error**: Terminal rendering failures, VtNetCore exceptions

---

## 8. Security Considerations

- **Private key passphrase**: DPAPI-encrypted (same as RDP passwords). Never exported.
- **SSH passwords**: Use existing credential system (DPAPI-encrypted, inheritable from folders)
- **No host key verification**: Current implementation accepts all host keys. Future enhancement: store known_hosts in AppSettings or dedicated table.
- **Private key files**: Path stored in DB, file read at connect time. File must be accessible to the current Windows user.
- **Export security**: Private key passphrases and passwords are never included in JSON exports.

---

## 9. Migration Path

### 9.1 Database Migration
- Existing connections are unaffected (ConnectionType defaults to 0 = RDP)
- New columns are nullable (SSH-specific fields)
- No data transformation required

### 9.2 Backward Compatibility
- All existing RDP workflows unchanged
- JSON import without `ConnectionType` field defaults to RDP
- Quick Connect without `ssh://` prefix defaults to RDP

---

## 10. Implementation Order

| Step | Task | Depends On |
|------|------|------------|
| 1 | `ConnectionType` enum (Domain) | — |
| 2 | `ConnectionEntry` SSH fields (Domain) | Step 1 |
| 3 | EF Core migration (Infrastructure) | Step 2 |
| 4 | NuGet packages: SSH.NET, VtNetCore (Presentation) | — |
| 5 | Port terminal controls from POC (Presentation/Controls/Terminal) | Step 4 |
| 6 | `IConnectionTab` interface (Presentation) | — |
| 7 | `ConnectionTabViewModel` implements `IConnectionTab` | Step 6 |
| 8 | `SshTabViewModel` (Presentation) | Step 6 |
| 9 | `SshTabView` (Presentation) | Steps 5, 8 |
| 10 | `ConnectionTabTemplateSelector` (Presentation) | Steps 7, 9 |
| 11 | `MainWindowViewModel` — OpenTabs refactor, OpenConnectionAsync branching | Steps 7, 8, 10 |
| 12 | `MainWindow.xaml` — tab templates, DataTemplateSelector | Steps 10, 11 |
| 13 | `TreeEntryTypeToIconConverter` — SSH icon | Step 1 |
| 14 | Properties dialog — ConnectionType dropdown, conditional fields | Steps 1, 2 |
| 15 | Quick Connect — `ssh://` prefix parsing | Steps 8, 11 |
| 16 | Import/Export — ConnectionType + SSH fields | Steps 1, 2 |
| 17 | Build & verify | All |

---

## 11. References
- SSH POC: `C:\Users\ChristianCasuttSolvi\source\repos\ssh-net-poc`
- POC PRD: `ssh-net-poc/PRD.md` — detailed terminal control requirements
- FEAT-001: Data Model & Persistence
- FEAT-003: Credential Encryption (DPAPI)
- FEAT-011: RDP Session Management — tab lifecycle pattern
- FEAT-009: Properties Panel & Dialog — UI pattern for connection editing
