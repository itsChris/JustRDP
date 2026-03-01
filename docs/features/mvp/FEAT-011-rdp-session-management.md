# FEAT-011: RDP Session Management

## Metadata

| Attribute | Value |
|-----------|-------|
| **Feature ID** | FEAT-011 |
| **Title** | RDP Session Management |
| **Category** | MVP |
| **Priority** | P0 (Critical) |
| **PRD Sections** | §5.3.1, §5.3.2, §5.3.3 |
| **Depends On** | FEAT-001 (data model), FEAT-003 (credential encryption) |
| **Dependents** | FEAT-015 (detachable tabs), FEAT-021 (status bar) |
| **Estimated Complexity** | L (1-2 weeks) |

---

## 1. Overview

### 1.1 Purpose
Implements the core RDP connection functionality. Users double-click a connection entry to open an RDP session in a tab. The session uses RoyalApps.Community.Rdp.WinForms hosted in a WPF WindowsFormsHost, with full connection lifecycle management (connect, disconnect, reconnect, error handling).

### 1.2 Scope
**In Scope:**
- ConnectionTabViewModel managing RDP session state
- ConnectionTabView hosting RdpControl via WindowsFormsHost
- RdpClientConfiguration mapping from ConnectionEntry
- Connection lifecycle: connecting → connected → disconnected
- Status overlays: spinner, error with reconnect
- Tab title from connection name
- Duplicate tab prevention (same connection opens only once)
- Tab close with clean disconnect

**Out of Scope:**
- Tab detachment (FEAT-015)
- Credential inheritance resolution (FEAT-013, consumed as Credential value object)
- Full-screen RDP mode (Post-MVP)

### 1.3 Key Decisions
- **Code-behind for ConnectionTabView**: WinForms interop requires imperative control creation; MVVM not practical here
- **RdpControl created in Loaded event**: Ensures WindowsFormsHost is ready before creating the WinForms control
- **Disconnect on Unloaded**: Clean resource release when tab is removed

### 1.4 Assumptions
> [!NOTE]
> **Implementation Assumptions:**
> - [ASSUMPTION] MsRdpEx (dependency of RoyalApps.Community.Rdp.WinForms) is included automatically via NuGet
> - [ASSUMPTION] RDP ActiveX is available on all target Windows versions (built into Windows)

---

## 2. User Stories & Acceptance Criteria

### US-011-01: Open RDP Session
**As a** user
**I want to** double-click a connection to open an RDP session
**So that** I can work on a remote server

**Acceptance Criteria:**
- [ ] AC1: Double-click opens new tab with RDP session
- [ ] AC2: Tab title shows connection name
- [ ] AC3: Spinner overlay shown while connecting
- [ ] AC4: Overlay disappears when connected
- [ ] AC5: If connection already open, switch to existing tab

### US-011-02: Handle Disconnection
**As a** user
**I want to** see clear error messages when connections fail
**So that** I can diagnose and retry

**Acceptance Criteria:**
- [ ] AC1: Error overlay shows disconnect code and description
- [ ] AC2: Reconnect button available on error overlay
- [ ] AC3: Normal disconnect (code 0/1) shows clean "Disconnected" message

### US-011-03: Close RDP Session
**As a** user
**I want to** close an RDP tab
**So that** I can free resources

**Acceptance Criteria:**
- [ ] AC1: Closing tab disconnects RDP session
- [ ] AC2: RdpControl disposed properly
- [ ] AC3: WindowsFormsHost.Child set to null

---

## 3. Functional Requirements

| Req ID | Requirement | Priority | PRD Ref |
|--------|-------------|----------|---------|
| FR-011-01 | Create RdpControl in WindowsFormsHost on tab load | Must | §5.3.1 |
| FR-011-02 | Map all ConnectionEntry properties to RdpClientConfiguration | Must | §5.3.2 |
| FR-011-03 | Handle OnConnected event (hide overlay) | Must | §5.3.3 |
| FR-011-04 | Handle OnDisconnected event (show status/error) | Must | §5.3.3 |
| FR-011-05 | Reconnect command on error overlay | Must | §5.3.3 |
| FR-011-06 | Clean disconnect and dispose on tab close | Must | §5.3.3 |
| FR-011-07 | Prevent duplicate tabs for same connection | Should | §5.3.1 |
| FR-011-08 | Auto-size desktop (Width/Height=0) | Should | §5.3.2 |

---

## 4. RDP Configuration Mapping

| Entity Property | RDP Config Property | Notes |
|----------------|---------------------|-------|
| HostName | config.Server | Top-level |
| Port | config.Port | Top-level |
| CredentialUsername | config.Credentials.Username | Via resolved Credential |
| CredentialPassword | config.Credentials.Password | SensitiveString wrapper |
| NLA | config.Credentials.NetworkLevelAuthentication | |
| DesktopWidth | config.Display.DesktopWidth | 0 = auto |
| ColorDepth | config.Display.ColorDepth | Cast to enum |
| ResizeBehavior | config.Display.ResizeBehavior | Scrollbars/SmartSizing/SmartReconnect |
| RedirectClipboard | config.Redirection.RedirectClipboard | |
| AudioRedirectionMode | config.Redirection.AudioRedirectionMode | Cast to enum |
| AutoReconnect | config.Connection.EnableAutoReconnect | |
| Compression | config.Connection.Compression | |
| GatewayHostName | config.Gateway.GatewayHostname | If non-empty |

---

## 5. Testing

### 5.1 Unit Tests

| Test ID | Case | Expected |
|---------|------|----------|
| UT-011-01 | ConnectionTabViewModel created with correct title | TabTitle = connection.Name |
| UT-011-02 | Disconnect sets IsConnected=false | State cleaned up |
| UT-011-03 | Duplicate prevention in MainWindowViewModel | Existing tab selected instead |

---

## 6. Implementation Notes

### 6.1 Key Files
- `ViewModels/ConnectionTabViewModel.cs` — Session state and RDP config
- `Views/ConnectionTabView.xaml(.cs)` — WindowsFormsHost and overlays

### 6.2 RoyalApps API
- Control: `RoyalApps.Community.Rdp.WinForms.Controls.RdpControl`
- Config: `RoyalApps.Community.Rdp.WinForms.Configuration.*`
- Password type: `SensitiveString` (constructor from string)
- Events: `OnConnected`, `OnDisconnected` (with `DisconnectedEventArgs`)

---

## 7. Enhancements (2026-03-01)

### Auto-close on disconnect
Tabs now close automatically when the remote session disconnects (user logoff, remote disconnection). Implemented via a `CloseRequested` event on `ConnectionTabViewModel` that fires at the end of the `OnDisconnected` handler. `MainWindowViewModel` subscribes to this event and routes it to `CloseTab()`.

### Keyboard input forwarding
Added RDP input configuration to forward keyboard shortcuts to the remote session:
- `config.Input.KeyboardHookMode = true`
- `config.Input.AcceleratorPassthrough = true`
- `config.Input.EnableWindowsKey = true`

### Quick Connect
A toolbar text field allows ad-hoc connections by typing `host:port` without creating a persistent tree entry. Creates a temporary in-memory `ConnectionEntry` and opens an RDP tab.

## 8. References
- §5.3: RDP Connections — Full specification
- FEAT-003: Credential decryption for password
- FEAT-013: Resolves credentials before connection
- FEAT-015: Adds detachable tab behavior on top
