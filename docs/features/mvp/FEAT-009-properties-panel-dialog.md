# FEAT-009: Properties Panel & Dialog

## Metadata

| Attribute | Value |
|-----------|-------|
| **Feature ID** | FEAT-009 |
| **Title** | Properties Panel & Dialog |
| **Category** | MVP |
| **Priority** | P1 (High) |
| **PRD Sections** | §5.2 |
| **Depends On** | FEAT-001 (data model), FEAT-003 (credential encryption) |
| **Dependents** | — |
| **Estimated Complexity** | L (1-2 weeks) |

---

## 1. Overview

### 1.1 Purpose
Provides two complementary views for connection/folder properties: a read-only summary side panel and a full modal editing dialog with tabbed sections for all connection settings.

### 1.2 Scope
**In Scope:**
- PropertiesPanel (UserControl, right side panel) — read-only summary
- PropertiesDialog (Window, modal) — full editing with tabs
- PropertiesViewModel for side panel state
- ConnectionPropertiesViewModel for dialog editing state
- Tab sections: General, Credentials, Display, Redirection, Connection, Notes
- Save to database via TreeService
- Credential display with inheritance info
- Folder credential editing (for inheritance)

**Out of Scope:**
- Credential inheritance resolution logic (FEAT-013)
- RDP connection settings validation against RDP control (FEAT-011)

### 1.3 Key Decisions
- **Two-view approach**: Side panel for quick glance, dialog for full editing — mirrors Royal TS pattern
- **Folder credentials**: Editable on folders to enable credential inheritance
- **PasswordBox**: Used for password entry; password decrypted on dialog open, re-encrypted on save

---

## 2. User Stories & Acceptance Criteria

### US-009-01: View Entry Properties
**As a** user
**I want to** see a summary of selected entry properties
**So that** I can quickly check settings without opening a dialog

**Acceptance Criteria:**
- [ ] AC1: Right panel shows Name, Type, Host, Port, Credentials, Notes
- [ ] AC2: Credentials display shows "inherited from: <folder>" when applicable
- [ ] AC3: Panel updates when tree selection changes
- [ ] AC4: "Properties..." button opens modal dialog

### US-009-02: Edit Connection Properties
**As a** user
**I want to** edit all connection settings in a tabbed dialog
**So that** I can configure RDP sessions

**Acceptance Criteria:**
- [ ] AC1: General tab: Name, Host Name, Port
- [ ] AC2: Credentials tab: Username, Domain, Password
- [ ] AC3: Display tab: Width, Height, Color Depth, Resize Behavior
- [ ] AC4: Redirection tab: Clipboard, Printers, Drives, Smart Cards, Ports, Audio
- [ ] AC5: Connection tab: Auto Reconnect, NLA, Compression, Gateway
- [ ] AC6: Notes tab: Multi-line text
- [ ] AC7: OK saves all changes; Cancel discards
- [ ] AC8: Tree entry name updates after save

### US-009-03: Edit Folder Credentials
**As a** user
**I want to** set credentials on a folder
**So that** child connections inherit them

**Acceptance Criteria:**
- [ ] AC1: Folder properties dialog shows Credentials tab
- [ ] AC2: Connection-only tabs hidden for folders
- [ ] AC3: Saved credentials applied to child connections via inheritance

---

## 3. Functional Requirements

| Req ID | Requirement | Priority | PRD Ref |
|--------|-------------|----------|---------|
| FR-009-01 | Read-only properties panel in right column | Must | §5.2.1 |
| FR-009-02 | Modal dialog with tabbed interface | Must | §5.2.2 |
| FR-009-03 | General tab with Name, Host, Port | Must | §5.2.2 |
| FR-009-04 | Credentials tab with Username, Domain, Password | Must | §5.2.2 |
| FR-009-05 | Display tab with resolution and resize settings | Must | §5.2.2 |
| FR-009-06 | Redirection tab with all flags and audio mode | Must | §5.2.2 |
| FR-009-07 | Connection tab with auto-reconnect, NLA, compression, gateway | Must | §5.2.2 |
| FR-009-08 | Notes tab with multi-line editing | Must | §5.2.2 |
| FR-009-09 | Password encrypted on save, decrypted on load | Must | §7.1 |
| FR-009-10 | Folder shows only Name + Credentials tabs | Must | §5.2.2 |

---

## 4. UI Components

### 4.1 Properties Panel (Right side)
```
┌─────────────────────┐
│ Properties          │
│                     │
│ Name                │
│ Server01            │
│                     │
│ Type                │
│ Connection          │
│                     │
│ Host                │
│ 10.0.0.1            │
│                     │
│ Port                │
│ 3389                │
│                     │
│ Credentials         │
│ admin (from: Prod)  │
│                     │
│ [Properties...]     │
└─────────────────────┘
```

### 4.2 Properties Dialog
```
┌──────────────────────────────────┐
│ Properties                   [x] │
│ ┌────────────────────────────┐   │
│ │ General | Creds | Display  │   │
│ │ Redirect | Conn | Notes   │   │
│ ├────────────────────────────┤   │
│ │                            │   │
│ │  Name: [_______________]   │   │
│ │  Host: [_______________]   │   │
│ │  Port: [3389___________]   │   │
│ │                            │   │
│ └────────────────────────────┘   │
│              [OK]  [Cancel]      │
└──────────────────────────────────┘
```

---

## 5. Testing

### 5.1 Unit Tests

| Test ID | Case | Expected |
|---------|------|----------|
| UT-009-01 | LoadEntry with ConnectionEntry | All fields populated |
| UT-009-02 | LoadEntry with FolderEntry | IsFolder=true, connection fields hidden |
| UT-009-03 | LoadEntry with inherited credential | CredentialDisplay includes source |
| UT-009-04 | SaveAsync updates entity and database | Changes persisted |

---

## 6. Implementation Notes

### 6.1 Key Files
- `ViewModels/PropertiesViewModel.cs` — Side panel read-only state
- `ViewModels/ConnectionPropertiesViewModel.cs` — Dialog editing state
- `Views/PropertiesPanel.xaml(.cs)` — Side panel UserControl
- `Views/PropertiesDialog.xaml(.cs)` — Modal dialog Window

---

## 7. References
- §5.2: Properties — Full specification
- FEAT-003: Credential encryption used in password handling
- FEAT-013: Credential inheritance displayed in panel
